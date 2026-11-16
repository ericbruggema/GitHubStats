using System.Net.Http.Headers;
using System.Text.Json;

namespace GitHubStats;

internal sealed class GhResult
{
    /// <summary>The parsed JSON body (valid when <see cref="Error"/> is null).</summary>
    public JsonElement Root;
    /// <summary>All items of a paged array endpoint (null on error).</summary>
    public List<JsonElement>? Items;
    public string? Error;
    /// <summary>HTTP status; 0 = network problem.</summary>
    public int Status;
    public bool Ok => Error == null;
    /// <summary>Token-related response headers (lower-case names): x-oauth-scopes, github-authentication-token-expiration, x-accepted-github-permissions.</summary>
    public Dictionary<string, string> Headers = new();
}

internal static class Rates
{
    public static bool Blocked(RateInfo? r) => r != null && r.Remaining <= 0 && r.Reset > Clock.Now();

    public static string Msg(RateInfo r) =>
        "GitHub rate limit reached, resets at " + Fmt.Hm(r.Reset) + ". A token in config.json gives much higher limits.";

    public static long RetryAt(RateInfo? r) => Blocked(r) ? r!.Reset + 5 : Clock.Now() + 1800;
}

internal sealed class GitHubClient
{
    private const string StarAccept = "application/vnd.github.star+json";

    private static readonly HttpClient Http = Create();
    private static readonly string BaseUrl =
        (Environment.GetEnvironmentVariable("GHSTATS_API") ?? "https://api.github.com").TrimEnd('/');

    private readonly string _token;
    public RateInfo? Rate;

    public GitHubClient(string token, RateInfo? rate)
    {
        _token = token;
        Rate = rate;
    }

    private static HttpClient Create()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("github-stats-app/1.1");
        h.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        h.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return h;
    }

    /// <summary>GET a JSON array endpoint, following pages (max 10 x 100 items).</summary>
    public Task<GhResult> ListAsync(string path, params (string Key, string Value)[] query) =>
        ListPagedAsync(path, query, null, 1, 10);

    /// <summary>The stargazers of a repo with the date of each star (oldest first), from page <paramref name="startPage"/> on.</summary>
    public Task<GhResult> StargazersAsync(string user, string repo, int startPage, int maxPages) =>
        ListPagedAsync("/repos/" + Uri.EscapeDataString(user) + "/" + Uri.EscapeDataString(repo) + "/stargazers",
                       Array.Empty<(string, string)>(), StarAccept, startPage, maxPages);

    public async Task<GhResult> ListPagedAsync(string path, (string Key, string Value)[] query, string? accept, int startPage, int maxPages)
    {
        var all = new List<JsonElement>();
        for (int page = startPage; page < startPage + maxPages; page++)
        {
            var parts = query.Select(q => Uri.EscapeDataString(q.Key) + "=" + Uri.EscapeDataString(q.Value)).ToList();
            parts.Add("per_page=100");
            parts.Add("page=" + page);
            var r = await SendAsync(BaseUrl + path + "?" + string.Join("&", parts), accept);
            if (!r.Ok) return r;
            if (r.Root.ValueKind != JsonValueKind.Array)
                return new GhResult { Error = "Unexpected response from GitHub", Status = r.Status };
            int count = 0;
            foreach (var e in r.Root.EnumerateArray()) { all.Add(e); count++; }
            if (count < 100) break;
        }
        return new GhResult { Items = all, Status = 200 };
    }

    /// <summary>GET a JSON endpoint (object or array) once.</summary>
    public Task<GhResult> GetAsync(string path) => SendAsync(BaseUrl + path, null);

    private async Task<GhResult> SendAsync(string url, string? accept)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (accept != null)
            {
                req.Headers.Accept.Clear();
                req.Headers.Accept.ParseAdd(accept);
            }
            if (_token.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var resp = await Http.SendAsync(req);

            if (resp.Headers.TryGetValues("X-RateLimit-Remaining", out var rem))
            {
                long.TryParse(rem.FirstOrDefault(), out long remaining);
                long limit = 0, reset = 0;
                if (resp.Headers.TryGetValues("X-RateLimit-Limit", out var lim)) long.TryParse(lim.FirstOrDefault(), out limit);
                if (resp.Headers.TryGetValues("X-RateLimit-Reset", out var rst)) long.TryParse(rst.FirstOrDefault(), out reset);
                Rate = new RateInfo { Limit = limit, Remaining = remaining, Reset = reset, Checked = Clock.Now() };
            }

            var heads = new Dictionary<string, string>();
            foreach (var h in new[] { "X-OAuth-Scopes", "github-authentication-token-expiration", "X-Accepted-GitHub-Permissions", "X-Accepted-OAuth-Scopes" })
                if (resp.Headers.TryGetValues(h, out var hv)) heads[h.ToLowerInvariant()] = string.Join(",", hv);

            int code = (int)resp.StatusCode;
            string body = await resp.Content.ReadAsStringAsync();
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(body); } catch (JsonException) { }

            using (doc)
            {
                if (code != 200)
                {
                    string msg = doc != null && doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString() ?? ("HTTP " + code) : "HTTP " + code;
                    if ((code == 403 || code == 429) && Rate != null && Rate.Remaining <= 0) msg = Rates.Msg(Rate);
                    return new GhResult { Error = msg, Status = code, Headers = heads };
                }
                if (doc == null) return new GhResult { Error = "Unexpected response from GitHub", Status = code };
                return new GhResult { Root = doc.RootElement.Clone(), Status = code, Headers = heads };
            }
        }
        catch (Exception ex)
        {
            return new GhResult { Error = "Network error: " + ex.Message, Status = 0 };
        }
    }
}
