using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GiHATE;

internal static class DeviceManager
{
    private const uint CrSuccess = 0;
    private const uint CmDisablePersist = 0x00000008;

    private const uint DigcfAllClasses = 0x00000004;
    private const uint DifPropertyChange = 0x00000012;
    private const uint DicsEnable = 0x00000001;
    private const uint DicsDisable = 0x00000002;
    private const uint DicsFlagGlobal = 0x00000001;
    private const uint SpdrpConfigFlags = 0x0000000A;
    private const uint ConfigFlagDisabled = 0x00000001;
    private const int ErrorInvalidData = 13;
    private const int ErrorFileNotFound = 2;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static DeviceChangeResult DisablePersistent(string instanceId)
    {
        AppLog.Info($"Device disable requested: {instanceId}");
        var devInst = Locate(instanceId);
        var before = GetStatus(instanceId);
        if (before is not null)
            AppLog.Info($"Device status before disable: status=0x{before.Value.Status:X8}, problem={before.Value.Problem}");

        var result = CM_Disable_DevNode(devInst, CmDisablePersist);
        if (result == CrSuccess)
        {
            AppLog.Info("CM_Disable_DevNode(CM_DISABLE_PERSIST) succeeded.");
            return new DeviceChangeResult("CfgMgr32", false);
        }

        AppLog.Warn($"CM_Disable_DevNode returned {DescribeConfigRet(result)} (0x{result:X8}). Trying SetupAPI DIF_PROPERTYCHANGE/DICS_DISABLE.");

        try
        {
            SetupApiPropertyChange(instanceId, disable: true);
            AppLog.Info("SetupAPI disable request succeeded. The device change may remain pending until reboot.");
            return new DeviceChangeResult("SetupAPI", true);
        }
        catch (Exception setupEx)
        {
            AppLog.Exception($"SetupAPI live disable failed after CONFIGRET 0x{result:X8}", setupEx);
            AppLog.Warn("Scheduling the disable for next boot by setting CONFIGFLAG_DISABLED through SetupAPI.");

            try
            {
                SetPersistentDisabledFlag(instanceId, disabled: true);
                AppLog.Info("CONFIGFLAG_DISABLED is set. The GiMATE vendor HID will be disabled when Windows restarts.");
                return new DeviceChangeResult("SetupAPI ConfigFlags", true);
            }
            catch (Exception flagEx)
            {
                AppLog.Exception("Persistent CONFIGFLAG_DISABLED fallback failed", flagEx);
                throw new InvalidOperationException(
                    $"Could not disable or schedule disable of the GiMATE vendor HID device. CfgMgr32 returned {DescribeConfigRet(result)} (0x{result:X8}), the live SetupAPI change failed, and the persistent ConfigFlags fallback failed. See {AppLog.LogPath} for full details.",
                    new AggregateException(setupEx, flagEx));
            }
        }
    }

    public static DeviceChangeResult Enable(string instanceId)
    {
        AppLog.Info($"Device enable requested: {instanceId}");

        // Clear our persistent disabled bit first. This also cancels a disable
        // that was scheduled but has not reached a reboot yet.
        try
        {
            SetPersistentDisabledFlag(instanceId, disabled: false);
            AppLog.Info("CONFIGFLAG_DISABLED cleared before enable attempt.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Could not clear CONFIGFLAG_DISABLED before live enable; continuing with normal enable paths", ex);
        }

        var devInst = Locate(instanceId, allowPhantom: true);
        var result = CM_Enable_DevNode(devInst, 0);
        if (result == CrSuccess)
        {
            AppLog.Info("CM_Enable_DevNode succeeded.");
            return new DeviceChangeResult("CfgMgr32", false);
        }

        AppLog.Warn($"CM_Enable_DevNode returned {DescribeConfigRet(result)} (0x{result:X8}). Trying SetupAPI DIF_PROPERTYCHANGE/DICS_ENABLE.");

        try
        {
            SetupApiPropertyChange(instanceId, disable: false);
            AppLog.Info("SetupAPI enable request succeeded. The device change may remain pending until reboot.");
            return new DeviceChangeResult("SetupAPI", true);
        }
        catch (Exception setupEx)
        {
            AppLog.Exception($"SetupAPI live enable failed after CONFIGRET 0x{result:X8}", setupEx);

            try
            {
                SetPersistentDisabledFlag(instanceId, disabled: false);
                AppLog.Info("Persistent disabled bit is cleared. The device should return after reboot.");
                return new DeviceChangeResult("SetupAPI ConfigFlags", true);
            }
            catch (Exception flagEx)
            {
                AppLog.Exception("Persistent ConfigFlags enable fallback failed", flagEx);
                throw new InvalidOperationException(
                    $"Could not re-enable or schedule re-enable of the GiMATE vendor HID device. CfgMgr32 returned {DescribeConfigRet(result)} (0x{result:X8}), the live SetupAPI change failed, and the persistent ConfigFlags fallback failed. See {AppLog.LogPath} for full details.",
                    new AggregateException(setupEx, flagEx));
            }
        }
    }

    public static IReadOnlyList<string> FindInstanceIds(string contains)
    {
        AppLog.Info($"Searching PnP instance IDs for: {contains}");
        var result = CM_Get_Device_ID_List_SizeW(out var length, null, 0);
        if (result != CrSuccess || length == 0)
        {
            AppLog.Warn($"CM_Get_Device_ID_List_SizeW returned {DescribeConfigRet(result)} (0x{result:X8}), length={length}.");
            return Array.Empty<string>();
        }

        var buffer = new char[checked((int)length)];
        result = CM_Get_Device_ID_ListW(null, buffer, length, 0);
        if (result != CrSuccess)
        {
            AppLog.Warn($"CM_Get_Device_ID_ListW returned {DescribeConfigRet(result)} (0x{result:X8}).");
            return Array.Empty<string>();
        }

        var matches = new string(buffer)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Contains(contains, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AppLog.Info($"PnP search matched {matches.Length} device(s): {string.Join(" | ", matches)}");
        return matches;
    }

    public static (uint Status, uint Problem)? GetStatus(string instanceId)
    {
        try
        {
            var devInst = Locate(instanceId, allowPhantom: true);
            var result = CM_Get_DevNode_Status(out var status, out var problem, devInst, 0);
            if (result != CrSuccess)
            {
                AppLog.Warn($"CM_Get_DevNode_Status for '{instanceId}' returned {DescribeConfigRet(result)} (0x{result:X8}).");
                return null;
            }

            return (status, problem);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Could not query device status for '{instanceId}'", ex);
            return null;
        }
    }

    private static uint Locate(string instanceId, bool allowPhantom = false)
    {
        const uint CmLocateDevnodePhantom = 0x00000001;
        var flags = allowPhantom ? CmLocateDevnodePhantom : 0u;
        var result = CM_Locate_DevNodeW(out var devInst, instanceId, flags);
        if (result != CrSuccess)
            throw new InvalidOperationException($"Could not locate '{instanceId}'. {DescribeConfigRet(result)} (CONFIGRET 0x{result:X8}).");
        return devInst;
    }

    private static void SetupApiPropertyChange(string instanceId, bool disable)
    {
        AppLog.Info($"SetupAPI property change: {(disable ? "disable" : "enable")} '{instanceId}'");
        var (deviceInfoSet, deviceInfo) = OpenDeviceInfo(instanceId);

        try
        {
            var propertyChange = new SP_PROPCHANGE_PARAMS
            {
                ClassInstallHeader = new SP_CLASSINSTALL_HEADER
                {
                    cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                    InstallFunction = DifPropertyChange
                },
                StateChange = disable ? DicsDisable : DicsEnable,
                Scope = DicsFlagGlobal,
                HwProfile = 0
            };

            if (!SetupDiSetClassInstallParamsW(deviceInfoSet, ref deviceInfo, ref propertyChange, Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiSetClassInstallParamsW failed.");

            if (!SetupDiCallClassInstaller(DifPropertyChange, deviceInfoSet, ref deviceInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"SetupDiCallClassInstaller(DIF_PROPERTYCHANGE/{(disable ? "DICS_DISABLE" : "DICS_ENABLE")}) failed.");
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static void SetPersistentDisabledFlag(string instanceId, bool disabled)
    {
        var (deviceInfoSet, deviceInfo) = OpenDeviceInfo(instanceId);

        try
        {
            var buffer = new byte[sizeof(uint)];
            uint configFlags = 0;
            if (SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, SpdrpConfigFlags, out _, buffer, (uint)buffer.Length, out _))
            {
                configFlags = BitConverter.ToUInt32(buffer, 0);
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorInvalidData && error != ErrorFileNotFound)
                    throw new Win32Exception(error, "SetupDiGetDeviceRegistryPropertyW(SPDRP_CONFIGFLAGS) failed.");
            }

            var updated = disabled ? configFlags | ConfigFlagDisabled : configFlags & ~ConfigFlagDisabled;
            AppLog.Info($"Device ConfigFlags: 0x{configFlags:X8} -> 0x{updated:X8} ({(disabled ? "set" : "clear")} CONFIGFLAG_DISABLED)");

            var updatedBytes = BitConverter.GetBytes(updated);
            if (!SetupDiSetDeviceRegistryPropertyW(deviceInfoSet, ref deviceInfo, SpdrpConfigFlags, updatedBytes, (uint)updatedBytes.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiSetDeviceRegistryPropertyW(SPDRP_CONFIGFLAGS) failed.");
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static (IntPtr DeviceInfoSet, SP_DEVINFO_DATA DeviceInfo) OpenDeviceInfo(string instanceId)
    {
        // Do not require DIGCF_PRESENT here. Revert must be able to find a
        // disabled/non-started device and clear a pending persistent flag.
        var deviceInfoSet = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DigcfAllClasses);
        if (deviceInfoSet == InvalidHandleValue)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiGetClassDevsW failed.");

        var deviceInfo = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
        if (SetupDiOpenDeviceInfoW(deviceInfoSet, instanceId, IntPtr.Zero, 0, ref deviceInfo))
            return (deviceInfoSet, deviceInfo);

        var error = Marshal.GetLastWin32Error();
        SetupDiDestroyDeviceInfoList(deviceInfoSet);
        throw new Win32Exception(error, $"SetupDiOpenDeviceInfoW failed for '{instanceId}'.");
    }

    private static string DescribeConfigRet(uint value) => value switch
    {
        0x00 => "CR_SUCCESS",
        0x05 => "CR_INVALID_DEVNODE",
        0x0D => "CR_NO_SUCH_DEVNODE",
        0x13 => "CR_FAILURE",
        0x15 => "CR_CREATE_BLOCKED",
        0x17 => "CR_REMOVE_VETOED",
        0x1D => "CR_REGISTRY_ERROR",
        0x33 => "CR_ACCESS_DENIED",
        0x34 => "CR_CALL_NOT_IMPLEMENTED",
        _ => "CONFIGRET error"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceId, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Disable_DevNode(uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Enable_DevNode(uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_List_SizeW(out uint pulLen, string? pszFilter, uint ulFlags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_ListW(string? pszFilter, [Out] char[] buffer, uint bufferLen, uint ulFlags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevsW(IntPtr ClassGuid, string? Enumerator, IntPtr hwndParent, uint Flags);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiOpenDeviceInfoW(IntPtr DeviceInfoSet, string DeviceInstanceId, IntPtr hwndParent, uint OpenFlags, ref SP_DEVINFO_DATA DeviceInfoData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiSetClassInstallParamsW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_PROPCHANGE_PARAMS ClassInstallParams, int ClassInstallParamsSize);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiCallClassInstaller(uint InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, [Out] byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiSetDeviceRegistryPropertyW(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, byte[] PropertyBuffer, uint PropertyBufferSize);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
}

internal sealed record DeviceChangeResult(string Method, bool RestartRequired);
