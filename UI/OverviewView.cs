using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubStats;

// ============================================================================ Overview: every number on one page

internal sealed class OverviewView : View
{
    private readonly StackPanel _root;
    private readonly ChartControl _chart = new() { Height = 200 };
    private readonly DailyBarChart _daily = new() { Height = 200 };
    private bool _dailyWired;

    public OverviewView()
    {
        Children.Add(Scroll(out _root));
    }

    public override void Update(Dashboard db)
    {
        _root.Children.Clear();
        var app = Host.App;
        _root.Children.Add(Title("GitHub stats - " + db.User));
        var sub = Ui.Text("Public repositories  |  last refresh " + db.LastRefresh + "  |  refreshes every " + Every(app.Cfg.IntervalSeconds) + "  |  " + db.RateText, 12, "Muted", false, true);
        sub.Margin = new Thickness(0, 0, 0, 14);
        _root.Children.Add(sub);
        foreach (var m in db.Messages) _root.Children.Add(Ui.Banner(m));

        if (!db.HasData)
        {
            _root.Children.Add(Ui.Box(Ui.Text(db.Running ? "Fetching the repository list from GitHub..." : "No data yet. The first refresh starts automatically.", 13, "Muted", false, true)));
            return;
        }

        // ---- key numbers
        var (views, clones, uViews, uClones) = Traffic();
        var kpis = new MasonryPanel { MinColumnWidth = 168, Gap = 10, RowByRow = true };
        kpis.Children.Add(Kpi("downloads", "", "S0", "Downloads", Fmt.N(db.TotalDownloads), Delta(db.DownloadsDelta, "last refresh"), Delta(db.DownloadsWin[1].Value, "7 days")));
        kpis.Children.Add(Kpi("stars", "", "S1", "Stars", Fmt.N(db.TotalStars), Delta(db.StarsDelta, "last refresh"), Delta(db.StarsWin[1].Value, "7 days")));
        kpis.Children.Add(Kpi("views", "", "S2", "Views, 14 days", views == null ? "-" : Fmt.N(views.Value), views == null ? ("Needs a token with repository access", 0) : (Fmt.N(uViews ?? 0) + " unique visitors", 0), null));
        kpis.Children.Add(Kpi("clones", "", "S2", "Clones, 14 days", clones == null ? "-" : Fmt.N(clones.Value), clones == null ? ("Needs a token with repository access", 0) : (Fmt.N(uClones ?? 0) + " unique cloners", 0), null));
        kpis.Children.Add(Kpi("downloads", "", "S0", "Downloads per day", db.DownloadsPerDay is double pd ? pd.ToString(pd < 10 ? "0.0" : "0", CultureInfo.CurrentCulture) : "-", ("average over 7 days", 0), null));
        kpis.Children.Add(Milestone(db));
        kpis.Children.Add(Kpi("repos", "", "Muted", "Repositories", Fmt.N(db.Totals.Repos), (app.Cfg.HiddenRepos.Count > 0 ? app.Cfg.HiddenRepos.Count + " hidden" : "public, owned by " + db.User, 0), null));
        kpis.Children.Add(Kpi("releases", "", "Muted", "Releases", Fmt.N(db.Totals.Releases), (Fmt.N(TotalAssets()) + " files attached", 0), null));
        kpis.Children.Add(Kpi("forks", "", "Muted", "Forks", Fmt.N(db.Totals.Forks), ("copies of your repositories", 0), null));
        kpis.Children.Add(Kpi("issues", "", "Muted", "Open issues", Fmt.N(db.Totals.OpenIssues), ("including pull requests", 0), null));
        _root.Children.Add(kpis);

        // ---- growth: one slim strip
        _root.Children.Add(GrowthStrip(db));

        // ---- charts side by side: downloads per day, and the totals over time
        var snaps = app.Data.Snapshots;
        double histDays = snaps.Count > 0 ? (Clock.Now() - snaps[0].T) / 86400.0 : 0;
        int nDays = Math.Max(14, Math.Min(60, (int)Math.Ceiling(histDays) + 1));
        _daily.SetDays(WidgetStyles.DailyDownloads(nDays), "downloads");
        if (!_dailyWired) { _daily.DayClicked += d => Host.Window?.ShowDetail("day", d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); _dailyWired = true; }
        if (_daily.Parent is Border od) od.Child = null;
        _chart.SetModel(db.All);
        if (_chart.Parent is Border oc) oc.Child = null;
        var charts = new MasonryPanel { MinColumnWidth = 430, Gap = 14, RowByRow = true, Margin = new Thickness(0, 12, 0, 0) };
        charts.Children.Add(Panel("Downloads per day", ChartBody(_daily, "Last " + nDays + " days. Dashed line: 7-day average. Click a bar for that day."), "downloads"));
        charts.Children.Add(Panel("All repositories over time", ChartBody(_chart, db.All?.Caption ?? ""), "downloads"));
        _root.Children.Add(charts);

        // ---- detail panels, as many columns as fit
        var panels = new MasonryPanel { MinColumnWidth = 380, Gap = 14, Margin = new Thickness(0, 14, 0, 0) };
        panels.Children.Add(Panel("Top repositories", TopRepos(db), "downloads"));
        panels.Children.Add(Panel("Download calendar", Heat(), "downloads"));
        panels.Children.Add(Panel("Latest stars", LatestStars(db), "stars"));
        panels.Children.Add(Panel("Traffic, last 14 days", TrafficPanel(db), "views"));
        panels.Children.Add(Panel("Latest releases", LatestReleases(), "releases"));
        panels.Children.Add(Panel("Most downloaded files", Bars(db.TopFiles.Take(6), x => Fmt.N(x.Value), 0, x => x.Detail), "files"));
        panels.Children.Add(Panel("Downloads by platform", Bars(db.Platforms, x => Fmt.N(x.Value) + "  " + x.Detail, 2, null), "downloads"));
        panels.Children.Add(Panel("Highlights", Highlights(db), null));
        _root.Children.Add(panels);

        var foot = Ui.Text("Download counts only include files attached to releases (not the automatic source code archives), and GitHub counts every download, including bots, CI runs and mirrors. "
            + "Views and clones come from GitHub's traffic data (14 days); this program keeps every day it has seen.", 11, "Muted", false, true);
        foot.Margin = new Thickness(0, 18, 0, 0);
        _root.Children.Add(foot);
    }

    // ------------------------------------------------------------------ cards

    private static (string Text, long Sign)? Delta(long? d, string what) =>
        d == null ? null : (Fmt.DeltaText(d.Value) + " " + what, d.Value);

    private static FrameworkElement Kpi(string detail, string glyph, string brush, string label, string value, (string Text, long Sign)? line1, (string Text, long Sign)? line2)
    {
        var sp = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Glyph(glyph, brush, 12));
        var l = Ui.Text("  " + label.ToUpperInvariant(), 10.5, "Muted");
        l.FontWeight = FontWeights.SemiBold;
        head.Children.Add(l);
        sp.Children.Add(head);
        var v = Ui.Text(value, 22, "Text", true);
        v.Margin = new Thickness(0, 1, 0, 0);
        sp.Children.Add(v);
        foreach (var line in new[] { line1, line2 })
        {
            if (line == null) continue;
            sp.Children.Add(Ui.Text(line.Value.Text, 11, line.Value.Sign > 0 ? "Up" : line.Value.Sign < 0 ? "Down" : "Muted", false, true));
        }
        var b = Ui.Box(sp, 10, new Thickness(13, 8, 13, 8));
        b.MinHeight = 84;
        return Clickable(b, detail);
    }

    /// <summary>Makes a block open its detail statistics on click, with a hover highlight.</summary>
    private static Border Clickable(Border b, string detail)
    {
        b.Cursor = Cursors.Hand;
        b.ToolTip = detail == "repos" ? "Open Repositories" : "Show detailed statistics";
        ToolTipService.SetInitialShowDelay(b, 600);
        b.MouseEnter += (s, e) => b.Res(Border.BorderBrushProperty, "Link");
        b.MouseLeave += (s, e) => b.Res(Border.BorderBrushProperty, "Border");
        b.MouseLeftButtonUp += (s, e) => { if (!e.Handled) Host.Window?.ShowDetail(detail); };
        return b;
    }

    private static FrameworkElement Milestone(Dashboard db)
    {
        long total = db.TotalDownloads;
        var ms = Host.App.Cfg.Milestones;
        long next = ms.FirstOrDefault(m => m > total), prev = ms.LastOrDefault(m => m <= total);
        double p = next == 0 ? 1 : (double)(total - prev) / Math.Max(1, next - prev);
        var sp = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Glyph("", "S2", 12));
        var l = Ui.Text("  NEXT MILESTONE", 10.5, "Muted");
        l.FontWeight = FontWeights.SemiBold;
        head.Children.Add(l);
        sp.Children.Add(head);
        var v = Ui.Text(next == 0 ? "All reached" : Fmt.N(next), 22, "Text", true);
        v.Margin = new Thickness(0, 1, 0, 0);
        sp.Children.Add(v);
        sp.Children.Add(Bar(p, "S2", 6, new Thickness(0, 4, 0, 4)));
        string eta = "";
        if (next > 0 && db.DownloadsPerDay is double pd && pd > 0.05)
        {
            double days = (next - total) / pd;
            eta = ", about " + (days < 1.5 ? "a day" : days < 60 ? Math.Round(days) + " days" : Math.Round(days / 30) + " months") + " to go";
        }
        sp.Children.Add(Ui.Text(next == 0 ? "add more in Settings" : Math.Round(p * 100) + "%" + eta, 11, "Muted", false, true));
        var b = Ui.Box(sp, 10, new Thickness(13, 8, 13, 8));
        b.MinHeight = 84;
        return Clickable(b, "milestone");
    }

    /// <summary>Growth in one slim line: downloads and stars over the last refresh, 24 hours, 7 and 30 days.</summary>
    private static FrameworkElement GrowthStrip(Dashboard db)
    {
        var wrap = new WrapPanel();
        var head = Ui.Text("GROWTH", 10.5, "Muted");
        head.FontWeight = FontWeights.SemiBold;
        head.Margin = new Thickness(0, 0, 18, 0);
        wrap.Children.Add(head);
        void Group(string glyph, string brush, string name, long? last, WinDelta[] win)
        {
            var g = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 28, 0) };
            g.Children.Add(Glyph(glyph, brush, 12));
            g.Children.Add(Ui.Text("  " + name + "  ", 12.5, "Text", true));
            void Item(string label, long? v, bool partial)
            {
                var t = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                var val = new System.Windows.Documents.Run(v == null ? "-" : Fmt.DeltaText(v.Value) + (partial ? "*" : "")) { FontWeight = FontWeights.SemiBold };
                val.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, v > 0 ? "Up" : v < 0 ? "Down" : "Text");
                var lab = new System.Windows.Documents.Run(" " + label);
                lab.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Muted");
                t.Inlines.Add(val);
                t.Inlines.Add(lab);
                if (partial) t.ToolTip = "* Not enough history yet for the whole period: since the first refresh.";
                g.Children.Add(t);
            }
            Item("last refresh", last, false);
            Item("24h", win[0].Value, win[0].Partial);
            Item("7d", win[1].Value, win[1].Partial);
            Item("30d", win[2].Value, win[2].Partial);
            wrap.Children.Add(g);
        }
        Group("", "S0", "Downloads", db.DownloadsDelta, db.DownloadsWin);
        Group("", "S1", "Stars", db.StarsDelta, db.StarsWin);
        var b = Ui.Box(wrap, 10, new Thickness(14, 8, 14, 8));
        b.Margin = new Thickness(0, 10, 0, 0);
        return Clickable(b, "downloads");
    }

    private static FrameworkElement ChartBody(FrameworkElement chart, string caption)
    {
        // the chart controls are reused on every update: detach them from the previous page first
        if (chart.Parent is Panel old) old.Children.Remove(chart);
        else if (chart.Parent is Border ob) ob.Child = null;
        var sp = new StackPanel();
        sp.Children.Add(chart);
        if (caption.Length > 0)
        {
            var c = Ui.Text(caption, 10.5, "Muted", false, true);
            c.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(c);
        }
        return sp;
    }

    private static void Cell(Grid g, FrameworkElement e, int row, int col, bool right)
    {
        while (g.RowDefinitions.Count <= row) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        e.Margin = new Thickness(0, 4, 0, 4);
        if (right) e.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(e, row);
        Grid.SetColumn(e, col);
        g.Children.Add(e);
    }

    // ------------------------------------------------------------------ panels

    private FrameworkElement Panel(string title, FrameworkElement body, string? detail)
    {
        var sp = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        if (detail != null)
        {
            var more = Ui.Text("Details \u203A", 12, "Link");
            DockPanel.SetDock(more, Dock.Right);
            head.Children.Add(more);
        }
        head.Children.Add(Ui.Text(title, 14, "Text", true));
        sp.Children.Add(head);
        sp.Children.Add(body);
        var b = Ui.Box(sp, 10, new Thickness(16, 12, 16, 14));
        return detail == null ? b : Clickable(b, detail);
    }

    private static FrameworkElement TopRepos(Dashboard db)
    {
        var rows = db.Repos.OrderByDescending(r => r.Downloads).Take(8).ToList();
        if (rows.Count == 0) return Ui.Text("No data yet.", 12, "Muted");
        long max = Math.Max(1, rows[0].Downloads);
        var sp = new StackPanel();
        foreach (var r in rows)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var n = Ui.Text(r.Name, 12.5);
            n.TextTrimming = TextTrimming.CharacterEllipsis;
            n.ToolTip = r.Name + (r.Description.Length > 0 ? " - " + r.Description : "");
            g.Children.Add(n);
            var bar = Bar((double)r.Downloads / max, "S0", 6, new Thickness(6, 0, 8, 0));
            Grid.SetColumn(bar, 1);
            g.Children.Add(bar);
            var v = Ui.Text(Fmt.Compact(r.Downloads), 12.5, "Text", true);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(v, 2);
            g.Children.Add(v);
            var d7 = Ui.Text(r.Delta7, 11.5, r.Delta7Sort > 0 ? "Up" : "Muted");
            d7.HorizontalAlignment = HorizontalAlignment.Right;
            d7.ToolTip = "Downloads in the last 7 days";
            Grid.SetColumn(d7, 3);
            g.Children.Add(d7);
            var st = Ui.Text("★ " + r.Stars, 11.5, "Muted");
            st.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(st, 4);
            g.Children.Add(st);
            g.Cursor = Cursors.Hand;
            g.Background = Brushes.Transparent;
            string repoName = r.Name;
            g.MouseLeftButtonUp += (s, e) => { e.Handled = true; Host.Window?.ShowDetail("repo", repoName); };
            sp.Children.Add(g);
        }
        var hint = Ui.Text("downloads  ·  last 7 days  ·  stars. Click a repository for its details.", 10.5, "Muted");
        hint.Margin = new Thickness(0, 6, 0, 0);
        sp.Children.Add(hint);
        return sp;
    }

    private static FrameworkElement Heat()
    {
        // as many weeks as there is history (at least 6, at most 20)
        var snaps = Host.App.Data.Snapshots;
        double histDays = snaps.Count > 0 ? (Clock.Now() - snaps[0].T) / 86400.0 : 0;
        int weeks = Math.Max(6, Math.Min(20, (int)Math.Ceiling(histDays / 7) + 1));
        var days = WidgetStyles.DailyDownloads(weeks * 7);
        var sp = new StackPanel();
        var grid = WidgetStyles.HeatGrid(days, weeks, weeks <= 8 ? 22 : weeks <= 12 ? 18 : 15);
        grid.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Margin = new Thickness(-1.5, 0, 0, 4);
        sp.Children.Add(grid);
        sp.Children.Add(WidgetStyles.HeatLegend());
        long thisWeek = days.Skip(days.Count - 7).Sum(d => d.Value ?? 0);
        long lastWeek = days.Skip(days.Count - 14).Take(7).Sum(d => d.Value ?? 0);
        var best = days.Where(d => d.Value.HasValue).OrderByDescending(d => d.Value).FirstOrDefault();
        var facts = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        facts.Children.Add(Fact("Last 7 days", Fmt.N(thisWeek), thisWeek >= lastWeek ? "Up" : "Down"));
        facts.Children.Add(Fact("The 7 days before", Fmt.N(lastWeek), "Text"));
        if (best.Value > 0) facts.Children.Add(Fact("Best day", Fmt.N(best.Value!.Value) + " on " + best.Day.ToString("d MMM", CultureInfo.InvariantCulture), "Text"));
        sp.Children.Add(facts);
        return sp;
    }

    private static FrameworkElement LatestStars(Dashboard db)
    {
        var sp = new StackPanel();
        if (db.Stars.Count == 0)
        {
            sp.Children.Add(Ui.Text(db.HasToken
                ? "No star dates yet. They need a token with repository access (Settings > What your token can read)."
                : "Star dates need a GitHub token (Settings).", 12, "Muted", false, true));
            return sp;
        }
        string[] keys = { "S0", "S1", "S2", "Link" };
        int i = 0;
        foreach (var s in db.Stars.Take(8))
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var av = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 10, 0) };
            av.Res(Border.BackgroundProperty, keys[i++ % keys.Length]);
            av.Child = new TextBlock { Text = Initials(s.User), Foreground = Brushes.White, FontSize = 10.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(av, Dock.Left);
            row.Children.Add(av);
            var ago = Ui.Text(s.Ago, 11.5, "Muted");
            DockPanel.SetDock(ago, Dock.Right);
            row.Children.Add(ago);
            var t = new TextBlock { FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            t.Res(TextBlock.ForegroundProperty, "Text");
            t.Inlines.Add(new System.Windows.Documents.Run(s.User) { FontWeight = FontWeights.SemiBold });
            t.Inlines.Add(new System.Windows.Documents.Run("  " + s.Repo) { Foreground = Theme.B("Muted") });
            row.Children.Add(t);
            row.ToolTip = s.When;
            string url = s.Url;
            row.MouseLeftButtonUp += (o, e) => { e.Handled = true; Ui.OpenUrl(url); };
            sp.Children.Add(row);
        }
        return sp;
    }

    private static FrameworkElement TrafficPanel(Dashboard db)
    {
        var sp = new StackPanel();
        var (views, clones, uViews, uClones) = Traffic();
        if (views == null)
        {
            sp.Children.Add(Ui.Text("No traffic data. It needs a token with repository access (Settings > What your token can read).", 12, "Muted", false, true));
            return sp;
        }
        var facts = new WrapPanel();
        facts.Children.Add(Fact("Views", Fmt.N(views.Value), "S0"));
        facts.Children.Add(Fact("Unique visitors", Fmt.N(uViews ?? 0), "Text"));
        facts.Children.Add(Fact("Clones", Fmt.N(clones ?? 0), "S2"));
        facts.Children.Add(Fact("Unique cloners", Fmt.N(uClones ?? 0), "Text"));
        sp.Children.Add(facts);

        var shown = Host.App.Data.Repos;
        var byViews = Host.App.Traffic.Where(kv => shown.ContainsKey(kv.Key) && kv.Value.Views14 > 0)
            .OrderByDescending(kv => kv.Value.Views14).Take(5)
            .Select(kv => new NameValue { Name = kv.Key, Value = kv.Value.Views14, Detail = kv.Value.Clones14 + " clones" }).ToList();
        if (byViews.Count > 0)
        {
            long max = byViews.Max(x => x.Value);
            foreach (var x in byViews) x.Percent = x.Value * 100.0 / max;
            sp.Children.Add(SubHead("Most viewed"));
            sp.Children.Add(Ui.BarList(byViews, x => Fmt.N(x.Value) + " views", 0));
        }
        if (db.Referrers.Count > 0)
        {
            long max = Math.Max(1, db.Referrers.Max(r => r.Count));
            sp.Children.Add(SubHead("Where visitors come from"));
            sp.Children.Add(Ui.BarList(db.Referrers.Take(5).Select(r => new NameValue { Name = r.Name, Value = r.Count, Percent = r.Count * 100.0 / max, Detail = r.Uniques + " unique" }), x => Fmt.N(x.Value), 1));
        }
        return sp;
    }

    private static FrameworkElement LatestReleases()
    {
        var list = Host.App.Data.Repos.Values
            .SelectMany(r => r.Releases.Select(rel => (Repo: r.Name, Rel: rel)))
            .Where(x => DateTime.TryParse(x.Rel.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _))
            .OrderByDescending(x => DateTime.Parse(x.Rel.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal))
            .Take(6).ToList();
        var sp = new StackPanel();
        if (list.Count == 0) { sp.Children.Add(Ui.Text("No releases yet.", 12, "Muted")); return sp; }
        foreach (var (repo, rel) in list)
        {
            var when = DateTime.Parse(rel.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            var t = new TextBlock { FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis };
            t.Res(TextBlock.ForegroundProperty, "Text");
            t.Inlines.Add(new System.Windows.Documents.Run(repo) { FontWeight = FontWeights.SemiBold });
            t.Inlines.Add(new System.Windows.Documents.Run("  " + rel.Tag) { Foreground = Theme.B("Muted") });
            g.Children.Add(t);
            var d = Ui.Text(when.ToString("d MMM", CultureInfo.InvariantCulture), 11.5, "Muted");
            d.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(d, 1);
            g.Children.Add(d);
            var v = Ui.Text(Fmt.N(rel.Downloads) + " ↓", 12, "Text", true);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(v, 2);
            g.Children.Add(v);
            g.ToolTip = (rel.Name.Length > 0 ? rel.Name + " - " : "") + Fmt.N(rel.Downloads) + " downloads";
            string key = repo + "|" + rel.Tag;
            g.MouseLeftButtonUp += (s, e) => { e.Handled = true; Host.Window?.ShowDetail("release", key); };
            sp.Children.Add(g);
        }
        return sp;
    }

    private static FrameworkElement Highlights(Dashboard db)
    {
        var sp = new StackPanel();
        void Add(string glyph, string brush, string label, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var i = Glyph(glyph, brush, 14);
            i.Width = 24;
            i.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(i, Dock.Left);
            row.Children.Add(i);
            var t = new StackPanel();
            t.Children.Add(Ui.Text(label, 11, "Muted"));
            t.Children.Add(Ui.Text(text, 12.5, "Text", false, true));
            row.Children.Add(t);
            sp.Children.Add(row);
        }
        Add("", "S0", "Biggest gainer", db.TopGainer);
        Add("", "S1", "Latest star", db.LatestStar);
        Add("", "S0", "Most downloaded file", db.TopFile);
        var pushed = Host.App.Data.Repos.Values.Where(r => r.PushedAt.Length > 0).OrderByDescending(r => r.PushedAt).FirstOrDefault();
        if (pushed != null && DateTime.TryParse(pushed.PushedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var pd))
            Add("", "Muted", "Most recently updated", pushed.Name + ", " + pd.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
        var lang = Host.App.Data.Repos.Values.Where(r => r.Language.Length > 0).GroupBy(r => r.Language).OrderByDescending(g => g.Count()).Select(g => g.Key + " (" + g.Count() + ")").Take(3).ToList();
        if (lang.Count > 0) Add("", "Muted", "Languages", string.Join(", ", lang));
        if (sp.Children.Count == 0) sp.Children.Add(Ui.Text("Highlights appear after a few refreshes.", 12, "Muted"));
        return sp;
    }

    // ------------------------------------------------------------------ helpers

    private static (long? Views, long? Clones, long? UViews, long? UClones) Traffic()
    {
        var shown = Host.App.Data.Repos;
        var tr = Host.App.Traffic.Where(kv => shown.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        if (tr.Count == 0) return (null, null, null, null);
        return (tr.Sum(t => t.Views14), tr.Sum(t => t.Clones14), tr.Sum(t => t.Uniques14), tr.Sum(t => t.CloneUniques14));
    }

    private static long TotalAssets() => Host.App.Data.Repos.Values.Sum(r => (long)r.Releases.Sum(x => x.Assets.Count));

    private static FrameworkElement Bars(IEnumerable<NameValue> items, Func<NameValue, string> value, int slot, Func<NameValue, string>? sub)
    {
        var list = items.ToList();
        if (list.Count == 0) return Ui.Text("Nothing to show yet.", 12, "Muted");
        return Ui.BarList(list, value, slot);
    }

    private static FrameworkElement Fact(string label, string value, string brush)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 22, 6) };
        sp.Children.Add(Ui.Text(label, 11, "Muted"));
        sp.Children.Add(Ui.Text(value, 17, brush, true));
        return sp;
    }

    private static TextBlock SubHead(string text)
    {
        var t = Ui.Text(text, 11.5, "Muted");
        t.FontWeight = FontWeights.SemiBold;
        t.Margin = new Thickness(0, 10, 0, 2);
        return t;
    }

    private static FrameworkElement Bar(double fraction, string brush, double height, Thickness margin)
    {
        double f = Math.Max(0.02, Math.Min(1, fraction));
        var g = new Grid { Height = height, Margin = margin, VerticalAlignment = VerticalAlignment.Center };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(f, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - f + 0.0001, GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(height / 2) };
        track.Res(Border.BackgroundProperty, "Accent");
        Grid.SetColumnSpan(track, 2);
        g.Children.Add(track);
        var fill = new Border { CornerRadius = new CornerRadius(height / 2) };
        fill.Res(Border.BackgroundProperty, brush);
        g.Children.Add(fill);
        return g;
    }

    private static TextBlock Glyph(string glyph, string brush, double size)
    {
        var t = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        t.Res(TextBlock.ForegroundProperty, brush);
        return t;
    }

    private static string Initials(string user)
    {
        var parts = user.Split('-', '_', '.', ' ').Where(p => p.Length > 0).ToArray();
        string s = parts.Length >= 2 ? parts[0][..1] + parts[1][..1] : user.Length >= 2 ? user[..2] : user;
        return s.ToUpperInvariant();
    }
}

/// <summary>
/// Lays its children out in as many equal columns as fit (each at least <see cref="MinColumnWidth"/> wide),
/// putting every child in the currently shortest column: a "masonry" layout that fills the window.
/// </summary>
internal sealed class MasonryPanel : Panel
{
    public double MinColumnWidth { get; set; } = 360;
    public double Gap { get; set; } = 14;
    /// <summary>true = fill row by row in order (equal cards); false = masonry (each child in the shortest column).</summary>
    public bool RowByRow { get; set; }
    private readonly List<Rect> _slots = new();

    private int Columns(double width) =>
        double.IsInfinity(width) ? 1 : Math.Max(1, (int)((width + Gap) / (MinColumnWidth + Gap)));

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? MinColumnWidth : available.Width;
        int cols = Math.Min(Columns(width), Math.Max(1, InternalChildren.Count));
        double colW = (width - Gap * (cols - 1)) / cols;
        var heights = new double[cols];
        _slots.Clear();
        if (RowByRow)
        {
            double y = 0;
            for (int i = 0; i < InternalChildren.Count; i += cols)
            {
                double rowH = 0;
                for (int k = 0; k < cols && i + k < InternalChildren.Count; k++)
                {
                    var ch = InternalChildren[i + k];
                    ch.Measure(new Size(colW, double.PositiveInfinity));
                    rowH = Math.Max(rowH, ch.DesiredSize.Height);
                }
                for (int k = 0; k < cols && i + k < InternalChildren.Count; k++)
                    _slots.Add(new Rect(k * (colW + Gap), y, colW, rowH));
                y += rowH + Gap;
            }
            return new Size(width, Math.Max(0, y - Gap));
        }
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(colW, double.PositiveInfinity));
            int c = Array.IndexOf(heights, heights.Min());
            double y = heights[c];
            _slots.Add(new Rect(c * (colW + Gap), y, colW, child.DesiredSize.Height));
            heights[c] += child.DesiredSize.Height + Gap;
        }
        return new Size(width, Math.Max(0, heights.Max() - Gap));
    }

    protected override Size ArrangeOverride(Size final)
    {
        for (int i = 0; i < InternalChildren.Count && i < _slots.Count; i++) InternalChildren[i].Arrange(_slots[i]);
        return final;
    }
}
