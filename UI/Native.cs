using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GitHubStats;

internal static class Native
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>Makes a window click-through, never activated and hidden from Alt+Tab (call from SourceInitialized or later).</summary>
    public static void Overlay(Window w, bool clickThrough)
    {
        try
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;
            int ex = GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (clickThrough) ex |= WS_EX_TRANSPARENT;
            SetWindowLong(h, GWL_EXSTYLE, ex);
        }
        catch (Exception ex) { Logger.Write("Window style failed: " + ex.Message); }
    }
}
