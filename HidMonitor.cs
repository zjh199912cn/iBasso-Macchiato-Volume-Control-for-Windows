using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MacchiatoTray;

public static class HidMonitor
{
    static readonly Guid HidGuid = new("745a17a0-74d3-11d0-b6fe-00a0c90f57da");

    const int DIGCF_PRESENT = 0x02;
    const int DIGCF_DEVICEINTERFACE = 0x10;

    // ── SetupAPI ──
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern nint SetupDiGetClassDevs(ref Guid ClassGuid, nint Enumerator,
        nint hwndParent, int Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInfo(nint DeviceInfoSet,
        int MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceInstanceId(nint DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder? DeviceInstanceId,
        int DeviceInstanceIdSize, out int RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(nint DeviceInfoSet);

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public int DevInst;
        public nint Reserved;
    }

    // ── RegisterDeviceNotification ──
    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint RegisterDeviceNotification(nint hRecipient,
        ref DEV_BROADCAST_DEVICEINTERFACE NotificationFilter, int Flags);

    [DllImport("user32.dll")]
    public static extern bool UnregisterDeviceNotification(nint Handle);

    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_DEVICEINTERFACE
    {
        public int dbcc_size;
        public int dbcc_devicetype;
        public int dbcc_reserved;
        public Guid dbcc_classguid;
    }

    // ── 常量 ──
    public const int DBT_DEVTYP_DEVICEINTERFACE = 5;
    public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
    public const int WM_DEVICECHANGE = 0x0219;
    public const int DBT_DEVICEARRIVAL = 0x8000;
    public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    // ── 注册设备插拔通知 ──
    public static nint Register(IntPtr hWnd)
    {
        var filter = new DEV_BROADCAST_DEVICEINTERFACE
        {
            dbcc_size = Marshal.SizeOf<DEV_BROADCAST_DEVICEINTERFACE>(),
            dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
            dbcc_classguid = HidGuid,
        };
        return RegisterDeviceNotification(hWnd, ref filter, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    // ── 枚举 HID 设备 ──
    public static bool IsDevicePresent(int vendorId, int[] productIds)
    {
        var guid = HidGuid;
        nint deviceInfoSet = SetupDiGetClassDevs(ref guid, nint.Zero, nint.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == (nint)(-1))
            return false;

        try
        {
            for (int index = 0; ; index++)
            {
                var devInfoData = new SP_DEVINFO_DATA
                {
                    cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>()
                };

                if (!SetupDiEnumDeviceInfo(deviceInfoSet, index, ref devInfoData))
                    break;

                if (!SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfoData,
                        null, 0, out int requiredSize))
                {
                    if (Marshal.GetLastWin32Error() != 122)
                        continue;
                }

                if (requiredSize <= 0) continue;

                var sb = new StringBuilder(requiredSize);
                if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfoData,
                        sb, sb.Capacity, out _))
                {
                    if (TryParseVidPid(sb.ToString(), out int vid, out int pid) &&
                        vid == vendorId && productIds.Contains(pid))
                        return true;
                }
            }
            return false;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    // ── 解析 VID/PID ──
    static bool TryParseVidPid(string instanceId, out int vid, out int pid)
    {
        vid = 0;
        pid = 0;
        var match = Regex.Match(instanceId,
            @"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})");
        if (!match.Success) return false;
        vid = Convert.ToInt32(match.Groups[1].Value, 16);
        pid = Convert.ToInt32(match.Groups[2].Value, 16);
        return true;
    }
}
