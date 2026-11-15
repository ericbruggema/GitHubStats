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
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string? title);
    private const int GWLP_HWNDPARENT = -8;

    /// <summary>
    /// Makes a widget behave like a desktop gadget: owned by the desktop (Progman), so "Show desktop" (Win+D)
    /// and minimising other windows don't hide it, and kept out of Alt+Tab.
    /// </summary>
    public static void PinToDesktop(Window w)
    {
        try
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
            IntPtr desk = FindWindow("Progman", null);
            if (desk != IntPtr.Zero) SetWindowLongPtr(h, GWLP_HWNDPARENT, desk);
        }
        catch (Exception ex) { Logger.Write("Pinning the widget failed: " + ex.Message); }
    }

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
