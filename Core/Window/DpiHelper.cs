using System.Runtime.InteropServices;

namespace SmoothScroller.Core.Window;

/// <summary>
/// Reads the DPI of the monitor containing the foreground window
/// and exposes a scaling factor relative to 96 DPI (100%).
/// </summary>
public static class DpiHelper
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>
    /// Returns the DPI scale factor (1.0 = 100% = 96 dpi) for the monitor
    /// that contains the current foreground window.
    /// </summary>
    public static double GetForegroundScaleFactor()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return 1.0;

            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return 1.0;

            int hr = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
            if (hr != 0) return 1.0;

            return dpiX / 96.0;
        }
        catch { return 1.0; }
    }
}
