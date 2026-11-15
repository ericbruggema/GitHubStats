using System;

namespace GitHubStats;

/// <summary>"Start with Windows": a value in HKCU\...\Run (current user only, no admin rights needed).</summary>
internal static class Autostart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GitHubStats";

    public static bool IsEnabled()
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath);
        return k?.GetValue(ValueName) is string s && s.Length > 0;
    }

    public static void Set(bool on, string exePath)
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(KeyPath);
        if (k == null) return;
        if (on) k.SetValue(ValueName, "\"" + exePath + "\" --autostart");
        else k.DeleteValue(ValueName, false);
    }
}
