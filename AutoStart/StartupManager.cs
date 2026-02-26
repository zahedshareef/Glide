using Microsoft.Win32;

namespace SmoothScroller.AutoStart;

/// <summary>
/// Manages the Windows startup registry entry so SmoothScroller
/// launches automatically on user login.
/// </summary>
public static class StartupManager
{
    private const string RegistryKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName      = "SmoothScroller";

    private static string ExePath =>
        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true)
                        ?? throw new InvalidOperationException("Cannot open startup registry key.");
        key.SetValue(AppName, $"\"{ExePath}\" --minimized");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static void Sync(bool enabled)
    {
        if (enabled) Enable();
        else         Disable();
    }
}
