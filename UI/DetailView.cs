using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>One row of a detail table (public properties for the DataGrid binding).</summary>
public sealed class DetailRow
{
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public string C { get; set; } = "";
    public string D { get; set; } = "";
    public string E { get; set; } = "";
    public string F { get; set; } = "";
    public string G { get; set; } = "";
    public double SA { get; set; }
    public double SB { get; set; }
    public double SC { get; set; }
    public double SD { get; set; }
    public double SE { get; set; }
    public double SF { get; set; }
    public string Url { get; set; } = "";
}

/// <summary>
/// Detail statistics for one block of the Overview (downloads, stars, views, clones, milestones, releases, files,
/// forks, issues). Opened by clicking the block; "Back to overview" returns.
/// </summary>
internal sealed class DetailView : View
{
    public static readonly Dictionary<string, string> Titles = new()
    {
        ["downloads"] = "Downloads",
        ["stars"] = "Stars",
        ["views"] = "Views",
        ["clones"] = "Clones",
        ["milestone"] = "Milestones",
        ["releases"] = "Releases",
        ["files"] = "Downloaded files",
        ["forks"] = "Forks",
        ["issues"] = "Open issues",
    };

    private readonly string _kind;
    private readonly StackPanel _root;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public DetailView(string kind)
    {
        _kind = Titles.ContainsKey(kind) ? kind : "downloads";
        Children.Add(Scroll(out _root));
    }

    public override void Update(Dashboard db)
    {
        _root.Children.Clear();
        var back = S.Link("‹  Back to overview", () => Host.Window?.Select("overview"));
        back.FontSize = 12.5;
        back.Margin = new Thickness(0, 0, 0, 6);
        _root.Children.Add(back);
        _root.Children.Add(Title(Titles[_kind]));
        if (!db.HasData) { _root.Children.Add(Ui.Text("No data yet.", 13, "Muted")); return; }
        try
        {
            switch (_kind)
            {
                case "stars": Stars(db); break;
                case "views": Traffic(db, clones: false); break;
                case "clones": Traffic(db, clones: true); break;
                case "milestone": Milestones(db); break;
                case "releases": Releases(); break;
                case "files": Files(); break;
                case "forks": PerRepo(db, "Forks", r => r.Forks, "forks"); break;
                case "issues": PerRepo(db, "Open issues", r => r.OpenIssues, "issues"); break;
                default: Downloads(db); break;
            }
        }
        catch (Exception ex)
        {
            Logger.Write("Detail " + _kind + " failed: " + ex);
            _root.Children.Add(Ui.Text("This page could not be shown: " + ex.Message, 12, "Down", false, true));
        }
    }

    // ================================================================== downloads

    private void Downloads(Dashboard db)
    {
        var days = WidgetStyles.DailyDownloads(HistoryDays(365));
        var known = days.Where(d => d.Value.HasValue).ToList();
        var best = known.OrderByDescending(d => d.Value).FirstOrDefault();
        Facts(("Total", Fmt.N(db.TotalDownloads), "S0"),
              ("Last 24 hours", D(db.DownloadsWin[0].Value), Sign(db.DownloadsWin[0].Value)),
              ("Last 7 days", D(db.DownloadsWin[1].Value), Sign(db.DownloadsWin[1].Value)),
              ("Last 30 days", D(db.DownloadsWin[2].Value), Sign(db.DownloadsWin[2].Value)),
              ("Average per day", db.DownloadsPerDay is double pd ? pd.ToString("0.#", CultureInfo.CurrentCulture) : "-", "Text"),
              ("Best day", best.Value > 0 ? Fmt.N(best.Value!.Value) + " on " + best.Day.ToString("d MMM", Inv) : "-", "Text"),
              ("Days measured", Fmt.N(known.Count), "Text"));

        var bars = new DailyBarChart { Height = 240 };
        bars.SetDays(days, "downloads");
        Section("Downloads per day", bars, "Every day since the first refresh (up to a year). Dashed line: 7-day average. Hover a bar for the exact number.");

        var total = new ChartControl { Height = 240 };
        total.SetModel(db.All);
        Section("Totals over time", total, db.All?.Caption ?? "");

        Heading("Per repository");
        _root.Children.Add(Table(new[] { ("Repository", "A", "", 0.0, false), ("Downloads", "B", "SB", 90, true), ("Last refresh", "C", "SC", 90, true), ("7 days", "D", "SD", 80, true), ("Per day", "E", "SE", 80, true), ("Releases", "F", "SF", 80, true) },
            db.Repos.OrderByDescending(r => r.Downloads).Select(r => new DetailRow
            {
                A = r.Name, B = Fmt.N(r.Downloads), SB = r.Downloads, C = r.DeltaLast, SC = r.DeltaLastSort, D = r.Delta7, SD = r.Delta7Sort,
                E = r.PerDay, SE = r.PerDaySort, F = r.Releases.ToString(), SF = r.Releases, Url = r.Url,
            })));

        var two = new MasonryPanel { MinColumnWidth = 420, Gap = 14, Margin = new Thickness(0, 14, 0, 0) };
        two.Children.Add(Box("Downloads by platform (guessed from file names)", db.Platforms.Count == 0 ? Ui.Text("Nothing yet.", 12, "Muted") : Ui.BarList(db.Platforms, x => Fmt.N(x.Value) + "  " + x.Detail, 2)));
        two.Children.Add(Box("Most downloaded files", db.TopFiles.Count == 0 ? Ui.Text("Nothing yet.", 12, "Muted") : Ui.BarList(db.TopFiles, x => Fmt.N(x.Value), 0), "All files", "files"));
        _root.Children.Add(two);
    }

    // ================================================================== stars

    private void Stars(Dashboard db)
    {
        var shown = Host.App.Data.Repos;
        var recs = Host.App.Stars.Where(kv => shown.ContainsKey(kv.Key))
            .SelectMany(kv => kv.Value.Where(s => s.T > 0).Select(s => (Repo: kv.Key, s.U, s.T))).OrderByDescending(x => x.T).ToList();
        string firstStar = recs.Count > 0 ? Local(recs[^1].T).ToString("d MMM yyyy", Inv) : "-";
        Facts(("Total", Fmt.N(db.TotalStars), "S1"),
              ("Last 7 days", D(db.StarsWin[1].Value), Sign(db.StarsWin[1].Value)),
              ("Last 30 days", D(db.StarsWin[2].Value), Sign(db.StarsWin[2].Value)),
              ("Stargazers known", Fmt.N(recs.Count), "Text"),
              ("First star", firstStar, "Text"),
              ("Latest star", recs.Count > 0 ? Ago(recs[0].T) : "-", "Text"));

        if (recs.Count == 0)
        {
            _root.Children.Add(Ui.Banner(db.HasToken
                ? "No star dates yet. Since July 2026 GitHub only shows them with repository access: see Settings > What your token can read."
                : "Star dates need a GitHub token with repository access (Settings)."));
        }
        else
        {
            // stars per day from the first star until today
            var today = Local(Clock.Now()).Date;
            var start = Local(recs[^1].T).Date;
            if ((today - start).TotalDays > 365) start = today.AddDays(-365);
            var counts = recs.GroupBy(r => Local(r.T).Date).ToDictionary(g => g.Key, g => (long)g.Count());
            var days = new List<(DateTime, long?)>();
            var cumulative = new List<double?>();
            long run = recs.Count(r => Local(r.T).Date < start);
            for (var d = start; d <= today; d = d.AddDays(1))
            {
                long c = counts.TryGetValue(d, out var v) ? v : 0;
                run += c;
                days.Add((d, c));
                cumulative.Add(null);
            }
            var bars = new DailyBarChart { Height = 220 };
            bars.SetDays(days, "stars");
            Section("Stars per day", bars, "Exact dates as reported by GitHub. Dashed line: 7-day average.");
        }

        Heading("Per repository");
        _root.Children.Add(Table(new[] { ("Repository", "A", "", 0.0, false), ("Stars", "B", "SB", 80, true), ("Stargazers known", "C", "SC", 130, true), ("Latest star", "D", "SD", 130, true) },
            db.Repos.OrderByDescending(r => r.Stars).Select(r =>
            {
                var mine = recs.Where(x => x.Repo == r.Name).ToList();
                return new DetailRow { A = r.Name, B = Fmt.N(r.Stars), SB = r.Stars, C = Fmt.N(mine.Count), SC = mine.Count, D = mine.Count > 0 ? Ago(mine[0].T) : "-", SD = mine.Count > 0 ? mine[0].T : 0, Url = r.Url + "/stargazers" };
            })));

        if (recs.Count > 0)
        {
            Heading("All stargazers");
            _root.Children.Add(Table(new[] { ("When", "A", "SA", 150, false), ("", "B", "SA", 90, false), ("Repository", "C", "", 200, false), ("Who", "D", "", 0.0, false) },
                recs.Select(x => new DetailRow { A = Local(x.T).ToString("yyyy-MM-dd HH:mm", Inv), SA = x.T, B = Ago(x.T), C = x.Repo, D = x.U, Url = "https://github.com/" + x.U })));
        }
    }

    // ================================================================== views and clones

    private void Traffic(Dashboard db, bool clones)
    {
        var shown = Host.App.Data.Repos;
        var tr = Host.App.Traffic.Where(kv => shown.ContainsKey(kv.Key)).ToList();
        string what = clones ? "clones" : "views";
        if (tr.Count == 0)
        {
            _root.Children.Add(Ui.Banner("No traffic data. It needs a token with repository access (Settings > What your token can read)."));
            return;
        }
        // per day over all repositories
        var perDay = new SortedDictionary<DateTime, (long N, long U)>();
        foreach (var (_, t) in tr)
            foreach (var (key, d) in t.Days)
            {
                if (!DateTime.TryParseExact(key, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var day)) continue;
                perDay.TryGetValue(day, out var cur);
                perDay[day] = clones ? (cur.N + d.C, cur.U + d.Uc) : (cur.N + d.V, cur.U + d.Uv);
            }
        long last14 = tr.Sum(x => clones ? x.Value.Clones14 : x.Value.Views14);
        long uniq14 = tr.Sum(x => clones ? x.Value.CloneUniques14 : x.Value.Uniques14);
        long stored = perDay.Values.Sum(v => v.N);
        var bestDay = perDay.OrderByDescending(kv => kv.Value.N).FirstOrDefault();
        Facts(("Last 14 days", Fmt.N(last14), clones ? "S2" : "S0"),
              (clones ? "Unique cloners, 14 days" : "Unique visitors, 14 days", Fmt.N(uniq14), "Text"),
              ("All stored days", Fmt.N(stored), "Text"),
              ("Days stored", Fmt.N(perDay.Count), "Text"),
              ("Busiest day", bestDay.Value.N > 0 ? Fmt.N(bestDay.Value.N) + " on " + bestDay.Key.ToString("d MMM", Inv) : "-", "Text"));

        if (perDay.Count > 0)
        {
            var today = DateTime.UtcNow.Date;
            var start = perDay.Keys.First();
            if ((today - start).TotalDays > 365) start = today.AddDays(-365);
            var days = new List<(DateTime, long?)>();
            var uniques = new List<double?>();
            for (var d = start; d <= today; d = d.AddDays(1))
            {
                bool has = perDay.TryGetValue(d, out var v);
                days.Add((d, has ? v.N : (d >= today.AddDays(-1) ? null : 0)));
                uniques.Add(has ? v.U : null);
            }
            var bars = new DailyBarChart { Height = 240 };
            bars.SetDays(days, what, uniques.ToArray(), clones ? "unique cloners" : "unique visitors");
            Section((clones ? "Clones" : "Views") + " per day", bars,
                "All repositories together, per day (UTC). Dashed line: " + (clones ? "unique cloners" : "unique visitors") + ". GitHub only keeps 14 days; this program keeps every day it has seen.");
        }

        Heading("Per repository");
        _root.Children.Add(Table(new[] { ("Repository", "A", "", 0.0, false), ("Last 14 days", "B", "SB", 100, true), (clones ? "Unique cloners" : "Unique visitors", "C", "SC", 120, true), ("All stored days", "D", "SD", 120, true), (clones ? "Views, 14 days" : "Clones, 14 days", "E", "SE", 120, true) },
            tr.OrderByDescending(x => clones ? x.Value.Clones14 : x.Value.Views14).Select(x =>
            {
                long n = clones ? x.Value.Clones14 : x.Value.Views14, u = clones ? x.Value.CloneUniques14 : x.Value.Uniques14, other = clones ? x.Value.Views14 : x.Value.Clones14;
                long all = x.Value.Days.Values.Sum(d => clones ? d.C : d.V);
                return new DetailRow { A = x.Key, B = Fmt.N(n), SB = n, C = Fmt.N(u), SC = u, D = Fmt.N(all), SD = all, E = Fmt.N(other), SE = other, Url = shown.TryGetValue(x.Key, out var r) ? r.Url + "/graphs/traffic" : "" };
            })));

        if (!clones)
        {
            var two = new MasonryPanel { MinColumnWidth = 420, Gap = 14, Margin = new Thickness(0, 14, 0, 0) };
            var refs = tr.SelectMany(x => x.Value.Referrers).GroupBy(r => r.Referrer).Select(g => new NameValue { Name = g.Key, Value = g.Sum(r => r.Count), Detail = g.Sum(r => r.Uniques) + " unique" }).OrderByDescending(x => x.Value).Take(12).ToList();
            Scale(refs);
            two.Children.Add(Box("Where visitors come from (14 days)", refs.Count == 0 ? Ui.Text("No referrers reported.", 12, "Muted") : Ui.BarList(refs, x => Fmt.N(x.Value) + "  (" + x.Detail + ")", 1)));
            var paths = tr.SelectMany(x => x.Value.Paths.Select(p => (Repo: x.Key, p))).OrderByDescending(x => x.p.Count).Take(12)
                .Select(x => new NameValue { Name = x.p.Path, Value = x.p.Count, Detail = x.p.Uniques + " unique" }).ToList();
            Scale(paths);
            two.Children.Add(Box("Popular pages (14 days)", paths.Count == 0 ? Ui.Text("No pages reported.", 12, "Muted") : Ui.BarList(paths, x => Fmt.N(x.Value), 0)));
            _root.Children.Add(two);
        }
    }

    // ================================================================== milestones

    private void Milestones(Dashboard db)
    {
        long total = db.TotalDownloads;
        var snaps = Host.App.Data.Snapshots;
        double? pd = db.DownloadsPerDay;
        var ms = Host.App.Cfg.Milestones;
        long next = ms.FirstOrDefault(m => m > total);
        Facts(("Downloads now", Fmt.N(total), "S0"),
              ("Next milestone", next == 0 ? "all reached" : Fmt.N(next), "S2"),
              ("Still to go", next == 0 ? "-" : Fmt.N(next - total), "Text"),
              ("Average per day", pd is double v ? v.ToString("0.#", CultureInfo.CurrentCulture) : "-", "Text"));
        Heading("All milestones");
        _root.Children.Add(Table(new[] { ("Milestone", "A", "SA", 120, true), ("Status", "B", "", 160, false), ("Reached on", "C", "SC", 140, false), ("Progress", "D", "SD", 100, true), ("Expected", "E", "SE", 0.0, false) },
            ms.Select(m =>
            {
                var hit = snaps.FirstOrDefault(s => s.Downloads >= m);
                bool reached = total >= m;
                double p = Math.Min(1, (double)total / m);
                string eta = "";
                double days = 0;
                if (!reached && pd is double rate && rate > 0.05)
                {
                    days = (m - total) / rate;
                    eta = days < 1.5 ? "about a day" : days < 60 ? "about " + Math.Round(days) + " days (" + DateTime.Now.AddDays(days).ToString("d MMM yyyy", Inv) + ")" : days < 730 ? "about " + Math.Round(days / 30) + " months" : "years away";
                }
                return new DetailRow
                {
                    A = Fmt.N(m), SA = m,
                    B = reached ? "✓ reached" : m == next ? "next" : "",
                    C = reached ? (hit != null ? Local(hit.T).ToString("d MMM yyyy", Inv) : "before the first refresh") : "", SC = hit?.T ?? 0,
                    D = Math.Round(p * 100) + "%", SD = p,
                    E = eta, SE = days,
                };
            })));
        var hint = Ui.Text("Change the milestones in Settings > Repositories > Download milestones.", 11.5, "Muted");
        hint.Margin = new Thickness(0, 6, 0, 0);
        _root.Children.Add(hint);
    }

    // ================================================================== releases and files

    private void Releases()
    {
        var all = Host.App.Data.Repos.Values.SelectMany(r => r.Releases.Select(rel => (Repo: r.Name, Rel: rel))).ToList();
        DateTime When(Release r) => DateTime.TryParse(r.PublishedAt, Inv, DateTimeStyles.AdjustToUniversal, out var d) ? d : DateTime.MinValue;
        var newest = all.OrderByDescending(x => When(x.Rel)).FirstOrDefault();
        var best = all.OrderByDescending(x => x.Rel.Downloads).FirstOrDefault();
        Facts(("Releases", Fmt.N(all.Count), "S0"),
              ("Files attached", Fmt.N(all.Sum(x => x.Rel.Assets.Count)), "Text"),
              ("Downloads", Fmt.N(all.Sum(x => x.Rel.Downloads)), "Text"),
              ("Newest", newest.Rel != null ? newest.Repo + " " + newest.Rel.Tag : "-", "Text"),
              ("Most downloaded", best.Rel != null ? best.Repo + " " + best.Rel.Tag + " (" + Fmt.N(best.Rel.Downloads) + ")" : "-", "Text"));
        Heading("All releases");
        _root.Children.Add(Table(new[] { ("Repository", "A", "", 180, false), ("Release", "B", "", 0.0, false), ("Published", "C", "SC", 110, false), ("Downloads", "D", "SD", 90, true), ("Per day", "E", "SE", 80, true), ("Files", "F", "SF", 60, true) },
            all.OrderByDescending(x => When(x.Rel)).Select(x =>
            {
                var w = When(x.Rel);
                double age = Math.Max(1, (DateTime.UtcNow - w).TotalDays);
                double perDay = w == DateTime.MinValue ? 0 : x.Rel.Downloads / age;
                return new DetailRow
                {
                    A = x.Repo, B = x.Rel.Tag + (x.Rel.Name.Length > 0 && x.Rel.Name != x.Rel.Tag ? "  " + x.Rel.Name : "") + (x.Rel.Prerelease ? "  (pre-release)" : ""),
                    C = w == DateTime.MinValue ? "" : w.ToString("yyyy-MM-dd", Inv), SC = w.Ticks,
                    D = Fmt.N(x.Rel.Downloads), SD = x.Rel.Downloads, E = perDay.ToString("0.0", CultureInfo.CurrentCulture), SE = perDay,
                    F = x.Rel.Assets.Count.ToString(), SF = x.Rel.Assets.Count, Url = x.Rel.Url,
                };
            })));
    }

    private void Files()
    {
        var all = Host.App.Data.Repos.Values.SelectMany(r => r.Releases.SelectMany(rel => rel.Assets.Select(a => (Repo: r.Name, Rel: rel, A: a)))).ToList();
        Facts(("Files", Fmt.N(all.Count), "S0"),
              ("Downloads", Fmt.N(all.Sum(x => x.A.Downloads)), "Text"),
              ("Total size", Mb(all.Sum(x => x.A.Size)), "Text"),
              ("Never downloaded", Fmt.N(all.Count(x => x.A.Downloads == 0)), "Text"));
        Heading("All files attached to releases");
        _root.Children.Add(Table(new[] { ("File", "A", "", 0.0, false), ("Repository", "B", "", 170, false), ("Release", "C", "", 110, false), ("Size", "D", "SD", 90, true), ("Downloads", "E", "SE", 90, true) },
            all.OrderByDescending(x => x.A.Downloads).Select(x => new DetailRow
            {
                A = x.A.Name, B = x.Repo, C = x.Rel.Tag, D = Mb(x.A.Size), SD = x.A.Size, E = Fmt.N(x.A.Downloads), SE = x.A.Downloads, Url = x.Rel.Url,
            })));
    }

    // ================================================================== forks and issues

    private void PerRepo(Dashboard db, string label, Func<Repo, long> value, string urlSuffix)
    {
        var repos = Host.App.Data.Repos.Values.OrderByDescending(value).ThenBy(r => r.Name).ToList();
        long total = repos.Sum(value);
        Facts((label, Fmt.N(total), "S0"), ("Repositories with any", Fmt.N(repos.Count(r => value(r) > 0)), "Text"));
        Heading("Per repository");
        _root.Children.Add(Table(new[] { ("Repository", "A", "", 0.0, false), (label, "B", "SB", 110, true), ("Stars", "C", "SC", 80, true), ("Language", "D", "", 110, false) },
            repos.Select(r => new DetailRow { A = r.Name, B = Fmt.N(value(r)), SB = value(r), C = Fmt.N(r.Stars), SC = r.Stars, D = r.Language, Url = r.Url + "/" + (urlSuffix == "forks" ? "forks" : "issues") })));
        var hint = Ui.Text("Double-click a row to open the " + (urlSuffix == "forks" ? "forks" : "issues") + " on GitHub.", 11.5, "Muted");
        hint.Margin = new Thickness(0, 6, 0, 0);
        _root.Children.Add(hint);
    }

    // ================================================================== building blocks

    private int HistoryDays(int max)
    {
        var snaps = Host.App.Data.Snapshots;
        double d = snaps.Count > 0 ? (Clock.Now() - snaps[0].T) / 86400.0 : 0;
        return Math.Max(14, Math.Min(max, (int)Math.Ceiling(d) + 1));
    }

    private void Facts(params (string Label, string Value, string Brush)[] facts)
    {
        var wrap = new MasonryPanel { MinColumnWidth = 128, Gap = 10, RowByRow = true, Margin = new Thickness(0, 8, 0, 4) };
        foreach (var (label, value, brush) in facts)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(label, 11, "Muted"));
            var v = Ui.Text(value, 18, brush, true, true);
            v.Margin = new Thickness(0, 2, 0, 0);
            sp.Children.Add(v);
            wrap.Children.Add(Ui.Box(sp, 10, new Thickness(13, 9, 13, 9)));
        }
        _root.Children.Add(wrap);
    }

    private void Section(string title, FrameworkElement chart, string caption)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(title, 14, "Text", true));
        chart.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(chart);
        if (caption.Length > 0)
        {
            var c = Ui.Text(caption, 10.5, "Muted", false, true);
            c.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(c);
        }
        var b = Ui.Box(sp, 10, new Thickness(16, 12, 16, 12));
        b.Margin = new Thickness(0, 12, 0, 0);
        _root.Children.Add(b);
    }

    private void Heading(string text) => _root.Children.Add(Ui.Heading(text));

    private static FrameworkElement Box(string title, FrameworkElement body, string? linkText = null, string? detail = null)
    {
        var sp = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        if (linkText != null && detail != null)
        {
            var l = S.Link(linkText + " ›", () => Host.Window?.ShowDetail(detail));
            l.Margin = new Thickness(0);
            DockPanel.SetDock(l, Dock.Right);
            head.Children.Add(l);
        }
        head.Children.Add(Ui.Text(title, 14, "Text", true));
        sp.Children.Add(head);
        sp.Children.Add(body);
        return Ui.Box(sp, 10, new Thickness(16, 12, 16, 14));
    }

    /// <summary>A sortable table that grows with its rows (the page scrolls, not the table). Double-click opens the row's link.</summary>
    private static FrameworkElement Table((string Header, string Path, string Sort, double Width, bool Right)[] cols, IEnumerable<DetailRow> rows)
    {
        var g = Ui.Grid2();
        g.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        foreach (var (header, path, sort, width, right) in cols)
            g.Columns.Add(Ui.Col(header, path, width, right, null, sort.Length > 0 ? sort : null));
        g.ItemsSource = rows.ToList();
        g.MouseDoubleClick += (s, e) => { if (g.SelectedItem is DetailRow r && r.Url.Length > 0) Ui.OpenUrl(r.Url); };
        // let the mouse wheel scroll the page instead of the table
        g.PreviewMouseWheel += (s, e) =>
        {
            e.Handled = true;
            var ev = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = s };
            (g.Parent as UIElement)?.RaiseEvent(ev);
        };
        return g;
    }

    private static void Scale(List<NameValue> list)
    {
        long max = Math.Max(1, list.Count > 0 ? list.Max(x => x.Value) : 1);
        foreach (var x in list) x.Percent = x.Value * 100.0 / max;
    }

    private static string D(long? v) => v == null ? "-" : Fmt.DeltaText(v.Value);
    private static string Sign(long? v) => v > 0 ? "Up" : v < 0 ? "Down" : "Text";
    private static DateTime Local(long t) => TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(t), Clock.Tz).DateTime;
    private static string Mb(long bytes) => bytes >= 1L << 30 ? (bytes / 1073741824.0).ToString("0.0", CultureInfo.CurrentCulture) + " GB"
        : bytes >= 1 << 20 ? (bytes / 1048576.0).ToString("0.0", CultureInfo.CurrentCulture) + " MB" : (bytes / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB";

    private static string Ago(long t)
    {
        double d = (Clock.Now() - t) / 86400.0;
        return d < 1 / 24.0 ? "just now" : d < 1 ? Math.Round(d * 24) + " h ago" : d < 60 ? Math.Round(d) + " d ago" : Math.Round(d / 30) + " months ago";
    }
}
