using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Glide.Backend;

public sealed record BackendStatusResponse(string Status, string ProtocolVersion, string Version);
public sealed record DiagnosticsLogResponse(string Text, int LineCount, bool Truncated);

/// <summary>Local named-pipe health endpoint for the Tauri settings shell.</summary>
public sealed class BackendStatusServer : IDisposable
{
    public const string PipeName = "Glide.Backend.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<string> _settingsJsonProvider;
    private readonly Action<string, bool> _updateBooleanSetting;
    private readonly Action<string, JsonElement> _updateProfileSetting;
    private readonly Action<JsonElement> _updatePerApp;
    private readonly Action<string> _runDiagnosticsAction;
    private readonly Func<DiagnosticsLogResponse> _readDiagnosticsLog;
    private readonly Action _clearDiagnosticsLog;
    private readonly Func<string, string> _importSettings;
    private Task? _serverTask;

    public BackendStatusServer(
        Func<string> settingsJsonProvider,
        Action<string, bool> updateBooleanSetting,
        Action<string, JsonElement> updateProfileSetting,
        Action<JsonElement> updatePerApp,
        Action<string> runDiagnosticsAction,
        Func<DiagnosticsLogResponse> readDiagnosticsLog,
        Action clearDiagnosticsLog,
        Func<string, string> importSettings)
    {
        _settingsJsonProvider = settingsJsonProvider;
        _updateBooleanSetting = updateBooleanSetting;
        _updateProfileSetting = updateProfileSetting;
        _updatePerApp = updatePerApp;
        _runDiagnosticsAction = runDiagnosticsAction;
        _readDiagnosticsLog = readDiagnosticsLog;
        _clearDiagnosticsLog = clearDiagnosticsLog;
        _importSettings = importSettings;
    }

    public void Start() => _serverTask ??= Task.Run(RunAsync);

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_stop.Token);

                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true,
                };

                var request = await reader.ReadLineAsync(_stop.Token);
                if (string.Equals(request, "status", StringComparison.Ordinal))
                {
                    var response = new BackendStatusResponse(
                        "running",
                        "1",
                        typeof(BackendStatusServer).Assembly.GetName().Version?.ToString() ?? "unknown");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
                }
                else if (string.Equals(request, "settings", StringComparison.Ordinal))
                {
                    using var document = JsonDocument.Parse(_settingsJsonProvider());
                    await writer.WriteLineAsync(JsonSerializer.Serialize(document.RootElement, JsonOptions));
                }
                else if (request?.StartsWith("update ", StringComparison.Ordinal) == true)
                {
                    using var document = JsonDocument.Parse(request["update ".Length..]);
                    var root = document.RootElement;
                    var field = root.GetProperty("field").GetString();
                    var value = root.GetProperty("value").GetBoolean();
                    if (string.IsNullOrWhiteSpace(field))
                        throw new InvalidOperationException("Setting field is required.");

                    _updateBooleanSetting(field, value);
                    using var updated = JsonDocument.Parse(_settingsJsonProvider());
                    await writer.WriteLineAsync(JsonSerializer.Serialize(updated.RootElement, JsonOptions));
                }
                else if (request?.StartsWith("update-profile ", StringComparison.Ordinal) == true)
                {
                    using var document = JsonDocument.Parse(request["update-profile ".Length..]);
                    var root = document.RootElement;
                    var field = root.GetProperty("field").GetString();
                    if (string.IsNullOrWhiteSpace(field))
                        throw new InvalidOperationException("Profile field is required.");

                    _updateProfileSetting(field, root.GetProperty("value"));
                    using var updated = JsonDocument.Parse(_settingsJsonProvider());
                    await writer.WriteLineAsync(JsonSerializer.Serialize(updated.RootElement, JsonOptions));
                }
                else if (request?.StartsWith("per-app ", StringComparison.Ordinal) == true)
                {
                    using var document = JsonDocument.Parse(request["per-app ".Length..]);
                    _updatePerApp(document.RootElement);
                    using var updated = JsonDocument.Parse(_settingsJsonProvider());
                    await writer.WriteLineAsync(JsonSerializer.Serialize(updated.RootElement, JsonOptions));
                }
                else if (request?.StartsWith("diagnostics ", StringComparison.Ordinal) == true)
                {
                    var action = request["diagnostics ".Length..].Trim();
                    _runDiagnosticsAction(action);
                    await writer.WriteLineAsync("{\"ok\":true}");
                }
                else if (request == "diagnostics-log")
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(_readDiagnosticsLog(), JsonOptions));
                }
                else if (request == "clear-diagnostics-log")
                {
                    _clearDiagnosticsLog();
                    await writer.WriteLineAsync("{\"ok\":true}");
                }
                else if (request == "export-settings")
                {
                    using var document = JsonDocument.Parse(_settingsJsonProvider());
                    await writer.WriteLineAsync(JsonSerializer.Serialize(document.RootElement, JsonOptions));
                }
                else if (request?.StartsWith("import-settings ", StringComparison.Ordinal) == true)
                {
                    using var document = JsonDocument.Parse(request["import-settings ".Length..]);
                    var contents = document.RootElement.GetProperty("contents").GetString();
                    if (string.IsNullOrWhiteSpace(contents))
                        throw new InvalidOperationException("Settings file is empty.");

                    using var updated = JsonDocument.Parse(_importSettings(contents));
                    await writer.WriteLineAsync(JsonSerializer.Serialize(updated.RootElement, JsonOptions));
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A dropped UI connection must not bring down the input backend.
                try
                {
                    File.AppendAllText(
                        Path.Combine(Path.GetTempPath(), "Glide_crash.log"),
                        $"[{DateTime.Now:O}] Backend pipe error: {ex.GetType().Name}: {ex.Message}\n");
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _serverTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _stop.Dispose();
    }
}
