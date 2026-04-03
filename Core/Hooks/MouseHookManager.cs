using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glide.Core.Hooks;

/// <summary>
/// Installs a WH_MOUSE_LL hook to intercept wheel events before they reach any window.
/// Raises <see cref="WheelEvent"/> for each intercepted tick; suppresses the raw event.
/// </summary>
public sealed class MouseHookManager : IDisposable
{
    // â”€â”€ P/Invoke â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL   = 0x020A;
    private const int WM_MOUSEHWHEEL  = 0x020E;
    private const int WM_LBUTTONDOWN  = 0x0201;
    private const int WM_RBUTTONDOWN  = 0x0204;
    private const int WM_MBUTTONDOWN  = 0x0207;
    private const int WM_MBUTTONUP    = 0x020A; // reuse is OK, actual value differs
    private const int WM_XBUTTONDOWN  = 0x020B;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    // Injected messages carry this extra info to avoid re-entrancy
    private const int INJECTED_EXTRA = 0x12345678;

    // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly LowLevelMouseProc _proc;
    private Thread? _hookThread;
    private volatile bool _running;

    // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>Raised on every wheel tick. Return value from subscriber ignored.</summary>
    public event EventHandler<WheelEventArgs>? WheelEvent;
    /// <summary>Raised on any mouse button press (for click-to-stop).</summary>
    public event EventHandler? AnyButtonDown;
    /// <summary>Raised when middle-button is pressed (for autoscroll / drag-scroll).</summary>
    public event EventHandler<System.Drawing.Point>? MiddleButtonDown;
    /// <summary>Raised when middle-button is released.</summary>
    public event EventHandler? MiddleButtonUp;

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public MouseHookManager()
    {
        _proc = HookProc; // keep delegate alive
    }

    // â”€â”€ Install / Uninstall â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void Install()
    {
        if (IsInstalled) return;
        _running = true;
        _hookThread = new Thread(HookThreadProc) { IsBackground = true, Name = "MouseHookThread" };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public void Uninstall()
    {
        _running = false;
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private void HookThreadProc()
    {
        using var module = Process.GetCurrentProcess().MainModule!;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module.ModuleName!), 0);

        // Run a message pump so the hook receives messages
        System.Windows.Forms.Application.Run();
    }

    // â”€â”€ Hook Callback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

        // Guard: ignore our own injected events
        if (ms.dwExtraInfo.ToInt32() == INJECTED_EXTRA)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        int msg = (int)wParam;

        if (msg == WM_MOUSEWHEEL || msg == WM_MOUSEHWHEEL)
        {
            short delta = (short)((ms.mouseData >> 16) & 0xFFFF);
            bool horizontal = msg == WM_MOUSEHWHEEL;

            var args = new WheelEventArgs(delta, horizontal, ms.time);
            WheelEvent?.Invoke(this, args);

            // Suppress raw event so only our synthetic ones reach the target
            if (args.Suppress) return new IntPtr(1);
        }
        else if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_XBUTTONDOWN)
        {
            AnyButtonDown?.Invoke(this, EventArgs.Empty);
        }
        else if (msg == WM_MBUTTONDOWN)
        {
            AnyButtonDown?.Invoke(this, EventArgs.Empty);
            MiddleButtonDown?.Invoke(this, new System.Drawing.Point(ms.pt.x, ms.pt.y));
        }
        else if ((int)wParam == 0x020A) // WM_MBUTTONUP
        {
            MiddleButtonUp?.Invoke(this, EventArgs.Empty);
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}

public class WheelEventArgs : EventArgs
{
    public short Delta     { get; }
    public bool Horizontal { get; }
    public int  Timestamp  { get; }
    /// <summary>Set to true to suppress the raw wheel event.</summary>
    public bool Suppress   { get; set; } = true;

    public WheelEventArgs(short delta, bool horizontal, int timestamp)
    {
        Delta = delta; Horizontal = horizontal; Timestamp = timestamp;
    }
}
