using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace GitHubStats;

/// <summary>Owns the engine and all windows (main window, tray icon, widget) and wires them together.</summary>
internal static class Host
{
    public static App App = null!;
    public static UiState State = new();
    public static MainWindow? Window;
    public static Dashboard? LastDashboard;

    private static TrayHost? _tray;
    private static WidgetWindow? _widget;
    private static HoverCard? _hover;
    private static FireworksWindow? _fx;
    private static DispatcherTimer? _debounce;
    private static DispatcherTimer? _poll;
    private static int _pending;
    private static bool _quitting;

    public static bool AutostartOn()
    {
        try { return Autostart.IsEnabled(); } catch { return false; }
    }

    public static string? SetAutostart(bool on)
    {
        try
        {
            if (on == AutostartOn()) return null;
            Autostart.Set(on, Environment.ProcessPath ?? "");
            return null;
        }
        catch (Exception ex) { return "Could not change \"Start with Windows\": " + ex.Message; }
    }

    /// <summary>A newer version found on GitHub (null = none / not checked).</summary>
    public static UpdateInfo? Update;
    public static event Action? UpdateFound;

    public static void Start(bool hidden)
    {
        Theme.InstallStyles();
        Theme.Accent = State.Accent;
        Theme.Apply(State.Theme);
        Theme.WatchSystem();
        if (State.SeenStars == 0) State.SeenStars = Clock.Now(); // only stars from now on count as "new" for the taskbar badge

        Window = new MainWindow();
        Window.Closing += OnMainClosing;

        App.Changed += RequestUpdate;
        App.NewStars += n => Application.Current?.Dispatcher.BeginInvoke(new Action(() => Celebrate(n, false)));

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _debounce.Tick += (s, e) => { _debounce.Stop(); Interlocked.Exchange(ref _pending, 0); UpdateAll(); };

        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Config.TickSeconds) };
        _poll.Tick += (s, e) => { if (App.Cfg.BackgroundRefresh) App.Poll(); };
        _poll.Start();

        ApplyUi();

        bool keepHidden = hidden && State.Tray;
        if (!keepHidden)
        {
            Window.Show();
            if (hidden) Window.WindowState = WindowState.Minimized; // autostart without a tray icon
        }
        App.Poll(); // starting the program counts as opening it; the 4-per-day limit still applies

        if (App.Cfg.CheckUpdates && Clock.Now() - State.LastUpdateCheck > 86400) _ = CheckUpdateAsync(false);
        _ = RecheckAccessAsync();
    }

    /// <summary>
    /// At start: when the last refresh could not read star dates or traffic, check whether the token can now
    /// (for example a scope was ticked on GitHub) and refresh at once if so. Costs a few requests, only in that case.
    /// </summary>
    private static async Task RecheckAccessAsync()
    {
        try
        {
            var d = App.Data;
            if (App.Cfg.Token.Length == 0 || (d.StarsNote == null && d.TrafficNote == null)) return;
            string? sample = d.Repos.Values.Where(r => !r.Fork).OrderByDescending(r => r.Stars).Select(r => r.Name).FirstOrDefault();
            string? starred = d.Repos.Values.Where(r => r.Stars > 0).OrderBy(r => r.Stars).Select(r => r.Name).FirstOrDefault();
            var rep = await Tools.TokenReportAsync(App.Cfg.Token, App.Cfg.User, sample, starred);
            if (!rep.Valid) return;
            if ((rep.Stars == TrafficAccess.Yes && d.StarsNote != null) || (rep.Traffic == TrafficAccess.Yes && d.TrafficNote != null))
                App.RefreshForNewAccess();
        }
        catch (Exception ex) { Logger.Write("Access re-check failed: " + ex.Message); }
    }

    public static Version CurrentVersion
    {
        get
        {
            var v = typeof(Host).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>Looks for a newer release. manual = from the Settings button (always reports back).</summary>
    public static async Task<UpdateInfo> CheckUpdateAsync(bool manual)
    {
        var info = await Tools.CheckUpdateAsync(App.Cfg.UpdateRepo, App.Cfg.Token, CurrentVersion);
        State.LastUpdateCheck = Clock.Now();
        State.Save();
        if (info.Newer)
        {
            Update = info;
            UpdateFound?.Invoke();
            if (!manual) App.Notify("GitHub Stats " + info.Version + " is available", "Open Settings > Program to download it.");
        }
        if (info.Error != null) Logger.Write("Update check: " + info.Error);
        return info;
    }

    /// <summary>Closes this copy and starts a new one, which first restores the backup (if given).</summary>
    public static void RestartWith(string? restoreZip)
    {
        string exe = Environment.ProcessPath ?? "";
        var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add("--wait");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        if (restoreZip != null) { psi.ArgumentList.Add("--restore"); psi.ArgumentList.Add(restoreZip); }
        System.Diagnostics.Process.Start(psi);
        Quit();
    }

    /// <summary>Applies the UI settings (theme, tray icon, widget). Called at start and after the settings were saved.</summary>
    public static void ApplyUi()
    {
        Theme.Accent = State.Accent;
        Theme.Apply(State.Theme);

        if (State.Tray && _tray == null) _tray = new TrayHost();
        else if (!State.Tray && _tray != null) { _tray.Dispose(); _tray = null; }

        var tray = _tray;
        App.NotifyHandler = tray != null && App.Cfg.Notifications
            ? (title, text) => Application.Current.Dispatcher.BeginInvoke(new Action(() => tray.Balloon(title, text)))
            : null;

        _tray?.ApplyTooltipMode();
        if (!State.HoverCard) HideHover();
        SetWidget(State.Widget, false);
        _widget?.Apply();
        UpdateAll();
    }

    public static void SetWidget(bool on, bool save = true)
    {
        State.Widget = on;
        if (on && _widget == null)
        {
            _widget = new WidgetWindow();
            _widget.Show();
        }
        else if (!on && _widget != null)
        {
            _widget.Close();
            _widget = null;
        }
        if (save) State.Save();
        if (_widget != null && LastDashboard != null) _widget.Update(LastDashboard);
    }

    public static void ShowHover(int x, int y)
    {
        var db = LastDashboard;
        if (db == null || !State.HoverCard) return;
        try
        {
            _hover ??= new HoverCard();
            _hover.ShowAt(db, x, y);
        }
        catch (Exception ex) { Logger.Write("Hover card failed: " + ex.Message); }
    }

    public static void HideHover()
    {
        try { if (_hover != null && _hover.IsVisible) _hover.Hide(); }
        catch { /* ignore */ }
    }

    /// <summary>Fireworks and "+N" on the screen. force = also when the setting is off (the test button).</summary>
    public static void Celebrate(int stars, bool force)
    {
        if (!force && !State.Celebrate) return;
        try
        {
            if (_fx != null) { _fx.Close(); _fx = null; }
            var w = new FireworksWindow(stars);
            w.Closed += (s, e) => { if (ReferenceEquals(_fx, w)) _fx = null; };
            _fx = w;
            w.Show();
        }
        catch (Exception ex) { Logger.Write("Celebration failed: " + ex.Message); }
    }

    private static void RequestUpdate()
    {
        // called from the refresh thread: collapse bursts into one update on the UI thread
        if (Interlocked.Exchange(ref _pending, 1) == 1) return;
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => { if (_debounce != null && !_debounce.IsEnabled) _debounce.Start(); }));
    }

    public static void UpdateAll()
    {
        try
        {
            var db = Dashboard.Build(App);
            LastDashboard = db;
            Window?.ShowData(db);
            _tray?.Update(db);
            _widget?.Update(db);
        }
        catch (Exception ex) { Logger.Write("Update failed: " + ex); }
    }

    public static void ShowMain()
    {
        var w = Window;
        if (w == null) return;
        if (!w.IsVisible) w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        w.Topmost = true; // brings it to the front even when another program has the focus
        w.Topmost = false;
        w.Focus();
    }

    /// <summary>The window was activated: with background refresh off this is what triggers a refresh (if one is due).</summary>
    public static void OnWindowActivated()
    {
        if (!App.Cfg.BackgroundRefresh) App.Poll();
    }

    private static void OnMainClosing(object? sender, CancelEventArgs e)
    {
        if (_quitting) return;
        bool keepRunning = (State.Tray && State.CloseToTray) || State.Widget;
        Window?.SavePlacement();
        State.Save();
        if (keepRunning)
        {
            e.Cancel = true;
            Window?.Hide();
        }
        else Quit();
    }

    public static void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        try
        {
            Window?.SavePlacement();
            State.Save();
            _poll?.Stop();
            _tray?.Dispose();
            _tray = null;
            _widget?.Close();
            _hover?.Close();
            _fx?.Close();
        }
        catch { /* ignore */ }
        Application.Current.Shutdown();
    }
}

internal static class Program
{
    /// <summary>The folder next to the .exe: config.json and the data folder live here (portable).</summary>
    private static string Home()
    {
        string? over = Environment.GetEnvironmentVariable("GHSTATS_HOME");
        if (!string.IsNullOrWhiteSpace(over)) { Directory.CreateDirectory(over); return Path.GetFullPath(over); }
        string? exe = Environment.ProcessPath;
        string name = Path.GetFileNameWithoutExtension(exe ?? "");
        if (exe != null && !name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return Path.GetDirectoryName(exe)!;
        return AppContext.BaseDirectory;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Write("Unhandled error: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Logger.Write("Background error: " + e.Exception.GetBaseException().Message); e.SetObserved(); };

        bool hidden = args.Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));

        string home = Home();

        // restart after a restore: wait until the old copy has closed, then put the backup in place
        int wi = Array.FindIndex(args, a => a.Equals("--wait", StringComparison.OrdinalIgnoreCase));
        if (wi >= 0 && wi + 1 < args.Length && int.TryParse(args[wi + 1], out int waitPid))
        {
            try { System.Diagnostics.Process.GetProcessById(waitPid).WaitForExit(20000); } catch { /* already gone */ }
        }
        string? restoreNote = null;
        int ri = Array.FindIndex(args, a => a.Equals("--restore", StringComparison.OrdinalIgnoreCase));
        if (ri >= 0 && ri + 1 < args.Length)
        {
            var pre = Config.Load(home, out _);
            string preData = Path.IsPathRooted(pre.DataDir) ? pre.DataDir : Path.Combine(home, pre.DataDir);
            restoreNote = Backup.Restore(args[ri + 1], home, preData);
        }

        var cfg = Config.Load(home, out string? warning);
        string? tzWarning = Clock.SetZone(cfg.Timezone);
        string dataDir = Path.IsPathRooted(cfg.DataDir) ? cfg.DataDir : Path.Combine(home, cfg.DataDir);
        var warnings = new[] { warning, tzWarning, restoreNote }.Where(w => w != null).ToList();
        var app = new App(cfg, home, dataDir) { Warning = warnings.Count > 0 ? string.Join(" ", warnings) : null };
        try
        {
            Directory.CreateDirectory(dataDir);
            Logger.SetFolder(dataDir);
        }
        catch (Exception ex) { app.StoreProblem = "The data folder \"" + dataDir + "\" could not be created: " + ex.Message; }

        // Only one copy at a time: a second start brings the first one's window to the front.
        string id = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(home.ToLowerInvariant()))).ToLowerInvariant()[..12];
        using var mutex = new Mutex(true, "Local\\GitHubStats-" + id, out bool first);
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\GitHubStats-show-" + id);
        if (!first)
        {
            try { showEvent.Set(); } catch { /* ignore */ }
            return 0;
        }

        app.Load();
        if (app.Data.LastSuccess > 0 && app.PublishPath != null && !Directory.Exists(app.PublishPath)) app.PublishShareNow();
        Logger.Write("GitHub Stats started - public repositories of " + cfg.User + ", data in " + dataDir);

        var wpf = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        wpf.DispatcherUnhandledException += (s, e) =>
        {
            Logger.Write("UI error: " + e.Exception);
            e.Handled = true; // a display problem must not take the program down
        };

        Host.App = app;
        Host.State = UiState.Load(dataDir);
        Host.Start(hidden);

        ThreadPool.RegisterWaitForSingleObject(showEvent, (state, timedOut) =>
        {
            wpf.Dispatcher.BeginInvoke(new Action(Host.ShowMain));
        }, null, Timeout.Infinite, false);

        int code = wpf.Run();
        GC.KeepAlive(mutex);
        return code;
    }
}
