using System.Runtime.InteropServices;

namespace GiHATE;

internal static class DeviceManager
{
    private const uint CrSuccess = 0;
    private const uint CmDisablePersist = 0x00000008;

    public static void DisablePersistent(string instanceId)
    {
        var devInst = Locate(instanceId);
        var result = CM_Disable_DevNode(devInst, CmDisablePersist);
        if (result != CrSuccess) throw new InvalidOperationException($"CM_Disable_DevNode failed with CONFIGRET 0x{result:X8}.");
    }

    public static void Enable(string instanceId)
    {
        var devInst = Locate(instanceId);
        var result = CM_Enable_DevNode(devInst, 0);
        if (result != CrSuccess) throw new InvalidOperationException($"CM_Enable_DevNode failed with CONFIGRET 0x{result:X8}.");
    }

    public static IReadOnlyList<string> FindInstanceIds(string contains)
    {
        var result = CM_Get_Device_ID_List_SizeW(out var length, null, 0);
        if (result != CrSuccess || length == 0) return Array.Empty<string>();
        var buffer = new char[length];
        result = CM_Get_Device_ID_ListW(null, buffer, length, 0);
        if (result != CrSuccess) return Array.Empty<string>();
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains(contains, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static (uint Status, uint Problem)? GetStatus(string instanceId)
    {
        try
        {
            var devInst = Locate(instanceId);
            var result = CM_Get_DevNode_Status(out var status, out var problem, devInst, 0);
            return result == CrSuccess ? (status, problem) : null;
        }
        catch { return null; }
    }

    private static uint Locate(string instanceId)
    {
        var result = CM_Locate_DevNodeW(out var devInst, instanceId, 0);
        if (result != CrSuccess) throw new InvalidOperationException($"Could not locate '{instanceId}'. CONFIGRET 0x{result:X8}.");
        return devInst;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceId, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Disable_DevNode(uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Enable_DevNode(uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_Status(out uint pulStatus, out uint pulProblemNumber, uint dnDevInst, uint ulFlags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_List_SizeW(out uint pulLen, string? pszFilter, uint ulFlags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_ID_ListW(string? pszFilter, [Out] char[] buffer, uint bufferLen, uint ulFlags);
}
