using System.Runtime.InteropServices;

namespace SmoothScroller.Core.Input;

/// <summary>
/// Detects whether a HID device that generates wheel events is a touchpad
/// (which has its own inertia) vs. a discrete mouse wheel.
/// Uses the Raw Input API to enumerate connected HID devices.
/// </summary>
public sealed class TouchpadDetector
{
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIM_TYPEMOUSE   = 0;
    private const uint RIM_TYPEHID     = 2;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RAWINPUTDEVICELIST[]? pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }

    private const uint RIDI_DEVICENAME = 0x20000007;
    private const uint RIDI_DEVICEINFO = 0x2000000b;

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO
    {
        public uint cbSize;
        public uint dwType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] data; // union — large enough for all sub-types
    }

    private readonly HashSet<string> _touchpadPaths = new(StringComparer.OrdinalIgnoreCase);

    public TouchpadDetector() => Refresh();

    /// <summary>Re-enumerate HID devices (call after WM_INPUT_DEVICE_CHANGE).</summary>
    public void Refresh()
    {
        _touchpadPaths.Clear();
        uint count = 0;
        GetRawInputDeviceList(null, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());
        if (count == 0) return;

        var list = new RAWINPUTDEVICELIST[count];
        GetRawInputDeviceList(list, ref count, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());

        foreach (var item in list)
        {
            // Only HID devices can be touchpads
            if (item.dwType != RIM_TYPEHID) continue;

            uint size = 0;
            GetRawInputDeviceInfo(item.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0) continue;

            IntPtr buf = Marshal.AllocHGlobal((int)size * 2);
            try
            {
                GetRawInputDeviceInfo(item.hDevice, RIDI_DEVICENAME, buf, ref size);
                string path = Marshal.PtrToStringUni(buf) ?? "";

                // Touchpad device paths typically contain "\\?\HID" with a "VID_xxxx&PID_xxxx" and
                // usage page 0x0D (Digitizer) or usage 0x05 (Touch Pad).
                // Simple heuristic: check device name for "PRECISION" or "TOUCHPAD".
                if (path.Contains("PRECISION", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("TOUCHPAD",  StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("ELAN",      StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("SYNAPT",    StringComparison.OrdinalIgnoreCase))
                {
                    _touchpadPaths.Add(path);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    public bool HasTouchpad => _touchpadPaths.Count > 0;
}
