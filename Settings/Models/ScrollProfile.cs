using System.Text.Json.Serialization;

namespace SmoothScroller.Settings.Models;

/// <summary>
/// Represents a scroll configuration profile. Can be used as global defaults
/// or as a per-app override (some fields may be null, meaning "inherit global").
/// </summary>
public class ScrollProfile
{
    // ── Physics ──────────────────────────────────────────────────────────────
    /// <summary>Pixels to scroll per wheel notch (default 120).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StepSize { get; set; }

    /// <summary>Total duration of the scroll animation in ms (default 360).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnimationTime { get; set; }

    /// <summary>Max time between two wheel events to count as acceleration (ms, default 70).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AccelerationDelta { get; set; }

    /// <summary>Maximum acceleration multiplier (default 7).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AccelerationMax { get; set; }

    /// <summary>Ratio of tail speed to head speed for easing curve (default 3).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TailToHeadRatio { get; set; }

    // ── Toggles ──────────────────────────────────────────────────────────────
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AnimationEasing { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ShiftKeyHorizontal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HorizontalSmoothness { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReverseDirection { get; set; }

    // ── Advanced ─────────────────────────────────────────────────────────────
    /// <summary>Minimum wheel speed (delta/ms) below which animation is skipped (native scroll).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SpeedThresholdForSmooth { get; set; }

    /// <summary>When true, a mouse button click while scrolling halts current animation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ClickToStop { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a resolved profile by merging this instance on top of
    /// <paramref name="fallback"/>. Null fields fall back to fallback values.
    /// </summary>
    public ResolvedProfile Resolve(ScrollProfile fallback) => new ResolvedProfile
    {
        StepSize             = StepSize             ?? fallback.StepSize             ?? 120,
        AnimationTime        = AnimationTime        ?? fallback.AnimationTime        ?? 360,
        AccelerationDelta    = AccelerationDelta    ?? fallback.AccelerationDelta    ?? 70,
        AccelerationMax      = AccelerationMax      ?? fallback.AccelerationMax      ?? 7.0,
        TailToHeadRatio      = TailToHeadRatio      ?? fallback.TailToHeadRatio      ?? 3.0,
        AnimationEasing      = AnimationEasing      ?? fallback.AnimationEasing      ?? true,
        ShiftKeyHorizontal   = ShiftKeyHorizontal   ?? fallback.ShiftKeyHorizontal   ?? true,
        HorizontalSmoothness = HorizontalSmoothness ?? fallback.HorizontalSmoothness ?? true,
        ReverseDirection     = ReverseDirection     ?? fallback.ReverseDirection     ?? false,
        SpeedThresholdForSmooth = SpeedThresholdForSmooth ?? fallback.SpeedThresholdForSmooth ?? 0.0,
        ClickToStop          = ClickToStop          ?? fallback.ClickToStop          ?? true,
    };
}

/// <summary>
/// A fully-resolved, non-nullable profile ready for the animation engine.
/// </summary>
public class ResolvedProfile
{
    public int    StepSize             { get; init; } = 120;
    public int    AnimationTime        { get; init; } = 360;
    public int    AccelerationDelta    { get; init; } = 70;
    public double AccelerationMax      { get; init; } = 7.0;
    public double TailToHeadRatio      { get; init; } = 3.0;
    public bool   AnimationEasing      { get; init; } = true;
    public bool   ShiftKeyHorizontal   { get; init; } = true;
    public bool   HorizontalSmoothness { get; init; } = true;
    public bool   ReverseDirection     { get; init; } = false;
    public double SpeedThresholdForSmooth { get; init; } = 0.0;
    public bool   ClickToStop          { get; init; } = true;
}
