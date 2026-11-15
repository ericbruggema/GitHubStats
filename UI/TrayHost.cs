using System;
using System.IO;

namespace GitHubStats;

/// <summary>The icon in the notification area: tooltip with the totals, menu, and Windows notifications.</summary>
internal sealed class TrayHost : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Windows.Forms.ToolStripMenuItem _status;
    private readonly System.Windows.Forms.ToolStripMenuItem _widget;
    private readonly System.Windows.Threading.DispatcherTimer _hoverTimer;
    private long _lastMove;
    private int _lx, _ly;
    private string _plainText = "GitHub Stats";

    public TrayHost()
    {
        _menu = new System.Windows.Forms.ContextMenuStrip();

        var open = new System.Windows.Forms.ToolStripMenuItem("Open GitHub Stats");
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        open.Click += (s, e) => Host.ShowMain();
        _menu.Items.Add(open);

        _status = new System.Windows.Forms.ToolStripMenuItem("GitHub Stats") { Enabled = false };
        _menu.Items.Add(_status);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _widget = new System.Windows.Forms.ToolStripMenuItem("Show desktop widget");
        _widget.Click += (s, e) => Host.SetWidget(!Host.State.Widget);
        _menu.Items.Add(_widget);
        Add("Open data folder", () => Ui.OpenPath(Host.App.DataDir));
        Add("Open publish folder", () =>
        {
            string? p = Host.App.PublishPath;
            if (p == null) return;
            try { Directory.CreateDirectory(p); Ui.OpenPath(p); } catch (Exception ex) { Logger.Write(ex.Message); }
        });
        Add("Test the star celebration", () => Host.Celebrate(3, true));
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        Add("Quit", Host.Quit);
        _menu.Opening += (s, e) => { Host.HideHover(); RefreshMenu(); };

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "GitHub Stats",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) Host.ShowMain();
        };
        _icon.BalloonTipClicked += (s, e) => Host.ShowMain();
        _icon.MouseMove += (s, e) =>
        {
            _lastMove = Environment.TickCount64;
            var pos = System.Windows.Forms.Cursor.Position;
            _lx = pos.X;
            _ly = pos.Y;
            if (Host.State.HoverCard) Host.ShowHover(pos.X, pos.Y);
        };
        _icon.MouseDown += (s, e) => Host.HideHover();

        // the icon has no "mouse left" event: hide the card when the mouse is no longer where it last moved over the icon
        _hoverTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _hoverTimer.Tick += (s, e) =>
        {
            var c = System.Windows.Forms.Cursor.Position;
            bool moved = Math.Abs(c.X - _lx) > 10 || Math.Abs(c.Y - _ly) > 10;
            if (moved || Environment.TickCount64 - _lastMove > 15000) Host.HideHover();
        };
        _hoverTimer.Start();
    }

    private void Add(string text, Action click)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text);
        item.Click += (s, e) => click();
        _menu.Items.Add(item);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            using var s = typeof(TrayHost).Assembly.GetManifestResourceStream("GitHubStats.app.ico");
            if (s != null) return new System.Drawing.Icon(s, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch { /* use the default icon */ }
        return System.Drawing.SystemIcons.Application;
    }

    private void RefreshMenu()
    {
        var db = Host.LastDashboard;
        _widget.Checked = Host.State.Widget;
        _status.Text = db == null ? "GitHub Stats"
            : db.Running ? "Refreshing from GitHub..."
            : db.LastSuccess > 0 ? "Last refresh " + Fmt.Hm(db.LastSuccess) + ", next after " + db.NextRefresh
            : "No complete refresh yet";
    }

    private static string WinText(WinDelta d) =>
        d.Value == null ? "-" : Fmt.DeltaText(d.Value) + (d.Partial ? "*" : "");

    private static string Win(WinDelta[] v) => "24h " + WinText(v[0]) + "  7d " + WinText(v[1]) + "  30d " + WinText(v[2]);

    public void Update(Dashboard db)
    {
        string text = "GitHub Stats";
        if (db.HasData)
            text = "Downloads " + Fmt.N(db.TotalDownloads) + "   " + Win(db.DownloadsWin) + "\n"
                 + "Stars " + Fmt.N(db.TotalStars) + "   " + Win(db.StarsWin);
        _plainText = text.Length > 127 ? text.Substring(0, 127) : text; // limit of the tray tooltip
        ApplyTooltipMode();
    }

    /// <summary>With the hover card on, the plain Windows tooltip is switched off (an empty text shows no tooltip).</summary>
    public void ApplyTooltipMode() => _icon.Text = Host.State.HoverCard ? "" : _plainText;

    /// <summary>Shows a Windows notification (call on the UI thread).</summary>
    public void Balloon(string title, string text)
    {
        try
        {
            _icon.ShowBalloonTip(10000, title, text.Length > 250 ? text.Substring(0, 250) + "..." : text, System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex) { Logger.Write("Could not show a notification: " + ex.Message); }
    }

    public void Dispose()
    {
        try { _hoverTimer.Stop(); _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); } catch { /* ignore */ }
    }
}
