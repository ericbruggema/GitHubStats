using static GitHubStats.Fmt;

namespace GitHubStats;

// Plain row classes the WPF grids bind to (public properties only).

public sealed class RepoRow
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
    public string Language { get; set; } = "";
    public string Badges { get; set; } = "";
    public long Stars { get; set; }
    public long Forks { get; set; }
    public long Issues { get; set; }
    public int Releases { get; set; }
    public long Downloads { get; set; }
    /// <summary>Change between the last two refreshes ("" = not known)</summary>
    public string DeltaLast { get; set; } = "";
    public long DeltaLastSort { get; set; }
    public string Delta7 { get; set; } = "";
    public long Delta7Sort { get; set; }
    public string PerDay { get; set; } = "";
    public double PerDaySort { get; set; }
    public string Pushed { get; set; } = "";
    public bool Loading { get; set; }
}

public sealed class ReleaseRow
{
    public string Tag { get; set; } = "";
    public string Title { get; set; } = "";
    public string Published { get; set; } = "";
    public string Url { get; set; } = "";
    public long Downloads { get; set; }
    /// <summary>Share of the repo's downloads, 0..100</summary>
    public double Share { get; set; }
    public string ShareText { get; set; } = "";
    public string Delta7 { get; set; } = "";
    public string PerDay { get; set; } = "";
    public string Files { get; set; } = "";
    public bool Latest { get; set; }
}

public sealed class StarRow
{
    public long Time { get; set; }
    public string When { get; set; } = "";
    public string Ago { get; set; } = "";
    public string Repo { get; set; } = "";
    public string User { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class RefRow
{
    public string Name { get; set; } = "";
    public long Count { get; set; }
    public long Uniques { get; set; }
    public string Repo { get; set; } = "";
}

public sealed class NameValue
{
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public long Value { get; set; }
    /// <summary>0..100, for bars</summary>
    public double Percent { get; set; }
}

/// <summary>Change over a period. Partial = the history is shorter than the period, so Span seconds were measured instead.</summary>
public sealed class WinDelta
{
    public long? Value { get; set; }
    public bool Partial { get; set; }
    public long Span { get; set; }
}

public sealed class Card
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Detail { get; set; } = "";
    /// <summary>1 = up, -1 = down, 0 = neutral (colours the detail text)</summary>
    public int Trend { get; set; }
}

/// <summary>Everything the window shows, computed from the engine's data (UI independent, so it can be tested without Windows).</summary>
internal sealed class Dashboard
{
    public string User = "";
    public bool Running;
    public bool HasData;
    public Totals Totals = new();
    public List<Card> Cards = new();
    public List<RepoRow> Repos = new();
    public List<StarRow> Stars = new();
    public List<RefRow> Referrers = new();
    public List<NameValue> Platforms = new();
    public List<NameValue> TopFiles = new();
    public ChartModel? All;
    public bool HasTraffic;
    /// <summary>Problems and progress, most important first.</summary>
    public List<string> Messages = new();
    public string Progress = "";
    public string LastRefresh = "";
    public string NextRefresh = "";
    public string RateText = "";
    public long LastSuccess;
    public long TotalStars;
    public long TotalDownloads;
    /// <summary>Downloads over time (one value per refresh) for the small widget chart.</summary>
    public long[] DownloadHistory = Array.Empty<long>();
    public long? DownloadsDelta;
    public long? StarsDelta;
    /// <summary>Change over the last day, week and month (index 0, 1, 2); null = not enough history yet.</summary>
    public WinDelta[] DownloadsWin = { new(), new(), new() };
    public WinDelta[] StarsWin = { new(), new(), new() };
    /// <summary>Repository that gained the most downloads, e.g. "a  +120 in 7d"; "" = unknown.</summary>
    public string TopGainer = "";
    public string LatestStar = "";
    public string TopFile = "";
    public double? DownloadsPerDay;

    private App _app = null!;

    public static Dashboard Build(App app)
    {
        var d = app.Data;
        var stars = app.Stars;
        var traffic = app.Traffic;
        var db = new Dashboard { _app = app, User = app.Cfg.User, Running = app.Running };
        var repos = d.Repos.Values.ToList();
        var snaps = d.Snapshots;
        db.HasData = repos.Count > 0;
        var tot = Calc.Sum(repos);
        db.Totals = tot;
        db.TotalDownloads = tot.Downloads;
        db.TotalStars = tot.Stars;
        long now = Clock.Now();
        long nextAt = Calc.NextAt(d);

        int nSn = snaps.Count;
        var lastSnap = nSn >= 1 ? snaps[nSn - 1] : null;
        var prevSnap = nSn >= 2 ? snaps[nSn - 2] : null;
        var base7 = Calc.Baseline(snaps, 7 * 86400);
        long? dlLast = prevSnap != null ? lastSnap!.Downloads - prevSnap.Downloads : null;
        long? dl7 = base7 != null ? lastSnap!.Downloads - base7.Downloads : null;
        long? starsLast = prevSnap != null ? lastSnap!.Stars - prevSnap.Stars : null;
        long? stars7 = base7 != null ? lastSnap!.Stars - base7.Stars : null;
        db.DownloadsDelta = dlLast;
        db.StarsDelta = starsLast;
        db.DownloadHistory = snaps.Select(s => s.Downloads).ToArray();

        // ---- day / week / month: downloads from the refresh history, stars from the exact star dates when known
        long[] ages = { 86400, 7 * 86400, 30 * 86400 };
        string[] names = { "24h", "7d", "30d" };
        long starRecords = stars.Where(kv => d.Repos.ContainsKey(kv.Key)).Sum(kv => (long)kv.Value.Count);
        var allStarTimes = starRecords > 0
            ? stars.Where(kv => d.Repos.ContainsKey(kv.Key)).SelectMany(kv => kv.Value).Where(x => x.T > 0).Select(x => x.T).ToList()
            : new List<long>();
        for (int w = 0; w < 3; w++)
        {
            db.DownloadsWin[w] = WinOf(snaps, ages[w], x => x.Downloads);
            if (allStarTimes.Count > 0)
            {
                long cut = now - ages[w];
                db.StarsWin[w] = new WinDelta { Value = allStarTimes.Count(x => x >= cut) };
            }
            else db.StarsWin[w] = WinOf(snaps, ages[w], x => x.Stars);
        }

        // who gained the most downloads: day, else week, else month, else since the first measurement
        for (int w = 0; w <= 3 && db.TopGainer.Length == 0; w++)
        {
            var b = w < 3 ? BaseFor(snaps, ages[w], out bool part0) : snaps.Count >= 2 ? snaps[0] : null;
            if (b == null || lastSnap == null || ReferenceEquals(b, lastSnap)) continue;
            string best = ""; long bestV = 0;
            foreach (var (nm, arr) in lastSnap.Repos)
            {
                long gain = (arr.Length > 1 ? arr[1] : 0) - Calc.SnapDl(b, nm);
                if (gain > bestV) { bestV = gain; best = nm; }
            }
            if (bestV > 0) db.TopGainer = best + "  +" + N(bestV) + " in " + (w < 3 ? names[w] : Ago(b.T).Replace(" ago", ""));
        }
        if (base7 != null && lastSnap != null)
        {
            double days = (lastSnap.T - base7.T) / 86400.0;
            if (days >= 0.25) db.DownloadsPerDay = (lastSnap.Downloads - base7.Downloads) / days;
        }

        // ---- cards
        string Two(long? last, long? week) =>
            (last != null ? DeltaText(last) + " last refresh" : "") + (last != null && week != null ? "  |  " : "") + (week != null ? DeltaText(week) + " 7d" : "");
        int Tr(long? v) => v == null || v == 0 ? 0 : v > 0 ? 1 : -1;
        db.Cards.Add(new Card { Label = "Downloads", Value = N(tot.Downloads), Detail = Two(dlLast, dl7), Trend = Tr(dl7 ?? dlLast) });
        db.Cards.Add(new Card { Label = "Stars", Value = N(tot.Stars), Detail = Two(starsLast, stars7), Trend = Tr(stars7 ?? starsLast) });
        db.Cards.Add(new Card { Label = "Forks", Value = N(tot.Forks) });
        db.Cards.Add(new Card { Label = "Repositories", Value = N(tot.Repos) });
        db.Cards.Add(new Card { Label = "Releases", Value = N(tot.Releases) });
        db.Cards.Add(new Card { Label = "Open issues", Value = N(tot.OpenIssues) });
        db.HasTraffic = Calc.HasTraffic(traffic);
        if (db.HasTraffic)
        {
            var t14 = Calc.Traffic14(d, traffic);
            db.Cards.Add(new Card { Label = "Views, 14 days", Value = N(t14.Views), Detail = N(t14.Uniques) + " unique" });
            db.Cards.Add(new Card { Label = "Clones, 14 days", Value = N(t14.Clones), Detail = N(t14.CloneUniques) + " unique cloners" });
        }

        // ---- repository rows (sorted by downloads; the grid can re-sort)
        foreach (var r in repos.OrderByDescending(r => r.Downloads).ThenByDescending(r => r.Stars))
        {
            long? curDl = lastSnap != null && lastSnap.Repos.TryGetValue(r.Name, out var cur) && cur.Length > 1 ? cur[1] : null;
            long? dLast = prevSnap != null && curDl != null ? curDl - Calc.SnapDl(prevSnap, r.Name) : null;
            long? d7 = base7 != null && curDl != null ? curDl - Calc.SnapDl(base7, r.Name) : null;
            double? perDay = Calc.RepoPerDay(snaps, r.Name);
            var badges = new List<string>();
            if (r.Archived) badges.Add("archived");
            if (r.Fork) badges.Add("fork");
            db.Repos.Add(new RepoRow
            {
                Name = r.Name, Url = GhUrl(r.Url), Description = r.Description, Language = r.Language,
                Badges = string.Join(", ", badges),
                Stars = r.Stars, Forks = r.Forks, Issues = r.OpenIssues, Releases = r.ReleaseCount, Downloads = r.Downloads,
                DeltaLast = DeltaText(dLast), DeltaLastSort = dLast ?? 0,
                Delta7 = DeltaText(d7), Delta7Sort = d7 ?? 0,
                PerDay = perDay == null ? "" : PerDay(perDay.Value), PerDaySort = perDay ?? 0,
                Pushed = FDate(r.PushedAt), Loading = r.ReleasesAt == 0,
            });
        }

        // ---- stars, referrers, platforms, files
        foreach (var (t, repo, user) in Calc.RecentStars(d, stars, 200))
            db.Stars.Add(new StarRow { Time = t, When = Dt(t), Ago = Ago(t), Repo = repo, User = user, Url = "https://github.com/" + Uri.EscapeDataString(user) });
        foreach (var (name, count, uniques) in Calc.Referrers(d, traffic, 30))
            db.Referrers.Add(new RefRow { Name = name, Count = count, Uniques = uniques });
        var plats = Calc.Platforms(d);
        long platTotal = Math.Max(1, plats.Sum(p => p.Downloads));
        foreach (var (p, n) in plats) db.Platforms.Add(new NameValue { Name = p, Value = n, Percent = n * 100.0 / platTotal, Detail = Math.Round(n * 100.0 / platTotal) + "%" });
        var top = Calc.TopAssets(d, 10);
        long topMax = Math.Max(1, top.Count > 0 ? top[0].A.Downloads : 1);
        foreach (var (repo, rel, a) in top)
            db.TopFiles.Add(new NameValue { Name = a.Name, Detail = repo + " " + rel.Tag, Value = a.Downloads, Percent = a.Downloads * 100.0 / topMax });

        if (db.Stars.Count > 0) db.LatestStar = db.Stars[0].User + " starred " + db.Stars[0].Repo + ", " + db.Stars[0].Ago;
        if (db.TopFiles.Count > 0) db.TopFile = db.TopFiles[0].Name + " (" + N(db.TopFiles[0].Value) + ")";

        if (nSn >= 2) db.All = ChartModel.AllRepos(snaps);

        // ---- status line and messages
        db.LastSuccess = d.LastSuccess;
        db.LastRefresh = d.LastSuccess > 0 ? Dt(d.LastSuccess) + " (" + Ago(d.LastSuccess) + ")" : "not yet";
        db.NextRefresh = d.CycleStart > 0 || d.NextAttempt > 0 ? Hm(Math.Max(nextAt, now)) : "soon";
        var rt = d.Rate;
        db.RateText = rt != null && rt.Limit > 0 ? "API requests left: " + N(rt.Remaining) + " / " + N(rt.Limit) : "";

        var prog = new List<string>();
        if (d.Pending.Count > 0) prog.Add("release data: " + N(Math.Max(0, repos.Count - d.Pending.Count)) + " of " + N(repos.Count) + " repositories");
        if (d.PendingStars.Count > 0) prog.Add("star history: " + N(d.PendingStars.Count) + " repositor" + (d.PendingStars.Count == 1 ? "y" : "ies") + " left");
        if (d.PendingTraffic.Count > 0) prog.Add("traffic: " + N(d.PendingTraffic.Count) + " repositor" + (d.PendingTraffic.Count == 1 ? "y" : "ies") + " left");
        db.Progress = string.Join(" - ", prog);

        if (app.Warning != null) db.Messages.Add(app.Warning);
        if (app.StoreProblem != null) db.Messages.Add(app.StoreProblem);
        if (app.ShareProblem != null) db.Messages.Add(app.ShareProblem);
        if (db.Running)
            db.Messages.Add("Refreshing from GitHub... " + (db.Progress.Length > 0 ? db.Progress + ". Numbers are filled in as they arrive." : "contacting GitHub."));
        else if (d.PendingAll > 0)
            db.Messages.Add("Still gathering data: " + db.Progress + ". " + (d.LastError != null ? d.LastError + " " : "")
                + "It continues automatically after " + Hm(Math.Max(nextAt, now)) + ".");
        else if (d.LastError != null)
            db.Messages.Add("Last refresh failed: " + d.LastError + (db.HasData ? " Showing cached data." : "") + " Next attempt after " + Hm(Math.Max(nextAt, now)) + ".");
        if (d.TrafficNote != null && app.Cfg.TrackTraffic) db.Messages.Add(d.TrafficNote);
        if (d.StarsNote != null) db.Messages.Add(d.StarsNote);
        return db;
    }

    private static Snapshot? BaseFor(List<Snapshot> snaps, long age, out bool partial)
    {
        partial = false;
        int n = snaps.Count;
        if (n < 2) return null;
        var last = snaps[n - 1];
        for (int i = n - 2; i >= 0; i--)
            if (snaps[i].T <= last.T - age) return snaps[i];
        partial = true;
        return snaps[0];
    }

    private static WinDelta WinOf(List<Snapshot> snaps, long age, Func<Snapshot, long> sel)
    {
        var b = BaseFor(snaps, age, out bool partial);
        if (b == null) return new WinDelta();
        var last = snaps[^1];
        return new WinDelta { Value = sel(last) - sel(b), Partial = partial, Span = last.T - b.T };
    }

    public sealed class RepoDetail
    {
        public ChartModel? Main;
        public ChartModel? Traffic;
        public string TrafficInfo = "";
        public List<ReleaseRow> Releases = new();
        public List<RefRow> Referrers = new();
        public List<NameValue> Paths = new();
        public bool RealStars;
    }

    /// <summary>Charts and release table of one repository.</summary>
    public RepoDetail Detail(string name)
    {
        var d = _app.Data;
        var det = new RepoDetail();
        if (!d.Repos.TryGetValue(name, out var r)) return det;
        _app.Stars.TryGetValue(name, out var sl);
        var (t, v, real) = Charts.RepoPoints(d.Snapshots, name, sl, r.Stars);
        det.RealStars = real;
        if (t.Length >= 2) det.Main = ChartModel.Pct3(name, t, v, real);

        if (_app.Traffic.TryGetValue(name, out var rt) && rt.FetchedAt > 0)
        {
            var tp = Calc.TrafficPoints(rt);
            if (tp != null) det.Traffic = ChartModel.Traffic(name, tp.Value.T, tp.Value.V);
            det.TrafficInfo = "Last 14 days: " + N(rt.Views14) + " views (" + N(rt.Uniques14) + " unique), " + N(rt.Clones14) + " clones (" + N(rt.CloneUniques14) + " unique cloners)";
            foreach (var x in rt.Referrers) det.Referrers.Add(new RefRow { Name = x.Referrer, Count = x.Count, Uniques = x.Uniques, Repo = name });
            long pmax = Math.Max(1, rt.Paths.Count > 0 ? rt.Paths.Max(p => p.Count) : 1);
            foreach (var p in rt.Paths) det.Paths.Add(new NameValue { Name = p.Path, Detail = p.Title + " (" + N(p.Uniques) + " unique)", Value = p.Count, Percent = p.Count * 100.0 / pmax });
        }

        var latest = r.Releases.FirstOrDefault(x => !x.Prerelease);
        long total = Math.Max(1, r.Releases.Sum(x => x.Downloads));
        foreach (var rel in r.Releases)
        {
            double share = rel.Downloads * 100.0 / total;
            det.Releases.Add(new ReleaseRow
            {
                Tag = rel.Tag + (rel.Prerelease ? " (pre-release)" : "") + (ReferenceEquals(rel, latest) ? "  - latest" : ""), Title = rel.Name, Published = FDate(rel.PublishedAt), Url = GhUrl(rel.Url),
                Downloads = rel.Downloads, Share = share, ShareText = Math.Round(share) + "%",
                Delta7 = DeltaText(Calc.ReleaseDelta(rel, 7 * 86400)), PerDay = PerDay(Calc.AvgPerDay(rel)),
                Files = string.Join(", ", rel.Assets.Where(a => a.Downloads > 0).OrderByDescending(a => a.Downloads).Take(4).Select(a => a.Name + " " + N(a.Downloads))),
                Latest = ReferenceEquals(rel, latest),
            });
        }
        return det;
    }
}
