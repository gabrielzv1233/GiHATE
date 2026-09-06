using System.Text.Json;
using System.Text.Json.Serialization;

namespace GiHATE;

public sealed class AppConfig
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;
    public DetectedProfile? Profile { get; set; }
    public string TargetKey { get; set; } = "F24";
    public bool Applied { get; set; }
    public bool OriginalMappingCaptured { get; set; }
    public bool HadOriginalSourceMapping { get; set; }
    public ushort OriginalSourceDestination { get; set; }

    public bool RestartRequired { get; set; }
    public string RestartBootId { get; set; } = "";
    public string RestartReason { get; set; } = "";
    public DateTimeOffset? RestartRequestedAt { get; set; }
    public bool PendingExpectedHidDisabled { get; set; }
    public bool PendingExpectedMappingExists { get; set; }
    public ushort PendingExpectedMappingDestination { get; set; }
    public string VerificationTaskName { get; set; } = "";

    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GiHATE");
    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    [JsonIgnore]
    public bool IsRestartPending => RestartRequired && BootSession.IsSameBoot(RestartBootId);

    [JsonIgnore]
    public bool RestartOccurred => RestartRequired && !string.IsNullOrWhiteSpace(RestartBootId) && !BootSession.IsSameBoot(RestartBootId);

    public void MarkRestartRequired(
        string reason,
        bool expectedHidDisabled,
        bool expectedMappingExists,
        ushort expectedMappingDestination)
    {
        RestartRequired = true;
        RestartBootId = BootSession.CurrentId;
        RestartReason = reason;
        RestartRequestedAt = DateTimeOffset.Now;
        PendingExpectedHidDisabled = expectedHidDisabled;
        PendingExpectedMappingExists = expectedMappingExists;
        PendingExpectedMappingDestination = expectedMappingDestination;
        AppLog.Info($"Restart marked required. Reason={reason}, BootId={RestartBootId}, ExpectedHidDisabled={expectedHidDisabled}, ExpectedMappingExists={expectedMappingExists}, ExpectedMappingDestination=0x{expectedMappingDestination:X4}");
    }

    public void ClearRestartRequired()
    {
        AppLog.Info($"Clearing restart-required state. Previous reason={RestartReason}, BootId={RestartBootId}");
        RestartRequired = false;
        RestartBootId = "";
        RestartReason = "";
        RestartRequestedAt = null;
        PendingExpectedHidDisabled = false;
        PendingExpectedMappingExists = false;
        PendingExpectedMappingDestination = 0;
        VerificationTaskName = "";
    }

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

            if (config.Version < 2 && config.Applied)
                config.OriginalMappingCaptured = true;

            config.Version = CurrentVersion;
            AppLog.Info($"Config loaded. Version={config.Version}, Applied={config.Applied}, OriginalMappingCaptured={config.OriginalMappingCaptured}, Target={config.TargetKey}, RestartRequired={config.RestartRequired}, RestartPending={config.IsRestartPending}, Profile={(config.Profile is null ? "none" : config.Profile.DisplayName)}");
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
        Version = CurrentVersion;
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions()));
        AppLog.Info($"Config saved. Applied={Applied}, OriginalMappingCaptured={OriginalMappingCaptured}, Target={TargetKey}, RestartRequired={RestartRequired}, RestartPending={IsRestartPending}, Profile={(Profile is null ? "none" : Profile.DisplayName)}");
    }

    public void CopyFrom(AppConfig other)
    {
        Version = CurrentVersion;
        Profile = other.Profile;
        TargetKey = other.TargetKey;
        Applied = other.Applied;
        OriginalMappingCaptured = other.OriginalMappingCaptured;
        HadOriginalSourceMapping = other.HadOriginalSourceMapping;
        OriginalSourceDestination = other.OriginalSourceDestination;
        RestartRequired = other.RestartRequired;
        RestartBootId = other.RestartBootId;
        RestartReason = other.RestartReason;
        RestartRequestedAt = other.RestartRequestedAt;
        PendingExpectedHidDisabled = other.PendingExpectedHidDisabled;
        PendingExpectedMappingExists = other.PendingExpectedMappingExists;
        PendingExpectedMappingDestination = other.PendingExpectedMappingDestination;
        VerificationTaskName = other.VerificationTaskName;
    }

    internal static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
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
