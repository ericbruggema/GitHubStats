using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>The popup that appears when you hover the tray icon: all the numbers at a glance.</summary>
internal sealed class HoverCard : Window
{
    private readonly StackPanel _body = new() { Width = 330 };

    public HoverCard()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true;
        Title = "GitHub Stats";
        FontFamily = new FontFamily("Segoe UI");

        var box = new Border { Child = _body, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(10) };
        box.Res(Border.BackgroundProperty, "Card");
        box.Res(Border.BorderBrushProperty, "Border");
        box.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.4 };
        Content = box;
        SourceInitialized += (s, e) => Native.Overlay(this, true);
    }

    private static string Span(long sec) => sec < 3600 ? Math.Max(1, sec / 60) + "m" : sec < 48 * 3600 ? sec / 3600 + "h" : sec / 86400 + "d";

    private static StackPanel Block(string label, long total, WinDelta[] w)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        sp.Children.Add(Ui.Text(label, 10, "Muted"));
        sp.Children.Add(Ui.Text(Fmt.N(total), 26, "Text", true));
        string[] names = { "24h", "7d", "30d" };
        for (int i = 0; i < 3; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
            var n = Ui.Text(names[i], 11, "Muted");
            n.Width = 34;
            row.Children.Add(n);
            var d = w[i];
            if (d.Value == null) row.Children.Add(Ui.Text("-", 12, "Muted"));
            else
            {
                long v = d.Value.Value;
                row.Children.Add(Ui.Text(Fmt.DeltaText(v), 12, v > 0 ? "Up" : v < 0 ? "Down" : "Muted", true));
                if (d.Partial)
                {
                    var suf = Ui.Text("  in " + Span(d.Span), 10, "Muted");
                    row.Children.Add(suf);
                }
            }
            sp.Children.Add(row);
        }
        return sp;
    }

    private void Info(string label, string value)
    {
        if (value.Length == 0) return;
        var g = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(Ui.Text(label, 11, "Muted"));
        var v = Ui.Text(value, 12, "Text", false, true);
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        _body.Children.Add(g);
    }

    private void Rebuild(Dashboard db)
    {
        _body.Children.Clear();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        head.Children.Add(Ui.Text("GitHub Stats", 12, "Text", true));
        head.Children.Add(Ui.Text("   " + db.User + (db.Running ? "   refreshing..." : ""), 12, "Muted"));
        _body.Children.Add(head);

        if (!db.HasData)
        {
            _body.Children.Add(Ui.Text(db.Running ? "Fetching your repositories from GitHub..." : "No data yet.", 12, "Muted", false, true));
            return;
        }

        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        two.Children.Add(Block("DOWNLOADS", db.TotalDownloads, db.DownloadsWin));
        var st = Block("STARS", db.TotalStars, db.StarsWin);
        Grid.SetColumn(st, 1);
        two.Children.Add(st);
        _body.Children.Add(two);

        if (db.DownloadHistory.Length >= 2)
        {
            var sp = new Sparkline { Margin = new Thickness(0, 8, 0, 4) };
            sp.SetValues(db.DownloadHistory);
            _body.Children.Add(sp);
        }

        var line = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 6) };
        line.Res(Border.BackgroundProperty, "Border");
        _body.Children.Add(line);

        Info("Best gainer", db.TopGainer);
        Info("Latest star", db.LatestStar);
        Info("Top download", db.TopFile);
        if (db.DownloadsPerDay != null) Info("Per day", Fmt.PerDay(db.DownloadsPerDay.Value) + " downloads (7-day average)");
        Info("Repositories", Fmt.N(db.Totals.Repos) + "  |  " + Fmt.N(db.Totals.Releases) + " releases  |  " + Fmt.N(db.Totals.Forks) + " forks  |  " + Fmt.N(db.Totals.OpenIssues) + " open issues");
        var tr = db.Cards.FirstOrDefault(c => c.Label.StartsWith("Views"));
        var cl = db.Cards.FirstOrDefault(c => c.Label.StartsWith("Clones"));
        if (tr != null && cl != null) Info("Traffic 14d", tr.Value + " views (" + tr.Detail + ")  |  " + cl.Value + " clones");
        Info("Updated", db.Running ? "now" : db.LastRefresh + "  |  next after " + db.NextRefresh);
        if (db.RateText.Length > 0) Info("GitHub API", db.RateText.Replace("API requests left: ", "") + " requests left");
    }

    /// <summary>Shows the card next to the tray icon. cursorX/cursorY are the mouse position in screen pixels.</summary>
    public void ShowAt(Dashboard db, int cursorX, int cursorY)
    {
        if (IsVisible) return;
        Rebuild(db);
        Opacity = 0;
        Left = -10000;
        Top = -10000;
        Show();
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        double cx = cursorX / dpi.DpiScaleX, cy = cursorY / dpi.DpiScaleY;
        var wa = SystemParameters.WorkArea;
        double w = ActualWidth, h = ActualHeight;
        double x = Math.Max(wa.Left + 4, Math.Min(cx - w / 2, wa.Right - w - 4));
        double y = cy > (wa.Top + wa.Bottom) / 2 ? wa.Bottom - h - 4 : wa.Top + 4;
        Left = x;
        Top = y;
        Opacity = 1;
    }
}
