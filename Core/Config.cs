using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GitHubStats;

internal sealed class Config
{
    public string User { get; set; } = "ericbruggema";
    public string Token { get; set; } = "";
    public bool IncludeForks { get; set; }
    public string Timezone { get; set; } = "";
    public string DataDir { get; set; } = "data";

    /// <summary>"auto" (only with a token), "on" or "off": real star dates and who starred.</summary>
    public string StarsDetail { get; set; } = "auto";
    /// <summary>"auto" (only with a token), "on" or "off": views, clones, referrers (needs push access).</summary>
    public string Traffic { get; set; } = "auto";
    /// <summary>Refresh by itself while the program runs (every <see cref="RefreshMinutes"/>).</summary>
    public bool BackgroundRefresh { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public List<long> Milestones { get; set; } = new() { 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };
    /// <summary>Folder that receives stats.md, stats.html, CSV files and badges after every refresh ("" = off).</summary>
    public string PublishDir { get; set; } = "share";
    /// <summary>"auto" (the Windows list separator, which is what Excel expects), ",", ";" or "tab".</summary>
    public string CsvSeparator { get; set; } = "auto";
    /// <summary>Repositories left out of every screen, total and export (their data is kept).</summary>
    public List<string> HiddenRepos { get; set; } = new();
    /// <summary>Look for a newer version on GitHub when the program starts (at most once a day).</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>"owner/repo" whose GitHub releases contain new versions of this program.</summary>
    public string UpdateRepo { get; set; } = "ericbruggema/GitHubStats";

    /// <summary>Minutes between automatic refreshes; 0 = auto (30 minutes with a token, 6 hours without).
    /// With a token this can go down to 15; without one it is at least 6 hours.</summary>
    public int RefreshMinutes { get; set; }

    public const int AutoMinutesWithToken = 30;

    public const int MinMinutesWithToken = 15;
    public const int MinMinutesWithoutToken = 360;

    /// <summary>The settings in use (set by <see cref="Load"/>), so static helpers can read the refresh interval.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public static Config? Current { get; private set; }

    private static readonly int TestInterval =
        int.TryParse(Environment.GetEnvironmentVariable("GHSTATS_MININTERVAL"), out int mi) && mi > 0 ? mi : 0; // test hook

    /// <summary>Seconds between two automatic refresh cycles (the chosen interval, but never below what the token allows).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int IntervalSeconds => TestInterval > 0 ? TestInterval
        : (RefreshMinutes <= 0
            ? (Token.Length > 0 ? AutoMinutesWithToken : MinMinutesWithoutToken)
            : Math.Max(RefreshMinutes, Token.Length > 0 ? MinMinutesWithToken : MinMinutesWithoutToken)) * 60;

    /// <summary>"Refresh now" is possible this long after the previous refresh: 5 minutes with a token, 1 hour without.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int ManualSeconds => Token.Length > 0 ? 300 : 3600;

    public static int MinIntervalSeconds => Current?.IntervalSeconds ?? 6 * 3600;
    public static readonly int TickSeconds =
        int.TryParse(Environment.GetEnvironmentVariable("GHSTATS_TICK"), out int tk) && tk > 0 ? tk : 60;
    public const int MaxSnapshots = 4000;
    public const int MaxEvents = 300;
    public const int MaxStarPages = 50; // 5,000 stars per repo; older ones beyond that are not listed individually

    /// <summary>True when the token comes from the GITHUB_TOKEN environment variable (then it is never written to config.json).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TokenFromEnv { get; set; }

    public bool TrackStars => StarsDetail == "on" || (StarsDetail == "auto" && Token.Length > 0);
    public bool TrackTraffic => Traffic == "on" || (Traffic == "auto" && Token.Length > 0);

    public string Sep => CsvSeparator switch
    {
        "," => ",",
        ";" => ";",
        "tab" => "\t",
        _ => CultureInfo.CurrentCulture.TextInfo.ListSeparator is { Length: 1 } s && (s == ";" || s == ",") ? s : ",",
    };

    private const string Template = """
{
  // Settings for GitHub Stats. Easiest: use the Settings screen in the program; you can also edit this file and restart.

  // GitHub username whose PUBLIC repositories are shown
  "user": "ericbruggema",

  // Optional but recommended: a GitHub personal access token.
  // Without it GitHub allows only 60 API requests per hour per IP address.
  //  - Stats, downloads and star dates: a classic token with NO scopes ticked is enough.
  //  - Traffic (views, clones, referrers): GitHub only gives that to someone with push access.
  //    Use a fine-grained token (your repositories, permission "Administration: read-only")
  //    or a classic token with the "public_repo" scope.
  // Create one at https://github.com/settings/tokens
  "token": "",

  // Show forked repositories too?
  "include_forks": false,

  // Time zone for the times on the page, e.g. "Europe/Amsterdam" or "W. Europe Standard Time".
  // Leave empty to use the Windows time zone.
  "timezone": "",

  // Real star dates and who starred: "auto" (when a token is set), "on" or "off"
  "stars_detail": "auto",

  // Traffic history (views, clones, referrers; GitHub only keeps 14 days, this program keeps it all):
  // "auto" (when a token is set; quietly skipped if the token has no push access), "on" or "off"
  "traffic": "auto",

  // Refresh by itself while the program is running (every refresh_minutes, see below).
  // false = refresh only when the window is opened.
  "background_refresh": true,

  // Minutes between automatic refreshes. 0 = auto: every 30 minutes with a token, every 6 hours without.
  // Without a token at least 360 (6 hours), with a token at least 15.
  "refresh_minutes": 0,

  // Windows notifications for new stars and download milestones (needs the tray icon)
  "notifications": true,

  // Download totals that trigger a milestone notification
  "milestones": [100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000],

  // Folder that receives stats.md (README block), stats.html, CSV files and badge images
  // after every refresh. Relative paths are relative to this program. "" = off.
  "publish_dir": "share",

  // CSV separator: "auto" (the Windows list separator, so Excel opens it correctly), ",", ";" or "tab"
  "csv_separator": "auto",

  // Folder for the cached data (stats.json, stars.json, traffic.json, log.txt).
  // Relative paths are relative to this program.
  "data_dir": "data"
}
""";

    public static Config Load(string home, out string? warning)
    {
        warning = null;
        string path = Path.Combine(home, "config.json");
        if (!File.Exists(path))
        {
            try { File.WriteAllText(path, Template.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false)); }
            catch (Exception ex) { warning = "Could not create config.json: " + ex.Message; }
        }

        var c = new Config();
        if (File.Exists(path))
        {
            try { c = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Json.Opt) ?? new Config(); }
            catch (Exception ex) { warning = "config.json could not be read (" + ex.Message + "). Using defaults."; }
        }

        c.User = (c.User ?? "").Trim();
        if (c.User.Length == 0) c.User = "ericbruggema";

        c.Token = (c.Token ?? "").Trim().Trim('"', '\'').Trim();
        if (c.Token.Length == 0)
        {
            c.Token = (Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "").Trim();
            c.TokenFromEnv = c.Token.Length > 0;
        }

        c.StarsDetail = Mode(c.StarsDetail);
        c.Traffic = Mode(c.Traffic);
        c.CsvSeparator = (c.CsvSeparator ?? "auto").Trim().ToLowerInvariant();
        c.PublishDir = (c.PublishDir ?? "").Trim();
        c.Milestones = (c.Milestones ?? new List<long>()).Where(m => m > 0).Distinct().OrderBy(m => m).ToList();

        c.HiddenRepos = (c.HiddenRepos ?? new List<string>()).Select(h => (h ?? "").Trim()).Where(h => h.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList();
        c.UpdateRepo = (c.UpdateRepo ?? "").Trim().Trim('/');

        c.RefreshMinutes = c.RefreshMinutes <= 0 ? 0 : Math.Max(MinMinutesWithToken, Math.Min(24 * 60, c.RefreshMinutes));

        if (string.IsNullOrWhiteSpace(c.DataDir)) c.DataDir = "data";
        Current = c;
        return c;
    }

    /// <summary>Saves the settings to config.json (the explanatory comments of the first-run file are not kept). Returns an error message or null.</summary>
    public string? Save(string home)
    {
        try
        {
            var copy = (Config)MemberwiseClone();
            if (TokenFromEnv) copy.Token = "";
            string path = Path.Combine(home, "config.json");
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(copy, Json.Pretty), new UTF8Encoding(false));
            File.Move(tmp, path, true);
            return null;
        }
        catch (Exception ex) { return "Could not save config.json: " + ex.Message; }
    }

    private static string Mode(string? v)
    {
        v = (v ?? "auto").Trim().ToLowerInvariant();
        return v is "on" or "off" or "auto" ? v : "auto";
    }
}

internal static class Clock
{
    public static TimeZoneInfo Tz = TimeZoneInfo.Local;

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static string? SetZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) { Tz = TimeZoneInfo.Local; return null; }
        try { Tz = TimeZoneInfo.FindSystemTimeZoneById(id.Trim()); return null; }
        catch (Exception)
        {
            Tz = TimeZoneInfo.Local;
            return "Time zone \"" + id + "\" was not found; using the computer's time zone.";
        }
    }
}
