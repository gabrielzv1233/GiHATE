using System.Runtime.InteropServices;
using System.Text;

namespace GiHATE;

internal sealed class RawInputCapture
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidiDeviceInfo = 0x2000000B;
    private const uint RimTypeKeyboard = 1;
    private const uint RimTypeHid = 2;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevPageOnly = 0x00000020;

    public event Action<KeyboardRawEvent>? Keyboard;
    public event Action<HidRawEvent>? Hid;

    public void Register(IntPtr hwnd)
    {
        var devices = new List<RAWINPUTDEVICE>
        {
            new() { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RidevInputSink, hwndTarget = hwnd }
        };

        foreach (var page in EnumerateVendorUsagePages())
            devices.Add(new RAWINPUTDEVICE { usUsagePage = page, usUsage = 0, dwFlags = RidevInputSink | RidevPageOnly, hwndTarget = hwnd });

        if (!devices.Any(x => x.usUsagePage == 0xFF02))
            devices.Add(new RAWINPUTDEVICE { usUsagePage = 0xFF02, usUsage = 0, dwFlags = RidevInputSink | RidevPageOnly, hwndTarget = hwnd });

        if (!RegisterRawInputDevices(devices.ToArray(), (uint)devices.Count, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            throw new InvalidOperationException($"RegisterRawInputDevices failed: {Marshal.GetLastWin32Error()}");
    }

    public bool ProcessMessage(Message m)
    {
        if (m.Msg != WmInput) return false;

        uint size = 0;
        GetRawInputData(m.LParam, RidInput, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0) return true;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(m.LParam, RidInput, buffer, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != size) return true;
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            var devicePath = GetDeviceName(header.hDevice);

            if (header.dwType == RimTypeKeyboard)
            {
                var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(buffer, Marshal.SizeOf<RAWINPUTHEADER>()));
                Keyboard?.Invoke(new KeyboardRawEvent(DateTime.UtcNow, devicePath, ToInstanceId(devicePath), keyboard.MakeCode, keyboard.Flags, keyboard.VKey, keyboard.Message));
            }
            else if (header.dwType == RimTypeHid)
            {
                var offset = Marshal.SizeOf<RAWINPUTHEADER>();
                var hid = Marshal.PtrToStructure<RAWHID_HEADER>(IntPtr.Add(buffer, offset));
                var byteCount = checked((int)(hid.dwSizeHid * hid.dwCount));
                var bytes = new byte[byteCount];
                Marshal.Copy(IntPtr.Add(buffer, offset + Marshal.SizeOf<RAWHID_HEADER>()), bytes, 0, byteCount);
                var info = GetDeviceInfo(header.hDevice);
                Hid?.Invoke(new HidRawEvent(DateTime.UtcNow, devicePath, ToInstanceId(devicePath), info.VendorId, info.ProductId, info.UsagePage, info.Usage, bytes));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return true;
    }

    private static IEnumerable<ushort> EnumerateVendorUsagePages()
    {
        uint count = 0;
        var entrySize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
        if (GetRawInputDeviceList(IntPtr.Zero, ref count, entrySize) != 0 || count == 0) yield break;

        var memory = Marshal.AllocHGlobal(checked((int)(count * entrySize)));
        try
        {
            var actual = count;
            if (GetRawInputDeviceList(memory, ref actual, entrySize) == uint.MaxValue) yield break;
            var seen = new HashSet<ushort>();
            for (var i = 0; i < actual; i++)
            {
                var item = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(IntPtr.Add(memory, checked((int)(i * entrySize))));
                if (item.dwType != RimTypeHid) continue;
                var info = GetDeviceInfo(item.hDevice);
                if (info.UsagePage >= 0xFF00 && seen.Add(info.UsagePage)) yield return info.UsagePage;
            }
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    private static string GetDeviceName(IntPtr device)
    {
        uint chars = 0;
        GetRawInputDeviceInfoW(device, RidiDeviceName, IntPtr.Zero, ref chars);
        if (chars == 0) return "";
        var sb = new StringBuilder((int)chars + 1);
        return GetRawInputDeviceInfoW(device, RidiDeviceName, sb, ref chars) == uint.MaxValue ? "" : sb.ToString();
    }

    private static HidDeviceInfo GetDeviceInfo(IntPtr device)
    {
        var info = new RID_DEVICE_INFO { cbSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>() };
        uint size = info.cbSize;
        if (GetRawInputDeviceInfoW(device, RidiDeviceInfo, ref info, ref size) == uint.MaxValue || info.dwType != RimTypeHid) return default;
        return new HidDeviceInfo((ushort)info.hid.dwVendorId, (ushort)info.hid.dwProductId, info.hid.usUsagePage, info.hid.usUsage);
    }

    public static string ToInstanceId(string devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath)) return "";
        var value = devicePath.StartsWith("\\\\?\\", StringComparison.Ordinal) ? devicePath[4..] : devicePath;
        var classIndex = value.IndexOf("#{", StringComparison.Ordinal);
        if (classIndex >= 0) value = value[..classIndex];
        return value.Replace('#', '\\');
    }

    [StructLayout(LayoutKind.Sequential)] private struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }
    [StructLayout(LayoutKind.Sequential)] private struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }
    [StructLayout(LayoutKind.Sequential)] private struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }
    [StructLayout(LayoutKind.Sequential)] private struct RAWKEYBOARD { public ushort MakeCode; public ushort Flags; public ushort Reserved; public ushort VKey; public uint Message; public uint ExtraInformation; }
    [StructLayout(LayoutKind.Sequential)] private struct RAWHID_HEADER { public uint dwSizeHid; public uint dwCount; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] private struct RID_DEVICE_INFO { [FieldOffset(0)] public uint cbSize; [FieldOffset(4)] public uint dwType; [FieldOffset(8)] public HIDINFO hid; }
    [StructLayout(LayoutKind.Sequential)] private struct HIDINFO { public uint dwVendorId; public uint dwProductId; public uint dwVersionNumber; public ushort usUsagePage; public ushort usUsage; }
    private readonly record struct HidDeviceInfo(ushort VendorId, ushort ProductId, ushort UsagePage, ushort Usage);

    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, StringBuilder pData, ref uint pcbSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, ref RID_DEVICE_INFO pData, ref uint pcbSize);
}

internal sealed record KeyboardRawEvent(DateTime Time, string DevicePath, string InstanceId, ushort ScanCode, ushort Flags, ushort VKey, uint Message)
{
    public bool IsKeyDown => Message is 0x0100 or 0x0104;
}

internal sealed record HidRawEvent(DateTime Time, string DevicePath, string InstanceId, ushort VendorId, ushort ProductId, ushort UsagePage, ushort Usage, byte[] Bytes)
{
    public string Hex => BitConverter.ToString(Bytes).Replace('-', ' ');
}
