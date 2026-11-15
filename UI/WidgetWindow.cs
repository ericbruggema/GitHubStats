using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GitHubStats;

/// <summary>
/// The optional desktop widget. It hosts one of ten styles (see <see cref="WidgetStyles"/>), is pinned to the desktop
/// (survives "Show desktop"), can be dragged anywhere, and double-click opens the main window.
/// </summary>
internal sealed class WidgetWindow : Window
{
    private readonly ContentControl _host = new();
    private readonly MenuItem _topmostItem, _styleMenu, _sizeMenu, _tilesMenu, _opacityMenu, _autoHideItem;
    private readonly DispatcherTimer _dockTimer;
    private Dashboard? _db;
    private string _style = "glass";
    private bool _collapsed;

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
        Content = _host;
        _style = WidgetStyles.Normalize(Host.State.WidgetStyle);

        // ---- context menu
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open GitHub Stats" };
        open.Click += (s, e) => Host.ShowMain();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());

        _styleMenu = new MenuItem { Header = "Style" };
        foreach (var (key, label) in WidgetStyles.All)
        {
            string k = key;
            var it = new MenuItem { Header = label, Tag = k };
            it.Click += (s, e) => SetStyle(k);
            _styleMenu.Items.Add(it);
        }
        menu.Items.Add(_styleMenu);

        _sizeMenu = new MenuItem { Header = "Size" };
        foreach (var (scale, label) in new[] { (0.85, "Small"), (1.0, "Normal"), (1.2, "Large") })
        {
            double sc = scale;
            var it = new MenuItem { Header = label, Tag = sc };
            it.Click += (s, e) => { Host.State.WidgetScale = sc; Host.State.Save(); Rebuild(); KeepOnScreen(); };
            _sizeMenu.Items.Add(it);
        }
        menu.Items.Add(_sizeMenu);

        _tilesMenu = new MenuItem { Header = "Tiles" };
        foreach (var (key, label) in WidgetStyles.Metrics)
        {
            string k = key;
            var it = new MenuItem { Header = label, Tag = k, StaysOpenOnClick = true };
            it.Click += (s, e) =>
            {
                var list = Host.State.WidgetTiles;
                if (list.Contains(k)) { if (list.Count > 1) list.Remove(k); }
                else if (list.Count < 6) list.Add(k);
                // keep the order of the metrics list
                Host.State.WidgetTiles = WidgetStyles.Metrics.Select(m => m.Key).Where(list.Contains).ToList();
                Host.State.Save();
                SyncMenu();
                Rebuild();
            };
            _tilesMenu.Items.Add(it);
        }
        menu.Items.Add(_tilesMenu);

        _autoHideItem = new MenuItem { Header = "Slide away when not in use" };
        _autoHideItem.Click += (s, e) => { Host.State.WidgetAutoHide = !Host.State.WidgetAutoHide; Host.State.Save(); SyncMenu(); Expand(); };
        menu.Items.Add(_autoHideItem);

        menu.Items.Add(new Separator());
        _topmostItem = new MenuItem { Header = "Keep on top" };
        _topmostItem.Click += (s, e) => { Topmost = !Topmost; Host.State.WidgetTopmost = Topmost; Host.State.Save(); SyncMenu(); };
        menu.Items.Add(_topmostItem);
        _opacityMenu = new MenuItem { Header = "Opacity" };
        foreach (int pct in new[] { 100, 90, 75, 50 })
        {
            int p = pct;
            var it = new MenuItem { Header = p + "%", Tag = p };
            it.Click += (s, e) => { Opacity = p / 100.0; Host.State.WidgetOpacity = Opacity; Host.State.Save(); };
            _opacityMenu.Items.Add(it);
        }
        menu.Items.Add(_opacityMenu);
        menu.Items.Add(new Separator());
        var hide = new MenuItem { Header = "Hide widget" };
        hide.Click += (s, e) => Host.SetWidget(false);
        menu.Items.Add(hide);
        menu.Opened += (s, e) => { SyncMenu(); Expand(); };
        ContextMenu = menu;

        // ---- drag, double-click
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2) { Host.ShowMain(); return; }
            try { DragMove(); } catch { /* the button was released already */ }
            if (_style == "dock") SnapToEdge();
            Host.State.WidgetX = Left;
            Host.State.WidgetY = Top;
            Host.State.Save();
        };

        // ---- edge dock: slide away when the mouse leaves, back when it returns
        _dockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        long leftAt = 0;
        _dockTimer.Tick += (s, e) =>
        {
            if (_style != "dock" || !Host.State.WidgetAutoHide || (ContextMenu?.IsOpen ?? false)) return;
            if (IsMouseOver) { leftAt = 0; if (_collapsed) Expand(); return; }
            if (leftAt == 0) leftAt = Environment.TickCount64;
            else if (!_collapsed && Environment.TickCount64 - leftAt > 1500) Collapse();
        };
        _dockTimer.Start();
        Closed += (s, e) => _dockTimer.Stop();

        SourceInitialized += (s, e) => { Place(); Native.PinToDesktop(this); };
        Theme.Changed += Rebuild;
        Closed += (s, e) => Theme.Changed -= Rebuild;
    }

    private void SetStyle(string key)
    {
        _style = WidgetStyles.Normalize(key);
        Host.State.WidgetStyle = _style;
        Host.State.Save();
        _collapsed = false;
        Rebuild();
        if (_style == "dock") Dispatcher.BeginInvoke(new Action(SnapToEdge), DispatcherPriority.Loaded);
        else KeepOnScreen();
    }

    private void SyncMenu()
    {
        foreach (MenuItem it in _styleMenu.Items) it.IsChecked = (string)it.Tag == _style;
        foreach (MenuItem it in _sizeMenu.Items) it.IsChecked = Math.Abs((double)it.Tag - Host.State.WidgetScale) < 0.01;
        foreach (MenuItem it in _tilesMenu.Items) it.IsChecked = Host.State.WidgetTiles.Contains((string)it.Tag);
        foreach (MenuItem it in _opacityMenu.Items) it.IsChecked = Math.Abs(Opacity * 100 - (int)it.Tag) < 1;
        _tilesMenu.Visibility = _style == "tiles" ? Visibility.Visible : Visibility.Collapsed;
        _autoHideItem.Visibility = _style == "dock" ? Visibility.Visible : Visibility.Collapsed;
        _autoHideItem.IsChecked = Host.State.WidgetAutoHide;
        _topmostItem.IsChecked = Topmost;
    }

    private void Rebuild()
    {
        if (_db == null) return;
        var content = WidgetStyles.Build(_style, _db, Rebuild);
        double sc = Math.Max(0.7, Math.Min(1.5, Host.State.WidgetScale));
        content.LayoutTransform = Math.Abs(sc - 1) < 0.01 ? Transform.Identity : new ScaleTransform(sc, sc);
        _host.Content = content;
    }

    // ------------------------------------------------------------------ placement

    /// <summary>The work area of the screen the widget is on (falls back to the primary screen).</summary>
    private Rect WorkArea()
    {
        try
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var scr = System.Windows.Forms.Screen.FromHandle(h).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            return new Rect(scr.Left / dpi.DpiScaleX, scr.Top / dpi.DpiScaleY, scr.Width / dpi.DpiScaleX, scr.Height / dpi.DpiScaleY);
        }
        catch { return SystemParameters.WorkArea; }
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
            Left = wa.Right - 300;
            Top = wa.Bottom - 220;
        }
    }

    private void KeepOnScreen()
    {
        BeginAnimation(LeftProperty, null);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var wa = WorkArea();
            if (Left + ActualWidth > wa.Right) Left = Math.Max(wa.Left, wa.Right - ActualWidth);
            if (Top + ActualHeight > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - ActualHeight);
        }), DispatcherPriority.Loaded);
    }

    private double DockLeft(bool collapsed) => WorkArea().Right - (collapsed ? 14 : ActualWidth);

    private void SnapToEdge()
    {
        BeginAnimation(LeftProperty, null);
        _collapsed = false;
        Left = DockLeft(false);
        var wa = WorkArea();
        if (Top + ActualHeight > wa.Bottom) Top = wa.Bottom - ActualHeight;
        if (Top < wa.Top) Top = wa.Top;
    }

    private void Collapse()
    {
        _collapsed = true;
        BeginAnimation(LeftProperty, new DoubleAnimation(Left, DockLeft(true), TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase() });
    }

    private void Expand()
    {
        if (_style != "dock" || !_collapsed) return;
        _collapsed = false;
        BeginAnimation(LeftProperty, new DoubleAnimation(Left, DockLeft(false), TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase() });
    }

    // ------------------------------------------------------------------ host API

    public void Apply()
    {
        Topmost = Host.State.WidgetTopmost;
        Opacity = Math.Max(0.4, Math.Min(1.0, Host.State.WidgetOpacity));
        string st = WidgetStyles.Normalize(Host.State.WidgetStyle);
        if (st != _style) SetStyle(st); else Rebuild();
    }

    public void Update(Dashboard db)
    {
        bool first = _db == null;
        _db = db;
        Rebuild();
        if (_style == "dock" && !_collapsed && first) Dispatcher.BeginInvoke(new Action(SnapToEdge), DispatcherPriority.Loaded);
    }
}
