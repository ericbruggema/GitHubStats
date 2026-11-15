using System.Text;
using static GitHubStats.Fmt;

namespace GitHubStats;

/// <summary>CSV files, the README block, a static HTML page and SVG badges, built from the stored data.</summary>
internal static class Exports
{
    public static readonly string[] CsvKinds = { "repos", "releases", "assets", "history", "stars", "traffic" };

    // ------------------------------------------------------------------ CSV

    private static string Cell(object? v, string sep)
    {
        if (v == null) return "";
        switch (v)
        {
            case bool b: return b ? "yes" : "no";
            case long or int or short or byte: return Convert.ToString(v, C) ?? "";
            case double d: return d.ToString("0.##", C);
        }
        string s = v.ToString() ?? "";
        // Spreadsheets run text that starts with = + - @ as a formula; names and descriptions come from GitHub, so defuse them.
        if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@' || s[0] == '\t' || s[0] == '\r')) s = "'" + s;
        if (s.IndexOfAny(new[] { '"', '\n', '\r' }) >= 0 || s.Contains(sep)) s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string Row(string sep, params object?[] cells) => string.Join(sep, cells.Select(c => Cell(c, sep)));

    public static (string Content, string FileName)? Csv(App app, string what)
    {
        string sep = app.Cfg.Sep;
        var d = app.Data;
        var lines = new List<string>();
        var repos = d.Repos.Values.OrderByDescending(r => r.Downloads).ThenByDescending(r => r.Stars).ToList();
        var base7 = Calc.Baseline(d.Snapshots, 7 * 86400);

        switch (what)
        {
            case "repos":
                lines.Add(Row(sep, "repo", "url", "description", "language", "stars", "forks", "open_issues", "releases", "downloads", "downloads_7d", "archived", "fork", "license", "created", "last_push"));
                foreach (var r in repos)
                {
                    long? d7 = base7 != null && d.Snapshots.Count > 0 && d.Snapshots[^1].Repos.ContainsKey(r.Name)
                        ? Calc.SnapDl(d.Snapshots[^1], r.Name) - Calc.SnapDl(base7, r.Name) : null;
                    lines.Add(Row(sep, r.Name, r.Url, r.Description, r.Language, r.Stars, r.Forks, r.OpenIssues, r.ReleaseCount, r.Downloads, d7, r.Archived, r.Fork, r.License, FDate(r.CreatedAt), FDate(r.PushedAt)));
                }
                break;

            case "releases":
                lines.Add(Row(sep, "repo", "tag", "name", "published", "prerelease", "downloads", "share_percent", "avg_per_day", "downloads_7d", "assets", "url"));
                foreach (var r in repos)
                    foreach (var rel in r.Releases)
                        lines.Add(Row(sep, r.Name, rel.Tag, rel.Name, FDate(rel.PublishedAt), rel.Prerelease, rel.Downloads,
                            r.Downloads > 0 ? Math.Round(100.0 * rel.Downloads / r.Downloads, 1) : 0.0,
                            Math.Round(Calc.AvgPerDay(rel), 2), Calc.ReleaseDelta(rel, 7 * 86400), rel.Assets.Count, rel.Url));
                break;

            case "assets":
                lines.Add(Row(sep, "repo", "tag", "asset", "platform_guess", "size_bytes", "downloads"));
                foreach (var r in repos)
                    foreach (var rel in r.Releases)
                        foreach (var a in rel.Assets)
                            lines.Add(Row(sep, r.Name, rel.Tag, a.Name, Calc.Platform(a.Name), a.Size, a.Downloads));
                break;

            case "history":
                lines.Add(Row(sep, "time", "repo", "stars", "downloads", "releases"));
                foreach (var s in d.Snapshots)
                {
                    string time = Iso(s.T);
                    long rel = s.Repos.Values.Sum(x => x.Length > 2 ? x[2] : 0);
                    lines.Add(Row(sep, time, "(all repositories)", s.Stars, s.Downloads, rel));
                    foreach (var (name, v) in s.Repos.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                        lines.Add(Row(sep, time, name, v.Length > 0 ? v[0] : 0, v.Length > 1 ? v[1] : 0, v.Length > 2 ? v[2] : (long?)null));
                }
                break;

            case "stars":
                lines.Add(Row(sep, "time", "repo", "user", "profile"));
                var all = new List<(long T, string Repo, string User)>();
                foreach (var (repo, list) in app.Stars)
                    if (d.Repos.ContainsKey(repo))
                        foreach (var s in list) all.Add((s.T, repo, s.U));
                foreach (var s in all.OrderByDescending(x => x.T))
                    lines.Add(Row(sep, s.T > 0 ? Iso(s.T) : "", s.Repo, s.User, "https://github.com/" + s.User));
                break;

            case "traffic":
                lines.Add(Row(sep, "date_utc", "repo", "views", "unique_visitors", "clones", "unique_cloners"));
                foreach (var (repo, rt) in app.Traffic.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                    if (d.Repos.ContainsKey(repo))
                        foreach (var (day, x) in rt.Days.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                            lines.Add(Row(sep, day, repo, x.V, x.Uv, x.C, x.Uc));
                break;

            default:
                return null;
        }
        return (string.Join("\r\n", lines) + "\r\n", "github-stats-" + what + ".csv");
    }

    // ------------------------------------------------------------------ README block

    public static string Markdown(App app)
    {
        var d = app.Data;
        var repos = d.Repos.Values.OrderByDescending(r => r.Downloads).ThenByDescending(r => r.Stars).ToList();
        var sb = new StringBuilder();
        sb.Append("<!-- github-stats:start -->\n");
        if (repos.Count == 0)
        {
            sb.Append("_No data yet._\n");
        }
        else
        {
            var tot = Calc.Sum(repos);
            sb.Append("| Repository | Stars | Forks | Releases | Downloads |\n|---|---:|---:|---:|---:|\n");
            foreach (var r in repos)
                sb.Append("| [").Append(Md(r.Name)).Append("](").Append(GhUrl(r.Url)).Append(") | ").Append(N(r.Stars)).Append(" | ").Append(N(r.Forks)).Append(" | ")
                  .Append(N(r.ReleaseCount)).Append(" | ").Append(N(r.Downloads)).Append(" |\n");
            sb.Append("| **Total** | **").Append(N(tot.Stars)).Append("** | **").Append(N(tot.Forks)).Append("** | **").Append(N(tot.Releases)).Append("** | **").Append(N(tot.Downloads)).Append("** |\n");
            sb.Append("\n_Updated ").Append(d.LastSuccess > 0 ? Day(d.LastSuccess) : Day(Clock.Now())).Append(" - downloads are the counts of the files attached to releases._\n");
        }
        sb.Append("<!-- github-stats:end -->\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ static HTML (no script, easy to embed or upload)

    public static string StaticHtml(App app)
    {
        var d = app.Data;
        string user = app.Cfg.User;
        var repos = d.Repos.Values.OrderByDescending(r => r.Downloads).ThenByDescending(r => r.Stars).ToList();
        var tot = Calc.Sum(repos);
        var sb = new StringBuilder(8192);
        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>GitHub stats &ndash; ").Append(H(user)).Append("</title>\n<style>\n");
        sb.Append(":root{color-scheme:light dark;--bg:#fff;--text:#1f2328;--muted:#656d76;--border:#d0d7de;--accent:#eaeef2;--link:#0969da}\n");
        sb.Append("@media(prefers-color-scheme:dark){:root{--bg:#0d1117;--text:#e6edf3;--muted:#8d96a0;--border:#30363d;--accent:#21262d;--link:#4493f8}}\n");
        sb.Append("body{margin:0;padding:16px;background:var(--bg);color:var(--text);font:15px/1.5 system-ui,-apple-system,\"Segoe UI\",Roboto,sans-serif}\n");
        sb.Append("a{color:var(--link);text-decoration:none}h1{font-size:1.3rem;margin:0 0 4px}.sub{color:var(--muted);font-size:.85rem;margin-bottom:12px}\n");
        sb.Append("table{border-collapse:collapse;width:100%}th,td{padding:6px 10px;text-align:left;border-bottom:1px solid var(--border)}\n");
        sb.Append("th{font-size:.75rem;text-transform:uppercase;letter-spacing:.04em;color:var(--muted);background:var(--accent)}td.n,th.n{text-align:right;font-variant-numeric:tabular-nums}\n");
        sb.Append("tfoot td{font-weight:600}\n</style>\n</head>\n<body>\n<h1>GitHub stats &middot; <a href=\"").Append(H(GhUrl("https://github.com/" + user))).Append("\">").Append(H(user)).Append("</a></h1>\n");
        sb.Append("<div class=\"sub\">Public repositories &middot; updated ").Append(H(d.LastSuccess > 0 ? Dt(d.LastSuccess) : "not yet")).Append("</div>\n");
        if (repos.Count > 0)
        {
            sb.Append("<table>\n<thead><tr><th>Repository</th><th class=\"n\">Stars</th><th class=\"n\">Forks</th><th class=\"n\">Releases</th><th class=\"n\">Downloads</th></tr></thead>\n<tbody>\n");
            foreach (var r in repos)
                sb.Append("<tr><td><a href=\"").Append(H(GhUrl(r.Url))).Append("\">").Append(H(r.Name)).Append("</a></td><td class=\"n\">").Append(N(r.Stars)).Append("</td><td class=\"n\">")
                  .Append(N(r.Forks)).Append("</td><td class=\"n\">").Append(N(r.ReleaseCount)).Append("</td><td class=\"n\">").Append(N(r.Downloads)).Append("</td></tr>\n");
            sb.Append("</tbody>\n<tfoot><tr><td>Total</td><td class=\"n\">").Append(N(tot.Stars)).Append("</td><td class=\"n\">").Append(N(tot.Forks)).Append("</td><td class=\"n\">")
              .Append(N(tot.Releases)).Append("</td><td class=\"n\">").Append(N(tot.Downloads)).Append("</td></tr></tfoot>\n</table>\n");
        }
        else sb.Append("<p>No data yet.</p>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ badges

    private static string BadgeValue(long v) => v >= 100_000 ? Compact(v) : N(v);

    public static string Badge(string label, string value, string color)
    {
        int lw = (int)Math.Round(label.Length * 6.3 + 12);
        int rw = (int)Math.Round(value.Length * 6.6 + 12);
        int w = lw + rw;
        string l = Xml(label), v = Xml(value);
        var sb = new StringBuilder(1024);
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(w).Append("\" height=\"20\" role=\"img\" aria-label=\"").Append(l).Append(": ").Append(v).Append("\">");
        sb.Append("<title>").Append(l).Append(": ").Append(v).Append("</title>");
        sb.Append("<linearGradient id=\"s\" x2=\"0\" y2=\"100%\"><stop offset=\"0\" stop-color=\"#bbb\" stop-opacity=\".1\"/><stop offset=\"1\" stop-opacity=\".1\"/></linearGradient>");
        sb.Append("<clipPath id=\"r\"><rect width=\"").Append(w).Append("\" height=\"20\" rx=\"3\" fill=\"#fff\"/></clipPath>");
        sb.Append("<g clip-path=\"url(#r)\"><rect width=\"").Append(lw).Append("\" height=\"20\" fill=\"#555\"/><rect x=\"").Append(lw).Append("\" width=\"").Append(rw)
          .Append("\" height=\"20\" fill=\"").Append(color).Append("\"/><rect width=\"").Append(w).Append("\" height=\"20\" fill=\"url(#s)\"/></g>");
        sb.Append("<g fill=\"#fff\" text-anchor=\"middle\" font-family=\"Verdana,Geneva,DejaVu Sans,sans-serif\" text-rendering=\"geometricPrecision\" font-size=\"11\">");
        sb.Append("<text x=\"").Append((lw / 2.0).ToString("0.#", C)).Append("\" y=\"15\" fill=\"#010101\" fill-opacity=\".3\">").Append(l).Append("</text><text x=\"").Append((lw / 2.0).ToString("0.#", C)).Append("\" y=\"14\">").Append(l).Append("</text>");
        sb.Append("<text x=\"").Append((lw + rw / 2.0).ToString("0.#", C)).Append("\" y=\"15\" fill=\"#010101\" fill-opacity=\".3\">").Append(v).Append("</text><text x=\"").Append((lw + rw / 2.0).ToString("0.#", C)).Append("\" y=\"14\">").Append(v).Append("</text>");
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static string Xml(string s) => System.Net.WebUtility.HtmlEncode(s);

    private const string ColDl = "#2a78d6", ColSt = "#c2501f", ColRel = "#1a7f4f";

    /// <summary>total-downloads | total-stars | total-releases | &lt;repo&gt;-downloads | &lt;repo&gt;-stars | &lt;repo&gt;-releases; null = unknown badge.</summary>
    public static string? BadgeFor(App app, string name)
    {
        var d = app.Data;
        var tot = Calc.Sum(d.Repos.Values);
        bool any = d.Repos.Count > 0;
        switch (name)
        {
            case "total-downloads": return Badge("downloads", any ? BadgeValue(tot.Downloads) : "n/a", ColDl);
            case "total-stars": return Badge("stars", any ? BadgeValue(tot.Stars) : "n/a", ColSt);
            case "total-releases": return Badge("releases", any ? BadgeValue(tot.Releases) : "n/a", ColRel);
        }
        foreach (var (suffix, label, color) in new[] { ("-downloads", "downloads", ColDl), ("-stars", "stars", ColSt), ("-releases", "releases", ColRel) })
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            string repoName = name[..^suffix.Length];
            var repo = d.Repos.Values.FirstOrDefault(r => string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
            if (repo == null) return null;
            long v = suffix == "-downloads" ? repo.Downloads : suffix == "-stars" ? repo.Stars : repo.ReleaseCount;
            return Badge(label, BadgeValue(v), color);
        }
        return null;
    }

    // ------------------------------------------------------------------ publish folder

    private static void WriteText(string path, string text, bool bom)
    {
        File.WriteAllText(path, text, new UTF8Encoding(bom));
    }

    /// <summary>Write all shareable files into the publish folder (stats.md, stats.html, stats.json, CSV files, badges).</summary>
    public static void Publish(App app)
    {
        string? dir = app.PublishPath;
        if (dir == null) return;
        string badges = Path.Combine(dir, "badges");
        Directory.CreateDirectory(badges);

        WriteText(Path.Combine(dir, "stats.md"), Markdown(app), false);
        WriteText(Path.Combine(dir, "stats.html"), StaticHtml(app), false);
        WriteText(Path.Combine(dir, "stats.json"), Page.Export(app, false), false);
        foreach (var kind in CsvKinds)
        {
            var c = Csv(app, kind);
            if (c != null) WriteText(Path.Combine(dir, "github-stats-" + kind + ".csv"), c.Value.Content, true);
        }
        var names = new List<string> { "total-downloads", "total-stars", "total-releases" };
        foreach (var r in app.Data.Repos.Values)
            foreach (var s in new[] { "-downloads", "-stars", "-releases" })
                names.Add(r.Name + s);
        foreach (var n in names)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(n, "^[A-Za-z0-9._-]{1,120}$")) continue;
            string? svg = BadgeFor(app, n);
            if (svg != null) WriteText(Path.Combine(badges, n + ".svg"), svg, false);
        }
    }
}
