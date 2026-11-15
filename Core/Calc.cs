using System.Globalization;
using System.Text.RegularExpressions;

namespace GitHubStats;

/// <summary>Numbers derived from the stored data; shared by the page and the exports.</summary>
internal static class Calc
{
    public static Totals Sum(IEnumerable<Repo> repos)
    {
        var t = new Totals();
        foreach (var r in repos)
        {
            t.Repos++;
            t.Stars += r.Stars;
            t.Forks += r.Forks;
            t.OpenIssues += r.OpenIssues;
            t.Releases += r.ReleaseCount;
            t.Downloads += r.Downloads;
            if (r.ReleasesAt == 0) t.ReposLoading++;
        }
        return t;
    }

    /// <summary>Latest snapshot at least <paramref name="age"/> seconds older than the newest; else the oldest earlier one.</summary>
    public static Snapshot? Baseline(List<Snapshot> snaps, long age)
    {
        int n = snaps.Count;
        if (n < 2) return null;
        long cut = snaps[n - 1].T - age;
        Snapshot? best = null;
        for (int i = n - 2; i >= 0; i--)
        {
            if (snaps[i].T <= cut) return snaps[i];
            best = snaps[i];
        }
        return best;
    }

    public static long SnapDl(Snapshot s, string name) =>
        s.Repos.TryGetValue(name, out var a) && a.Length > 1 ? a[1] : 0;

    public static long NextAt(StatsData d) =>
        d.PendingAll > 0 ? d.NextAttempt : Math.Max(d.NextAttempt, d.CycleStart + Config.MinIntervalSeconds);

    // ------------------------------------------------------------------ releases

    /// <summary>Average downloads per day since the release was published.</summary>
    public static double AvgPerDay(Release r)
    {
        if (!DateTimeOffset.TryParse(r.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p)) return 0;
        double days = Math.Max(1.0, (Clock.Now() - p.ToUnixTimeSeconds()) / 86400.0);
        return r.Downloads / days;
    }

    /// <summary>Downloads gained over about <paramref name="age"/> seconds, from the release's own history; null = not enough history.</summary>
    public static long? ReleaseDelta(Release r, long age)
    {
        var h = r.History;
        if (h.Count < 2) return null;
        var last = h[^1];
        long[] bas = h[0];
        for (int i = h.Count - 2; i >= 0; i--)
        {
            bas = h[i];
            if (h[i][0] <= last[0] - age) break;
        }
        return last[1] - bas[1];
    }

    /// <summary>Downloads per day over the last ~7 days for a whole repo (from the snapshots); null = not enough history.</summary>
    public static double? RepoPerDay(List<Snapshot> snaps, string name)
    {
        var b = Baseline(snaps, 7 * 86400);
        if (b == null) return null;
        var last = snaps[^1];
        double days = (last.T - b.T) / 86400.0;
        if (days < 0.25) return null;
        return (SnapDl(last, name) - SnapDl(b, name)) / days;
    }

    // ------------------------------------------------------------------ assets and platforms

    private static readonly Regex MacRx = new(@"(?<![a-z])(mac|macos|osx|darwin)(?![a-z])|\.(dmg|pkg)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WinRx = new(@"(?<![a-z])win(dows|32|64)?(?![a-z])|\.(exe|msi|msix)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LinuxRx = new(@"(?<![a-z])linux(?![a-z])|\.(deb|rpm|appimage|snap)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>A guess from the file name of an asset.</summary>
    public static string Platform(string asset)
    {
        if (MacRx.IsMatch(asset)) return "macOS";
        if (WinRx.IsMatch(asset)) return "Windows";
        if (LinuxRx.IsMatch(asset)) return "Linux";
        if (asset.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".aab", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (asset.EndsWith(".ipa", StringComparison.OrdinalIgnoreCase)) return "iOS";
        return "Other / unknown";
    }

    public static List<(string Repo, Release Rel, Asset A)> TopAssets(StatsData d, int n)
    {
        var all = new List<(string, Release, Asset)>();
        foreach (var r in d.Repos.Values)
            foreach (var rel in r.Releases)
                foreach (var a in rel.Assets)
                    if (a.Downloads > 0) all.Add((r.Name, rel, a));
        return all.OrderByDescending(x => x.Item3.Downloads).Take(n).ToList();
    }

    public static List<(string Platform, long Downloads)> Platforms(StatsData d)
    {
        var map = new Dictionary<string, long>();
        foreach (var r in d.Repos.Values)
            foreach (var rel in r.Releases)
                foreach (var a in rel.Assets)
                    if (a.Downloads > 0)
                    {
                        string p = Platform(a.Name);
                        map[p] = map.GetValueOrDefault(p) + a.Downloads;
                    }
        return map.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    // ------------------------------------------------------------------ stars

    /// <summary>The latest stars across all repos (exact times), newest first.</summary>
    public static List<(long T, string Repo, string User)> RecentStars(StatsData d, Dictionary<string, List<StarRec>> stars, int n)
    {
        var all = new List<(long, string, string)>();
        foreach (var (repo, list) in stars)
        {
            if (!d.Repos.ContainsKey(repo)) continue;
            foreach (var s in list.TakeLast(n))
                if (s.T > 0) all.Add((s.T, repo, s.U));
        }
        return all.OrderByDescending(x => x.Item1).Take(n).ToList();
    }

    // ------------------------------------------------------------------ traffic

    public static long ToUnix(string yyyyMmDd) =>
        DateTime.TryParseExact(yyyyMmDd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds() : 0;

    /// <summary>One point per calendar day (UTC) from the first to the last stored day; days GitHub did not report count as 0. Row = [views, uniques, clones].</summary>
    public static (long[] T, long?[][] V)? TrafficPoints(RepoTraffic rt)
    {
        var keys = rt.Days.Keys.Where(k => ToUnix(k) > 0).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (keys.Count == 0) return null;
        long first = ToUnix(keys[0]), last = ToUnix(keys[^1]);
        if (last - first < 86400) return null; // a line needs at least two days
        if ((last - first) / 86400 > 400) first = last - 400L * 86400;
        var t = new List<long>();
        var v = new List<long?[]>();
        for (long ts = first; ts <= last; ts += 86400)
        {
            string key = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            rt.Days.TryGetValue(key, out var day);
            t.Add(ts);
            v.Add(new long?[] { day?.V ?? 0, day?.Uv ?? 0, day?.C ?? 0 });
        }
        return (t.ToArray(), v.ToArray());
    }

    public static (long Views, long Uniques, long Clones, long CloneUniques) Traffic14(StatsData d, Dictionary<string, RepoTraffic> traffic)
    {
        long v = 0, u = 0, c = 0, cu = 0;
        foreach (var (name, rt) in traffic)
        {
            if (!d.Repos.ContainsKey(name)) continue;
            v += rt.Views14; u += rt.Uniques14; c += rt.Clones14; cu += rt.CloneUniques14;
        }
        return (v, u, c, cu);
    }

    public static List<(string Referrer, long Count, long Uniques)> Referrers(StatsData d, Dictionary<string, RepoTraffic> traffic, int n)
    {
        var map = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, rt) in traffic)
        {
            if (!d.Repos.ContainsKey(name)) continue;
            foreach (var r in rt.Referrers)
            {
                var cur = map.GetValueOrDefault(r.Referrer);
                map[r.Referrer] = (cur.Item1 + r.Count, cur.Item2 + r.Uniques);
            }
        }
        return map.OrderByDescending(kv => kv.Value.Item1).Take(n).Select(kv => (kv.Key, kv.Value.Item1, kv.Value.Item2)).ToList();
    }

    public static bool HasTraffic(Dictionary<string, RepoTraffic> traffic) =>
        traffic.Values.Any(t => t.FetchedAt > 0);
}
