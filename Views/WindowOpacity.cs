using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using pxdCommander.Core;

namespace pxdCommander.Views;

/// <summary>
/// Settings > Look > "Window opacity": the main window and popped-out terminals become see-through (Windows' own
/// layered-window alpha, so the desktop and other apps show through). 100 % turns the layering off again. The image
/// viewer stays solid.
/// </summary>
public static class WindowOpacity
{
    public const int Min = 50;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, 100);

    /// <summary>Applies the setting now and whenever the window is (re)opened.</summary>
    public static void Track(Window w)
    {
        w.Opened += (_, _) => Apply(w);
        if (w.IsVisible) Apply(w);
    }

    /// <summary>Every open main and terminal window follows a change.</summary>
    public static void ApplyAll()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime life) return;
        foreach (var w in life.Windows)
            if (w is MainWindow or TerminalWindow) Apply(w);
    }

    public static void Apply(Window w)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (w.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        var percent = Clamp(AppSettings.Current.WindowOpacity);
        var style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        if (percent >= 100)
        {
            if ((style & WS_EX_LAYERED) != 0) SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(style & ~WS_EX_LAYERED));
            return;
        }
        if ((style & WS_EX_LAYERED) == 0) SetWindowLongPtrW(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_LAYERED));
        SetLayeredWindowAttributes(hwnd, 0, (byte)Math.Round(percent * 255 / 100.0), LWA_ALPHA);
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
}
