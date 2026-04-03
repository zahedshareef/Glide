using System.Runtime.InteropServices;

namespace Glide.Core.Input;

/// <summary>
/// Wraps the Win32 <c>SendInput</c> API to inject synthetic mouse-wheel events
/// that are distinguishable from real events via a custom <c>dwExtraInfo</c> value.
/// </summary>
public static class SyntheticInputDispatcher
{
    private const int INJECTED_EXTRA = 0x12345678;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;       // INPUT_MOUSE = 0
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint MOUSEEVENTF_WHEEL  = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x01000;
    private const uint INPUT_MOUSE        = 0;

    /// <summary>
    /// Sends a synthetic vertical or horizontal wheel event.
    /// </summary>
    /// <param name="delta">Positive = up/right, negative = down/left.</param>
    /// <param name="horizontal">True for MOUSEEVENTF_HWHEEL.</param>
    public static void SendWheel(int delta, bool horizontal)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = 0, dy = 0,
                mouseData = delta,
                dwFlags   = horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL,
                time      = 0,
                dwExtraInfo = new IntPtr(INJECTED_EXTRA)
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
