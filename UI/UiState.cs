using System;
using System.IO;
using System.Text.Json;

namespace GitHubStats;

/// <summary>Settings that only concern the Windows UI (kept in data\ui.json, separate from config.json).</summary>
internal sealed class UiState
{
    public string Theme { get; set; } = "auto";
    public bool Tray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    /// <summary>Rich popup card when hovering the tray icon (instead of the plain Windows tooltip).</summary>
    public bool HoverCard { get; set; } = true;
    /// <summary>Fireworks and "+N" on the screen when new stars arrive.</summary>
    public bool Celebrate { get; set; } = true;
    public bool Widget { get; set; }
    public double WidgetOpacity { get; set; } = 0.92;
    public bool WidgetTopmost { get; set; } = true;
    public double? WidgetX { get; set; }
    public double? WidgetY { get; set; }
    public double? WinX { get; set; }
    public double? WinY { get; set; }
    public double? WinW { get; set; }
    public double? WinH { get; set; }
    public bool WinMax { get; set; }
    /// <summary>Time of the newest star the user has seen (the taskbar badge counts newer ones).</summary>
    public long SeenStars { get; set; }
    /// <summary>Accent colour name (see Theme.Accents).</summary>
    public string Accent { get; set; } = "blue";
    /// <summary>When the program last looked for a newer version (unix seconds).</summary>
    public long LastUpdateCheck { get; set; }

    private static string _file = "";

    public static UiState Load(string dataDir)
    {
        _file = Path.Combine(dataDir, "ui.json");
        try
        {
            if (File.Exists(_file)) return JsonSerializer.Deserialize<UiState>(File.ReadAllText(_file)) ?? new UiState();
        }
        catch (Exception ex) { Logger.Write("ui.json could not be read: " + ex.Message); }
        return new UiState();
    }

    public void Save()
    {
        try
        {
            if (_file.Length == 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Logger.Write("ui.json could not be saved: " + ex.Message); }
    }
}
