using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace GitHubStats;

/// <summary>The main window: navigation on the left, one screen on the right, status bar at the bottom.</summary>
internal sealed class MainWindow : Window
{
    private readonly Dictionary<string, View> _views = new();
    private readonly Dictionary<string, Border> _nav = new();
    private readonly Border _host;
    private readonly TextBlock _left;
    private readonly TextBlock _right;
    private readonly TextBlock _refresh;
    private readonly TextBlock _update;
    private string _current = "overview";
    private Dashboard? _db;

    public MainWindow()
    {
        Title = "GitHub Stats";
        Width = 1120;
        Height = 760;
        MinWidth = 780;
        MinHeight = 520;
        FontFamily = new FontFamily("Segoe UI");
        this.Res(BackgroundProperty, "Bg");
        RestorePlacement();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---- navigation
        var navBox = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
        navBox.Res(Border.BackgroundProperty, "Card");
        navBox.Res(Border.BorderBrushProperty, "Border");
        var nav = new StackPanel { Margin = new Thickness(10, 16, 10, 10) };
        var brand = Ui.Text("GitHub Stats", 16, "Text", true);
        brand.Margin = new Thickness(8, 0, 0, 14);
        nav.Children.Add(brand);
        AddNav(nav, "overview", "\uE80F", "Overview", new OverviewView());
        AddNav(nav, "repos", "\uE8B7", "Repositories", new ReposView());
        AddNav(nav, "stars", "\uE734", "Stars", new StarsView());
        AddNav(nav, "insights", "\uE9D9", "Insights", new InsightsView());
        AddNav(nav, "export", "\uE72D", "Export & share", new ExportView());
        AddNav(nav, "settings", "\uE713", "Settings", new SettingsView());
        AddNav(nav, "about", "\uE946", "About", new AboutView());
        navBox.Child = nav;
        Grid.SetRow(navBox, 0);
        Grid.SetColumn(navBox, 0);
        grid.Children.Add(navBox);

        // ---- content
        _host = new Border();
        Grid.SetRow(_host, 0);
        Grid.SetColumn(_host, 1);
        grid.Children.Add(_host);

        // ---- status bar
        var status = new Grid();
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _left = Ui.Text("", 12, "Muted");
        _left.Margin = new Thickness(12, 0, 0, 0);
        _right = Ui.Text("", 12, "Muted");
        _right.Margin = new Thickness(0, 0, 12, 0);
        var rightBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _update = Ui.Text("", 12, "Link");
        _update.Margin = new Thickness(0, 0, 16, 0);
        _update.Cursor = Cursors.Hand;
        _update.Visibility = Visibility.Collapsed;
        _update.MouseLeftButtonUp += (s, e) => Ui.OpenUrl(Host.Update?.Url);
        _refresh = Ui.Text("\u21BB  Refresh now", 12, "Link");
        _refresh.Margin = new Thickness(0, 0, 16, 0);
        _refresh.Cursor = Cursors.Hand;
        _refresh.MouseLeftButtonUp += (s, e) =>
        {
            Host.App.RefreshNow();
            UpdateStatus();
        };
        ToolTipService.SetInitialShowDelay(_refresh, 200);
        rightBox.Children.Add(_update);
        rightBox.Children.Add(_refresh);
        rightBox.Children.Add(_right);
        Grid.SetColumn(rightBox, 1);
        status.Children.Add(_left);
        status.Children.Add(rightBox);
        Host.UpdateFound += () => Dispatcher.BeginInvoke(new Action(ShowUpdate));
        var tick = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        tick.Tick += (s, e) => UpdateStatus();
        tick.Start();
        var bar = new Border { Child = status, Height = 28, BorderThickness = new Thickness(0, 1, 0, 0) };
        bar.Res(Border.BackgroundProperty, "Card");
        bar.Res(Border.BorderBrushProperty, "Border");
        Grid.SetRow(bar, 1);
        Grid.SetColumnSpan(bar, 2);
        grid.Children.Add(bar);

        Content = grid;
        TaskbarItemInfo = new TaskbarItemInfo { Description = "GitHub Stats" };
        Select("overview");

        Activated += (s, e) => OnActivated2();
    }

    private readonly Dictionary<string, Border> _bars = new();
    private readonly Dictionary<string, TextBlock> _icons = new();
    private readonly Dictionary<string, TextBlock> _labels = new();

    private void AddNav(StackPanel panel, string key, string glyph, string label, View view)
    {
        _views[key] = view;
        var row = new DockPanel { Margin = new Thickness(0, 7, 10, 7) };
        var bar = new Border { Width = 3, Height = 16, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 9, 0), Visibility = Visibility.Hidden };
        bar.Res(Border.BackgroundProperty, "Link");
        _bars[key] = bar;
        var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Width = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        icon.Res(TextBlock.ForegroundProperty, "Muted");
        _icons[key] = icon;
        var t = Ui.Text(label, 14);
        _labels[key] = t;
        row.Children.Add(bar);
        row.Children.Add(icon);
        row.Children.Add(t);
        var b = new Border { Child = row, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        b.MouseEnter += (s, e) => { if (_current != key) b.Res(Border.BackgroundProperty, "Bg"); };
        b.MouseLeave += (s, e) => { if (_current != key) b.Background = Brushes.Transparent; };
        b.MouseLeftButtonUp += (s, e) => Select(key);
        _nav[key] = b;
        panel.Children.Add(b);
    }

    public void Select(string key)
    {
        _current = key;
        foreach (var kv in _nav)
        {
            bool on = kv.Key == key;
            if (on) kv.Value.Res(Border.BackgroundProperty, "Accent"); else kv.Value.Background = Brushes.Transparent;
            _bars[kv.Key].Visibility = on ? Visibility.Visible : Visibility.Hidden;
            _icons[kv.Key].Res(TextBlock.ForegroundProperty, on ? "Link" : "Muted");
            _labels[kv.Key].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
        var view = _views[key];
        if (!ReferenceEquals(_host.Child, view)) _host.Child = view;
        if (_db != null) view.Update(_db);
    }

    /// <summary>Called on the UI thread with fresh numbers.</summary>
    public void ShowData(Dashboard db)
    {
        _db = db;
        _views[_current].Update(db);

        UpdateStatus();

        // taskbar: progress while refreshing, tooltip with the totals, badge with new stars
        var tb = TaskbarItemInfo;
        if (tb != null)
        {
            tb.ProgressState = db.Running ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.None;
            tb.Description = "GitHub Stats - " + Fmt.N(db.TotalDownloads) + " downloads, " + Fmt.N(db.TotalStars) + " stars";
            UpdateBadge(db);
        }
    }

    /// <summary>Status bar: last refresh, live countdown to the next one, "Refresh now" when allowed.</summary>
    private void UpdateStatus()
    {
        var db = _db;
        if (db == null) return;
        var app = Host.App;
        long now = Clock.Now();
        bool running = app.Running;
        string next = running ? "" : "  |  next automatic refresh " + In(app.NextAutoAt - now);
        _left.Text = (running ? "Refreshing from GitHub..." : "Last refresh: " + db.LastRefresh + next) + (db.Progress.Length > 0 && !running ? "  |  " + db.Progress : "");
        _right.Text = db.RateText;

        long manual = app.ManualAllowedAt;
        bool can = !running && now >= manual;
        _refresh.Text = running ? "\u21BB  Refreshing..." : "\u21BB  Refresh now";
        _refresh.Res(TextBlock.ForegroundProperty, can ? "Link" : "Muted");
        _refresh.Cursor = can ? Cursors.Hand : Cursors.Arrow;
        _refresh.ToolTip = running ? "A refresh is running." : can ? "Fetch the latest numbers from GitHub now."
            : "To stay well within GitHub's limits, a manual refresh is possible 1 hour after the previous one (" + In(manual - now) + ").";
    }

    private static string In(long sec)
    {
        if (sec <= 60) return "any moment";
        long m = (sec + 59) / 60;
        return m < 60 ? "in " + m + " min" : "in " + (m / 60) + " h " + (m % 60).ToString("00") + " min";
    }

    private void ShowUpdate()
    {
        var u = Host.Update;
        if (u == null || !u.Newer) { _update.Visibility = Visibility.Collapsed; return; }
        _update.Text = "\u2B07  Version " + u.Version + " available";
        _update.ToolTip = "Download the new version from GitHub";
        _update.Visibility = Visibility.Visible;
    }

    private void UpdateBadge(Dashboard db)
    {
        try
        {
            long seen = Host.State.SeenStars;
            int unseen = IsActive ? 0 : db.Stars.Count(x => x.Time > seen);
            if (unseen <= 0) { TaskbarItemInfo.Overlay = null; return; }
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xd9, 0x59, 0x26)), null, new Point(16, 16), 15, 15);
                var ft = new FormattedText(unseen > 99 ? "99+" : unseen.ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), unseen > 9 ? 15 : 20, Brushes.White, 1.0);
                dc.DrawText(ft, new Point(16 - ft.Width / 2, 16 - ft.Height / 2));
            }
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            TaskbarItemInfo.Overlay = bmp;
        }
        catch (Exception ex) { Logger.Write("Taskbar badge failed: " + ex.Message); }
    }

    private void OnActivated2()
    {
        // The user is looking at the window: the new stars count as seen.
        try
        {
            if (_db != null && _db.Stars.Count > 0) Host.State.SeenStars = Math.Max(Host.State.SeenStars, _db.Stars.Max(x => x.Time));
            if (TaskbarItemInfo != null) TaskbarItemInfo.Overlay = null;
            Host.OnWindowActivated();
        }
        catch { /* ignore */ }
    }

    private void RestorePlacement()
    {
        var ui = Host.State;
        if (ui.WinW is double w && ui.WinH is double h && w >= MinWidth && h >= MinHeight)
        {
            Width = Math.Min(w, SystemParameters.VirtualScreenWidth);
            Height = Math.Min(h, SystemParameters.VirtualScreenHeight);
        }
        if (ui.WinX is double x && ui.WinY is double y
            && x > SystemParameters.VirtualScreenLeft - 50 && y > SystemParameters.VirtualScreenTop - 10
            && x < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = x;
            Top = y;
        }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (ui.WinMax) WindowState = WindowState.Maximized;
    }

    public void SavePlacement()
    {
        var ui = Host.State;
        ui.WinMax = WindowState == WindowState.Maximized;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (b.Width > 100 && b.Height > 100) { ui.WinX = b.Left; ui.WinY = b.Top; ui.WinW = b.Width; ui.WinH = b.Height; }
    }
}
