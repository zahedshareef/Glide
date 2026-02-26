using Hardcodet.Wpf.TaskbarNotification;

namespace SmoothScroller.UI;

/// <summary>
/// Manages the system tray icon using Hardcodet.NotifyIcon.Wpf.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _trayIcon;

    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;
    public event Action? ToggleRequested;

    public TrayIconManager()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "SmoothScroller",
            IconSource  = CreateIcon(enabled: true),
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
        _trayIcon.ContextMenu           = BuildContextMenu();
    }

    private System.Windows.Media.ImageSource CreateIcon(bool enabled)
    {
        var drawing = new System.Windows.Media.DrawingImage(
            new System.Windows.Media.GeometryDrawing(
                enabled ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Gray,
                null,
                System.Windows.Media.Geometry.Parse(
                    "M8,2 C4.7,2 2,4.7 2,8 C2,11.3 4.7,14 8,14 C11.3,14 14,11.3 14,8 C14,4.7 11.3,2 8,2Z")));
        drawing.Freeze();
        return drawing;
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "⚙ Settings" };
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();

        var toggleItem = new System.Windows.Controls.MenuItem { Header = "⏸ Pause / Resume" };
        toggleItem.Click += (_, _) => ToggleRequested?.Invoke();

        var exitItem = new System.Windows.Controls.MenuItem { Header = "✕ Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(settingsItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(toggleItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);

        return menu;
    }

    public void UpdateIcon(bool enabled)
    {
        _trayIcon.IconSource  = CreateIcon(enabled);
        _trayIcon.ToolTipText = enabled ? "SmoothScroller — Active" : "SmoothScroller — Paused";
    }

    public void ShowToast(string title, string message)
    {
        _trayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    public void Dispose() => _trayIcon.Dispose();
}
