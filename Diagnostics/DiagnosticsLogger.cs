using System.IO;

namespace Glide.Diagnostics;

/// <summary>
/// Lightweight append-only logger for the diagnostics mode.
/// Thread-safe; writes to %AppData%\Glide\diagnostics.log.
/// </summary>
public sealed class DiagnosticsLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glide", "diagnostics.log");

    private readonly object _lock = new();
    public bool IsEnabled { get; set; }

    public event EventHandler<string>? LogEntry;

    public void Log(string message)
    {
        if (!IsEnabled) return;
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }
        }
        LogEntry?.Invoke(this, line);
    }

    public IEnumerable<string> ReadLines()
    {
        lock (_lock)
        {
            if (!File.Exists(LogPath)) return Enumerable.Empty<string>();
            return File.ReadAllLines(LogPath);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (File.Exists(LogPath)) File.Delete(LogPath);
        }
    }
}
