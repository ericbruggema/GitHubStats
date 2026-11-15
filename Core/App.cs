using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static GitHubStats.Fmt;

namespace GitHubStats;

/// <summary>
/// Holds the cached data and runs the (incremental) refresh in the background.
/// A new refresh cycle starts at most every 6 hours (max 4 per day), when the page is loaded or,
/// with background_refresh on, by the timer.
/// </summary>
internal sealed class App
{
    public readonly Config Cfg;
    public readonly string Home;
    public readonly string DataDir;
    public readonly string DataFile;
    public readonly string StarsFile;
    public readonly string TrafficFile;
    public string? Warning;       // config problems
    public string? StoreProblem;  // data folder / file problems
    public string? ShareProblem;  // publish folder problems

    /// <summary>Shows a Windows notification (set by the tray icon); null when there is no tray.</summary>
    public Action<string, string>? NotifyHandler;

    /// <summary>Raised (from a background thread) whenever the data or the running state changed.</summary>
    public event Action? Changed;
    private void RaiseChanged() { try { Changed?.Invoke(); } catch { /* a UI problem must never stop a refresh */ } }

    /// <summary>Raised after a complete refresh that found new stars (argument: how many); from a background thread.</summary>
    public event Action<int>? NewStars;

    private readonly object _gate = new();
    private volatile StatsData _data = new();
    private volatile Dictionary<string, List<StarRec>> _stars = new();
    private volatile Dictionary<string, RepoTraffic> _traffic = new();
    private int _running;

    public App(Config cfg, string home, string dataDir)
    {
        Cfg = cfg;
        Home = home;
        DataDir = dataDir;
        DataFile = Path.Combine(dataDir, "stats.json");
        StarsFile = Path.Combine(dataDir, "stars.json");
        TrafficFile = Path.Combine(dataDir, "traffic.json");
    }

    /// <summary>The data as shown everywhere (screens, exports, widget): hidden repositories are left out
    /// and the totals of every snapshot are recalculated without them. Nothing is deleted from stats.json,
    /// so un-hiding a repository brings its full history back.</summary>
    public StatsData Data
    {
        get
        {
            var d = _data;
            var hidden = Cfg.HiddenRepos;
            if (hidden.Count == 0) return d;
            string key = string.Join("\n", hidden);
            var cache = _filtered;
            if (cache != null && ReferenceEquals(cache.Value.Src, d) && cache.Value.Key == key) return cache.Value.View;
            var set = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
            var f = Clone(d);
            foreach (var n in f.Repos.Keys.Where(set.Contains).ToList()) f.Repos.Remove(n);
            foreach (var s in f.Snapshots)
            {
                foreach (var n in s.Repos.Keys.Where(set.Contains).ToList()) s.Repos.Remove(n);
                if (s.Repos.Count > 0)
                {
                    s.Stars = s.Repos.Values.Sum(a => a.Length > 0 ? a[0] : 0);
                    s.Downloads = s.Repos.Values.Sum(a => a.Length > 1 ? a[1] : 0);
                }
            }
            f.StarEvents = f.StarEvents.Where(e => !set.Contains(e.Repo)).ToList();
            _filtered = (d, key, f);
            return f;
        }
    }
    private (StatsData Src, string Key, StatsData View)? _filtered;

    /// <summary>Every repository in stats.json, including hidden ones (for the Settings list).</summary>
    public List<string> AllRepoNames => _data.Repos.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>repo -> who starred it and when (oldest first)</summary>
    public Dictionary<string, List<StarRec>> Stars => _stars;
    public Dictionary<string, RepoTraffic> Traffic => _traffic;
    public bool Running => Volatile.Read(ref _running) == 1;

    public string? PublishPath =>
        string.IsNullOrWhiteSpace(Cfg.PublishDir) ? null
        : Path.IsPathRooted(Cfg.PublishDir) ? Cfg.PublishDir : Path.Combine(Home, Cfg.PublishDir);

    public string AuthFp => Cfg.Token.Length == 0
        ? ""
        : Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Cfg.Token))).ToLowerInvariant()[..10];

    // ------------------------------------------------------------------ storage

    public void Load()
    {
        _data = Store.Load(DataFile, new StatsData());
        _stars = Store.Load(StarsFile, new Dictionary<string, List<StarRec>>());
        _traffic = Store.Load(TrafficFile, new Dictionary<string, RepoTraffic>());
    }

    private static StatsData Clone(StatsData d) =>
        JsonSerializer.Deserialize<StatsData>(JsonSerializer.Serialize(d, Json.Opt), Json.Opt)!;

    /// <summary>Write the working copy to disk and make it the copy the web pages see.</summary>
    private void Publish(StatsData d)
    {
        string json = JsonSerializer.Serialize(d, Json.Opt);
        try
        {
            Directory.CreateDirectory(DataDir);
            string tmp = DataFile + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, DataFile, true);
            StoreProblem = null;
        }
        catch (Exception ex)
        {
            StoreProblem = "Could not write " + DataFile + ": " + ex.Message;
        }
        _data = JsonSerializer.Deserialize<StatsData>(json, Json.Opt)!;
        RaiseChanged();
    }

    private void PublishStars(Dictionary<string, List<StarRec>> stars)
    {
        string? err = Store.Save(StarsFile, stars);
        if (err != null) StoreProblem = err;
        _stars = new Dictionary<string, List<StarRec>>(stars); // the lists are never changed after this, only replaced
        RaiseChanged();
    }

    private void PublishTraffic(Dictionary<string, RepoTraffic> traffic)
    {
        string? err = Store.Save(TrafficFile, traffic);
        if (err != null) StoreProblem = err;
        _traffic = new Dictionary<string, RepoTraffic>(traffic);
        RaiseChanged();
    }

    /// <summary>Write stats.md, stats.html, CSV files and badges into the publish folder.</summary>
    public void PublishShareNow()
    {
        try
        {
            Exports.Publish(this);
            ShareProblem = null;
        }
        catch (Exception ex)
        {
            ShareProblem = "Could not write the publish folder \"" + PublishPath + "\": " + ex.Message;
            Logger.Write(ShareProblem);
        }
    }

    // ------------------------------------------------------------------ scheduling

    private static bool Due(StatsData d)
    {
        long now = Clock.Now();
        if (now < d.NextAttempt) return false;
        if (d.PendingAll > 0) return true; // resume an unfinished cycle
        return now - d.CycleStart >= Config.MinIntervalSeconds;
    }

    /// <summary>Called for every page load and by the background timer: starts a refresh when one is due.</summary>
    /// <summary>A manual "Refresh now" is allowed this long after the start of the last cycle.</summary>
    public const int ManualIntervalSeconds = 3600;

    /// <summary>When the next automatic refresh may start (unix seconds).</summary>
    public long NextAutoAt
    {
        get
        {
            var d = _data;
            if (d.PendingAll > 0 || d.CycleStart == 0) return d.NextAttempt;
            return Math.Max(d.NextAttempt, d.CycleStart + Config.MinIntervalSeconds);
        }
    }

    /// <summary>When "Refresh now" becomes available (unix seconds): one hour after the last cycle started, and never during a rate-limit pause.</summary>
    public long ManualAllowedAt
    {
        get
        {
            var d = _data;
            long at = d.NextAttempt;
            if (d.PendingAll == 0 && d.CycleStart > 0) at = Math.Max(at, d.CycleStart + ManualIntervalSeconds);
            return Math.Min(at, NextAutoAt);
        }
    }

    /// <summary>Starts a refresh now if allowed. Returns null when started, otherwise the reason why not.</summary>
    public string? RefreshNow()
    {
        if (Running) return "A refresh is already running.";
        long at = ManualAllowedAt;
        if (Clock.Now() < at) return "Possible again at " + Hm(at) + ".";
        Poll(force: true);
        return null;
    }

    public void Poll() => Poll(false);

    public void Poll(bool force)
    {
        lock (_gate)
        {
            var d = _data;
            if (d.User.Length > 0 && !string.Equals(d.User, Cfg.User, StringComparison.OrdinalIgnoreCase))
            {
                d = new StatsData(); // username changed: start fresh
                _data = d;
                _stars = new Dictionary<string, List<StarRec>>();
                _traffic = new Dictionary<string, RepoTraffic>();
            }
            if (d.Auth != AuthFp)
            {
                // Token added/changed: an old rate-limit block says nothing about the new token.
                d = Clone(d);
                d.Rate = null;
                d.NextAttempt = 0;
                d.Auth = AuthFp;
                _data = d;
            }
            bool go = Due(d) || (force && Clock.Now() >= d.NextAttempt);
            if (go && Interlocked.CompareExchange(ref _running, 1, 0) == 0)
            {
                Logger.Write("Refresh started.");
                RaiseChanged();
                _ = Task.Run(RunSafe);
            }
        }
    }

    public void OnPageLoad() => Poll();

    private async Task RunSafe()
    {
        try
        {
            await RefreshAsync();
            var d = _data;
            Logger.Write(d.PendingAll == 0 && d.LastError == null
                ? "Refresh complete."
                : "Refresh paused: " + (d.LastError ?? "unfinished") + " (continues after " + Hm(d.NextAttempt) + ")");
        }
        catch (Exception ex)
        {
            try
            {
                var d = Clone(_data);
                d.LastAttempt = Clock.Now();
                d.LastError = "Unexpected error: " + ex.Message;
                d.NextAttempt = Clock.Now() + 1800;
                Publish(d);
            }
            catch { /* ignore */ }
            Logger.Write("Refresh failed: " + ex);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
            RaiseChanged();
        }
    }

    // ------------------------------------------------------------------ refresh

    private static bool NeedsStarSync(Repo r, Dictionary<string, List<StarRec>> stars) =>
        r.StarsSyncedTotal != r.Stars || (r.Stars > 0 && !stars.ContainsKey(r.Name));

    private static void AddHistory(List<long[]> h, long t, long downloads)
    {
        if (h.Count > 0 && h[^1][0] > t - 3600) h[^1] = new[] { t, downloads };
        else h.Add(new[] { t, downloads });
        while (h.Count > 60) h.RemoveAt(0);
    }

    /// <summary>
    /// Step 1 (new cycle): one request for all repo metadata, saved at once so stars/forks show immediately.
    /// Step 2: release downloads, repo by repo. Step 3: star dates. Step 4: traffic. Each repo is saved as soon as it is done;
    /// on any failure the progress is kept and the cycle resumes later.
    /// </summary>
    private async Task RefreshAsync()
    {
        long now = Clock.Now();
        var d = Clone(_data);
        var stars = new Dictionary<string, List<StarRec>>(_stars);
        var traffic = new Dictionary<string, RepoTraffic>(_traffic);
        string user = Cfg.User;
        d.User = user;
        d.Auth = AuthFp;
        d.LastAttempt = now;
        var gh = new GitHubClient(Cfg.Token, d.Rate);

        if (Rates.Blocked(d.Rate)) // known from the previous attempt: don't waste a request
        {
            d.LastError = Rates.Msg(d.Rate!);
            d.NextAttempt = d.Rate!.Reset + 5;
            Publish(d);
            return;
        }

        // Step 1: start a new cycle
        if (d.PendingAll == 0)
        {
            var list = await gh.ListAsync("/users/" + Uri.EscapeDataString(user) + "/repos", ("type", "owner"), ("sort", "full_name"));
            if (list.Items == null)
            {
                d.LastError = list.Error;
                d.Rate = gh.Rate;
                d.NextAttempt = Rates.RetryAt(gh.Rate);
                Publish(d);
                return;
            }

            bool had = d.Repos.Count > 0;
            var events = d.StarEvents;
            var fresh = new Dictionary<string, Repo>();
            foreach (var r in list.Items)
            {
                if (Json.Bool(r, "private")) continue; // public only, just in case
                if (!Cfg.IncludeForks && Json.Bool(r, "fork")) continue;

                string name = Json.Str(r, "name");
                if (name.Length == 0) continue;
                d.Repos.TryGetValue(name, out var old);
                long starCount = Json.Long(r, "stargazers_count");
                if (had) // log star changes (the very first cycle only sets the baseline)
                {
                    long prev = old?.Stars ?? 0;
                    if (prev != starCount)
                        events.Add(new StarEvent { T = now, Repo = name, From = prev, To = starCount, Delta = starCount - prev });
                }

                string license = "";
                if (r.TryGetProperty("license", out var lic) && lic.ValueKind == JsonValueKind.Object)
                    license = Json.Str(lic, "spdx_id");

                fresh[name] = new Repo
                {
                    Name = name,
                    Url = Json.Str(r, "html_url"),
                    Description = Json.Str(r, "description"),
                    Language = Json.Str(r, "language"),
                    Stars = starCount,
                    Forks = Json.Long(r, "forks_count"),
                    OpenIssues = Json.Long(r, "open_issues_count"),
                    Archived = Json.Bool(r, "archived"),
                    Fork = Json.Bool(r, "fork"),
                    License = license,
                    PushedAt = Json.Str(r, "pushed_at"),
                    CreatedAt = Json.Str(r, "created_at"),
                    // release data: keep what we had until it is refreshed
                    Downloads = old?.Downloads ?? 0,
                    ReleaseCount = old?.ReleaseCount ?? 0,
                    Releases = old?.Releases ?? new List<Release>(),
                    ReleasesAt = old?.ReleasesAt ?? 0,
                    StarsSyncedTotal = old?.StarsSyncedTotal ?? 0,
                };
            }

            d.Repos = fresh;
            d.StarEvents = events.TakeLast(Config.MaxEvents).ToList();
            d.CycleStart = now;
            d.Pending = fresh.Values.OrderBy(r => r.ReleasesAt).Select(r => r.Name).ToList(); // never-fetched first
            d.PendingStars = Cfg.TrackStars ? fresh.Values.Where(r => NeedsStarSync(r, stars)).Select(r => r.Name).ToList() : new List<string>();
            d.StarsNote = null;

            bool trafficBlocked = d.TrafficNote != null && d.TrafficNoteAuth == AuthFp && now - d.TrafficNoteAt < 86400; // retry once a day
            if (!Cfg.TrackTraffic) { d.TrafficNote = null; d.PendingTraffic = new List<string>(); }
            else if (trafficBlocked) d.PendingTraffic = new List<string>();
            else { d.TrafficNote = null; d.PendingTraffic = fresh.Keys.ToList(); }

            d.Rate = gh.Rate;
            d.LastError = null;
            Publish(d); // stars, forks, ... are visible from now on
        }

        bool failed = false;
        string? err = null;

        // Step 2: releases, repo by repo
        while (d.Pending.Count > 0)
        {
            if (Rates.Blocked(gh.Rate)) { err = Rates.Msg(gh.Rate!); failed = true; break; }

            string name = d.Pending[0];
            if (!d.Repos.TryGetValue(name, out var repo)) { d.Pending.RemoveAt(0); continue; }

            var res = await gh.ListAsync("/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(name) + "/releases");
            if (res.Items == null && res.Status == 404) res = new GhResult { Items = new List<JsonElement>(), Status = 200 };
            if (res.Items == null) { err = res.Error; failed = true; break; }

            long t = Clock.Now();
            var releases = new List<Release>();
            long repoDl = 0;
            foreach (var rel in res.Items)
            {
                if (Json.Bool(rel, "draft")) continue;
                var assets = new List<Asset>();
                long relDl = 0;
                if (rel.TryGetProperty("assets", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in arr.EnumerateArray())
                    {
                        long dl = Json.Long(a, "download_count");
                        relDl += dl;
                        assets.Add(new Asset { Name = Json.Str(a, "name"), Size = Json.Long(a, "size"), Downloads = dl });
                    }
                }
                repoDl += relDl;
                string tag = Json.Str(rel, "tag_name");
                var hist = new List<long[]>(repo.Releases.FirstOrDefault(o => o.Tag == tag)?.History ?? new List<long[]>());
                AddHistory(hist, t, relDl);
                releases.Add(new Release
                {
                    Tag = tag,
                    Name = Json.Str(rel, "name"),
                    Url = Json.Str(rel, "html_url"),
                    PublishedAt = Json.Str(rel, "published_at"),
                    Prerelease = Json.Bool(rel, "prerelease"),
                    Downloads = relDl,
                    Assets = assets,
                    History = hist,
                });
            }

            repo.Downloads = repoDl;
            repo.ReleaseCount = releases.Count;
            repo.Releases = releases;
            repo.ReleasesAt = t;
            d.Pending.RemoveAt(0);
            d.Rate = gh.Rate;
            Publish(d); // downloads show up one repository at a time
        }

        // Step 3: who starred, and when
        while (!failed && d.PendingStars.Count > 0)
        {
            if (Rates.Blocked(gh.Rate)) { err = Rates.Msg(gh.Rate!); failed = true; break; }

            string name = d.PendingStars[0];
            if (!d.Repos.TryGetValue(name, out var repo)) { d.PendingStars.RemoveAt(0); continue; }

            var (pause, perr, note) = await SyncStarsAsync(gh, user, repo, stars);
            if (pause) { err = perr; failed = true; break; }
            if (note != null) d.StarsNote = note;
            d.PendingStars.RemoveAt(0);
            d.Rate = gh.Rate;
            PublishStars(stars);
            Publish(d);
        }

        // Step 4: traffic (views, clones, referrers, popular pages)
        while (!failed && d.PendingTraffic.Count > 0)
        {
            if (Rates.Blocked(gh.Rate)) { err = Rates.Msg(gh.Rate!); failed = true; break; }

            string name = d.PendingTraffic[0];
            var (pause, denied, terr) = await FetchTrafficAsync(gh, user, name, traffic);
            if (pause) { err = terr; failed = true; break; }
            if (denied)
            {
                // Usually: the token has no push access. Not an error; remember it and try again tomorrow.
                d.TrafficNote = terr;
                d.TrafficNoteAuth = AuthFp;
                d.TrafficNoteAt = Clock.Now();
                d.PendingTraffic.Clear();
                break;
            }
            d.PendingTraffic.RemoveAt(0);
            d.Rate = gh.Rate;
            PublishTraffic(traffic);
            Publish(d);
        }
        d.Rate = gh.Rate;

        if (failed)
        {
            d.LastError = err;
            d.NextAttempt = Rates.RetryAt(gh.Rate);
            Publish(d); // partial data stays visible, the cycle resumes later
            return;
        }

        // Cycle complete: history snapshot
        var prevSnap = d.Snapshots.Count > 0 ? d.Snapshots[^1] : null;
        var snap = new Snapshot { T = Clock.Now() };
        foreach (var (name, r) in d.Repos)
        {
            snap.Stars += r.Stars;
            snap.Forks += r.Forks;
            snap.Downloads += r.Downloads;
            snap.Repos[name] = new long[] { r.Stars, r.Downloads, r.ReleaseCount };
        }
        d.Snapshots.Add(snap);
        if (d.Snapshots.Count > Config.MaxSnapshots) d.Snapshots = d.Snapshots.TakeLast(Config.MaxSnapshots).ToList();
        d.LastSuccess = Clock.Now();
        d.LastError = null;
        d.NextAttempt = 0;
        Publish(d);

        PublishShareNow();
        foreach (var (title, text) in BuildNotifications(prevSnap, snap, d, stars)) Notify(title, text);
        if (prevSnap != null)
        {
            long gained = d.StarEvents.Where(x => x.T == d.CycleStart && x.Delta > 0).Sum(x => x.Delta);
            if (gained > 0) RaiseNewStars((int)Math.Min(gained, 999));
        }
    }

    /// <summary>Also used by the "test fireworks" button.</summary>
    public void RaiseNewStars(int n)
    {
        try { NewStars?.Invoke(n); }
        catch (Exception ex) { Logger.Write("New-star handler failed: " + ex.Message); }
    }

    // ------------------------------------------------------------------ stars

    /// <summary>Fetches the stargazers of one repo: all of them the first time, afterwards only the last page(s).</summary>
    private static async Task<(bool Pause, string? Error, string? Note)> SyncStarsAsync(
        GitHubClient gh, string user, Repo repo, Dictionary<string, List<StarRec>> stars)
    {
        long total = repo.Stars;
        stars.TryGetValue(repo.Name, out var existing);
        existing ??= new List<StarRec>();

        if (total == 0)
        {
            stars[repo.Name] = new List<StarRec>();
            repo.StarsSyncedTotal = 0;
            return (false, null, null);
        }

        int lastPage = (int)((total + 99) / 100);
        long known = repo.StarsSyncedTotal;
        long offset = known - existing.Count; // stars older than the stored list (only when a repo has more than 5,000)
        bool full = existing.Count == 0 || total < known || offset < 0; // (a drop means someone un-starred: list again)
        int startPage = 1;
        int keep = 0;
        if (!full)
        {
            startPage = (int)(known / 100) + 1; // the page that holds the first star we have not seen yet
            long k = (long)(startPage - 1) * 100 - offset;
            if (k < 0 || k > existing.Count) full = true;
            else keep = (int)k;
        }
        if (full)
        {
            startPage = Math.Max(1, lastPage - Config.MaxStarPages + 1);
            keep = 0;
        }
        if (startPage > lastPage)
        {
            repo.StarsSyncedTotal = total;
            return (false, null, null);
        }

        int maxPages = Math.Min(Config.MaxStarPages, lastPage - startPage + 1);
        var res = await gh.StargazersAsync(user, repo.Name, startPage, maxPages);
        if (res.Items == null)
        {
            if (res.Status == 0 || Rates.Blocked(gh.Rate)) return (true, res.Error, null);
            return (false, null, "Could not read the stargazers of " + repo.Name + ": " + res.Error);
        }

        var fetched = new List<StarRec>(res.Items.Count);
        foreach (var e in res.Items)
        {
            long t = 0;
            string at = Json.Str(e, "starred_at");
            if (at.Length > 0 && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                t = dt.ToUnixTimeSeconds();
            string login = e.TryGetProperty("user", out var u) ? Json.Str(u, "login") : "";
            fetched.Add(new StarRec { U = login, T = t });
        }

        var list = new List<StarRec>(keep + fetched.Count);
        list.AddRange(existing.Take(keep));
        list.AddRange(fetched);
        stars[repo.Name] = list; // a new list: the one the page may be reading is never changed
        repo.StarsSyncedTotal = total;
        string? note = fetched.Count > 0 && fetched.All(f => f.T == 0) ? "GitHub did not return the star dates of " + repo.Name + "." : null;
        return (false, null, note);
    }

    // ------------------------------------------------------------------ traffic

    private static (bool Pause, bool Denied, string? Error) Classify(GhResult r, GitHubClient gh)
    {
        if (r.Status == 0 || Rates.Blocked(gh.Rate)) return (true, false, r.Error);
        return (false, true,
            "Traffic data (views, clones, referrers) is not available with this token: GitHub only gives it to someone with push access to the repository. "
            + "Use a fine-grained token with the permission \"Administration: read-only\" on your repositories, or a classic token with the \"public_repo\" scope. "
            + "GitHub said: " + r.Error);
    }

    /// <summary>Fetches views, clones, referrers and popular pages of one repo and merges the daily numbers into the stored history.</summary>
    private static async Task<(bool Pause, bool Denied, string? Error)> FetchTrafficAsync(
        GitHubClient gh, string user, string name, Dictionary<string, RepoTraffic> traffic)
    {
        string baseP = "/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(name) + "/traffic";
        var views = await gh.GetAsync(baseP + "/views");
        if (!views.Ok) return Classify(views, gh);
        var clones = await gh.GetAsync(baseP + "/clones");
        if (!clones.Ok) return Classify(clones, gh);
        var refs = await gh.GetAsync(baseP + "/popular/referrers");
        if (!refs.Ok && (refs.Status == 0 || Rates.Blocked(gh.Rate))) return (true, false, refs.Error);
        var paths = await gh.GetAsync(baseP + "/popular/paths");
        if (!paths.Ok && (paths.Status == 0 || Rates.Blocked(gh.Rate))) return (true, false, paths.Error);

        traffic.TryGetValue(name, out var old);
        var days = old != null ? new Dictionary<string, TrafficDay>(old.Days) : new Dictionary<string, TrafficDay>();

        void Merge(JsonElement root, string arrayName, bool isViews)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
            foreach (var e in arr.EnumerateArray())
            {
                string ts = Json.Str(e, "timestamp");
                if (ts.Length < 10) continue;
                string key = ts[..10]; // days are UTC; GitHub reports the latest (partial) day too, so a newer value replaces an older one
                var cur = days.TryGetValue(key, out var x) ? new TrafficDay { V = x.V, Uv = x.Uv, C = x.C, Uc = x.Uc } : new TrafficDay();
                long c = Json.Long(e, "count"), u = Json.Long(e, "uniques");
                if (isViews) { cur.V = c; cur.Uv = u; } else { cur.C = c; cur.Uc = u; }
                days[key] = cur;
            }
        }
        Merge(views.Root, "views", true);
        Merge(clones.Root, "clones", false);
        while (days.Count > 1500)
            days.Remove(days.Keys.OrderBy(k => k, StringComparer.Ordinal).First());

        var refList = new List<RefStat>();
        if (refs.Ok && refs.Root.ValueKind == JsonValueKind.Array)
            foreach (var e in refs.Root.EnumerateArray().Take(20))
                refList.Add(new RefStat { Referrer = Json.Str(e, "referrer"), Count = Json.Long(e, "count"), Uniques = Json.Long(e, "uniques") });
        var pathList = new List<PathStat>();
        if (paths.Ok && paths.Root.ValueKind == JsonValueKind.Array)
            foreach (var e in paths.Root.EnumerateArray().Take(20))
                pathList.Add(new PathStat { Path = Json.Str(e, "path"), Title = Json.Str(e, "title"), Count = Json.Long(e, "count"), Uniques = Json.Long(e, "uniques") });

        traffic[name] = new RepoTraffic
        {
            Days = days,
            Referrers = refList,
            Paths = pathList,
            FetchedAt = Clock.Now(),
            Views14 = Json.Long(views.Root, "count"),
            Uniques14 = Json.Long(views.Root, "uniques"),
            Clones14 = Json.Long(clones.Root, "count"),
            CloneUniques14 = Json.Long(clones.Root, "uniques"),
        };
        return (false, false, null);
    }

    // ------------------------------------------------------------------ notifications

    private List<(string Title, string Text)> BuildNotifications(Snapshot? prev, Snapshot cur, StatsData d, Dictionary<string, List<StarRec>> stars)
    {
        var res = new List<(string, string)>();
        if (prev == null) return res; // the first complete refresh only sets the baseline

        var starLines = new List<string>();
        foreach (var e in d.StarEvents.Where(x => x.T == d.CycleStart && x.Delta > 0))
        {
            string who = "";
            if (stars.TryGetValue(e.Repo, out var l) && l.Count > 0)
            {
                int take = (int)Math.Min(e.Delta, 3);
                who = string.Join(", ", l.TakeLast(take).Select(x => x.U).Where(x => x.Length > 0).Reverse());
                if (e.Delta > 3 && who.Length > 0) who += " +" + (e.Delta - 3) + " more";
            }
            starLines.Add(e.Repo + ": +" + e.Delta + (who.Length > 0 ? " (" + who + ")" : ""));
        }
        if (starLines.Count > 0) res.Add(("New GitHub stars", string.Join("\n", starLines)));

        long Crossed(long a, long b) => Cfg.Milestones.Where(m => a < m && m <= b).DefaultIfEmpty(0).Max();
        var lines = new List<string>();
        long mAll = Crossed(prev.Downloads, cur.Downloads);
        if (mAll > 0) lines.Add("All repositories passed " + N(mAll) + " downloads");
        foreach (var (name, v) in cur.Repos)
        {
            if (!prev.Repos.TryGetValue(name, out var pv) || pv.Length < 2 || v.Length < 2) continue;
            long m = Crossed(pv[1], v[1]);
            if (m > 0) lines.Add(name + " passed " + N(m) + " downloads");
        }
        if (lines.Count > 0) res.Add(("Download milestone", string.Join("\n", lines)));
        return res;
    }

    public void Notify(string title, string text)
    {
        Logger.Write("Notification: " + title + " - " + text.Replace("\n", "; "));
        if (!Cfg.Notifications) return;
        try { NotifyHandler?.Invoke(title, text); }
        catch (Exception ex) { Logger.Write("Notification failed: " + ex.Message); }
    }
}
