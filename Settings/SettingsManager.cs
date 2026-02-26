using System.IO;
using System.Text.Json;
using SmoothScroller.Settings.Models;

namespace SmoothScroller.Settings;

public class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmoothScroller", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private GlobalSettings _settings = new();
    public GlobalSettings Current => _settings;

    public event EventHandler? SettingsChanged;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
            }
        }
        catch
        {
            _settings = new GlobalSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, JsonOptions));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string ExportJson() => JsonSerializer.Serialize(_settings, JsonOptions);

    public void ImportJson(string json)
    {
        _settings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
        Save();
    }

    public void Update(Action<GlobalSettings> mutate)
    {
        mutate(_settings);
        Save();
    }
}
