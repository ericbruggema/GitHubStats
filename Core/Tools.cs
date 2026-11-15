using System.Text.Json;

namespace GitHubStats;

internal sealed class UpdateInfo
{
    public bool Newer;
    public string Version = "";
    public string Url = "";
    public string? Error;
}

/// <summary>One-off GitHub checks used by the Settings screen: token test and update check.</summary>
internal static class Tools
{
    /// <summary>Checks a token and what it unlocks. Each line: (ok, text). Uses /rate_limit (free) and one traffic request.</summary>
    public static async Task<List<(bool Ok, string Text)>> TestTokenAsync(string token, string user, string? sampleRepo)
    {
        var lines = new List<(bool, string)>();
        var gh = new GitHubClient(token, null);
        var rl = await gh.GetAsync("/rate_limit");
        if (!rl.Ok)
        {
            if (rl.Status == 401) lines.Add((false, "The token is not valid (GitHub says: " + rl.Error + "). Check that it was copied completely and has not expired."));
            else lines.Add((false, "Could not reach GitHub: " + rl.Error));
            return lines;
        }
        long limit = 0, left = 0;
        if (rl.Root.TryGetProperty("resources", out var res) && res.TryGetProperty("core", out var core))
        {
            limit = Json.Long(core, "limit");
            left = Json.Long(core, "remaining");
        }
        if (token.Length == 0)
        {
            lines.Add((false, "No token: " + Fmt.N(limit) + " requests per hour, shared by everything on this internet connection."));
            lines.Add((false, "Star dates and traffic need a token."));
            return lines;
        }
        lines.Add((true, "Token works: " + Fmt.N(limit) + " requests per hour (" + Fmt.N(left) + " left now)."));
        lines.Add((true, "Downloads, stars and star dates."));

        if (sampleRepo == null)
        {
            lines.Add((false, "Traffic could not be tested yet (no repositories loaded)."));
            return lines;
        }
        var tr = await gh.GetAsync("/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(sampleRepo) + "/traffic/views");
        if (tr.Ok) lines.Add((true, "Traffic (views, clones, referrers)."));
        else if (tr.Status is 403 or 404)
            lines.Add((false, "Traffic: no access. It needs push access: a fine-grained token with \"Administration: read-only\", or a classic token with the \"public_repo\" scope."));
        else lines.Add((false, "Traffic could not be tested: " + tr.Error));
        return lines;
    }

    /// <summary>Asks GitHub for the latest release of <paramref name="repo"/> ("owner/name") and compares it with <paramref name="current"/>.</summary>
    public static async Task<UpdateInfo> CheckUpdateAsync(string repo, string token, Version current)
    {
        var info = new UpdateInfo();
        if (repo.Split('/').Length != 2) { info.Error = "No update source set."; return info; }
        var gh = new GitHubClient(token, null);
        var parts = repo.Split('/');
        var r = await gh.GetAsync("/repos/" + Uri.EscapeDataString(parts[0]) + "/" + Uri.EscapeDataString(parts[1]) + "/releases/latest");
        if (!r.Ok)
        {
            info.Error = r.Status == 404 ? "No releases published on github.com/" + repo + " yet." : "Could not check: " + r.Error;
            return info;
        }
        string tag = Json.Str(r.Root, "tag_name");
        info.Version = tag.TrimStart('v', 'V');
        info.Url = Json.Str(r.Root, "html_url");
        if (r.Root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                string name = Json.Str(a, "name");
                if (name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase)) { info.Url = Json.Str(a, "browser_download_url"); break; }
            }
        }
        info.Newer = System.Version.TryParse(Pad(info.Version), out var v) && v > current;
        return info;
    }

    private static string Pad(string v)
    {
        var p = v.Split('-')[0].Split('.');
        return p.Length == 1 ? p[0] + ".0" : string.Join(".", p.Take(4));
    }
}
