using System.Text;
using System.Text.Json;
using static GitHubStats.Fmt;

namespace GitHubStats;

internal static class Page
{
    // ------------------------------------------------------------------ JSON export

    public static string Export(App app, bool history)
    {
        var d = app.Data;
        var repos = d.Repos.Values.ToList();
        long now = Clock.Now();
        long nextAt = Calc.NextAt(d);
        var t14 = Calc.Traffic14(d, app.Traffic);
        var o = new Dictionary<string, object?>
        {
            ["user"] = app.Cfg.User,
            ["generated_at"] = Iso(now),
            ["last_refresh"] = d.LastSuccess > 0 ? Iso(d.LastSuccess) : null,
            ["complete"] = d.PendingAll == 0 && d.LastSuccess > 0,
            ["repos_pending"] = d.Pending.Count,
            ["stars_pending"] = d.PendingStars.Count,
            ["traffic_pending"] = d.PendingTraffic.Count,
            ["next_refresh_after"] = d.LastAttempt > 0 ? Iso(Math.Max(nextAt, now)) : null,
            ["last_error"] = d.LastError,
            ["stars_note"] = d.StarsNote,
            ["traffic_note"] = d.TrafficNote,
            ["totals"] = Calc.Sum(repos),
            ["traffic_14_days"] = Calc.HasTraffic(app.Traffic)
                ? new { views = t14.Views, unique_visitors = t14.Uniques, clones = t14.Clones, unique_cloners = t14.CloneUniques } : null,
            ["downloads_by_platform"] = Calc.Platforms(d).Select(p => new { platform = p.Platform, downloads = p.Downloads }).ToList(),
            ["repos"] = repos,
            ["star_events"] = d.StarEvents.Select(e => new Dictionary<string, object?>
            {
                ["t"] = e.T, ["repo"] = e.Repo, ["from"] = e.From, ["to"] = e.To, ["delta"] = e.Delta, ["time"] = Iso(e.T),
            }).ToList(),
        };
        if (history)
        {
            o["history"] = d.Snapshots;
            o["stars"] = app.Stars.Where(kv => d.Repos.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => new { user = s.U, time = s.T > 0 ? Iso(s.T) : null }).ToList());
            o["traffic"] = app.Traffic.Where(kv => d.Repos.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        return JsonSerializer.Serialize(o, Json.Pretty);
    }

    // ------------------------------------------------------------------ HTML helpers

    private static string Bar(double pct) =>
        "<span class=\"mbar\"><i style=\"width:" + Math.Clamp(pct, 0, 100).ToString("F1", C) + "%\"></i></span>";

    private static string ProfileLink(string login) =>
        login.Length == 0 ? "&ndash;" : "<a href=\"" + H(GhUrl("https://github.com/" + login)) + "\">" + H(login) + "</a>";

    private static string TrafficBlock(string name, RepoTraffic rt, Dictionary<string, object> chartData)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"tblock\"><div class=\"lgd\"><span class=\"lg\"><b>Traffic, last 14 days</b> &middot; views <b>").Append(N(rt.Views14))
          .Append("</b> (").Append(N(rt.Uniques14)).Append(" unique) &middot; clones <b>").Append(N(rt.Clones14)).Append("</b> (").Append(N(rt.CloneUniques14))
          .Append(" unique) &middot; fetched ").Append(H(Ago(rt.FetchedAt))).Append("</span></div>");

        var pts = Calc.TrafficPoints(rt);
        if (pts != null)
        {
            var co = Charts.Traffic("traffic:" + name, name, pts.Value.T, pts.Value.V);
            chartData["traffic:" + name] = co.Data;
            sb.Append(co.Html);
        }
        else sb.Append("<div class=\"desc\">The daily chart appears once a second day of traffic has been collected.</div>");

        if (rt.Referrers.Count > 0 || rt.Paths.Count > 0)
        {
            sb.Append("<div class=\"two small\">");
            sb.Append("<div><div class=\"desc\">Top referrers (14 days)</div><ul class=\"assets\">");
            foreach (var r in rt.Referrers.Take(6)) sb.Append("<li>").Append(H(r.Referrer)).Append(" &ndash; ").Append(N(r.Count)).Append(" views, ").Append(N(r.Uniques)).Append(" unique</li>");
            if (rt.Referrers.Count == 0) sb.Append("<li>none</li>");
            sb.Append("</ul></div><div><div class=\"desc\">Popular pages (14 days)</div><ul class=\"assets\">");
            foreach (var p in rt.Paths.Take(6)) sb.Append("<li>").Append(H(p.Path)).Append(" &ndash; ").Append(N(p.Count)).Append(" views, ").Append(N(p.Uniques)).Append(" unique</li>");
            if (rt.Paths.Count == 0) sb.Append("<li>none</li>");
            sb.Append("</ul></div></div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ the page

    public static string Render(App app, string sort)
    {
        var d = app.Data;
        bool running = app.Running;
        string user = app.Cfg.User;
        var repos = d.Repos.Values.ToList();
        var tot = Calc.Sum(repos);
        var snaps = d.Snapshots;
        long now = Clock.Now();
        long nextAt = Calc.NextAt(d);

        repos = sort switch
        {
            "stars" => repos.OrderByDescending(r => r.Stars).ThenByDescending(r => r.Downloads).ToList(),
            "forks" => repos.OrderByDescending(r => r.Forks).ThenByDescending(r => r.Downloads).ToList(),
            "name" => repos.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "pushed" => repos.OrderByDescending(r => r.PushedAt, StringComparer.Ordinal).ToList(),
            _ => repos.OrderByDescending(r => r.Downloads).ThenByDescending(r => r.Stars).ToList(),
        };

        // Growth is calculated between completed refresh cycles (snapshots).
        int nSn = snaps.Count;
        var lastSnap = nSn >= 1 ? snaps[nSn - 1] : null;
        var prevSnap = nSn >= 2 ? snaps[nSn - 2] : null;
        var base7 = Calc.Baseline(snaps, 7 * 86400);
        long? dlLast = prevSnap != null ? lastSnap!.Downloads - prevSnap.Downloads : null;
        long? dl7 = base7 != null ? lastSnap!.Downloads - base7.Downloads : null;
        long? starsLast = prevSnap != null ? lastSnap!.Stars - prevSnap.Stars : null;
        long? stars7 = base7 != null ? lastSnap!.Stars - base7.Stars : null;

        // ---- charts: one per repo (+ traffic), and one over all repositories
        var chartData = new Dictionary<string, object>();
        var repoCharts = new Dictionary<string, string>();
        foreach (var r in repos)
        {
            app.Stars.TryGetValue(r.Name, out var sl);
            var (t, v, real) = Charts.RepoPoints(snaps, r.Name, sl, r.Stars);
            var html = new StringBuilder();
            if (t.Length >= 2)
            {
                var co = Charts.Pct3("repo:" + r.Name, r.Name, t, v, real);
                chartData["repo:" + r.Name] = co.Data;
                html.Append(co.Html);
            }
            else html.Append("<div class=\"desc\">The chart appears once this repository is part of at least two completed refreshes.</div>");
            if (app.Traffic.TryGetValue(r.Name, out var rt) && rt.FetchedAt > 0) html.Append(TrafficBlock(r.Name, rt, chartData));
            repoCharts[r.Name] = html.ToString();
        }
        string allChart = "";
        if (nSn >= 2)
        {
            var at = snaps.Select(s => s.T).ToArray();
            var av = snaps.Select(s => new long?[] { s.Stars, s.Downloads, s.Repos.Values.Sum(x => x.Length > 2 ? x[2] : 0) }).ToArray();
            var co = Charts.Pct3("all", "all repositories", at, av, false);
            chartData["all"] = co.Data;
            allChart = co.Html;
        }

        var sb = new StringBuilder(128 * 1024);
        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<meta name=\"robots\" content=\"noindex\">\n");
        if (running) sb.Append("<meta http-equiv=\"refresh\" content=\"4\">\n");
        sb.Append("<title>GitHub stats &ndash; ").Append(H(user)).Append("</title>\n<style>\n").Append(Assets.Css).Append("\n</style>\n</head>\n<body>\n<main>\n");

        sb.Append("    <h1>GitHub stats &middot; <a href=\"").Append(H(GhUrl("https://github.com/" + user))).Append("\">").Append(H(user)).Append("</a></h1>\n");
        sb.Append("    <div class=\"sub\">\n        Public repositories only &middot; last complete refresh: ")
          .Append(d.LastSuccess > 0 ? H(Dt(d.LastSuccess)) + " (" + H(Ago(d.LastSuccess)) + ")" : "not yet")
          .Append("\n        &middot; next refresh possible after ")
          .Append(d.CycleStart > 0 || d.NextAttempt > 0 ? H(Hm(Math.Max(nextAt, now))) : "the next page load")
          .Append("\n        &middot; <a href=\"?export=json\">JSON</a>\n    </div>\n\n");

        // ---- export & share
        sb.Append("    <details class=\"share\"><summary>Export &amp; share</summary><div class=\"sharebody\">\n");
        sb.Append("        <p><b>CSV</b> (opens in Excel): ");
        sb.Append(string.Join(" &middot; ", Exports.CsvKinds.Select(k => "<a href=\"?export=csv&amp;what=" + k + "\">" + k + "</a>")));
        sb.Append("<br><b>Other:</b> <a href=\"?export=json\">JSON</a> &middot; <a href=\"?export=json&amp;history=1\">JSON with history, star log and traffic</a>")
          .Append(" &middot; <a href=\"?export=html\">stats.html</a> (a plain page you can upload or embed) &middot; <a href=\"?export=md\">README block as text</a></p>\n");
        sb.Append("        <p><b>README / website block</b> (Markdown table; paste it into your README):<br>")
          .Append("<textarea id=\"mdblock\" readonly rows=\"8\">").Append(H(Exports.Markdown(app))).Append("</textarea><br><button type=\"button\" id=\"copymd\">Copy</button></p>\n");
        sb.Append("        <p><b>Badges</b> (SVG; save them or use the ones from the publish folder): ");
        foreach (var b in new[] { "total-downloads", "total-stars", "total-releases" })
            sb.Append("<a href=\"/badge/").Append(b).Append(".svg\"><img class=\"badge-img\" alt=\"").Append(b).Append("\" src=\"/badge/").Append(b).Append(".svg\" loading=\"lazy\"></a> ");
        sb.Append("<br><span class=\"muted\">Per repository: <code>/badge/&lt;repo&gt;-downloads.svg</code>, <code>-stars.svg</code> or <code>-releases.svg</code>.</span></p>\n");
        string? pub = app.PublishPath;
        sb.Append("        <p class=\"muted\">")
          .Append(pub != null ? "After every complete refresh all of this is also written to <code>" + H(pub) + "</code> (stats.md, stats.html, stats.json, CSV files, badges/), ready to upload or commit." : "The publish folder is switched off (<code>publish_dir</code> in config.json).")
          .Append("</p>\n    </div></details>\n\n");

        // ---- banners
        if (app.Warning != null) sb.Append("    <div class=\"banner\">").Append(H(app.Warning)).Append("</div>\n");
        if (app.StoreProblem != null) sb.Append("    <div class=\"banner\">").Append(H(app.StoreProblem)).Append("</div>\n");
        if (app.ShareProblem != null) sb.Append("    <div class=\"banner\">").Append(H(app.ShareProblem)).Append("</div>\n");

        var prog = new List<string>();
        if (d.Pending.Count > 0) prog.Add("release data: " + N(Math.Max(0, repos.Count - d.Pending.Count)) + " of " + N(repos.Count) + " repositories");
        if (d.PendingStars.Count > 0) prog.Add("star history: " + N(d.PendingStars.Count) + " repositor" + (d.PendingStars.Count == 1 ? "y" : "ies") + " left");
        if (d.PendingTraffic.Count > 0) prog.Add("traffic: " + N(d.PendingTraffic.Count) + " repositor" + (d.PendingTraffic.Count == 1 ? "y" : "ies") + " left");
        string progress = string.Join(" &middot; ", prog);

        if (running)
        {
            sb.Append("    <div class=\"banner\"><strong>Refreshing from GitHub&hellip;</strong> ")
              .Append(progress.Length > 0 ? progress + ". Numbers below are filled in as they arrive." : "Contacting GitHub.")
              .Append(" This page reloads automatically.</div>\n");
        }
        else if (d.PendingAll > 0)
        {
            sb.Append("    <div class=\"banner\">\n        <strong>Still gathering data:</strong> ").Append(progress).Append(".\n        ")
              .Append(repos.Count > 0 ? "What is shown is current where it has been loaded; the rest is partial." : "");
            if (d.LastError != null) sb.Append("<br>").Append(H(d.LastError));
            sb.Append("\n        <br>It continues automatically after ").Append(H(Hm(Math.Max(nextAt, now)))).Append(app.Cfg.BackgroundRefresh ? "." : ", on a page load.").Append("\n    </div>\n");
        }
        else if (d.LastError != null)
        {
            sb.Append("    <div class=\"banner\">\n        <strong>Last refresh failed:</strong> ").Append(H(d.LastError)).Append(repos.Count > 0 ? " Showing cached data." : "")
              .Append("\n        <br>Next attempt after ").Append(H(Hm(Math.Max(nextAt, now)))).Append(app.Cfg.BackgroundRefresh ? "." : ", on a page load.").Append("\n    </div>\n");
        }
        if (d.TrafficNote != null && app.Cfg.TrackTraffic) sb.Append("    <div class=\"note\">").Append(H(d.TrafficNote)).Append("</div>\n");
        if (d.StarsNote != null) sb.Append("    <div class=\"note\">").Append(H(d.StarsNote)).Append("</div>\n");

        if (repos.Count == 0)
        {
            sb.Append("    <div class=\"card\">")
              .Append(running ? "Fetching the repository list from GitHub&hellip; this page reloads automatically."
                              : "No data yet." + (d.LastError != null ? "" : " Reload the page to fetch it."))
              .Append("</div>\n");
        }
        else
        {
            // ---- cards
            bool hasTraffic = Calc.HasTraffic(app.Traffic);
            var t14 = Calc.Traffic14(d, app.Traffic);
            sb.Append("    <div class=\"cards\">\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Downloads</div><div class=\"v\">").Append(N(tot.Downloads)).Append("</div>\n            <div class=\"d\">")
              .Append(tot.ReposLoading > 0
                  ? "partial &middot; " + N(tot.ReposLoading) + " repo" + (tot.ReposLoading == 1 ? "" : "s") + " still loading"
                  : Delta(dlLast) + " last refresh &middot; " + Delta(dl7) + " 7d")
              .Append("</div></div>\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Stars</div><div class=\"v\">").Append(N(tot.Stars)).Append("</div>\n            <div class=\"d\">")
              .Append(Delta(starsLast)).Append(" last refresh &middot; ").Append(Delta(stars7)).Append(" 7d</div></div>\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Forks</div><div class=\"v\">").Append(N(tot.Forks)).Append("</div></div>\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Repos</div><div class=\"v\">").Append(N(tot.Repos)).Append("</div></div>\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Releases</div><div class=\"v\">").Append(N(tot.Releases)).Append("</div></div>\n");
            sb.Append("        <div class=\"card\"><div class=\"l\">Open issues</div><div class=\"v\">").Append(N(tot.OpenIssues)).Append("</div></div>\n");
            if (hasTraffic)
            {
                sb.Append("        <div class=\"card\"><div class=\"l\">Views &middot; 14 days</div><div class=\"v\">").Append(N(t14.Views)).Append("</div><div class=\"d\">").Append(N(t14.Uniques)).Append(" unique visitors</div></div>\n");
                sb.Append("        <div class=\"card\"><div class=\"l\">Clones &middot; 14 days</div><div class=\"v\">").Append(N(t14.Clones)).Append("</div><div class=\"d\">").Append(N(t14.CloneUniques)).Append(" unique cloners</div></div>\n");
            }
            sb.Append("    </div>\n\n");

            // ---- all repositories over time
            if (allChart.Length > 0)
                sb.Append("    <div class=\"chart-only\"><h2>All repositories over time</h2><div class=\"wrap pad\">").Append(allChart).Append("</div></div>\n");

            // ---- star log
            var recent = Calc.RecentStars(d, app.Stars, 15);
            var rows = new List<(long T, string Repo, string Who, string Change)>();
            if (recent.Count > 0)
            {
                foreach (var s in recent) rows.Add((s.T, s.Repo, ProfileLink(s.User), Delta(1)));
                foreach (var e in d.StarEvents.Where(x => x.Delta < 0).TakeLast(15))
                    rows.Add((e.T, e.Repo, "<span class=\"muted\">un-starred (noticed at a refresh)</span>", Delta(e.Delta)));
            }
            else
            {
                foreach (var e in d.StarEvents.TakeLast(15))
                    rows.Add((e.T, e.Repo, "<span class=\"muted\">" + N(e.From) + " &rarr; " + N(e.To) + "</span>", Delta(e.Delta)));
            }
            if (rows.Count > 0)
            {
                sb.Append("    <h2>Recent star activity</h2>\n    <div class=\"wrap\"><table>\n        <tr><th>When</th><th>Repo</th><th>Who</th><th class=\"num\">Change</th></tr>\n");
                foreach (var r in rows.OrderByDescending(x => x.T).Take(15))
                    sb.Append("        <tr><td>").Append(H(Dt(r.T))).Append(" <span class=\"muted\">(").Append(H(Ago(r.T))).Append(")</span></td><td>")
                      .Append(H(r.Repo)).Append("</td><td>").Append(r.Who).Append("</td><td class=\"num\">").Append(r.Change).Append("</td></tr>\n");
                sb.Append("    </table></div>\n    <div class=\"muted\" style=\"font-size:.8rem;margin-top:6px\">")
                  .Append(recent.Count > 0 ? "Exact times and names as reported by GitHub."
                                           : "Stars are compared at each refresh, so the time shown is when the change was noticed. With a token (stars_detail in config.json) you get the exact dates and names.")
                  .Append("</div>\n");
            }

            // ---- where visitors come from
            var refs = hasTraffic ? Calc.Referrers(d, app.Traffic, 8) : new List<(string Referrer, long Count, long Uniques)>();
            if (refs.Count > 0)
            {
                sb.Append("    <h2>Where visitors come from <span class=\"muted\" style=\"font-weight:400\">(last 14 days, all repositories)</span></h2>\n    <div class=\"wrap\"><table>\n        <tr><th>Referrer</th><th class=\"num\">Views</th><th class=\"num\">Unique</th></tr>\n");
                foreach (var r in refs)
                    sb.Append("        <tr><td>").Append(H(r.Referrer)).Append("</td><td class=\"num\">").Append(N(r.Count)).Append("</td><td class=\"num\">").Append(N(r.Uniques)).Append("</td></tr>\n");
                sb.Append("    </table></div>\n");
            }

            // ---- download insight: top files and platforms
            var top = Calc.TopAssets(d, 10);
            var plat = Calc.Platforms(d);
            if (top.Count > 0)
            {
                long platTotal = plat.Sum(p => p.Downloads);
                sb.Append("    <h2>Download insight</h2>\n    <div class=\"two\">\n        <div class=\"wrap\"><table>\n            <tr><th>Most downloaded files</th><th>Release</th><th class=\"num\">Downloads</th></tr>\n");
                foreach (var (repo, rel, a) in top)
                    sb.Append("            <tr><td>").Append(H(a.Name)).Append("</td><td><span class=\"muted\">").Append(H(repo)).Append("</span> ").Append(H(rel.Tag)).Append("</td><td class=\"num\">").Append(N(a.Downloads)).Append("</td></tr>\n");
                sb.Append("        </table></div>\n        <div class=\"wrap\"><table>\n            <tr><th>Downloads by platform <span class=\"muted\" style=\"text-transform:none\">(guessed from file names)</span></th><th class=\"num\">Downloads</th></tr>\n");
                foreach (var (p, dl) in plat)
                    sb.Append("            <tr><td>").Append(Bar(platTotal > 0 ? 100.0 * dl / platTotal : 0)).Append(H(p)).Append("</td><td class=\"num\">").Append(N(dl)).Append(" <span class=\"muted\">")
                      .Append(platTotal > 0 ? Math.Round(100.0 * dl / platTotal).ToString("0", C) : "0").Append("%</span></td></tr>\n");
                sb.Append("        </table></div>\n    </div>\n");
            }

            // ---- repositories
            sb.Append("    <h2>Repositories</h2>\n    <div class=\"bar\">\n        <p class=\"sort\">Sort by:\n");
            foreach (var (key, label) in new[] { ("downloads", "Downloads"), ("stars", "Stars"), ("forks", "Forks"), ("pushed", "Last push"), ("name", "Name") })
                sb.Append("            <a").Append(key == sort ? " class=\"active\"" : "").Append(" href=\"?sort=").Append(key).Append("\">").Append(label).Append("</a>\n");
            sb.Append("        </p>\n        <label class=\"toggle\"><input type=\"checkbox\" id=\"showcharts\"> Show charts</label>\n    </div>\n");
            sb.Append("    <script>try { if (localStorage.getItem('ghstats.charts') === '1') document.getElementById('showcharts').checked = true; } catch (e) {}</script>\n");

            sb.Append("    <div class=\"wrap\"><table>\n        <tr>\n            <th>Repository</th><th>Language</th>\n")
              .Append("            <th class=\"num\">Stars</th><th class=\"num\">Forks</th><th class=\"num\">Issues</th>\n")
              .Append("            <th class=\"num\">Releases</th><th class=\"num\">Downloads</th>\n")
              .Append("            <th class=\"num\" title=\"Between the last two completed refreshes\">&Delta; last</th>\n")
              .Append("            <th class=\"num\" title=\"Since about 7 days ago").Append(base7 != null ? " (" + H(Dt(base7.T)) + ")" : "").Append("\">&Delta; 7d</th>\n")
              .Append("            <th class=\"num\" title=\"Average downloads per day over the last ~7 days\">Per day</th>\n")
              .Append("            <th>Last push</th>\n        </tr>\n");

            foreach (var r in repos)
            {
                string name = r.Name;
                bool loading = r.ReleasesAt == 0;
                long? curDl = lastSnap != null && lastSnap.Repos.TryGetValue(name, out var cur) && cur.Length > 1 ? cur[1] : null;
                long? dLast = prevSnap != null && curDl != null ? curDl - Calc.SnapDl(prevSnap, name) : null;
                long? d7 = base7 != null && curDl != null ? curDl - Calc.SnapDl(base7, name) : null;
                double? perDay = Calc.RepoPerDay(snaps, name);

                sb.Append("        <tr>\n            <td>\n                <a href=\"").Append(H(GhUrl(r.Url))).Append("\">").Append(H(name)).Append("</a>");
                if (r.Archived) sb.Append(" <span class=\"badge\">archived</span>");
                if (r.Fork) sb.Append(" <span class=\"badge\">fork</span>");
                if (r.Description.Length > 0) sb.Append("<div class=\"desc\">").Append(H(r.Description)).Append("</div>");
                sb.Append("\n            </td>\n            <td>").Append(H(r.Language)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(N(r.Stars)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(N(r.Forks)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(N(r.OpenIssues)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(loading ? "<span class=\"zero\" title=\"Release data not loaded yet\">&hellip;</span>" : N(r.ReleaseCount)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(loading ? "<span class=\"zero\" title=\"Release data not loaded yet\">&hellip;</span>" : "<strong>" + N(r.Downloads) + "</strong>").Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(Delta(dLast)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(Delta(d7)).Append("</td>\n")
                  .Append("            <td class=\"num\">").Append(perDay == null ? "<span class=\"zero\">&ndash;</span>" : PerDay(Math.Max(0, perDay.Value))).Append("</td>\n")
                  .Append("            <td>").Append(H(FDate(r.PushedAt))).Append("</td>\n        </tr>\n");

                sb.Append("        <tr class=\"chart\"><td colspan=\"11\">\n            ").Append(repoCharts[name]).Append("\n        </td></tr>\n");

                if (r.Releases.Count > 0)
                {
                    var latest = r.Releases.FirstOrDefault(x => !x.Prerelease);
                    sb.Append("        <tr class=\"rel\"><td colspan=\"11\">\n            <details>\n                <summary>").Append(N(r.ReleaseCount))
                      .Append(" release").Append(r.ReleaseCount == 1 ? "" : "s").Append(" &middot; downloads per release</summary>\n")
                      .Append("                <table class=\"rtable\">\n                    <tr><th>Release</th><th>Published</th><th class=\"num\">Downloads</th><th>Share</th><th class=\"num\" title=\"Downloads gained in about the last 7 days\">7 days</th><th class=\"num\" title=\"Average per day since it was published\">Per day</th></tr>\n");
                    foreach (var rel in r.Releases)
                    {
                        double share = r.Downloads > 0 ? 100.0 * rel.Downloads / r.Downloads : 0;
                        sb.Append("                    <tr>\n                        <td>\n                            <a href=\"").Append(H(GhUrl(rel.Url))).Append("\">")
                          .Append(H(rel.Name.Length > 0 ? rel.Name : rel.Tag)).Append("</a>");
                        if (rel.Name.Length > 0 && rel.Name != rel.Tag) sb.Append(" <span class=\"muted\">").Append(H(rel.Tag)).Append("</span>");
                        if (ReferenceEquals(rel, latest)) sb.Append(" <span class=\"badge\">latest</span>");
                        if (rel.Prerelease) sb.Append(" <span class=\"badge\">pre-release</span>");
                        if (rel.Assets.Count > 0)
                        {
                            sb.Append("\n                            <ul class=\"assets\">");
                            foreach (var a in rel.Assets)
                                sb.Append("<li>").Append(H(a.Name)).Append(" <span>(").Append(H(Bytes(a.Size))).Append(")</span> &ndash; ").Append(N(a.Downloads)).Append("</li>");
                            sb.Append("</ul>");
                        }
                        else sb.Append("\n                            <div class=\"desc\">No downloadable assets</div>");
                        sb.Append("\n                        </td>\n                        <td>").Append(H(FDate(rel.PublishedAt)))
                          .Append("</td>\n                        <td class=\"num\">").Append(N(rel.Downloads)).Append("</td>\n                        <td class=\"num\">")
                          .Append(Bar(share)).Append(Math.Round(share).ToString("0", C)).Append("%</td>\n                        <td class=\"num\">")
                          .Append(Delta(Calc.ReleaseDelta(rel, 7 * 86400))).Append("</td>\n                        <td class=\"num\">").Append(PerDay(Calc.AvgPerDay(rel))).Append("</td>\n                    </tr>\n");
                    }
                    sb.Append("                </table>\n            </details>\n        </td></tr>\n");
                }
            }
            sb.Append("    </table></div>\n");
        }

        sb.Append("\n    <footer>\n        Download counts only include files attached to releases (not the automatic source code archives), and GitHub counts every download, including bots, CI runs and mirrors.\n")
          .Append(app.Cfg.BackgroundRefresh
              ? "        A full refresh starts every " + (app.Cfg.IntervalSeconds / 60) + " minutes, by itself while this program is running.\n        "
              : "        A full refresh starts at most every " + (app.Cfg.IntervalSeconds / 60) + " minutes, when the page is loaded.\n        ");
        sb.Append(app.Cfg.Token.Length > 0
            ? "Using an API token."
            : "No API token: GitHub allows 60 requests/hour per IP address. A token in config.json avoids this, and unlocks star dates and traffic.");
        if (d.Rate != null) sb.Append(" API requests left at last check: ").Append(N(d.Rate.Remaining)).Append('/').Append(N(d.Rate.Limit)).Append('.');
        sb.Append(" History: ").Append(N(snaps.Count)).Append(" snapshot").Append(snaps.Count == 1 ? "" : "s").Append(".\n    </footer>\n</main>\n");

        sb.Append("<div id=\"tip\" role=\"status\" aria-live=\"polite\"></div>\n");
        if (chartData.Count > 0)
            sb.Append("<script type=\"application/json\" id=\"hist-data\">").Append(JsonSerializer.Serialize(new { c = chartData })).Append("</script>\n");
        sb.Append("<script>\n").Append(Assets.Js).Append("\n</script>\n</body>\n</html>\n");
        return sb.ToString();
    }
}
