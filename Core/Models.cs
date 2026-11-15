using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitHubStats;

// The property names are written in snake_case (see Json.Opt), which matches the stats.json
// layout of the earlier versions; new fields simply default when an older file is loaded.

internal sealed class Asset
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public long Downloads { get; set; }
}

internal sealed class Release
{
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public bool Prerelease { get; set; }
    public long Downloads { get; set; }
    public List<Asset> Assets { get; set; } = new();
    /// <summary>Download count over time, [unix time, downloads], one entry per refresh (newest last, max 60).</summary>
    public List<long[]> History { get; set; } = new();
}

internal sealed class Repo
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
    public string Language { get; set; } = "";
    public long Stars { get; set; }
    public long Forks { get; set; }
    public long OpenIssues { get; set; }
    public bool Archived { get; set; }
    public bool Fork { get; set; }
    public string License { get; set; } = "";
    public string PushedAt { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public long Downloads { get; set; }
    public int ReleaseCount { get; set; }
    public List<Release> Releases { get; set; } = new();
    /// <summary>Unix time the release data was last fetched; 0 = not fetched yet.</summary>
    public long ReleasesAt { get; set; }
    /// <summary>The star count the stored stargazer list (stars.json) was synced at.</summary>
    public long StarsSyncedTotal { get; set; }
}

internal sealed class RateInfo
{
    public long Limit { get; set; }
    public long Remaining { get; set; }
    public long Reset { get; set; }
    public long Checked { get; set; }
}

internal sealed class StarEvent
{
    public long T { get; set; }
    public string Repo { get; set; } = "";
    public long From { get; set; }
    public long To { get; set; }
    public long Delta { get; set; }
}

internal sealed class Snapshot
{
    public long T { get; set; }
    public long Stars { get; set; }
    public long Forks { get; set; }
    public long Downloads { get; set; }
    /// <summary>repo name -> [stars, downloads, releases]</summary>
    public Dictionary<string, long[]> Repos { get; set; } = new();
}

internal sealed class StatsData
{
    public int Version { get; set; } = 3;
    public string User { get; set; } = "";
    public long LastAttempt { get; set; }
    public long LastSuccess { get; set; }
    public long CycleStart { get; set; }
    public long NextAttempt { get; set; }
    /// <summary>Repos whose releases still have to be fetched in this cycle.</summary>
    public List<string> Pending { get; set; } = new();
    /// <summary>Repos whose stargazers still have to be synced in this cycle.</summary>
    public List<string> PendingStars { get; set; } = new();
    /// <summary>Repos whose traffic data still has to be fetched in this cycle.</summary>
    public List<string> PendingTraffic { get; set; } = new();
    public string? LastError { get; set; }
    public RateInfo? Rate { get; set; }
    public string Auth { get; set; } = "";
    public string? StarsNote { get; set; }
    public string? TrafficNote { get; set; }
    public string TrafficNoteAuth { get; set; } = "";
    public long TrafficNoteAt { get; set; }
    public Dictionary<string, Repo> Repos { get; set; } = new();
    public List<Snapshot> Snapshots { get; set; } = new();
    public List<StarEvent> StarEvents { get; set; } = new();

    [JsonIgnore]
    public int PendingAll => Pending.Count + PendingStars.Count + PendingTraffic.Count;
}

// ---------- stars.json: who starred a repo and when (as reported by GitHub) ----------

internal sealed class StarRec
{
    /// <summary>GitHub login</summary>
    public string U { get; set; } = "";
    /// <summary>Unix time of the star</summary>
    public long T { get; set; }
}

// ---------- traffic.json: daily views/clones that GitHub only keeps for 14 days ----------

internal sealed class TrafficDay
{
    public long V { get; set; }
    public long Uv { get; set; }
    public long C { get; set; }
    public long Uc { get; set; }
}

internal sealed class RefStat
{
    public string Referrer { get; set; } = "";
    public long Count { get; set; }
    public long Uniques { get; set; }
}

internal sealed class PathStat
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public long Count { get; set; }
    public long Uniques { get; set; }
}

internal sealed class RepoTraffic
{
    /// <summary>yyyy-MM-dd (UTC) -> that day's numbers</summary>
    public Dictionary<string, TrafficDay> Days { get; set; } = new();
    public List<RefStat> Referrers { get; set; } = new();
    public List<PathStat> Paths { get; set; } = new();
    public long FetchedAt { get; set; }
    public long Views14 { get; set; }
    public long Uniques14 { get; set; }
    public long Clones14 { get; set; }
    public long CloneUniques14 { get; set; }
}

internal sealed class Totals
{
    public int Repos { get; set; }
    public long Stars { get; set; }
    public long Forks { get; set; }
    public long OpenIssues { get; set; }
    public long Releases { get; set; }
    public long Downloads { get; set; }
    public int ReposLoading { get; set; }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static readonly JsonSerializerOptions Pretty = new(Opt)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Str(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    public static long Long(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)
            ? l : 0;

    public static bool Bool(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
}

/// <summary>Atomic load/save of the small JSON side files (stars.json, traffic.json).</summary>
internal static class Store
{
    public static T Load<T>(string file, T fallback)
    {
        try
        {
            if (File.Exists(file))
            {
                var v = JsonSerializer.Deserialize<T>(File.ReadAllText(file), Json.Opt);
                if (v != null) return v;
            }
        }
        catch (Exception ex)
        {
            try { File.Move(file, file + ".bad-" + Clock.Now(), true); } catch { /* ignore */ }
            Logger.Write(Path.GetFileName(file) + " could not be read (" + ex.Message + "); started with empty data.");
        }
        return fallback;
    }

    /// <summary>Returns an error message, or null when saved.</summary>
    public static string? Save<T>(string file, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json.Opt), new System.Text.UTF8Encoding(false));
            File.Move(tmp, file, true);
            return null;
        }
        catch (Exception ex)
        {
            return "Could not write " + file + ": " + ex.Message;
        }
    }
}
