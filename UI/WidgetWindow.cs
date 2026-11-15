using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>The optional small desktop widget: totals, change since the last refresh and a trend line. Drag it anywhere; double-click opens the main window.</summary>
internal sealed class WidgetWindow : Window
{
    private readonly TextBlock _dl = Ui.Text("-", 26, "Text", true);
    private readonly TextBlock _dlDelta = Ui.Text("", 11, "Muted");
    private readonly TextBlock _stars = Ui.Text("-", 26, "Text", true);
    private readonly TextBlock _starsDelta = Ui.Text("", 11, "Muted");
    private readonly TextBlock _foot = Ui.Text("", 10, "Muted");
    private readonly Sparkline _spark = new();
    private readonly MenuItem _topmostItem;

    public WidgetWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;
        Topmost = Host.State.WidgetTopmost;
        Opacity = Math.Max(0.4, Math.Min(1.0, Host.State.WidgetOpacity));
        Title = "GitHub Stats widget";
        FontFamily = new FontFamily("Segoe UI");

        var cols = new Grid { Width = 232 };
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel();
        left.Children.Add(Ui.Text("DOWNLOADS", 10, "Muted"));
        left.Children.Add(_dl);
        left.Children.Add(_dlDelta);
        var right = new StackPanel();
        right.Children.Add(Ui.Text("STARS", 10, "Muted"));
        right.Children.Add(_stars);
        right.Children.Add(_starsDelta);
        Grid.SetColumn(right, 1);
        cols.Children.Add(left);
        cols.Children.Add(right);

        var stack = new StackPanel { Width = 232 }; // fixed: the trend line would otherwise stretch the widget across the screen
        stack.Children.Add(cols);
        _spark.Margin = new Thickness(0, 8, 0, 4);
        stack.Children.Add(_spark);
        stack.Children.Add(_foot);

        var box = new Border { Child = stack, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(14, 12, 14, 10), Margin = new Thickness(8) };
        box.Res(Border.BackgroundProperty, "Card");
        box.Res(Border.BorderBrushProperty, "Border");
        box.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.35 };
        Content = box;

        // context menu
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open GitHub Stats" };
        open.Click += (s, e) => Host.ShowMain();
        menu.Items.Add(open);
        _topmostItem = new MenuItem { Header = "Keep on top", IsCheckable = true, IsChecked = Topmost };
        _topmostItem.Click += (s, e) => { Topmost = _topmostItem.IsChecked; Host.State.WidgetTopmost = Topmost; Host.State.Save(); };
        menu.Items.Add(_topmostItem);
        var op = new MenuItem { Header = "Opacity" };
        foreach (int pct in new[] { 100, 90, 75, 50 })
        {
            int p = pct;
            var it = new MenuItem { Header = p + "%" };
            it.Click += (s, e) => { Opacity = p / 100.0; Host.State.WidgetOpacity = Opacity; Host.State.Save(); };
            op.Items.Add(it);
        }
        menu.Items.Add(op);
        menu.Items.Add(new Separator());
        var hide = new MenuItem { Header = "Hide widget" };
        hide.Click += (s, e) => Host.SetWidget(false);
        menu.Items.Add(hide);
        menu.Opened += (s, e) =>
        {
            foreach (MenuItem it in op.Items) it.IsChecked = Math.Abs(Opacity * 100 - int.Parse(((string)it.Header).TrimEnd('%'))) < 1;
        };
        ContextMenu = menu;

        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2) { Host.ShowMain(); return; }
            try { DragMove(); } catch { /* the button was released already */ }
            Host.State.WidgetX = Left;
            Host.State.WidgetY = Top;
            Host.State.Save();
        };

        SourceInitialized += (s, e) => { Place(); Native.PinToDesktop(this); };
    }

    private void Place()
    {
        var ui = Host.State;
        var wa = SystemParameters.WorkArea;
        if (ui.WidgetX is double x && ui.WidgetY is double y
            && x > SystemParameters.VirtualScreenLeft - 20 && y > SystemParameters.VirtualScreenTop - 20
            && x < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
            && y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            Left = x;
            Top = y;
        }
        else
        {
            Left = wa.Right - 260;
            Top = wa.Bottom - 190;
        }
    }

    public void Apply()
    {
        Topmost = Host.State.WidgetTopmost;
        _topmostItem.IsChecked = Topmost;
        Opacity = Math.Max(0.4, Math.Min(1.0, Host.State.WidgetOpacity));
    }

    private static void SetDelta(TextBlock t, long? d, string what)
    {
        if (d == null) { t.Text = ""; return; }
        t.Text = Fmt.DeltaText(d) + " " + what;
        t.Res(TextBlock.ForegroundProperty, d > 0 ? "Up" : d < 0 ? "Down" : "Muted");
    }

    public void Update(Dashboard db)
    {
        _dl.Text = db.HasData ? Fmt.Compact(db.TotalDownloads) : "-";
        _stars.Text = db.HasData ? Fmt.Compact(db.TotalStars) : "-";
        _dl.ToolTip = Fmt.N(db.TotalDownloads) + " downloads";
        _stars.ToolTip = Fmt.N(db.TotalStars) + " stars";
        SetDelta(_dlDelta, db.DownloadsDelta, "last refresh");
        SetDelta(_starsDelta, db.StarsDelta, "last refresh");
        _spark.SetValues(db.DownloadHistory);
        _foot.Text = db.Running ? "Refreshing..." : db.LastSuccess > 0 ? db.User + "  |  updated " + Fmt.Hm(db.LastSuccess) : db.User;
    }
}
