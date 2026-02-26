using SmoothScroller.AutoStart;
using SmoothScroller.Core.Engine;
using SmoothScroller.Core.Hooks;
using SmoothScroller.Core.Input;
using SmoothScroller.Core.Window;
using SmoothScroller.Diagnostics;
using SmoothScroller.Settings;
using SmoothScroller.UI;

namespace SmoothScroller;

/// <summary>
/// Application entry point. Wires up all services and installs hooks.
/// </summary>
public partial class App : System.Windows.Application
{
    // ── Services ──────────────────────────────────────────────────────────────
    public static SettingsManager        Settings          { get; private set; } = null!;
    public static ScrollAnimator         Animator          { get; private set; } = null!;
    public static MouseHookManager       MouseHook         { get; private set; } = null!;
    public static KeyboardHookManager    KeyboardHook      { get; private set; } = null!;
    public static ForegroundWindowWatcher ForegroundWatcher { get; private set; } = null!;
    public static TouchpadDetector       TouchpadDetector  { get; private set; } = null!;
    public static DiagnosticsLogger      Logger            { get; private set; } = null!;
    public static TrayIconManager        Tray              { get; private set; } = null!;

    private ProfileResolver _profileResolver = null!;
    private string _currentProcessName = "";

    private static readonly string CrashLog =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmoothScroller_crash.log");

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // ── Crash handlers — write to %TEMP%\SmoothScroller_crash.log ──────────
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteCrash("AppDomain.UnhandledException", ex.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, ex) =>
        {
            WriteCrash("Dispatcher.UnhandledException", ex.Exception);
            ex.Handled = true;   // keep the process alive so user can read the log
        };

        System.IO.File.WriteAllText(CrashLog, $"[{System.DateTime.Now}] SmoothScroller starting...\n");
        CrashLog_Append("OnStartup entered");

        try { base.OnStartup(e); } catch (System.Exception ex) { WriteCrash("base.OnStartup", ex); return; }

        // 1. Load settings
        CrashLog_Append("Step 1 – Load settings");
        try { Settings = new SettingsManager(); Settings.Load(); }
        catch (Exception ex) { WriteCrash("Step 1 – Load settings", ex); return; }

        // 2. Core services
        CrashLog_Append("Step 2 – Core services");
        try
        {
            Logger           = new DiagnosticsLogger { IsEnabled = Settings.Current.DiagnosticsMode };
            Animator         = new ScrollAnimator();
            TouchpadDetector = new TouchpadDetector();
            _profileResolver = new ProfileResolver(Settings);
        }
        catch (Exception ex) { WriteCrash("Step 2 – Core services", ex); return; }

        // 3. Hooks
        CrashLog_Append("Step 3 – Hooks");
        try
        {
            MouseHook    = new MouseHookManager();
            KeyboardHook = new KeyboardHookManager();
            MouseHook.WheelEvent    += OnWheel;
            MouseHook.AnyButtonDown += OnAnyButtonDown;
        }
        catch (Exception ex) { WriteCrash("Step 3 – Hooks", ex); return; }

        // 4. Foreground watcher
        CrashLog_Append("Step 4 – ForegroundWatcher");
        try
        {
            ForegroundWatcher = new ForegroundWindowWatcher();
            ForegroundWatcher.ForegroundAppChanged += OnForegroundAppChanged;
            ForegroundWatcher.Start();
        }
        catch (Exception ex) { WriteCrash("Step 4 – ForegroundWatcher", ex); return; }

        // 5. Tray
        CrashLog_Append("Step 5 – TrayIcon");
        try
        {
            Tray = new TrayIconManager();
            Tray.OpenSettingsRequested += () => OpenSettings();
            Tray.ExitRequested         += () => Shutdown();
            Tray.ToggleRequested       += () => ToggleEnabled();
        }
        catch (Exception ex) { WriteCrash("Step 5 – TrayIcon", ex); return; }

        // 6. Install hooks
        CrashLog_Append("Step 6 – Install hooks");
        try
        {
            MouseHook.Install();
            KeyboardHook.Install(Settings.Current.BypassHotkeyVk);
        }
        catch (Exception ex) { WriteCrash("Step 6 – Install hooks", ex); return; }

        // 7. Sync auto-start
        CrashLog_Append("Step 7 – AutoStart");
        try { StartupManager.Sync(Settings.Current.AutoStart); }
        catch (Exception ex) { WriteCrash("Step 7 – AutoStart", ex); return; }

        // 8. Settings changes → re-apply animator
        Settings.SettingsChanged += (_, _) =>
        {
            Logger.IsEnabled = Settings.Current.DiagnosticsMode;
            ApplyCurrentProfile();
        };
        ApplyCurrentProfile();

        // 9. Open settings unless minimized
        CrashLog_Append("Step 9 – OpenSettings");
        try
        {
            bool minimized = e.Args.Contains("--minimized") || Settings.Current.StartMinimized;
            if (!minimized)
            {
                OpenSettings();
            }
            else
            {
                Tray.ShowToast("SmoothScroller", "SmoothScroller is running in the background.");
            }
        }
        catch (Exception ex) { WriteCrash("Step 9 – OpenSettings", ex); return; }

        CrashLog_Append("Startup complete ✓");
    }

    private void OnForegroundAppChanged(object? sender, string processName)
    {
        _currentProcessName = processName;
        ApplyCurrentProfile();
    }

    private void ApplyCurrentProfile()
    {
        var result = _profileResolver.Resolve(_currentProcessName);
        if (result.Profile != null)
            Animator.ApplyProfile(result.Profile, Settings.Current.ScaleWithDpi);
        Logger.Log($"Foreground: {_currentProcessName} ({result.Reason})");
    }

    private void OnWheel(object? sender, Core.Hooks.WheelEventArgs e)
    {
        if (KeyboardHook.IsBypassActive) { e.Suppress = false; return; }
        if (Settings.Current.DisableTouchpad && TouchpadDetector.HasTouchpad) { e.Suppress = false; return; }

        var profile = _profileResolver.Resolve(_currentProcessName).Profile;
        if (profile == null) { e.Suppress = false; return; }

        bool horizontal = e.Horizontal || (profile.ShiftKeyHorizontal && KeyboardHook.IsShiftDown);
        e.Suppress = true;
        Animator.OnWheel(e.Delta, horizontal, e.Timestamp);
    }

    private void OnAnyButtonDown(object? sender, EventArgs e)
    {
        var profile = _profileResolver.Resolve(_currentProcessName).Profile;
        if (profile?.ClickToStop == true)
            Animator.StopAnimation();
    }

    public void ToggleEnabled()
    {
        Settings.Update(s => s.IsEnabled = !s.IsEnabled);
        Tray.UpdateIcon(Settings.Current.IsEnabled);
    }

    private MainWindow? _mainWindow;

    public void OpenSettings()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            _mainWindow = new MainWindow();
        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try { MouseHook?.Uninstall(); } catch { }
        try { KeyboardHook?.Uninstall(); } catch { }
        try { ForegroundWatcher?.Stop(); } catch { }
        try { Tray?.Dispose(); } catch { }
        try { MouseHook?.Dispose(); } catch { }
        try { ForegroundWatcher?.Dispose(); } catch { }
        base.OnExit(e);
    }

    // ── Crash logging helpers ─────────────────────────────────────────────────
    private static void CrashLog_Append(string message)
    {
        try { System.IO.File.AppendAllText(CrashLog, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } catch { }
    }

    private static void WriteCrash(string context, Exception? ex)
    {
        try
        {
            string msg = $"""
                [{DateTime.Now:HH:mm:ss.fff}] *** CRASH in {context} ***
                Type   : {ex?.GetType().FullName}
                Message: {ex?.Message}
                Stack  :
                {ex?.StackTrace}
                Inner  : {ex?.InnerException?.GetType().FullName}: {ex?.InnerException?.Message}
                {ex?.InnerException?.StackTrace}

                """;
            System.IO.File.AppendAllText(CrashLog, msg);
            System.Diagnostics.Debug.WriteLine(msg);
        }
        catch { }
    }
}
