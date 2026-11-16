using System.Text.Json;

namespace GitHubStats;

internal enum TrafficAccess { Untested, Yes, NoPermission, RepoNotIncluded }

/// <summary>What a token is and what GitHub Stats may read with it.</summary>
internal sealed class TokenReport
{
    public string Kind = "";
    public bool Valid;
    public string? Error;
    public string Login = "";
    public bool OtherOwner;
    /// <summary>Classic tokens only: the ticked scopes (empty list = none ticked).</summary>
    public List<string>? Scopes;
    public string? Expires;
    public long Limit, Remaining, Reset;
    public TrafficAccess Traffic;
    /// <summary>Star dates: since July 2026 GitHub only lists stargazers to repository admins and collaborators.</summary>
    public TrafficAccess Stars;
    public string StarsRepo = "";
    public string NeededPermission = "";
    public string TrafficError = "";
    public string SampleRepo = "";

    public bool FineGrained => Kind == "fine-grained";

    public static string KindOf(string token) =>
        token.Length == 0 ? "none"
        : token.StartsWith("github_pat_", StringComparison.Ordinal) ? "fine-grained"
        : token.StartsWith("ghp_", StringComparison.Ordinal) ? "classic"
        : token.StartsWith("gho_", StringComparison.Ordinal) ? "OAuth app"
        : token.StartsWith("ghu_", StringComparison.Ordinal) || token.StartsWith("ghs_", StringComparison.Ordinal) ? "GitHub App"
        : "unknown";
}

internal sealed class UpdateInfo
{
    public bool Newer;
    public string Version = "";
    public string Url = "";
    public string? Error;
}

/// <summary>One-off GitHub checks used by the Settings screen: token report and update check.</summary>
internal static class Tools
{
    /// <summary>
    /// Reads a token's details and checks, feature by feature, what it may read. Costs at most 3 requests
    /// (/rate_limit is free, /user, and one traffic request on <paramref name="sampleRepo"/>).
    /// </summary>
    public static async Task<TokenReport> TokenReportAsync(string token, string user, string? sampleRepo, string? starredRepo = null)
    {
        var rep = new TokenReport { Kind = TokenReport.KindOf(token) };
        var gh = new GitHubClient(token, null);

        var rl = await gh.GetAsync("/rate_limit");
        if (!rl.Ok)
        {
            rep.Error = rl.Status == 401
                ? "GitHub doesn't accept this token (" + rl.Error + "). Check that it was copied completely and hasn't expired or been deleted."
                : "Could not reach GitHub: " + rl.Error;
            return rep;
        }
        if (rl.Root.TryGetProperty("resources", out var res) && res.TryGetProperty("core", out var core))
        {
            rep.Limit = Json.Long(core, "limit");
            rep.Remaining = Json.Long(core, "remaining");
            rep.Reset = Json.Long(core, "reset");
        }
        if (token.Length == 0) return rep;

        var me = await gh.GetAsync("/user");
        if (me.Ok) rep.Login = Json.Str(me.Root, "login");
        foreach (var h in new[] { me.Headers, rl.Headers })
        {
            if (rep.Scopes == null && h.TryGetValue("x-oauth-scopes", out var sc)) rep.Scopes = sc.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            if (rep.Expires == null && h.TryGetValue("github-authentication-token-expiration", out var ex)) rep.Expires = ex;
        }
        rep.Valid = true;
        rep.OtherOwner = rep.Login.Length > 0 && !rep.Login.Equals(user, StringComparison.OrdinalIgnoreCase);

        // star dates: one stargazer of a repository that has stars
        if (starredRepo != null)
        {
            rep.StarsRepo = starredRepo;
            var st = await gh.GetAsync("/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(starredRepo) + "/stargazers?per_page=1");
            rep.Stars = st.Ok ? TrafficAccess.Yes : st.Status is 401 or 403 or 404 ? TrafficAccess.NoPermission : TrafficAccess.Untested;
        }

        if (sampleRepo == null) { rep.Traffic = TrafficAccess.Untested; return rep; }
        rep.SampleRepo = sampleRepo;
        var tr = await gh.GetAsync("/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(sampleRepo) + "/traffic/views");
        if (tr.Ok) rep.Traffic = TrafficAccess.Yes;
        else
        {
            tr.Headers.TryGetValue("x-accepted-github-permissions", out var need);
            rep.NeededPermission = need ?? "";
            rep.Traffic = tr.Status == 404 ? TrafficAccess.RepoNotIncluded
                : tr.Status == 403 ? TrafficAccess.NoPermission
                : TrafficAccess.Untested;
            if (rep.Traffic == TrafficAccess.Untested) rep.TrafficError = tr.Error ?? "";
        }
        return rep;
    }

    /// <summary>GitHub's page for a new fine-grained token, already filled in for GitHub Stats (template URL, 2025).</summary>
    public const string FineGrainedTemplateUrl =
        "https://github.com/settings/personal-access-tokens/new?name=GitHub%20Stats&description=Read-only%20statistics%20for%20GitHub%20Stats%20(downloads%2C%20stars%2C%20traffic)&administration=read";

    /// <summary>GitHub's page for a new classic token with only the public_repo scope ticked.</summary>
    public const string ClassicTemplateUrl = "https://github.com/settings/tokens/new?scopes=public_repo&description=GitHub%20Stats";

    /// <summary>The list of your fine-grained tokens (to edit an existing one).</summary>
    public const string FineGrainedListUrl = "https://github.com/settings/personal-access-tokens";

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
