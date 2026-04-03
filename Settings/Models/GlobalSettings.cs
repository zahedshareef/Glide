using System.Text.Json.Serialization;

namespace Glide.Settings.Models;

public class GlobalSettings
{
    // â”€â”€ App-level toggles â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool IsEnabled       { get; set; } = true;
    public bool StartMinimized  { get; set; } = true;
    public bool AutoStart       { get; set; } = false;
    public bool DisableTouchpad { get; set; } = true;
    public bool FrameRateAdaptive { get; set; } = false;
    public bool ScaleWithDpi    { get; set; } = true;
    public bool DiagnosticsMode { get; set; } = false;

    /// <summary>Hotkey (VK code) that passes raw scroll through while held. 0 = disabled.</summary>
    public int BypassHotkeyVk { get; set; } = 0x12; // VK_MENU = Alt

    // â”€â”€ Global profile (the fallback for all apps) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ScrollProfile GlobalProfile { get; set; } = new ScrollProfile
    {
        StepSize             = 120,
        AnimationTime        = 360,
        AccelerationDelta    = 70,
        AccelerationMax      = 7.0,
        TailToHeadRatio      = 3.0,
        AnimationEasing      = true,
        ShiftKeyHorizontal   = true,
        HorizontalSmoothness = true,
        ReverseDirection     = false,
        SpeedThresholdForSmooth = 0.0,
        ClickToStop          = true,
    };

    // â”€â”€ Per-app overrides â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>
    /// Key = process name without extension, lower-cased (e.g. "notepad", "devenv").
    /// Value = partial profile â€” only non-null fields override the global profile.
    /// </summary>
    public Dictionary<string, ScrollProfile> AppOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // â”€â”€ Blacklist / Whitelist â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>
    /// When Mode = Blacklist: smooth scroll ALL apps EXCEPT those listed.
    /// When Mode = Whitelist: smooth scroll ONLY listed apps.
    /// </summary>
    public FilterMode FilterMode { get; set; } = FilterMode.Blacklist;
    public List<string> FilterList { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterMode
{
    Blacklist,
    Whitelist
}
