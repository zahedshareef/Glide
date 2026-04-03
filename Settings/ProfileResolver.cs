using Glide.Settings.Models;

namespace Glide.Settings;

/// <summary>
/// Resolves the effective scroll profile for the currently active application,
/// respecting the blacklist/whitelist filter and per-app overrides.
/// </summary>
public class ProfileResolver
{
    private readonly SettingsManager _settingsManager;

    public ProfileResolver(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    /// <summary>
    /// Returns the resolved profile for <paramref name="processName"/>.
    /// Returns null if smooth scrolling should be bypassed for this app.
    /// </summary>
    public ResolvedProfile? Resolve(string processName)
    {
        var settings = _settingsManager.Current;
        if (!settings.IsEnabled) return null;

        var key = processName.ToLowerInvariant();

        // â”€â”€ Filter check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        bool inList = settings.FilterList.Any(e => e.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (settings.FilterMode == FilterMode.Blacklist && inList)
            return null; // Blacklisted â€” pass raw scroll through

        if (settings.FilterMode == FilterMode.Whitelist && !inList)
            return null; // Not whitelisted â€” pass raw scroll through

        // â”€â”€ Profile resolution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (settings.AppOverrides.TryGetValue(key, out var appProfile))
            return appProfile.Resolve(settings.GlobalProfile);

        return settings.GlobalProfile.Resolve(settings.GlobalProfile);
    }
}
