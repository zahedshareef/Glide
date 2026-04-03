using System.Runtime.InteropServices;

namespace Glide.Core.Window;

/// <summary>
/// Polls the foreground window and raises <see cref="ForegroundAppChanged"/>
/// when the active process changes.
/// </summary>
public sealed class ForegroundWindowWatcher : IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private readonly System.Timers.Timer _timer;
    private string _currentProcess = "";

    public event EventHandler<string>? ForegroundAppChanged;
    public string CurrentProcessName => _currentProcess;

    public ForegroundWindowWatcher(int pollIntervalMs = 200)
    {
        _timer = new System.Timers.Timer(pollIntervalMs);
        _timer.Elapsed += (_, _) => Poll();
        _timer.AutoReset = true;
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();

    private void Poll()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return;

            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            string name = proc.ProcessName.ToLowerInvariant();

            if (name != _currentProcess)
            {
                _currentProcess = name;
                ForegroundAppChanged?.Invoke(this, name);
            }
        }
        catch { /* process may have exited between calls */ }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
