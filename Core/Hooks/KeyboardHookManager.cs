using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glide.Core.Hooks;

/// <summary>
/// WH_KEYBOARD_LL hook to track Shift state (for horizontal scrolling)
/// and suppress/smooth keyboard scroll keys (Page Up/Down, arrow keys).
/// </summary>
public sealed class KeyboardHookManager : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_KEYUP       = 0x0101;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int WM_SYSKEYUP    = 0x0105;

    private const int VK_SHIFT   = 0x10;
    private const int VK_LSHIFT  = 0xA0;
    private const int VK_RSHIFT  = 0xA1;
    private const int VK_PRIOR   = 0x21; // Page Up
    private const int VK_NEXT    = 0x22; // Page Down
    private const int VK_UP      = 0x26;
    private const int VK_DOWN    = 0x28;
    private const int VK_LEFT    = 0x25;
    private const int VK_RIGHT   = 0x27;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;
    private Thread? _hookThread;

    private bool _shiftDown;
    private int  _bypassVk;

    // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public event EventHandler<bool>? ShiftStateChanged;
    public event EventHandler<KeyScrollEventArgs>? KeyScrollEvent;
    public event EventHandler<bool>? BypassHotkeyChanged;

    public bool IsShiftDown    => _shiftDown;
    public bool IsBypassActive { get; private set; }

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public KeyboardHookManager()
    {
        _proc = HookProc;
    }

    public void Install(int bypassHotkeyVk = 0)
    {
        if (IsInstalled) return;
        _bypassVk = bypassHotkeyVk;

        _hookThread = new Thread(() =>
        {
            using var module = Process.GetCurrentProcess().MainModule!;
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName!), 0);
            System.Windows.Forms.Application.Run();
        }) { IsBackground = true, Name = "KeyboardHookThread" };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public void Uninstall()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var kb  = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        int msg = (int)wParam;
        bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;

        // â”€â”€ Shift tracking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (kb.vkCode == VK_SHIFT || kb.vkCode == VK_LSHIFT || kb.vkCode == VK_RSHIFT)
        {
            if (_shiftDown != down)
            {
                _shiftDown = down;
                ShiftStateChanged?.Invoke(this, _shiftDown);
            }
        }

        // â”€â”€ Bypass hotkey tracking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (_bypassVk != 0 && kb.vkCode == _bypassVk)
        {
            bool active = down;
            if (IsBypassActive != active)
            {
                IsBypassActive = active;
                BypassHotkeyChanged?.Invoke(this, active);
            }
        }

        // â”€â”€ Keyboard scroll keys â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (down && KeyScrollEvent != null)
        {
            int scrollDelta = kb.vkCode switch
            {
                VK_PRIOR  => 3 * 120,
                VK_NEXT   => -3 * 120,
                VK_UP     => 120,
                VK_DOWN   => -120,
                VK_LEFT   => 0,
                VK_RIGHT  => 0,
                _         => 0
            };

            bool horizontal = kb.vkCode == VK_LEFT || kb.vkCode == VK_RIGHT;
            if (horizontal) scrollDelta = kb.vkCode == VK_LEFT ? 120 : -120;

            if (scrollDelta != 0)
            {
                var args = new KeyScrollEventArgs((short)scrollDelta, horizontal);
                KeyScrollEvent.Invoke(this, args);
                // Don't suppress keyboard events â€” too complex; just route through animator
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}

public class KeyScrollEventArgs : EventArgs
{
    public short Delta      { get; }
    public bool  Horizontal { get; }
    public KeyScrollEventArgs(short delta, bool horizontal) { Delta = delta; Horizontal = horizontal; }
}
