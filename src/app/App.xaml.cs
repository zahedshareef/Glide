using Glide.AutoStart;
using Glide.Backend;
using Glide.Core.Engine;
using Glide.Core.Hooks;
using Glide.Core.Input;
using Glide.Core.Window;
using Glide.Diagnostics;
using Glide.Settings;
using Glide.Settings.Models;
using Glide.UI;
using System.IO;

namespace Glide;

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
    public static BackendStatusServer    BackendStatus     { get; private set; } = null!;

    private ProfileResolver _profileResolver = null!;
    private string _currentProcessName = "";
    private bool _backendMode;

    private static readonly string CrashLog =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Glide_crash.log");

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _backendMode = e.Args.Contains("--backend", StringComparer.OrdinalIgnoreCase);
        // ── Crash handlers — write to %TEMP%\Glide_crash.log ──────────
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteCrash("AppDomain.UnhandledException", ex.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, ex) =>
        {
            WriteCrash("Dispatcher.UnhandledException", ex.Exception);
            ex.Handled = true;   // keep the process alive so user can read the log
        };

        System.IO.File.WriteAllText(CrashLog, $"[{System.DateTime.Now}] Glide starting...\n");
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

        BackendStatus = new BackendStatusServer(
            () => Settings.ExportJson(), UpdateBooleanSetting, UpdateProfileSetting, UpdatePerApp,
            RunDiagnosticsAction, ReadDiagnosticsLog, ClearDiagnosticsLog, ImportSettings);
        BackendStatus.Start();

        if (_backendMode)
        {
            CrashLog_Append("Backend mode ready");
            return;
        }

        // 9. Open settings unless minimized
        CrashLog_Append("Step 9 – OpenSettings");
        try
        {
            bool minimized = e.Args.Contains("--minimized") || Settings.Current.StartMinimized;
            if (!minimized) OpenSettings();
        }
        catch (Exception ex) { WriteCrash("Step 9 – OpenSettings", ex); return; }

        CrashLog_Append("Startup complete ✓");
    }

    private void OnForegroundAppChanged(object? sender, string processName)
    {
        _currentProcessName = processName;
        ApplyCurrentProfile();
        Logger.Log($"Foreground: {processName}");
    }

    private void ApplyCurrentProfile()
    {
        var profile = _profileResolver.Resolve(_currentProcessName);
        if (profile != null)
            Animator.ApplyProfile(profile, Settings.Current.ScaleWithDpi);
    }

    private void OnWheel(object? sender, Core.Hooks.WheelEventArgs e)
    {
        if (KeyboardHook.IsBypassActive) { e.Suppress = false; return; }
        if (Settings.Current.DisableTouchpad && TouchpadDetector.HasTouchpad) { e.Suppress = false; return; }

        var profile = _profileResolver.Resolve(_currentProcessName);
        if (profile == null) { e.Suppress = false; return; }

        bool horizontal = e.Horizontal || (profile.ShiftKeyHorizontal && KeyboardHook.IsShiftDown);
        e.Suppress = true;
        Logger.Log($"Wheel delta={e.Delta} horiz={horizontal}");
        Animator.OnWheel(e.Delta, horizontal, e.Timestamp);
    }

    private void OnAnyButtonDown(object? sender, EventArgs e)
    {
        var profile = _profileResolver.Resolve(_currentProcessName);
        if (profile?.ClickToStop == true)
            Animator.StopAnimation();
    }

    public void ToggleEnabled()
    {
        Settings.Update(s => s.IsEnabled = !s.IsEnabled);
        Tray.UpdateIcon(Settings.Current.IsEnabled);
    }

    private static void UpdateBooleanSetting(string field, bool value)
    {
        switch (field)
        {
            case "IsEnabled":
                Settings.Update(s => s.IsEnabled = value);
                break;
            case "StartMinimized":
                Settings.Update(s => s.StartMinimized = value);
                break;
            case "AutoStart":
                Settings.Update(s => s.AutoStart = value);
                break;
            case "DisableTouchpad":
                Settings.Update(s => s.DisableTouchpad = value);
                break;
            case "FrameRateAdaptive":
                Settings.Update(s => s.FrameRateAdaptive = value);
                break;
            case "ScaleWithDpi":
                Settings.Update(s => s.ScaleWithDpi = value);
                break;
            case "DiagnosticsMode":
                Settings.Update(s => s.DiagnosticsMode = value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported boolean setting: {field}");
        }
    }

    private static void UpdateProfileSetting(string field, System.Text.Json.JsonElement value)
    {
        Settings.Update(s =>
        {
            var profile = s.GlobalProfile;
            switch (field)
            {
                case "StepSize": profile.StepSize = ReadInt(value, 20, 500, field); break;
                case "AnimationTime": profile.AnimationTime = ReadInt(value, 50, 2000, field); break;
                case "AccelerationDelta": profile.AccelerationDelta = ReadInt(value, 1, 500, field); break;
                case "AccelerationMax": profile.AccelerationMax = ReadDouble(value, 1, 20, field); break;
                case "TailToHeadRatio": profile.TailToHeadRatio = ReadDouble(value, 1, 10, field); break;
                case "SpeedThresholdForSmooth": profile.SpeedThresholdForSmooth = ReadDouble(value, 0, 100, field); break;
                case "AnimationEasing": profile.AnimationEasing = ReadBool(value, field); break;
                case "ShiftKeyHorizontal": profile.ShiftKeyHorizontal = ReadBool(value, field); break;
                case "HorizontalSmoothness": profile.HorizontalSmoothness = ReadBool(value, field); break;
                case "ReverseDirection": profile.ReverseDirection = ReadBool(value, field); break;
                case "ClickToStop": profile.ClickToStop = ReadBool(value, field); break;
                default: throw new InvalidOperationException($"Unsupported profile setting: {field}");
            }
        });
    }

    private static int ReadInt(System.Text.Json.JsonElement value, int minimum, int maximum, string field)
    {
        if (!value.TryGetInt32(out var result) || result < minimum || result > maximum)
            throw new InvalidOperationException($"{field} must be between {minimum} and {maximum}.");
        return result;
    }

    private static double ReadDouble(System.Text.Json.JsonElement value, double minimum, double maximum, string field)
    {
        if (!value.TryGetDouble(out var result) || double.IsNaN(result) || result < minimum || result > maximum)
            throw new InvalidOperationException($"{field} must be between {minimum} and {maximum}.");
        return result;
    }

    private static bool ReadBool(System.Text.Json.JsonElement value, string field)
    {
        if (value.ValueKind != System.Text.Json.JsonValueKind.True &&
            value.ValueKind != System.Text.Json.JsonValueKind.False)
            throw new InvalidOperationException($"{field} must be a boolean.");
        return value.GetBoolean();
    }

    private static void UpdatePerApp(System.Text.Json.JsonElement request)
    {
        var operation = request.GetProperty("operation").GetString();
        switch (operation)
        {
            case "setFilterMode":
                var mode = request.GetProperty("filterMode").GetString();
                if (!Enum.TryParse<FilterMode>(mode, ignoreCase: false, out var filterMode))
                    throw new InvalidOperationException("Filter mode must be Blacklist or Whitelist.");
                Settings.Update(s => s.FilterMode = filterMode);
                break;
            case "addFilter":
                var filterName = NormalizeProcessName(request);
                Settings.Update(s =>
                {
                    if (!s.FilterList.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                        s.FilterList.Add(filterName);
                });
                break;
            case "addOverride":
                var overrideName = NormalizeProcessName(request);
                Settings.Update(s =>
                {
                    if (!s.AppOverrides.Keys.Any(x => x.Equals(overrideName, StringComparison.OrdinalIgnoreCase)))
                        s.AppOverrides[overrideName] = new ScrollProfile();
                });
                break;
            case "remove":
                var removeName = NormalizeProcessName(request);
                Settings.Update(s =>
                {
                    s.FilterList.RemoveAll(x => x.Equals(removeName, StringComparison.OrdinalIgnoreCase));
                    foreach (var key in s.AppOverrides.Keys.Where(x => x.Equals(removeName, StringComparison.OrdinalIgnoreCase)).ToList())
                        s.AppOverrides.Remove(key);
                });
                break;
            default:
                throw new InvalidOperationException($"Unsupported per-app operation: {operation}");
        }
    }

    private static string NormalizeProcessName(System.Text.Json.JsonElement request)
    {
        var name = request.GetProperty("appName").GetString()?.Trim().ToLowerInvariant().Replace(".exe", "") ?? "";
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(c => c is '\\' or '/' or ':'))
            throw new InvalidOperationException("App name must be a process name, such as chrome or notepad.");
        return name;
    }

    private static void RunDiagnosticsAction(string action)
    {
        switch (action)
        {
            case "checkForUpdates":
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/Glide/releases") { UseShellExecute = true });
                break;
            case "openLogFolder":
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glide");
                Directory.CreateDirectory(directory);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", directory));
                break;
            default:
                throw new InvalidOperationException($"Unsupported diagnostics action: {action}");
        }
    }

    private static DiagnosticsLogResponse ReadDiagnosticsLog()
    {
        const int maxLines = 500;
        var lines = Logger.ReadLines().ToArray();
        var firstLine = Math.Max(0, lines.Length - maxLines);
        return new DiagnosticsLogResponse(
            string.Join(Environment.NewLine, lines.Skip(firstLine)),
            lines.Length,
            firstLine > 0);
    }

    private static void ClearDiagnosticsLog() => Logger.Clear();

    private static string ImportSettings(string json)
    {
        Settings.ImportJson(json);
        return Settings.ExportJson();
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
        try { BackendStatus?.Dispose(); } catch { }
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
