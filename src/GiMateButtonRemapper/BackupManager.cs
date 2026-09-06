using System.Text.Json;

namespace GiHATE;

internal static class BackupManager
{
    private const int BackupVersion = 1;

    public static void Export(string path, AppConfig config)
    {
        var backup = new GiHateBackup
        {
            Version = BackupVersion,
            ExportedAt = DateTimeOffset.Now,
            ExportedByVersion = BuildDetails.Current.Version,
            ExportedByCommit = BuildDetails.Current.Commit,
            Config = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config, AppConfig.JsonOptions()), AppConfig.JsonOptions()) ?? new AppConfig(),
            ScancodeMapBase64 = ScancodeMap.ExportRawValue() is { } raw ? Convert.ToBase64String(raw) : null,
            VendorConfigFlags = config.Profile is null ? null : DeviceManager.GetPersistentConfigFlags(config.Profile.VendorInstanceId)
        };

        File.WriteAllText(path, JsonSerializer.Serialize(backup, AppConfig.JsonOptions()));
        AppLog.Info($"Backup exported to '{path}'. BackupVersion={backup.Version}, VendorConfigFlags={(backup.VendorConfigFlags is null ? "unknown" : $"0x{backup.VendorConfigFlags:X8}")}");
    }

    public static AppConfig Import(string path)
    {
        var backup = JsonSerializer.Deserialize<GiHateBackup>(File.ReadAllText(path), AppConfig.JsonOptions())
            ?? throw new InvalidOperationException("The backup file could not be parsed.");
        if (backup.Version != BackupVersion)
            throw new InvalidOperationException($"Unsupported GiHATE backup version {backup.Version}. This build supports version {BackupVersion}.");
        if (backup.Config is null)
            throw new InvalidOperationException("The backup does not contain a GiHATE configuration.");

        var raw = string.IsNullOrWhiteSpace(backup.ScancodeMapBase64) ? null : Convert.FromBase64String(backup.ScancodeMapBase64);
        ScancodeMap.ImportRawValue(raw);

        if (backup.Config.Profile is not null && backup.VendorConfigFlags is not null)
        {
            var disabled = (backup.VendorConfigFlags.Value & 0x00000001u) != 0;
            DeviceManager.SetPersistentDisabledState(backup.Config.Profile.VendorInstanceId, disabled);
            AppLog.Info($"Backup import restored vendor CONFIGFLAG_DISABLED={disabled}.");
        }

        backup.Config.MarkRestartRequired(
            "Backup import",
            expectedHidDisabled: backup.Config.Applied,
            expectedMappingExists: backup.Config.Profile is not null && ScancodeMap.GetSourceMapping(backup.Config.Profile.SourceScanCode).Exists,
            expectedMappingDestination: backup.Config.Profile is null ? (ushort)0 : ScancodeMap.GetSourceMapping(backup.Config.Profile.SourceScanCode).Destination);
        backup.Config.Save();

        AppLog.Info($"Backup imported from '{path}'. ExportedAt={backup.ExportedAt}, ExportedBy={backup.ExportedByVersion}/{backup.ExportedByCommit}");
        return backup.Config;
    }

    private sealed class GiHateBackup
    {
        public int Version { get; set; }
        public DateTimeOffset ExportedAt { get; set; }
        public string ExportedByVersion { get; set; } = "";
        public string ExportedByCommit { get; set; } = "";
        public AppConfig? Config { get; set; }
        public string? ScancodeMapBase64 { get; set; }
        public uint? VendorConfigFlags { get; set; }
    }
}
