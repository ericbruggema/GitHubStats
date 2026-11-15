using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Threading;

namespace GitHubStats;

// ============================================================================ Settings
//
// Windows 11 style: settings grouped in cards, one row per setting (title + explanation left,
// control right), toggle switches, segmented choices. Everything saves automatically.

internal sealed class SettingsView : View
{
    private readonly StackPanel _root;
    private readonly TextBox _user;
    private readonly PasswordBox _token;
    private readonly TextBox _tokenPlain;
    private readonly CheckBox _forks, _background, _notify, _tray, _closeTray, _startMin, _widget, _topmost, _autostart, _hover, _celebrate;
    private readonly Func<string> _stars, _traffic, _theme;
    private readonly Slider _opacity;
    private readonly TextBlock _opacityText;
    private readonly TextBox _publish;
    private readonly TextBlock _status, _rate;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly List<FrameworkElement> _trayRows = new(), _widgetRows = new();
    private bool _loading = true;
    private Func<string> _wStyle = () => "glass", _wSize = () => "1";
    private string _wStyleSaved = "", _wSizeSaved = "";
    private readonly StackPanel _tokenResult = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _testBtn;
    private readonly WrapPanel _repoChips = new() { Margin = new Thickness(16, 0, 16, 12) };
    private readonly HashSet<string> _hidden;
    private string _repoKey = "";
    private readonly List<long> _milestones;
    private readonly WrapPanel _msChips = new();
    private readonly TextBox _msInput;
    private readonly Func<string> _accent;
    private readonly CheckBox _updates;
    private readonly StackPanel _updateResult = new() { Margin = new Thickness(0, 4, 0, 0) };

    public SettingsView()
    {
        Children.Add(Scroll(out _root));
        _root.MaxWidth = 860;
        _root.HorizontalAlignment = HorizontalAlignment.Left;
        var cfg = Host.App.Cfg;
        var ui = Host.State;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); Save(); };
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _statusTimer.Tick += (s, e) => { _statusTimer.Stop(); if (_status.Tag as string != "error") _status.Text = ""; };

        // ---- header
        var head = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };
        _status = Ui.Text("", 12, "Up");
        _status.VerticalAlignment = VerticalAlignment.Bottom;
        DockPanel.SetDock(_status, Dock.Right);
        head.Children.Add(_status);
        head.Children.Add(Title("Settings"));
        _root.Children.Add(head);
        _root.Children.Add(Ui.Text("Changes are saved automatically.", 12, "Muted"));

        // ---- GitHub account
        _user = S.Input(cfg.User, 240);
        _user.TextChanged += (s, e) => Changed();

        _token = new PasswordBox { Password = cfg.TokenFromEnv ? "" : cfg.Token };
        S.StyleInput(_token, 240);
        _tokenPlain = S.Input(_token.Password, 240);
        _tokenPlain.Visibility = Visibility.Collapsed;
        _token.PasswordChanged += (s, e) => { if (_tokenPlain.Visibility != Visibility.Visible) _tokenPlain.Text = _token.Password; Changed(); };
        _tokenPlain.TextChanged += (s, e) => { if (_tokenPlain.Visibility == Visibility.Visible) _token.Password = _tokenPlain.Text; };
        var eye = S.IconBtn("", "Show / hide the token", () =>
        {
            bool show = _tokenPlain.Visibility != Visibility.Visible;
            _tokenPlain.Text = _token.Password;
            _tokenPlain.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _token.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        });
        var tokenBox = new StackPanel { Orientation = Orientation.Horizontal };
        tokenBox.Children.Add(_token);
        tokenBox.Children.Add(_tokenPlain);
        tokenBox.Children.Add(eye);
        _testBtn = S.SmallBtn("Test", TestToken, new Thickness(6, 0, 0, 0));
        _testBtn.ToolTip = "Check the token and see which features it unlocks";
        tokenBox.Children.Add(_testBtn);

        var tokenDesc = new StackPanel();
        tokenDesc.Children.Add(S.Desc("Optional, but without one GitHub allows only 60 requests per hour. Stored in config.json next to the program."));
        var links = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        links.Children.Add(S.Link("Create a token on GitHub", () => Ui.OpenUrl("https://github.com/settings/tokens")));
        var which = S.Link("Which permissions?", () => { });
        which.ToolTip = new ToolTip
        {
            MaxWidth = 380,
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "Downloads, stars and star dates: a classic token with NO scopes ticked is enough.\n\n" +
                       "Traffic (views, clones, referrers) needs push access: a fine-grained token with \"Administration: read-only\" on your repositories, " +
                       "or a classic token with the \"public_repo\" scope.",
            },
        };
        ToolTipService.SetInitialShowDelay(which, 100);
        ToolTipService.SetShowDuration(which, 60000);
        links.Children.Add(which);
        tokenDesc.Children.Add(links);
        tokenDesc.Children.Add(_tokenResult);
        if (cfg.TokenFromEnv) tokenDesc.Children.Add(S.Desc("Currently using the GITHUB_TOKEN environment variable."));

        _rate = Ui.Text("", 11, "Muted");
        _rate.Margin = new Thickness(0, 6, 0, 0);

        Section("GitHub account",
            S.Row("GitHub user", S.Desc("Whose public repositories are shown."), _user),
            S.Row("Personal access token", tokenDesc, tokenBox),
            S.Row("Include forked repositories", null, _forks = S.Toggle(cfg.IncludeForks, Changed)));
        _root.Children.Add(_rate);

        // ---- data
        var st = S.Segmented(new[] { "auto", "on", "off" }, new[] { "Auto", "On", "Off" }, cfg.StarsDetail, Changed);
        _stars = st.Get;
        var tr = S.Segmented(new[] { "auto", "on", "off" }, new[] { "Auto", "On", "Off" }, cfg.Traffic, Changed);
        _traffic = tr.Get;
        Section("Data",
            S.Row("Star dates and stargazers", S.Desc("Real dates of every star and who gave it. Auto = only with a token."), st.Panel),
            S.Row("Traffic history", S.Desc("Views, clones and referrers (GitHub keeps only 14 days, this program keeps them all). Auto = only with a token."), tr.Panel),
            S.Row("Refresh in the background", S.Desc("Refresh by itself while the program runs, at most 4 times a day. Off = only when the window is opened."), _background = S.Toggle(cfg.BackgroundRefresh, Changed)));

        // ---- repositories and milestones
        _hidden = new HashSet<string>(cfg.HiddenRepos, StringComparer.OrdinalIgnoreCase);
        var hiddenRow = new StackPanel();
        hiddenRow.Children.Add(S.Row("Shown repositories", S.Desc("Click a repository to hide or show it. Hidden ones are left out of every total, chart, widget and export. Their data is kept, so showing one again brings its history back."), new Border()));
        hiddenRow.Children.Add(_repoChips);

        _milestones = new List<long>(cfg.Milestones);
        _msInput = S.Input("", 110);
        _msInput.ToolTip = "A number, e.g. 750, 1500 or 2k";
        _msInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddMilestone(); };
        var msAdd = new StackPanel { Orientation = Orientation.Horizontal };
        msAdd.Children.Add(_msInput);
        msAdd.Children.Add(S.SmallBtn("Add", AddMilestone, new Thickness(6, 0, 0, 0)));
        var msRow = new StackPanel();
        msRow.Children.Add(S.Row("Download milestones", S.Desc("A notification when the total downloads pass one of these numbers."), msAdd));
        _msChips.Margin = new Thickness(16, 0, 16, 12);
        msRow.Children.Add(_msChips);
        FillMilestones();
        Section("Repositories", hiddenRow, msRow);

        // ---- appearance
        var th = S.Segmented(new[] { "auto", "light", "dark" }, new[] { "System", "Light", "Dark" }, ui.Theme, Changed);
        _theme = th.Get;
        var ac = S.Swatches(Theme.Accents.Select(a => (a.Name, a.Label, a.Light, a.Dark)).ToArray(), ui.Accent, Changed);
        _accent = ac.Get;
        var celebrateBox = new StackPanel { Orientation = Orientation.Horizontal };
        celebrateBox.Children.Add(S.SmallBtn("Test", () => Host.Celebrate(3, true)));
        celebrateBox.Children.Add(_celebrate = S.Toggle(ui.Celebrate, Changed));
        Section("Appearance",
            S.Row("Theme", S.Desc("Follow Windows, or always light or dark."), th.Panel),
            S.Row("Accent colour", S.Desc("Used for links, switches, buttons and the main chart line."), ac.Panel),
            S.Row("Celebrate new stars", S.Desc("Fireworks and +N on the screen when someone stars a repository."), celebrateBox));

        // ---- tray & startup
        _autostart = S.Toggle(Host.AutostartOn(), Changed);
        _tray = S.Toggle(ui.Tray, Changed);
        Section("Startup and tray",
            S.Row("Start with Windows", S.Desc("Starts quietly when you sign in to Windows."), _autostart),
            S.Row("Tray icon", S.Desc("Show an icon in the notification area."), _tray),
            Dep(_trayRows, S.Row("Keep running when closed", S.Desc("Closing the window hides it to the tray."), _closeTray = S.Toggle(ui.CloseToTray, Changed), 1)),
            Dep(_trayRows, S.Row("Start hidden", S.Desc("Open in the tray without showing the window."), _startMin = S.Toggle(ui.StartMinimized, Changed), 1)),
            Dep(_trayRows, S.Row("Hover card", S.Desc("Show a card with all numbers when hovering the tray icon."), _hover = S.Toggle(ui.HoverCard, Changed), 1)),
            Dep(_trayRows, S.Row("Notifications", S.Desc("New stars and download milestones."), _notify = S.Toggle(cfg.Notifications, Changed), 1)));

        // ---- widget
        _widget = S.Toggle(ui.Widget, Changed);
        var op = new StackPanel { Orientation = Orientation.Horizontal };
        _opacityText = Ui.Text("", 12, "Muted");
        _opacityText.Width = 40;
        _opacityText.TextAlignment = TextAlignment.Right;
        _opacityText.Margin = new Thickness(0, 0, 8, 0);
        _opacity = new Slider { Minimum = 0.4, Maximum = 1.0, Value = Math.Max(0.4, Math.Min(1.0, ui.WidgetOpacity)), Width = 170, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true, SmallChange = 0.05, LargeChange = 0.1 };
        _opacity.ValueChanged += (s, e) => { _opacityText.Text = Math.Round(_opacity.Value * 100) + "%"; Changed(); };
        _opacityText.Text = Math.Round(_opacity.Value * 100) + "%";
        op.Children.Add(_opacityText);
        op.Children.Add(_opacity);
        var wst = S.Segmented(WidgetStyles.All.Select(a => a.Key).ToArray(), WidgetStyles.All.Select(a => a.Label).ToArray(), WidgetStyles.Normalize(ui.WidgetStyle), Changed, wrap: true);
        _wStyle = wst.Get;
        var wsz = S.Segmented(new[] { "0.85", "1", "1.2" }, new[] { "Small", "Normal", "Large" },
            ui.WidgetScale < 0.95 ? "0.85" : ui.WidgetScale > 1.1 ? "1.2" : "1", Changed);
        _wSize = wsz.Get;
        _wStyleSaved = _wStyle();
        _wSizeSaved = _wSize();
        Section("Desktop widget",
            S.Row("Show the widget", S.Desc("A small window with downloads, stars and the trend."), _widget),
            Dep(_widgetRows, S.Row("Always on top", S.Desc("Keep the widget above other windows."), _topmost = S.Toggle(ui.WidgetTopmost, Changed), 1)),
            Dep(_widgetRows, S.Row("Style", S.Desc("Ten looks. Also on the widget: right-click > Style."), wst.Panel, 1)),
            Dep(_widgetRows, S.Row("Size", null, wsz.Panel, 1)),
            Dep(_widgetRows, S.Row("Opacity", null, op, 1)));

        // ---- publish folder
        _publish = S.Input(cfg.PublishDir, 220);
        _publish.TextChanged += (s, e) => Changed();
        var pub = new StackPanel { Orientation = Orientation.Horizontal };
        pub.Children.Add(_publish);
        pub.Children.Add(S.SmallBtn("Browse...", Browse, new Thickness(6, 0, 0, 0)));
        pub.Children.Add(S.IconBtn("", "Open the folder", () =>
        {
            var p = PublishPath();
            if (p != null) { Directory.CreateDirectory(p); Ui.OpenPath(p); }
        }));
        Section("Publish folder",
            S.Row("Folder", S.Desc("Receives stats.md, stats.html, CSV files and badges after every refresh. Empty = off. A relative path is relative to the program."), pub));

        // ---- backup
        var bk = new StackPanel { Orientation = Orientation.Horizontal };
        bk.Children.Add(S.SmallBtn("Make backup...", MakeBackup, new Thickness(0)));
        bk.Children.Add(S.SmallBtn("Restore...", RestoreBackup, new Thickness(6, 0, 0, 0)));
        Section("Backup",
            S.Row("Backup and restore", S.Desc("One zip file with your settings (including the token) and all collected history. Restoring restarts the program; your current data is saved first."), bk));

        // ---- about / files
        var files = new StackPanel { Orientation = Orientation.Horizontal };
        files.Children.Add(S.SmallBtn("Program folder", () => Ui.OpenPath(Host.App.Home), new Thickness(0)));
        files.Children.Add(S.SmallBtn("Data folder", () => Ui.OpenPath(Host.App.DataDir), new Thickness(6, 0, 0, 0)));
        var ver = typeof(SettingsView).Assembly.GetName().Version;
        _updates = S.Toggle(cfg.CheckUpdates, Changed);
        var upBox = new StackPanel { Orientation = Orientation.Horizontal };
        upBox.Children.Add(S.SmallBtn("Check now", CheckUpdate, new Thickness(0, 0, 14, 0)));
        upBox.Children.Add(_updates);
        var upDesc = new StackPanel();
        upDesc.Children.Add(S.Desc("Look for a newer version on github.com/" + cfg.UpdateRepo + " when the program starts (once a day)."));
        upDesc.Children.Add(_updateResult);
        Section("Program",
            S.Row("GitHub Stats " + (ver == null ? "" : ver.ToString(3)), S.Desc("Portable: settings and data live next to the program in " + Host.App.Home), files),
            S.Row("Updates", upDesc, upBox));
        Host.UpdateFound += () => Dispatcher.BeginInvoke(new Action(() => ShowUpdate(Host.Update!)));
        if (Host.Update != null) ShowUpdate(Host.Update);

        UpdateDependents();
        _loading = false;
    }

    private void Section(string title, params FrameworkElement[] rows)
    {
        var t = Ui.Text(title, 14, "Text", true);
        t.Margin = new Thickness(2, 22, 0, 8);
        _root.Children.Add(t);
        var sp = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0)
            {
                var line = new Border { Height = 1, Margin = new Thickness(16, 0, 0, 0) };
                line.Res(Border.BackgroundProperty, "Border");
                sp.Children.Add(line);
            }
            sp.Children.Add(rows[i]);
        }
        _root.Children.Add(Ui.Box(sp, 8, new Thickness(0)));
    }

    private static FrameworkElement Dep(List<FrameworkElement> list, FrameworkElement row)
    {
        list.Add(row);
        return row;
    }

    private void UpdateDependents()
    {
        foreach (var r in _trayRows) r.IsEnabled = _tray.IsChecked == true;
        foreach (var r in _widgetRows) r.IsEnabled = _widget.IsChecked == true;
    }

    private void Changed()
    {
        if (_loading) return;
        UpdateDependents();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private string? PublishPath()
    {
        string p = _publish.Text.Trim();
        if (p.Length == 0) return null;
        return Path.IsPathRooted(p) ? p : Path.Combine(Host.App.Home, p);
    }

    private void Browse()
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Publish folder" };
        var cur = PublishPath();
        if (cur != null && Directory.Exists(cur)) d.InitialDirectory = cur;
        if (d.ShowDialog(Window.GetWindow(this)) == true) _publish.Text = d.FolderName;
    }

    private void Save()
    {
        var cfg = Host.App.Cfg;
        var ui = Host.State;
        string oldUser = cfg.User, oldToken = cfg.Token;

        string user = _user.Text.Trim();
        if (user.Length > 0) cfg.User = user;
        string tok = _token.Password.Trim();
        if (tok.Length > 0 || !cfg.TokenFromEnv)
        {
            cfg.Token = tok;
            cfg.TokenFromEnv = false;
            if (tok.Length == 0)
            {
                string env = (Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "").Trim();
                if (env.Length > 0) { cfg.Token = env; cfg.TokenFromEnv = true; }
            }
        }
        cfg.IncludeForks = _forks.IsChecked == true;
        cfg.StarsDetail = _stars();
        cfg.Traffic = _traffic();
        cfg.BackgroundRefresh = _background.IsChecked == true;
        cfg.Notifications = _notify.IsChecked == true;
        cfg.PublishDir = _publish.Text.Trim();
        cfg.HiddenRepos = _hidden.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList();
        cfg.Milestones = _milestones.Distinct().OrderBy(m => m).ToList();
        cfg.CheckUpdates = _updates.IsChecked == true;

        ui.Tray = _tray.IsChecked == true;
        ui.CloseToTray = _closeTray.IsChecked == true;
        ui.StartMinimized = _startMin.IsChecked == true;
        ui.HoverCard = _hover.IsChecked == true;
        ui.Celebrate = _celebrate.IsChecked == true;
        ui.Widget = _widget.IsChecked == true;
        ui.WidgetTopmost = _topmost.IsChecked == true;
        // only when changed here: the widget's own right-click menu can change these too
        if (_wStyle() != _wStyleSaved) { ui.WidgetStyle = _wStyle(); _wStyleSaved = _wStyle(); }
        if (_wSize() != _wSizeSaved) { ui.WidgetScale = double.Parse(_wSize(), System.Globalization.CultureInfo.InvariantCulture); _wSizeSaved = _wSize(); }
        ui.WidgetOpacity = _opacity.Value;
        ui.Theme = _theme();
        ui.Accent = _accent();

        string? err = cfg.Save(Host.App.Home);
        ui.Save();
        string? autoErr = Host.SetAutostart(_autostart.IsChecked == true);
        Host.ApplyUi();
        if (cfg.User != oldUser || cfg.Token != oldToken) Host.App.Poll(); // the 4-per-day limit still applies

        string? problem = err ?? autoErr;
        _status.Tag = problem == null ? null : "error";
        _status.Res(TextBlock.ForegroundProperty, problem == null ? "Up" : "Down");
        _status.Text = problem ?? "✓ Saved";
        _statusTimer.Stop();
        if (problem == null) _statusTimer.Start();
    }

    public override void Update(Dashboard db)
    {
        // the fields keep what the user is typing; only the status line follows the data
        _rate.Text = db.RateText.Length > 0 ? db.RateText + "  |  last refresh " + db.LastRefresh : "";
        var names = Host.App.AllRepoNames;
        string key = string.Join("\n", names);
        if (key == _repoKey) return;
        _repoKey = key;
        _repoChips.Children.Clear();
        if (names.Count == 0) _repoChips.Children.Add(S.Desc("The list appears after the first refresh."));
        foreach (var n in names)
        {
            string name = n;
            _repoChips.Children.Add(S.Chip(name, !_hidden.Contains(name), shown =>
            {
                if (shown) _hidden.Remove(name); else _hidden.Add(name);
                Changed();
            }));
        }
    }

    // ---------------------------------------------------------------- token test

    private async void TestToken()
    {
        _tokenResult.Children.Clear();
        _tokenResult.Children.Add(S.Desc("Testing..."));
        _testBtn.IsEnabled = false;
        try
        {
            string token = _token.Password.Trim();
            if (token.Length == 0 && Host.App.Cfg.TokenFromEnv) token = Host.App.Cfg.Token;
            string user = _user.Text.Trim().Length > 0 ? _user.Text.Trim() : Host.App.Cfg.User;
            var data = Host.App.Data;
            string? sample = data.Repos.Values.Where(r => !r.Fork).OrderByDescending(r => r.Stars).Select(r => r.Name).FirstOrDefault();
            var lines = await Tools.TestTokenAsync(token, user, sample);
            _tokenResult.Children.Clear();
            foreach (var (ok, text) in lines) _tokenResult.Children.Add(S.Check(ok, text));
        }
        catch (Exception ex)
        {
            _tokenResult.Children.Clear();
            _tokenResult.Children.Add(S.Check(false, "The test failed: " + ex.Message));
        }
        finally { _testBtn.IsEnabled = true; }
    }

    // ---------------------------------------------------------------- milestones

    private void FillMilestones()
    {
        _msChips.Children.Clear();
        foreach (var m in _milestones.Distinct().OrderBy(x => x))
        {
            long v = m;
            _msChips.Children.Add(S.RemovableChip(Fmt.N(v), () => { _milestones.RemoveAll(x => x == v); FillMilestones(); Changed(); }));
        }
        if (_milestones.Count == 0) _msChips.Children.Add(S.Desc("No milestones: you will not get download notifications."));
    }

    private void AddMilestone()
    {
        string t = _msInput.Text.Trim().ToLowerInvariant().Replace(".", "").Replace(",", "").Replace(" ", "");
        long mult = 1;
        if (t.EndsWith("k")) { mult = 1000; t = t[..^1]; }
        else if (t.EndsWith("m")) { mult = 1000000; t = t[..^1]; }
        if (!long.TryParse(t, out long v) || v <= 0) { _msInput.ToolTip = "Type a positive number, e.g. 750 or 2k"; return; }
        v *= mult;
        if (!_milestones.Contains(v)) _milestones.Add(v);
        _msInput.Text = "";
        FillMilestones();
        Changed();
    }

    // ---------------------------------------------------------------- backup

    private void MakeBackup()
    {
        Host.State.Save();
        var d = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a backup",
            FileName = "GitHubStats-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip",
            Filter = "Zip file (*.zip)|*.zip",
            DefaultExt = ".zip",
        };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        string? err = Backup.Create(d.FileName, Host.App.Home, Host.App.DataDir);
        Status(err ?? "\u2713 Backup saved: " + Path.GetFileName(d.FileName), err != null);
    }

    private void RestoreBackup()
    {
        var d = new Microsoft.Win32.OpenFileDialog { Title = "Restore a backup", Filter = "Zip file (*.zip)|*.zip" };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!Backup.IsBackup(d.FileName, out string? err)) { Status(err!, true); return; }
        var answer = MessageBox.Show(Window.GetWindow(this)!,
            "Restore \"" + Path.GetFileName(d.FileName) + "\"?\n\nThis replaces your current settings and statistics with the ones in the backup. " +
            "Your current data is saved first as a backup-before-restore zip next to the program.\n\nGitHub Stats restarts to do this.",
            "Restore backup", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;
        Host.RestartWith(d.FileName);
    }

    // ---------------------------------------------------------------- updates

    private async void CheckUpdate()
    {
        _updateResult.Children.Clear();
        _updateResult.Children.Add(S.Desc("Checking..."));
        var info = await Host.CheckUpdateAsync(true);
        _updateResult.Children.Clear();
        if (info.Error != null) _updateResult.Children.Add(S.Check(false, info.Error));
        else if (info.Newer) ShowUpdate(info);
        else _updateResult.Children.Add(S.Check(true, "You have the latest version" + (info.Version.Length > 0 ? " (" + info.Version + ")" : "") + "."));
    }

    private void ShowUpdate(UpdateInfo info)
    {
        _updateResult.Children.Clear();
        var row = new WrapPanel();
        row.Children.Add(S.Check(true, "Version " + info.Version + " is available.  "));
        row.Children.Add(S.Link("Download", () => Ui.OpenUrl(info.Url)));
        _updateResult.Children.Add(row);
    }

    private void Status(string text, bool error)
    {
        _status.Tag = error ? "error" : null;
        _status.Res(TextBlock.ForegroundProperty, error ? "Down" : "Up");
        _status.Text = text;
        _statusTimer.Stop();
        if (!error) _statusTimer.Start();
    }
}

/// <summary>Controls for the settings screen: toggle switch, segmented choice, rounded inputs, rows.</summary>
internal static class S
{
    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' ";

    private static readonly Lazy<ControlTemplate> ToggleT = new(() => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='CheckBox'>" +
        "<Border Background='Transparent' Padding='2'>" +
        "<Border x:Name='track' Width='40' Height='20' CornerRadius='10' BorderThickness='1' BorderBrush='{DynamicResource Muted}' Background='Transparent'>" +
        "<Ellipse x:Name='knob' Width='12' Height='12' Fill='{DynamicResource Muted}' HorizontalAlignment='Left' Margin='4,0,0,0'/>" +
        "</Border></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='knob' Property='Width' Value='14'/><Setter TargetName='knob' Property='Height' Value='14'/><Setter TargetName='track' Property='Background' Value='{DynamicResource Accent}'/></Trigger>" +
        "<Trigger Property='IsChecked' Value='True'>" +
        "<Setter TargetName='track' Property='Background' Value='{DynamicResource Link}'/><Setter TargetName='track' Property='BorderBrush' Value='{DynamicResource Link}'/>" +
        "<Setter TargetName='knob' Property='Fill' Value='White'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='knob' Property='Margin' Value='0,0,4,0'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>"));

    private static readonly Lazy<ControlTemplate> SegT = new(() => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='RadioButton'>" +
        "<Border x:Name='bd' CornerRadius='5' Padding='14,4,14,5' Background='Transparent' BorderThickness='1' BorderBrush='Transparent'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='{DynamicResource Text}'/></Trigger>" +
        "<Trigger Property='IsChecked' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource Card}'/>" +
        "<Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource Border}'/><Setter Property='Foreground' Value='{DynamicResource Text}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>"));

    private static ControlTemplate InputTemplate(string type) => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='" + type + "'>" +
        "<Border x:Name='bd' CornerRadius='6' BorderThickness='1' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'>" +
        "<Grid><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>" +
        "<Border x:Name='line' Height='2' VerticalAlignment='Bottom' CornerRadius='0,0,6,6' Background='{DynamicResource Link}' Visibility='Collapsed'/></Grid></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource Muted}'/></Trigger>" +
        "<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='line' Property='Visibility' Value='Visible'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>");

    private static readonly Lazy<ControlTemplate> TextT = new(() => InputTemplate("TextBox"));
    private static readonly Lazy<ControlTemplate> PassT = new(() => InputTemplate("PasswordBox"));

    private static readonly Lazy<ControlTemplate> FlatBtnT = new(() => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='Button'>" +
        "<Border x:Name='bd' CornerRadius='6' BorderThickness='1' Padding='{TemplateBinding Padding}' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource Accent}'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"));

    private static readonly Lazy<ControlTemplate> ChipT = new(() => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='CheckBox'>" +
        "<Border x:Name='bd' CornerRadius='14' BorderThickness='1' Padding='10,4,12,5' Margin='0,0,6,6' Background='{DynamicResource Bg}' BorderBrush='{DynamicResource Border}'>" +
        "<StackPanel Orientation='Horizontal'><TextBlock x:Name='mark' Text='&#xE73E;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='11' Margin='0,1,6,0' VerticalAlignment='Center' Foreground='{DynamicResource Link}'/>" +
        "<ContentPresenter VerticalAlignment='Center'/></StackPanel></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource Muted}'/></Trigger>" +
        "<Trigger Property='IsChecked' Value='False'><Setter TargetName='mark' Property='Text' Value='&#xED1A;'/><Setter TargetName='mark' Property='Foreground' Value='{DynamicResource Muted}'/>" +
        "<Setter TargetName='bd' Property='Opacity' Value='0.55'/><Setter TargetName='bd' Property='Background' Value='Transparent'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>"));

    private static readonly Lazy<ControlTemplate> SwatchT = new(() => (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate " + Ns + "TargetType='RadioButton'>" +
        "<Grid Width='30' Height='30' Background='Transparent'>" +
        "<Ellipse x:Name='ring' Stroke='{DynamicResource Text}' StrokeThickness='2' Visibility='Hidden'/>" +
        "<Ellipse x:Name='dot' Width='20' Height='20' Fill='{TemplateBinding Background}'/></Grid>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='dot' Property='Width' Value='22'/><Setter TargetName='dot' Property='Height' Value='22'/></Trigger>" +
        "<Trigger Property='IsChecked' Value='True'><Setter TargetName='ring' Property='Visibility' Value='Visible'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>"));

    /// <summary>A pill that is on (shown) or off (hidden).</summary>
    public static CheckBox Chip(string text, bool on, Action<bool> changed)
    {
        var c = new CheckBox { Content = text, IsChecked = on, Template = ChipT.Value, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12.5 };
        c.Res(Control.ForegroundProperty, "Text");
        c.ToolTip = on ? "Shown - click to hide" : "Hidden - click to show";
        c.Checked += (s, e) => { c.ToolTip = "Shown - click to hide"; changed(true); };
        c.Unchecked += (s, e) => { c.ToolTip = "Hidden - click to show"; changed(false); };
        return c;
    }

    /// <summary>A pill with an x to remove it.</summary>
    public static Border RemovableChip(string text, Action remove)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(Ui.Text(text, 12.5));
        var x = new TextBlock { Text = "\uE711", FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 9, Margin = new Thickness(8, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Remove" };
        x.Res(TextBlock.ForegroundProperty, "Muted");
        x.MouseEnter += (s, e) => x.Res(TextBlock.ForegroundProperty, "Down");
        x.MouseLeave += (s, e) => x.Res(TextBlock.ForegroundProperty, "Muted");
        x.MouseLeftButtonUp += (s, e) => remove();
        sp.Children.Add(x);
        var b = new Border { Child = sp, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(12, 4, 10, 5), Margin = new Thickness(0, 0, 6, 6) };
        b.Res(Border.BackgroundProperty, "Bg");
        b.Res(Border.BorderBrushProperty, "Border");
        return b;
    }

    /// <summary>Round colour buttons; returns the chosen name.</summary>
    public static (FrameworkElement Panel, Func<string> Get) Swatches((string Name, string Label, string Light, string Dark)[] items, string selected, Action changed)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        string group = "sw" + Guid.NewGuid().ToString("N");
        var buttons = new List<(RadioButton B, string Light, string Dark)>();
        bool any = items.Any(i => i.Name == selected);
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            var r = new RadioButton { GroupName = group, Tag = it.Name, ToolTip = it.Label, Template = SwatchT.Value, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(2, 0, 2, 0), IsChecked = any ? it.Name == selected : i == 0 };
            r.Checked += (s, e) => changed();
            buttons.Add((r, it.Light, it.Dark));
            p.Children.Add(r);
        }
        void Paint()
        {
            foreach (var (b, l, d) in buttons)
                b.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Theme.Dark ? d : l));
        }
        Paint();
        Theme.Changed += Paint;
        string Get()
        {
            foreach (var (b, _, _) in buttons) if (b.IsChecked == true) return (string)b.Tag;
            return items[0].Name;
        }
        return (p, Get);
    }

    /// <summary>A result line with a green check or a red cross.</summary>
    public static FrameworkElement Check(bool ok, string text)
    {
        var g = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        var m = new TextBlock { Text = ok ? "\uE73E" : "\uE711", FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, Margin = new Thickness(0, 2, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        m.Res(TextBlock.ForegroundProperty, ok ? "Up" : "Down");
        g.Children.Add(m);
        g.Children.Add(Ui.Text(text, 11.5, "Text", false, true));
        return g;
    }

    public static CheckBox Toggle(bool value, Action changed)
    {
        var c = new CheckBox { IsChecked = value, Template = ToggleT.Value, Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Focusable = true };
        c.Checked += (s, e) => changed();
        c.Unchecked += (s, e) => changed();
        return c;
    }

    public static (FrameworkElement Panel, Func<string> Get) Segmented(string[] keys, string[] labels, string selected, Action changed, bool wrap = false)
    {
        string group = "seg" + Guid.NewGuid().ToString("N");
        Panel p = wrap ? new WrapPanel { MaxWidth = 430 } : new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<RadioButton>();
        bool any = Array.IndexOf(keys, selected) >= 0;
        for (int i = 0; i < keys.Length; i++)
        {
            var r = new RadioButton { Content = labels[i], GroupName = group, Tag = keys[i], IsChecked = any ? keys[i] == selected : i == 0, Template = SegT.Value, Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12.5 };
            r.Res(Control.ForegroundProperty, "Muted");
            r.Checked += (s, e) => changed();
            buttons.Add(r);
            p.Children.Add(r);
        }
        var bd = new Border { Child = p, CornerRadius = new CornerRadius(7), Padding = new Thickness(2), BorderThickness = new Thickness(1) };
        bd.Res(Border.BackgroundProperty, "Bg");
        bd.Res(Border.BorderBrushProperty, "Border");
        string Get()
        {
            foreach (var b in buttons) if (b.IsChecked == true) return (string)b.Tag;
            return keys[0];
        }
        return (bd, Get);
    }

    public static void StyleInput(Control c, double width)
    {
        c.Template = c is PasswordBox ? PassT.Value : TextT.Value;
        c.Width = width;
        c.Height = 30;
        c.Padding = new Thickness(8, 0, 8, 0);
        c.VerticalContentAlignment = VerticalAlignment.Center;
        c.Res(Control.BackgroundProperty, "Bg");
        c.Res(Control.ForegroundProperty, "Text");
        c.Res(Control.BorderBrushProperty, "Border");
        if (c is TextBox t) t.Res(TextBoxBase.CaretBrushProperty, "Text");
        if (c is PasswordBox pb) pb.Res(PasswordBox.CaretBrushProperty, "Text");
    }

    public static TextBox Input(string value, double width)
    {
        var t = new TextBox { Text = value };
        StyleInput(t, width);
        return t;
    }

    public static TextBlock Desc(string text)
    {
        var t = Ui.Text(text, 11.5, "Muted", false, true);
        t.Margin = new Thickness(0, 2, 0, 0);
        return t;
    }

    public static Button SmallBtn(string text, Action click, Thickness? margin = null)
    {
        var b = new Button { Content = text, Template = FlatBtnT.Value, Padding = new Thickness(12, 5, 12, 5), Height = 30, Margin = margin ?? new Thickness(0, 0, 10, 0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12.5 };
        b.Res(Control.BackgroundProperty, "Bg");
        b.Res(Control.ForegroundProperty, "Text");
        b.Res(Control.BorderBrushProperty, "Border");
        b.Click += (s, e) => click();
        return b;
    }

    public static Button IconBtn(string glyph, string tip, Action click)
    {
        var b = SmallBtn(glyph, click, new Thickness(6, 0, 0, 0));
        b.FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        b.FontSize = 13;
        b.Width = 32;
        b.Padding = new Thickness(0);
        b.ToolTip = tip;
        return b;
    }

    public static TextBlock Link(string text, Action click)
    {
        var t = Ui.Text(text, 11.5, "Link");
        t.Margin = new Thickness(0, 0, 14, 0);
        t.Cursor = System.Windows.Input.Cursors.Hand;
        t.MouseEnter += (s, e) => t.TextDecorations = TextDecorations.Underline;
        t.MouseLeave += (s, e) => t.TextDecorations = null;
        t.MouseLeftButtonUp += (s, e) => click();
        return t;
    }

    /// <summary>One setting: title (and explanation) on the left, the control on the right. indent=1 for a sub-setting.</summary>
    public static FrameworkElement Row(string title, FrameworkElement? desc, FrameworkElement control, int indent = 0)
    {
        var g = new Grid { Margin = new Thickness(16 + indent * 22, 12, 16, 12), MinHeight = 34 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
        left.Children.Add(Ui.Text(title, 13.5));
        if (desc != null) left.Children.Add(desc);
        g.Children.Add(left);
        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        // a click anywhere on the row of a toggle flips it (like Windows Settings)
        if (control is CheckBox cb)
        {
            g.Background = System.Windows.Media.Brushes.Transparent;
            g.Cursor = System.Windows.Input.Cursors.Hand;
            g.MouseLeftButtonUp += (s, e) => { if (!cb.IsMouseOver && g.IsEnabled) cb.IsChecked = cb.IsChecked != true; };
        }
        g.IsEnabledChanged += (s, e) => left.Opacity = g.IsEnabled ? 1 : 0.45;
        return g;
    }
}
