using Glide.Core.Input;
using Glide.Core.Window;
using Glide.Settings.Models;

namespace Glide.Core.Engine;

/// <summary>
/// Physics-based scroll animation engine.
/// Receives wheel-delta impulses and dispatches smooth, eased synthetic wheel events
/// via a high-frequency DispatcherTimer.
/// </summary>
public sealed class ScrollAnimator : IDisposable
{
    // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly object _lock = new();

    // Vertical accumulator
    private double _vPending;    // total pixels still to scroll (vertical)
    private double _vRemainder;  // sub-pixel carry-over

    // Horizontal accumulator
    private double _hPending;
    private double _hRemainder;

    // Acceleration tracking
    private int    _lastWheelTime;
    private int    _accelCount;

    // Animation timing
    private DateTime _animStart;
    private double   _vTotal;    // total distance for current animation wave
    private double   _hTotal;

    // High-freq timer
    private System.Windows.Threading.DispatcherTimer? _timer;
    private ResolvedProfile _profile;
    private bool _scaleWithDpi;

    // â”€â”€ Config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const int TICK_MS = 8; // ~120 fps tick rate

    public ScrollAnimator()
    {
        _profile = new ResolvedProfile();
    }

    public void ApplyProfile(ResolvedProfile profile, bool scaleWithDpi)
    {
        lock (_lock)
        {
            _profile     = profile;
            _scaleWithDpi = scaleWithDpi;
        }
    }

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Called by MouseHookManager when a raw wheel event fires.</summary>
    public void OnWheel(short rawDelta, bool horizontal, int timestamp)
    {
        lock (_lock)
        {
            var p = _profile;

            // Reverse direction
            double delta = p.ReverseDirection ? -rawDelta : rawDelta;

            // Shift â†’ horizontal
            if (!horizontal && p.ShiftKeyHorizontal)
            {
                // resolved by caller (KeyboardHookManager shift state)
            }

            // Acceleration
            int elapsed = timestamp - _lastWheelTime;
            _lastWheelTime = timestamp;

            if (elapsed > 0 && elapsed <= p.AccelerationDelta)
                _accelCount = Math.Min(_accelCount + 1, (int)p.AccelerationMax);
            else
                _accelCount = 1;

            double accel = Math.Min(_accelCount, p.AccelerationMax);

            // Step size (in px, scaled by DPI if enabled)
            double stepPx = p.StepSize * accel;
            if (_scaleWithDpi)
                stepPx *= DpiHelper.GetForegroundScaleFactor();

            double impulse = (delta / 120.0) * stepPx;

            if (horizontal)
            {
                if (p.HorizontalSmoothness)
                    _hPending += impulse;
                else
                    _hPending += impulse; // always add; HorizontalSmoothness controls easing shape below
            }
            else
            {
                _vPending += impulse;
            }

            _animStart = DateTime.UtcNow;
            _vTotal    = _vPending;
            _hTotal    = _hPending;

            EnsureTimerRunning();
        }
    }

    /// <summary>Immediately halts in-flight animation (click-to-stop).</summary>
    public void StopAnimation()
    {
        lock (_lock)
        {
            _vPending = _hPending = 0;
            _vTotal   = _hTotal   = 0;
            _vRemainder = _hRemainder = 0;
            _accelCount = 0;
        }
    }

    // â”€â”€ Timer loop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private void EnsureTimerRunning()
    {
        // Must be called on the UI thread for DispatcherTimer
        if (_timer != null && _timer.IsEnabled) return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_timer == null)
            {
                _timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(TICK_MS)
                };
                _timer.Tick += OnTick;
            }
            _timer.Start();
        });
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double vSend, hSend;
        ResolvedProfile profile;
        DateTime animStart;
        double vTotal, hTotal;

        lock (_lock)
        {
            profile   = _profile;
            animStart = _animStart;
            vTotal    = _vTotal;
            hTotal    = _hTotal;

            if (_vPending == 0 && _hPending == 0)
            {
                _timer?.Stop();
                return;
            }

            double elapsed = (DateTime.UtcNow - animStart).TotalMilliseconds;
            double t = Math.Min(elapsed / profile.AnimationTime, 1.0);

            double ease = profile.AnimationEasing ? EaseOutCubic(t) : t;

            // How much of the total distance should have been sent by now?
            double vSoFar  = vTotal * ease;
            double hSoFar  = hTotal * ease;

            // We already sent (vTotal - _vPending), so this tick sends:
            double vRaw = vSoFar - (vTotal  - _vPending);
            double hRaw = hSoFar - (hTotal  - _hPending);

            // Add carry-over, convert to int
            _vRemainder += vRaw;
            _hRemainder += hRaw;

            vSend = Math.Truncate(_vRemainder);
            hSend = Math.Truncate(_hRemainder);

            _vRemainder -= vSend;
            _hRemainder -= hSend;

            _vPending -= vSend;
            _hPending -= hSend;

            // Clamp: don't overshoot
            if (Math.Sign(vSend) != Math.Sign(_vPending) && _vPending != 0 && vSend != 0)
            {
                vSend    = _vPending;
                _vPending = 0;
            }
            if (Math.Sign(hSend) != Math.Sign(_hPending) && _hPending != 0 && hSend != 0)
            {
                hSend    = _hPending;
                _hPending = 0;
            }

            if (t >= 1.0)
            {
                // Drain any remaining amount
                vSend     += _vPending;
                hSend     += _hPending;
                _vPending  = 0;
                _hPending  = 0;
            }
        }

        if (vSend != 0) SyntheticInputDispatcher.SendWheel((int)vSend, horizontal: false);
        if (hSend != 0) SyntheticInputDispatcher.SendWheel((int)hSend, horizontal: true);

        lock (_lock)
        {
            if (_vPending == 0 && _hPending == 0)
                _timer?.Stop();
        }
    }

    // â”€â”€ Easing functions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    public void Dispose()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() => _timer?.Stop());
    }
}
