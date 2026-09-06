using Microsoft.Win32;

namespace GiHATE;

internal static class ScancodeMap
{
    private const string RegistryPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";
    private const string ValueName = "Scancode Map";
    public static readonly IReadOnlyDictionary<string, ushort> Targets = new Dictionary<string, ushort>
    {
        ["F13"] = 0x64, ["F14"] = 0x65, ["F15"] = 0x66, ["F16"] = 0x67,
        ["F17"] = 0x68, ["F18"] = 0x69, ["F19"] = 0x6A, ["F20"] = 0x6B,
        ["F21"] = 0x6C, ["F22"] = 0x6D, ["F23"] = 0x6E, ["F24"] = 0x76,
        ["Disabled"] = 0x0000
    };

    public static (bool Exists, ushort Destination) GetSourceMapping(ushort source)
    {
        var mappings = ReadMappings();
        var result = mappings.TryGetValue(source, out var destination) ? (true, destination) : (false, (ushort)0);
        AppLog.Info(result.Item1
            ? $"Existing scancode mapping: source 0x{source:X4} -> destination 0x{result.Item2:X4}"
            : $"No existing scancode mapping for source 0x{source:X4}");
        return result;
    }

    public static void SetMapping(ushort source, ushort destination)
    {
        AppLog.Info($"Setting system-wide scancode mapping: source 0x{source:X4} -> destination 0x{destination:X4}");
        var mappings = ReadMappings();
        mappings[source] = destination;
        WriteMappings(mappings);
        AppLog.Info("Scancode Map registry value written successfully. Reboot is required before Windows uses the new mapping.");
    }

    public static void RestoreSource(ushort source, bool existed, ushort originalDestination)
    {
        AppLog.Info($"Restoring source scan 0x{source:X4}. Had original mapping={existed}, original destination=0x{originalDestination:X4}");
        var mappings = ReadMappings();
        mappings.Remove(source);
        if (existed) mappings[source] = originalDestination;
        WriteMappings(mappings);
        AppLog.Info("Previous scancode mapping state restored. Reboot is required before Windows uses it.");
    }

    private static Dictionary<ushort, ushort> ReadMappings()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, false);
        var bytes = key?.GetValue(ValueName) as byte[];
        var mappings = new Dictionary<ushort, ushort>();
        if (bytes is null || bytes.Length < 16)
        {
            AppLog.Info("Scancode Map registry value is not present or empty.");
            return mappings;
        }

        var count = BitConverter.ToUInt32(bytes, 8);
        if (count < 1) return mappings;
        for (var i = 0; i < count - 1; i++)
        {
            var offset = 12 + i * 4;
            if (offset + 3 >= bytes.Length) break;
            var destination = BitConverter.ToUInt16(bytes, offset);
            var source = BitConverter.ToUInt16(bytes, offset + 2);
            if (source != 0) mappings[source] = destination;
        }

        AppLog.Info($"Read {mappings.Count} existing Scancode Map mapping(s): {string.Join(", ", mappings.OrderBy(x => x.Key).Select(x => $"0x{x.Key:X4}->0x{x.Value:X4}"))}");
        return mappings;
    }

    private static void WriteMappings(Dictionary<ushort, ushort> mappings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((uint)mappings.Count + 1);
        foreach (var pair in mappings.OrderBy(x => x.Key))
        {
            writer.Write(pair.Value);
            writer.Write(pair.Key);
        }
        writer.Write(0u);

        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, true) ?? throw new InvalidOperationException("Could not open the keyboard-layout registry key.");
        key.SetValue(ValueName, stream.ToArray(), RegistryValueKind.Binary);
    }
}
