using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GitHubStats;

/// <summary>The ten looks of the desktop widget. Each style builds its content from the dashboard; the window only hosts it.</summary>
internal static class WidgetStyles
{
    public static readonly (string Key, string Label)[] All =
    {
        ("glass", "Glass card"),
        ("pill", "Compact pill"),
        ("ring", "Milestone ring"),
        ("top", "Top repositories"),
        ("feed", "Star feed"),
        ("heatmap", "Download heatmap"),
        ("counter", "Big counter"),
        ("chart", "Trend chart"),
        ("dock", "Edge dock"),
        ("tiles", "Pick your tiles"),
    };

    public static readonly (string Key, string Label)[] Metrics =
    {
        ("downloads", "Downloads"),
        ("stars", "Stars"),
        ("dl7", "Downloads 7d"),
        ("stars7", "Stars 7d"),
        ("perday", "Downloads / day"),
        ("views", "Views 14d"),
        ("clones", "Clones 14d"),
        ("forks", "Forks"),
        ("repos", "Repositories"),
        ("releases", "Releases"),
        ("issues", "Open issues"),
    };

    public static string Normalize(string? key) => All.Any(a => a.Key == key) ? key! : "glass";

    /// <summary>Builds the content for <paramref name="style"/>. <paramref name="rebuild"/> redraws after an in-widget toggle.</summary>
    public static FrameworkElement Build(string style, Dashboard db, Action rebuild)
    {
        try
        {
            return style switch
            {
                "pill" => Pill(db),
                "ring" => Ring(db),
                "top" => Top(db),
                "feed" => Feed(db),
                "heatmap" => Heatmap(db),
                "counter" => Counter(db),
                "chart" => Chart(db, rebuild),
                "dock" => DockStyle(db),
                "tiles" => Tiles(db),
                _ => Glass(db),
            };
        }
        catch (Exception ex)
        {
            Logger.Write("Widget style " + style + " failed: " + ex.Message);
            return Card(Ui.Text("GitHub Stats", 13, "Text", true), 200);
        }
    }

    // ================================================================== 1. glass card

    private static FrameworkElement Glass(Dashboard db)
    {
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Stat("Downloads", db.HasData ? Fmt.Compact(db.TotalDownloads) : "-", db.DownloadsDelta, "last refresh", 28));
        var right = Stat("Stars", db.HasData ? Fmt.Compact(db.TotalStars) : "-", db.StarsDelta, "", 28);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var c in right.Children.OfType<TextBlock>()) c.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(right, 1);
        top.Children.Add(right);

        var sp = new StackPanel();
        sp.Children.Add(top);
        var series = Series(30);
        var chart = new MiniChart { Height = 46, Margin = new Thickness(-2, 10, -2, 4) };
        chart.Add(series.Downloads, "S0", true);
        sp.Children.Add(chart);
        sp.Children.Add(Foot(db));
        return Card(sp, 250, glass: true);
    }

    // ================================================================== 2. compact pill

    private static FrameworkElement Pill(Dashboard db)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Icon("", "S0", 14));
        row.Children.Add(Bold(db.HasData ? Fmt.N(db.TotalDownloads) : "-", 14, new Thickness(6, 0, 0, 0)));
        if (db.DownloadsDelta is long d && d != 0) row.Children.Add(DeltaText(d, "", 12, new Thickness(5, 1, 0, 0)));
        var sep = new Border { Width = 1, Height = 14, Margin = new Thickness(12, 0, 12, 0) };
        sep.Res(Border.BackgroundProperty, "Border");
        row.Children.Add(sep);
        row.Children.Add(Icon("", "S1", 14));
        row.Children.Add(Bold(db.HasData ? Fmt.N(db.TotalStars) : "-", 14, new Thickness(6, 0, 0, 0)));
        if (db.StarsDelta is long s && s != 0) row.Children.Add(DeltaText(s, "", 12, new Thickness(5, 1, 0, 0)));
        var b = Card(row, 0, radius: 20, pad: new Thickness(16, 8, 18, 9));
        b.ToolTip = db.User + " - updated " + (db.LastSuccess > 0 ? Fmt.Hm(db.LastSuccess) : "not yet");
        return b;
    }

    // ================================================================== 3. milestone ring

    private static FrameworkElement Ring(Dashboard db)
    {
        long total = db.TotalDownloads;
        var ms = Host.App.Cfg.Milestones;
        long next = ms.FirstOrDefault(m => m > total);
        long prev = ms.LastOrDefault(m => m <= total);
        double p = next == 0 ? 1 : (double)(total - prev) / Math.Max(1, next - prev);

        var ring = new RingProgress { Value = p, Width = 76, Height = 76 };
        var g = new Grid { Width = 76, Height = 76 };
        g.Children.Add(ring);
        var pct = Bold(Math.Round(p * 100) + "%", 15, new Thickness(0));
        pct.HorizontalAlignment = HorizontalAlignment.Center;
        g.Children.Add(pct);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        info.Children.Add(Bold(db.HasData ? Fmt.N(total) : "-", 26, new Thickness(0)));
        info.Children.Add(Ui.Text(next == 0 ? "all milestones reached" : "of " + Fmt.N(next) + " downloads", 11.5, "Muted"));
        string eta = "";
        if (next > 0 && db.DownloadsPerDay is double pd && pd > 0.05)
        {
            double days = (next - total) / pd;
            eta = days < 1.5 ? "about a day to go" : days < 60 ? "about " + Math.Round(days) + " days to go" : "about " + Math.Round(days / 30) + " months to go";
        }
        if (eta.Length > 0) info.Children.Add(Ui.Text(eta, 11.5, "Up"));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(g);
        row.Children.Add(info);
        var sp = new StackPanel();
        sp.Children.Add(row);
        sp.Children.Add(Foot(db, new Thickness(0, 8, 0, 0)));
        return Card(sp, 250);
    }

    // ================================================================== 4. top repositories

    private static FrameworkElement Top(Dashboard db)
    {
        var sp = new StackPanel();
        sp.Children.Add(Label("Top repositories"));
        var rows = db.Repos.OrderByDescending(r => r.Downloads).Take(5).ToList();
        long max = Math.Max(1, rows.Count > 0 ? rows[0].Downloads : 1);
        foreach (var r in rows)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            var n = Ui.Text(r.Name, 12);
            n.TextTrimming = TextTrimming.CharacterEllipsis;
            n.ToolTip = r.Name + (r.Delta7.Length > 0 ? "  (" + r.Delta7 + " in 7 days)" : "");
            g.Children.Add(n);
            var bar = new Grid { Height = 6, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            double f = Math.Max(0.03, (double)r.Downloads / max);
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(f, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - f + 0.0001, GridUnitType.Star) });
            var fill = new Border { CornerRadius = new CornerRadius(3) };
            fill.Res(Border.BackgroundProperty, "S0");
            bar.Children.Add(fill);
            Grid.SetColumn(bar, 1);
            g.Children.Add(bar);
            var v = Ui.Text(Fmt.Compact(r.Downloads), 12, "Text", true);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(v, 2);
            g.Children.Add(v);
            sp.Children.Add(g);
        }
        if (rows.Count == 0) sp.Children.Add(Ui.Text("No data yet.", 12, "Muted"));
        sp.Children.Add(Foot(db, new Thickness(0, 8, 0, 0)));
        return Card(sp, 270);
    }

    // ================================================================== 5. star feed

    private static readonly string[] AvatarKeys = { "S0", "S1", "S2", "Link" };

    private static FrameworkElement Feed(Dashboard db)
    {
        var sp = new StackPanel();
        var head = new DockPanel { LastChildFill = false };
        head.Children.Add(Label("Latest stars"));
        var total = Ui.Text("★ " + Fmt.N(db.TotalStars), 11, "Muted");
        DockPanel.SetDock(total, Dock.Right);
        head.Children.Add(total);
        sp.Children.Add(head);
        var stars = db.Stars.Take(4).ToList();
        if (stars.Count == 0)
        {
            var t = Ui.Text("Star dates need a GitHub token. Add one in Settings and they appear after the next refresh.", 11.5, "Muted", false, true);
            t.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(t);
        }
        int i = 0;
        foreach (var s in stars)
        {
            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var av = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 10, 0), Opacity = 0.9 };
            av.Res(Border.BackgroundProperty, AvatarKeys[i++ % AvatarKeys.Length]);
            av.Child = new TextBlock { Text = Initials(s.User), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(av, Dock.Left);
            row.Children.Add(av);
            var txt = new StackPanel();
            var line = new TextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            line.Res(TextBlock.ForegroundProperty, "Text");
            line.Inlines.Add(new System.Windows.Documents.Run(s.User) { FontWeight = FontWeights.SemiBold });
            line.Inlines.Add(new System.Windows.Documents.Run(" starred " + s.Repo));
            txt.Children.Add(line);
            txt.Children.Add(Ui.Text(s.Ago, 10.5, "Muted"));
            row.Children.Add(txt);
            row.ToolTip = s.User + " starred " + s.Repo + " - " + s.When;
            sp.Children.Add(row);
        }
        return Card(sp, 260);
    }

    private static string Initials(string user)
    {
        var parts = user.Split('-', '_', '.', ' ').Where(p => p.Length > 0).ToArray();
        string s = parts.Length >= 2 ? parts[0][..1] + parts[1][..1] : user.Length >= 2 ? user[..2] : user;
        return s.ToUpperInvariant();
    }

    // ================================================================== 6. download heatmap

    private static FrameworkElement Heatmap(Dashboard db)
    {
        const int weeks = 8;
        var days = DailyDownloads(weeks * 7);
        var grid = HeatGrid(days, weeks, 14);
        var sp = new StackPanel();
        var head = new DockPanel { LastChildFill = false };
        head.Children.Add(Label("Downloads per day"));
        long sum7 = days.Skip(days.Count - 7).Sum(d => d.Value ?? 0);
        var s7 = Ui.Text(Fmt.N(sum7) + " this week", 11, "Up");
        s7.Margin = new Thickness(14, 0, 0, 0);
        DockPanel.SetDock(s7, Dock.Right);
        head.Children.Add(s7);
        sp.Children.Add(head);
        grid.Margin = new Thickness(-1.5, 6, 0, 4);
        sp.Children.Add(grid);
        sp.Children.Add(HeatLegend());
        return Card(sp, 0);
    }

    /// <summary>The legend under a heatmap: less ... more.</summary>
    public static FrameworkElement HeatLegend()
    {
        var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        legend.Children.Add(Ui.Text("less ", 10, "Muted"));
        foreach (var o in new[] { 0.0, 0.3, 0.5, 0.75, 1.0 })
        {
            var l = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2), Margin = new Thickness(1.5, 0, 1.5, 0), Opacity = o == 0 ? 1 : o };
            l.Res(Border.BackgroundProperty, o == 0 ? "Accent" : "S2");
            legend.Children.Add(l);
        }
        legend.Children.Add(Ui.Text(" more", 10, "Muted"));
        return legend;
    }

    /// <summary>GitHub-style grid of downloads per day: one column per week (Monday on top), the last column ends today.</summary>
    public static Grid HeatGrid(List<(DateTime Day, long? Value)> days, int weeks, double size)
    {
        var known = days.Where(d => d.Value.HasValue && d.Value > 0).Select(d => d.Value!.Value).OrderBy(v => v).ToList();
        long q1 = Q(known, 0.25), q2 = Q(known, 0.5), q3 = Q(known, 0.75);

        var grid = new Grid();
        for (int c = 0; c < weeks; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int r = 0; r < 7; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // the last column ends today; rows are Monday..Sunday
        var today = days[^1].Day;
        int todayRow = ((int)today.DayOfWeek + 6) % 7;
        int start = days.Count - 1 - ((weeks - 1) * 7 + todayRow);
        for (int idx = Math.Max(0, start); idx < days.Count; idx++)
        {
            int offset = idx - start;
            int col = offset / 7, row = offset % 7;
            if (col >= weeks) break;
            var (day, val) = days[idx];
            var cell = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 4.5), Margin = new Thickness(1.5) };
            if (val == null) { cell.Res(Border.BackgroundProperty, "Accent"); cell.ToolTip = day.ToString("ddd d MMM") + ": no data"; }
            else
            {
                cell.Res(Border.BackgroundProperty, val > 0 ? "S2" : "Accent");
                if (val > 0) cell.Opacity = val <= q1 ? 0.3 : val <= q2 ? 0.5 : val <= q3 ? 0.75 : 1.0;
                cell.ToolTip = day.ToString("ddd d MMM") + ": " + Fmt.N(val.Value) + " downloads";
            }
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            grid.Children.Add(cell);
        }
        return grid;
    }

    private static long Q(List<long> sorted, double q) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];

    // ================================================================== 7. big counter

    private static long _shown = -1;
    private static DispatcherTimer? _roll;

    private static FrameworkElement Counter(Dashboard db)
    {
        long target = db.TotalDownloads;
        var big = new TextBlock { FontSize = 46, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
        big.Res(TextBlock.ForegroundProperty, "Text");
        big.Text = Fmt.N(_shown < 0 ? target : _shown);
        if (_shown >= 0 && _shown != target && db.HasData)
        {
            // roll the number up (or down) to the new value
            long from = _shown;
            var t0 = DateTime.Now;
            _roll?.Stop();
            _roll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _roll.Tick += (s, e) =>
            {
                double k = Math.Min(1, (DateTime.Now - t0).TotalMilliseconds / 1400.0);
                double ease = 1 - Math.Pow(1 - k, 3);
                big.Text = Fmt.N((long)Math.Round(from + (target - from) * ease));
                if (k >= 1) _roll!.Stop();
            };
            _roll.Start();
        }
        _shown = target;

        var sp = new StackPanel();
        var l = Label("Downloads");
        l.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(l);
        sp.Children.Add(big);
        long? day = db.DownloadsWin[0].Value;
        var badge = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 2, 10, 3), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        badge.Res(Border.BackgroundProperty, "Accent");
        badge.Child = DeltaText(day ?? db.DownloadsDelta ?? 0, day != null ? "in 24 hours" : "last refresh", 11.5, new Thickness(0));
        sp.Children.Add(badge);
        var stars = Ui.Text("★ " + Fmt.N(db.TotalStars) + " stars", 11.5, "Muted");
        stars.HorizontalAlignment = HorizontalAlignment.Center;
        stars.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(stars);
        return Card(sp, 220);
    }

    // ================================================================== 8. trend chart

    private static FrameworkElement Chart(Dashboard db, Action rebuild)
    {
        int range = Host.State.WidgetChartDays == 7 ? 7 : 30;
        var s = Series(range);

        var head = new DockPanel();
        var legend = new StackPanel { Orientation = Orientation.Horizontal };
        legend.Children.Add(Dot("S0"));
        legend.Children.Add(Ui.Text(" downloads  ", 11, "Muted"));
        legend.Children.Add(Dot("S1"));
        legend.Children.Add(Ui.Text(" stars", 11, "Muted"));
        var toggle = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (int r in new[] { 7, 30 })
        {
            int rr = r;
            var t = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(7, 1, 7, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            if (rr == range) t.Res(Border.BackgroundProperty, "Accent");
            t.Child = Ui.Text(rr + "d", 11, rr == range ? "Text" : "Muted", rr == range);
            t.MouseLeftButtonDown += (o, e) => { e.Handled = true; Host.State.WidgetChartDays = rr; Host.State.Save(); rebuild(); };
            toggle.Children.Add(t);
        }
        DockPanel.SetDock(toggle, Dock.Right);
        head.Children.Add(toggle);
        head.Children.Add(legend);

        var chart = new MiniChart { Height = 70, Margin = new Thickness(0, 8, 0, 6), SharedPercent = true };
        chart.Add(s.Downloads, "S0", true);
        chart.Add(s.Stars, "S1", false);

        var sums = new Grid();
        sums.ColumnDefinitions.Add(new ColumnDefinition());
        sums.ColumnDefinitions.Add(new ColumnDefinition());
        long dDl = s.Downloads.Length > 1 ? (long)(s.Downloads[^1] - s.Downloads[0]) : 0;
        long dSt = s.Stars.Length > 1 ? (long)(s.Stars[^1] - s.Stars[0]) : 0;
        sums.Children.Add(Stat("Downloads", Fmt.Compact(db.TotalDownloads), dDl, range + "d", 18));
        var st = Stat("Stars", Fmt.Compact(db.TotalStars), dSt, range + "d", 18);
        Grid.SetColumn(st, 1);
        sums.Children.Add(st);

        var sp = new StackPanel();
        sp.Children.Add(head);
        sp.Children.Add(chart);
        sp.Children.Add(sums);
        return Card(sp, 270);
    }

    // ================================================================== 9. edge dock

    private static FrameworkElement DockStyle(Dashboard db)
    {
        var sp = new StackPanel { Width = 64 };
        void Add(string glyph, string brush, string value, long? delta)
        {
            var i = Icon(glyph, brush, 16);
            i.HorizontalAlignment = HorizontalAlignment.Center;
            i.Margin = new Thickness(0, sp.Children.Count == 0 ? 0 : 10, 0, 0);
            sp.Children.Add(i);
            var v = Bold(value, 14, new Thickness(0, 2, 0, 0));
            v.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(v);
            if (delta != null)
            {
                var d = DeltaText(delta.Value, "", 10.5, new Thickness(0));
                d.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(d);
            }
        }
        Add("", "S0", db.HasData ? Fmt.Compact(db.TotalDownloads) : "-", db.DownloadsDelta);
        Add("", "S1", db.HasData ? Fmt.Compact(db.TotalStars) : "-", db.StarsDelta);
        var (views, _) = TrafficTotals();
        if (views != null) Add("", "S2", Fmt.Compact(views.Value), null);
        Add("", "Muted", Fmt.N(db.Totals.Repos), null);

        // accent strip on the screen side that stays visible when the dock slides away
        var strip = new Border { Width = 5, CornerRadius = new CornerRadius(3, 0, 0, 3) };
        strip.Res(Border.BackgroundProperty, "S0");
        var dock = new DockPanel();
        DockPanel.SetDock(strip, System.Windows.Controls.Dock.Left);
        dock.Children.Add(strip);
        var inner = new Border { Child = sp, Padding = new Thickness(6, 12, 8, 12) };
        dock.Children.Add(inner);
        var b = Card(dock, 0, radius: 12, pad: new Thickness(0));
        b.CornerRadius = new CornerRadius(12, 0, 0, 12);
        b.BorderThickness = new Thickness(1, 1, 0, 1);
        b.Margin = new Thickness(8, 8, 0, 8);
        b.ToolTip = db.User + " - updated " + (db.LastSuccess > 0 ? Fmt.Hm(db.LastSuccess) : "not yet");
        return b;
    }

    // ================================================================== 10. pick your tiles

    private static FrameworkElement Tiles(Dashboard db)
    {
        var keys = Host.State.WidgetTiles.Where(k => Metrics.Any(m => m.Key == k)).ToList();
        if (keys.Count == 0) keys = new List<string> { "downloads", "stars" };
        var wrap = new UniformGrid2(keys.Count <= 2 ? keys.Count : keys.Count <= 4 ? 2 : 3);
        foreach (var k in keys)
        {
            var (value, delta) = Metric(db, k);
            var t = new StackPanel();
            var l = new StackPanel { Orientation = Orientation.Horizontal };
            l.Children.Add(Icon(MetricIcon(k), MetricBrush(k), 11));
            l.Children.Add(Ui.Text("  " + Metrics.First(m => m.Key == k).Label, 10.5, "Muted"));
            t.Children.Add(l);
            t.Children.Add(Bold(value, 19, new Thickness(0, 2, 0, 0)));
            if (delta != null) t.Children.Add(DeltaText(delta.Value, "", 10.5, new Thickness(0)));
            var tile = new Border { Child = t, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7, 10, 8), Margin = new Thickness(3), MinWidth = 104, BorderThickness = new Thickness(1) };
            tile.Res(Border.BackgroundProperty, "Bg");
            tile.Res(Border.BorderBrushProperty, "Border");
            wrap.Add(tile);
        }
        var sp = new StackPanel();
        sp.Children.Add(wrap.Panel);
        var hint = Ui.Text("Right-click > Tiles to choose", 10, "Muted");
        hint.Margin = new Thickness(3, 4, 0, 0);
        sp.Children.Add(hint);
        return Card(sp, 0, pad: new Thickness(9, 9, 9, 7));
    }

    private static (string Value, long? Delta) Metric(Dashboard db, string key)
    {
        var (views, clones) = TrafficTotals();
        return key switch
        {
            "downloads" => (Fmt.N(db.TotalDownloads), db.DownloadsDelta),
            "stars" => (Fmt.N(db.TotalStars), db.StarsDelta),
            "dl7" => (db.DownloadsWin[1].Value is long a ? Fmt.DeltaText(a) : "-", null),
            "stars7" => (db.StarsWin[1].Value is long b ? Fmt.DeltaText(b) : "-", null),
            "perday" => (db.DownloadsPerDay is double pd ? pd.ToString(pd < 10 ? "0.0" : "0") : "-", null),
            "views" => (views is long v ? Fmt.N(v) : "token", null),
            "clones" => (clones is long c ? Fmt.N(c) : "token", null),
            "forks" => (Fmt.N(db.Totals.Forks), null),
            "repos" => (Fmt.N(db.Totals.Repos), null),
            "releases" => (Fmt.N(db.Totals.Releases), null),
            "issues" => (Fmt.N(db.Totals.OpenIssues), null),
            _ => ("-", null),
        };
    }

    private static string MetricIcon(string k) => k switch
    {
        "downloads" or "dl7" or "perday" => "",
        "stars" or "stars7" => "",
        "views" => "",
        "clones" => "",
        "forks" => "",
        "repos" => "",
        "releases" => "",
        "issues" => "",
        _ => "",
    };

    private static string MetricBrush(string k) => k switch
    {
        "downloads" or "dl7" or "perday" => "S0",
        "stars" or "stars7" => "S1",
        "views" or "clones" => "S2",
        _ => "Muted",
    };

    // ================================================================== data helpers

    /// <summary>Views and clones of the last 14 days over all shown repositories (null = no traffic data).</summary>
    public static (long? Views, long? Clones) TrafficTotals()
    {
        var shown = Host.App.Data.Repos;
        var tr = Host.App.Traffic.Where(kv => shown.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        if (tr.Count == 0) return (null, null);
        return (tr.Sum(t => t.Views14), tr.Sum(t => t.Clones14));
    }

    /// <summary>Downloads and stars over the last <paramref name="days"/> days (one point per snapshot).</summary>
    private static (double[] Downloads, double[] Stars) Series(int days)
    {
        long from = Clock.Now() - days * 86400L;
        var snaps = Host.App.Data.Snapshots;
        var pick = snaps.Where(s => s.T >= from).ToList();
        var before = snaps.LastOrDefault(s => s.T < from);
        if (before != null) pick.Insert(0, before);
        return (pick.Select(s => (double)s.Downloads).ToArray(), pick.Select(s => (double)s.Stars).ToArray());
    }

    /// <summary>Downloads per calendar day (local time), oldest first, ending today. null = no snapshot that day.</summary>
    public static List<(DateTime Day, long? Value)> DailyDownloads(int count)
    {
        var snaps = Host.App.Data.Snapshots;
        var lastOfDay = new SortedDictionary<DateTime, long>();
        foreach (var s in snaps)
        {
            var d = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(s.T), Clock.Tz).Date;
            lastOfDay[d] = s.Downloads;
        }
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Clock.Tz).Date;
        var list = new List<(DateTime, long?)>();
        for (int i = count - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            long? v = null;
            if (lastOfDay.TryGetValue(day, out long end))
            {
                // only when the previous day was measured too; otherwise this would hold several days at once
                if (lastOfDay.TryGetValue(day.AddDays(-1), out long before)) v = Math.Max(0, end - before);
            }
            list.Add((day, v));
        }
        return list;
    }

    // ================================================================== building blocks

    private static Border Card(UIElement child, double width, bool glass = false, double radius = 14, Thickness? pad = null)
    {
        var b = new Border { Child = child, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Padding = pad ?? new Thickness(16, 13, 16, 11), Margin = new Thickness(8) };
        if (width > 0) b.Width = width;
        if (glass && Theme.B("Card") is SolidColorBrush sc)
        {
            var c = sc.Color;
            b.Background = new SolidColorBrush(Color.FromArgb(238, c.R, c.G, c.B));
            var hi = Theme.Dark ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(90, 255, 255, 255);
            b.BorderBrush = new SolidColorBrush(hi);
        }
        else
        {
            b.Res(Border.BackgroundProperty, "Card");
            b.Res(Border.BorderBrushProperty, "Border");
        }
        b.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.35 };
        return b;
    }

    private static StackPanel Stat(string label, string value, long? delta, string suffix, double size)
    {
        var sp = new StackPanel();
        sp.Children.Add(Label(label));
        sp.Children.Add(Bold(value, size, new Thickness(0, 1, 0, 0)));
        if (delta != null) sp.Children.Add(DeltaText(delta.Value, suffix, 11, new Thickness(0)));
        return sp;
    }

    private static TextBlock Label(string text)
    {
        var t = Ui.Text(text.ToUpperInvariant(), 10, "Muted");
        t.FontWeight = FontWeights.SemiBold;
        return t;
    }

    private static TextBlock Bold(string text, double size, Thickness margin)
    {
        var t = Ui.Text(text, size, "Text", true);
        t.Margin = margin;
        return t;
    }

    private static TextBlock DeltaText(long d, string suffix, double size, Thickness margin)
    {
        var t = Ui.Text(Fmt.DeltaText(d) + (suffix.Length > 0 ? " " + suffix : ""), size, d > 0 ? "Up" : d < 0 ? "Down" : "Muted");
        t.Margin = margin;
        return t;
    }

    private static TextBlock Icon(string glyph, string brush, double size)
    {
        var t = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        t.Res(TextBlock.ForegroundProperty, brush);
        return t;
    }

    private static FrameworkElement Dot(string brush)
    {
        var e = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        e.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
        return e;
    }

    private static TextBlock Foot(Dashboard db, Thickness? margin = null)
    {
        var t = Ui.Text(db.Running ? "Refreshing..." : db.LastSuccess > 0 ? db.User + "  ·  updated " + Fmt.Hm(db.LastSuccess) : db.User, 10, "Muted");
        if (margin != null) t.Margin = margin.Value;
        return t;
    }
}

/// <summary>A simple grid with a fixed number of columns.</summary>
internal sealed class UniformGrid2
{
    public readonly Grid Panel = new();
    private readonly int _cols;
    private int _n;

    public UniformGrid2(int cols)
    {
        _cols = Math.Max(1, cols);
        for (int i = 0; i < _cols; i++) Panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    public void Add(FrameworkElement e)
    {
        int row = _n / _cols, col = _n % _cols;
        if (row >= Panel.RowDefinitions.Count) Panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(e, row);
        Grid.SetColumn(e, col);
        Panel.Children.Add(e);
        _n++;
    }
}

/// <summary>Small line chart: every series scaled to its own range, optional soft area under the line.</summary>
internal sealed class MiniChart : FrameworkElement
{
    private readonly List<(double[] V, string Brush, bool Fill)> _series = new();

    /// <summary>true = all series on one scale: % change since their first value (honest when sizes differ a lot).</summary>
    public bool SharedPercent;

    public void Add(double[] v, string brushKey, bool fill) => _series.Add((v, brushKey, fill));

    private static double[] Pct(double[] v)
    {
        double b = v[0] == 0 ? 1 : v[0];
        return v.Select(x => (x - v[0]) / b * 100).ToArray();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new Size(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, Height);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10) return;
        var grid = new Pen(Theme.B("Border"), 1);
        dc.DrawLine(grid, new Point(0, h - 0.5), new Point(w, h - 0.5));
        double gMin = 0, gMax = 0;
        if (SharedPercent)
        {
            var all = _series.Where(x => x.V.Length > 1).SelectMany(x => Pct(x.V)).ToList();
            if (all.Count > 0) { gMin = Math.Min(0, all.Min()); gMax = all.Max(); }
        }
        foreach (var (raw, key, fill) in _series)
        {
            if (raw.Length < 2) continue;
            var v = SharedPercent ? Pct(raw) : raw;
            double min = SharedPercent ? gMin : v.Min(), max = SharedPercent ? gMax : v.Max(), span = Math.Max(1e-9, max - min);
            var pts = v.Select((x, i) => new Point(i * w / (v.Length - 1), max == min ? h * 0.6 : 3 + (h - 6) * (1 - (x - min) / span))).ToList();
            var brush = Theme.B(key);
            if (fill)
            {
                var area = new StreamGeometry();
                using (var c = area.Open())
                {
                    c.BeginFigure(new Point(0, h), true, true);
                    foreach (var p in pts) c.LineTo(p, true, true);
                    c.LineTo(new Point(w, h), true, true);
                }
                area.Freeze();
                var col = ((SolidColorBrush)brush).Color;
                var g = new LinearGradientBrush(Color.FromArgb(90, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B), 90);
                dc.DrawGeometry(g, null, area);
            }
            var line = new StreamGeometry();
            using (var c = line.Open())
            {
                c.BeginFigure(pts[0], false, false);
                foreach (var p in pts.Skip(1)) c.LineTo(p, true, true);
            }
            line.Freeze();
            dc.DrawGeometry(null, new Pen(brush, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, line);
            dc.DrawEllipse(brush, null, pts[^1], 3, 3);
        }
    }
}

/// <summary>Circular progress (0..1) starting at the top, clockwise.</summary>
internal sealed class RingProgress : FrameworkElement
{
    public double Value;

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight), th = 8, r = (size - th) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(Theme.B("Accent"), th), c, r, r);
        double p = Math.Max(0, Math.Min(1, Value));
        var pen = new Pen(Theme.B("S2"), th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (p >= 0.999) { dc.DrawEllipse(null, pen, c, r, r); return; }
        if (p <= 0.001) return;
        double a = p * 2 * Math.PI;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, p > 0.5, SweepDirection.Clockwise, true, true);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }
}
