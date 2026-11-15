using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GitHubStats;

/// <summary>One screen of the main window.</summary>
internal abstract class View : Grid
{
    public abstract void Update(Dashboard db);

    protected static ScrollViewer Scroll(out StackPanel root)
    {
        root = new StackPanel { Margin = new Thickness(24, 18, 24, 24) };
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    protected static TextBlock Title(string text)
    {
        var t = Ui.Text(text, 22, "Text", true);
        t.Margin = new Thickness(0, 0, 0, 2);
        return t;
    }
}

// ============================================================================ Overview

internal sealed class OverviewView : View
{
    private readonly StackPanel _root;
    private readonly ChartControl _chart = new();

    public OverviewView()
    {
        Children.Add(Scroll(out _root));
    }

    public override void Update(Dashboard db)
    {
        _root.Children.Clear();
        _root.Children.Add(Title("GitHub stats - " + db.User));
        var sub = Ui.Text("Public repositories only  |  last complete refresh: " + db.LastRefresh + "  |  next refresh possible after " + db.NextRefresh, 12, "Muted", false, true);
        sub.Margin = new Thickness(0, 0, 0, 14);
        _root.Children.Add(sub);

        foreach (var m in db.Messages) _root.Children.Add(Ui.Banner(m));

        if (!db.HasData)
        {
            _root.Children.Add(Ui.Box(Ui.Text(db.Running ? "Fetching the repository list from GitHub..." : "No data yet. The first refresh starts automatically.", 13, "Muted", false, true)));
            return;
        }

        var wrap = new WrapPanel();
        foreach (var c in db.Cards) wrap.Children.Add(Ui.Card(c.Label, c.Value, c.Detail, c.Trend));
        _root.Children.Add(wrap);

        _root.Children.Add(Ui.Heading("All repositories over time"));
        _chart.SetModel(db.All);
        if (_chart.Parent is Border old) old.Child = null;
        var box = Ui.Box(_chart);
        _root.Children.Add(box);
        if (db.All != null)
        {
            var cap = Ui.Text(db.All.Caption, 11, "Muted", false, true);
            cap.Margin = new Thickness(2, 4, 0, 0);
            _root.Children.Add(cap);
        }

        var foot = Ui.Text("Download counts only include files attached to releases (not the automatic source code archives), and GitHub counts every download, including bots, CI runs and mirrors. "
            + "A full refresh starts at most 4 times a day. " + db.RateText, 11, "Muted", false, true);
        foot.Margin = new Thickness(0, 22, 0, 0);
        _root.Children.Add(foot);
    }
}

// ============================================================================ Repositories

internal sealed class ReposView : View
{
    private readonly DataGrid _grid;
    private readonly StackPanel _detail;
    private readonly ChartControl _main = new();
    private readonly ChartControl _traffic = new();
    private Dashboard? _db;
    private bool _updating;
    private string? _selected;
    private readonly TextBox _search;
    private readonly TextBlock _count;

    public ReposView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 140 });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.6, GridUnitType.Star), MinHeight = 120 });

        _grid = Ui.Grid2();
        // no right margin: the table's scrollbar then sits on the window edge, in line with the detail pane's scrollbar below
        _grid.Margin = new Thickness(24, 0, 0, 0);
        _grid.BorderThickness = new Thickness(1, 1, 0, 1);
        _grid.Columns.Add(Ui.Col("Repository", "Name", 170));
        _grid.Columns.Add(Ui.Col("Language", "Language", 90));
        _grid.Columns.Add(Ui.Col("Stars", "Stars", 64, true, "N0"));
        _grid.Columns.Add(Ui.Col("Forks", "Forks", 60, true, "N0"));
        _grid.Columns.Add(Ui.Col("Issues", "Issues", 60, true, "N0"));
        _grid.Columns.Add(Ui.Col("Releases", "Releases", 76, true, "N0"));
        _grid.Columns.Add(Ui.Col("Downloads", "Downloads", 92, true, "N0"));
        _grid.Columns.Add(Ui.Col("Change last", "DeltaLast", 90, true, null, "DeltaLastSort"));
        _grid.Columns.Add(Ui.Col("Change 7d", "Delta7", 84, true, null, "Delta7Sort"));
        _grid.Columns.Add(Ui.Col("Per day", "PerDay", 70, true, null, "PerDaySort"));
        _grid.Columns.Add(Ui.Col("Last push", "Pushed", 0));
        _grid.SelectionChanged += (s, e) =>
        {
            if (_updating) return;
            _selected = (_grid.SelectedItem as RepoRow)?.Name;
            ShowDetail();
        };
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is RepoRow r) Ui.OpenUrl(r.Url); };
        // search box above the table
        _search = S.Input("", 280);
        _search.Padding = new Thickness(30, 0, 8, 0);
        _search.TextChanged += (s, e) => { if (_db != null) Fill(); };
        _search.KeyDown += (s, e) => { if (e.Key == Key.Escape) _search.Text = ""; };
        var icon = new TextBlock { Text = "\uE721", FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        icon.Res(TextBlock.ForegroundProperty, "Muted");
        var hint = Ui.Text("Search name, language or description", 12.5, "Muted");
        hint.Margin = new Thickness(31, 0, 0, 0);
        hint.IsHitTestVisible = false;
        _search.TextChanged += (s, e) => hint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var sbox = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        sbox.Children.Add(_search);
        sbox.Children.Add(icon);
        sbox.Children.Add(hint);
        _count = Ui.Text("", 12, "Muted");
        _count.Margin = new Thickness(12, 0, 0, 0);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 18, 24, 10) };
        bar.Children.Add(sbox);
        bar.Children.Add(_count);

        var top = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        top.Children.Add(bar);
        top.Children.Add(_grid);
        SetRow(top, 0);
        Children.Add(top);

        var split = new GridSplitter { Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Background = System.Windows.Media.Brushes.Transparent };
        SetRow(split, 1);
        Children.Add(split);

        var sv = Scroll(out _detail);
        SetRow(sv, 2);
        Children.Add(sv);
    }

    public override void Update(Dashboard db)
    {
        _db = db;
        Fill();
    }

    /// <summary>Shows the repositories that match the search box.</summary>
    private void Fill()
    {
        var db = _db!;
        string q = _search.Text.Trim();
        var rows = q.Length == 0 ? db.Repos : db.Repos.Where(r =>
            r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (r.Language ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (r.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _count.Text = q.Length == 0 ? db.Repos.Count + " repositories" : rows.Count + " of " + db.Repos.Count;
        _updating = true;
        _grid.ItemsSource = rows;
        var sel = rows.FirstOrDefault(r => r.Name == _selected) ?? rows.FirstOrDefault();
        _grid.SelectedItem = sel;
        _selected = sel?.Name;
        _updating = false;
        ShowDetail();
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        var db = _db;
        var row = db?.Repos.FirstOrDefault(r => r.Name == _selected);
        if (db == null || row == null)
        {
            _detail.Children.Add(Ui.Text("Select a repository above.", 13, "Muted"));
            return;
        }
        var det = db.Detail(row.Name);

        var head = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 4) };
        var open = Ui.Btn("Open on GitHub", () => Ui.OpenUrl(row.Url));
        DockPanel.SetDock(open, Dock.Right);
        head.Children.Add(open);
        head.Children.Add(Ui.Text(row.Name + (row.Badges.Length > 0 ? "  (" + row.Badges + ")" : ""), 18, "Text", true));
        _detail.Children.Add(head);
        if (row.Description.Length > 0) _detail.Children.Add(Ui.Text(row.Description, 12, "Muted", false, true));

        _detail.Children.Add(Ui.Heading("Downloads, stars and releases"));
        _main.SetModel(det.Main);
        if (_main.Parent is Border b1) b1.Child = null;
        _detail.Children.Add(Ui.Box(_main));
        if (det.Main != null)
        {
            var cap = Ui.Text(det.Main.Caption, 11, "Muted", false, true);
            cap.Margin = new Thickness(2, 4, 0, 0);
            _detail.Children.Add(cap);
        }

        if (det.Traffic != null || det.TrafficInfo.Length > 0)
        {
            _detail.Children.Add(Ui.Heading("Traffic"));
            if (det.TrafficInfo.Length > 0) _detail.Children.Add(Ui.Text(det.TrafficInfo, 12, "Muted", false, true));
            _traffic.SetModel(det.Traffic);
            if (_traffic.Parent is Border b2) b2.Child = null;
            var tb = Ui.Box(_traffic);
            tb.Margin = new Thickness(0, 6, 0, 0);
            _detail.Children.Add(tb);
            if (det.Referrers.Count > 0)
            {
                _detail.Children.Add(Ui.Text("Top referrers (14 days)", 12, "Muted"));
                long max = Math.Max(1, det.Referrers.Max(r => r.Count));
                _detail.Children.Add(Ui.BarList(det.Referrers.Select(r => new NameValue { Name = r.Name, Value = r.Count, Percent = r.Count * 100.0 / max, Detail = r.Uniques + " unique" }),
                    x => Fmt.N(x.Value) + " views", 1));
            }
            if (det.Paths.Count > 0)
            {
                _detail.Children.Add(Ui.Text("Popular pages (14 days)", 12, "Muted"));
                _detail.Children.Add(Ui.BarList(det.Paths, x => Fmt.N(x.Value) + " views", 2));
            }
        }

        _detail.Children.Add(Ui.Heading("Releases - downloads per release"));
        if (det.Releases.Count == 0)
        {
            _detail.Children.Add(Ui.Text("This repository has no releases (yet).", 12, "Muted"));
        }
        else
        {
            var g = Ui.Grid2();
            g.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            g.Columns.Add(Ui.Col("Release", "Tag", 170));
            g.Columns.Add(Ui.Col("Published", "Published", 96));
            g.Columns.Add(Ui.Col("Downloads", "Downloads", 90, true, "N0"));
            g.Columns.Add(Ui.Col("Share", "ShareText", 60, true, null, "Share"));
            g.Columns.Add(Ui.Col("Change 7d", "Delta7", 84, true));
            g.Columns.Add(Ui.Col("Per day", "PerDay", 66, true));
            g.Columns.Add(Ui.Col("Top files", "Files", 0));
            g.ItemsSource = det.Releases;
            g.MouseDoubleClick += (s, e) => { if (g.SelectedItem is ReleaseRow r) Ui.OpenUrl(r.Url); };
            // let the mouse wheel scroll the page instead of this (non-scrolling) grid
            g.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                var ev = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = s };
                (g.Parent as UIElement)?.RaiseEvent(ev);
            };
            _detail.Children.Add(g);
            var hint = Ui.Text("Double-click a release to open it on GitHub.", 11, "Muted");
            hint.Margin = new Thickness(0, 4, 0, 0);
            _detail.Children.Add(hint);
        }
    }
}

// ============================================================================ Stars

internal sealed class StarsView : View
{
    private readonly DataGrid _grid;
    private readonly TextBlock _note;

    public StarsView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var top = new StackPanel { Margin = new Thickness(24, 18, 24, 10) };
        top.Children.Add(Title("Recent stars"));
        _note = Ui.Text("", 12, "Muted", false, true);
        top.Children.Add(_note);
        SetRow(top, 0);
        Children.Add(top);

        _grid = Ui.Grid2();
        _grid.Margin = new Thickness(24, 0, 24, 24);
        _grid.Columns.Add(Ui.Col("When", "When", 150));
        _grid.Columns.Add(Ui.Col("", "Ago", 90));
        _grid.Columns.Add(Ui.Col("Repository", "Repo", 200));
        _grid.Columns.Add(Ui.Col("Who", "User", 0));
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is StarRow r) Ui.OpenUrl(r.Url); };
        SetRow(_grid, 1);
        Children.Add(_grid);
    }

    public override void Update(Dashboard db)
    {
        _grid.ItemsSource = db.Stars;
        _note.Text = db.Stars.Count == 0
            ? "No star dates yet. They need a GitHub token (Settings) and appear after the first complete refresh."
            : "Exact times and names as reported by GitHub. Double-click a row to open the profile.";
    }
}

// ============================================================================ Insights

internal sealed class InsightsView : View
{
    private readonly StackPanel _root;

    public InsightsView()
    {
        Children.Add(Scroll(out _root));
    }

    public override void Update(Dashboard db)
    {
        _root.Children.Clear();
        _root.Children.Add(Title("Download insight"));

        _root.Children.Add(Ui.Heading("Most downloaded files"));
        if (db.TopFiles.Count == 0) _root.Children.Add(Ui.Text("No downloads recorded yet.", 12, "Muted"));
        else _root.Children.Add(Ui.Box(Ui.BarList(db.TopFiles, x => Fmt.N(x.Value) + "  " + x.Detail, 0)));

        _root.Children.Add(Ui.Heading("Downloads by platform (guessed from file names)"));
        if (db.Platforms.Count == 0) _root.Children.Add(Ui.Text("Nothing to show yet.", 12, "Muted"));
        else _root.Children.Add(Ui.Box(Ui.BarList(db.Platforms, x => Fmt.N(x.Value) + "  " + x.Detail, 0)));

        _root.Children.Add(Ui.Heading("Where visitors come from (last 14 days, all repositories)"));
        if (db.Referrers.Count == 0)
            _root.Children.Add(Ui.Text(db.HasTraffic ? "No referrers reported." : "Traffic needs a token with push access to your repositories (see Settings).", 12, "Muted", false, true));
        else
        {
            long max = Math.Max(1, db.Referrers.Max(r => r.Count));
            _root.Children.Add(Ui.Box(Ui.BarList(db.Referrers.Select(r => new NameValue { Name = r.Name, Value = r.Count, Percent = r.Count * 100.0 / max, Detail = r.Uniques + " unique" }),
                x => Fmt.N(x.Value) + " views  (" + x.Detail + ")", 1)));
        }
    }
}
