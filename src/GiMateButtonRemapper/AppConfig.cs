using System.Text.Json;

namespace GiHATE;

public sealed class AppConfig
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public DetectedProfile? Profile { get; set; }
    public string TargetKey { get; set; } = "F24";
    public bool Applied { get; set; }
    public bool HadOriginalSourceMapping { get; set; }
    public ushort OriginalSourceDestination { get; set; }

    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GiHATE");
    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                AppLog.Info("No existing config file found. Starting unconfigured.");
                return new AppConfig();
            }

            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions()) ?? new AppConfig();
            AppLog.Info($"Config loaded. Version={config.Version}, Applied={config.Applied}, Target={config.TargetKey}, Profile={(config.Profile is null ? "none" : config.Profile.DisplayName)}");
            if (config.Profile is not null)
            {
                AppLog.Info($"Config keyboard instance: {config.Profile.KeyboardInstanceId}");
                AppLog.Info($"Config vendor instance: {config.Profile.VendorInstanceId}");
                AppLog.Info($"Config vendor HID: usage=0x{config.Profile.VendorUsagePage:X4}/0x{config.Profile.VendorUsage:X2}, report={config.Profile.VendorReportHex}");
            }
            return config;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to load config. Starting with a new config", ex);
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions()));
        AppLog.Info($"Config saved. Applied={Applied}, Target={TargetKey}, Profile={(Profile is null ? "none" : Profile.DisplayName)}");
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}

public sealed class DetectedProfile
{
    public string KeyboardDevicePath { get; set; } = "";
    public string KeyboardInstanceId { get; set; } = "";
    public string VendorDevicePath { get; set; } = "";
    public string VendorInstanceId { get; set; } = "";
    public ushort SourceScanCode { get; set; }
    public ushort VendorUsagePage { get; set; }
    public ushort VendorUsage { get; set; }
    public string VendorReportHex { get; set; } = "";
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public string DisplayName => VendorId != 0 || ProductId != 0 ? $"VID_{VendorId:X4} PID_{ProductId:X4}, scan 0x{SourceScanCode:X2}" : $"scan 0x{SourceScanCode:X2}";
}
