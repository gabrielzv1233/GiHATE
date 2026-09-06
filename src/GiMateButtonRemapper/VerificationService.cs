using System.Diagnostics;

namespace GiHATE;

internal sealed record VerificationResult(
    bool HasProfile,
    bool? HidDisabled,
    bool? PersistentDisableFlagSet,
    bool MappingMatches,
    bool MappingExists,
    ushort MappingDestination,
    bool RestartPending,
    bool RestartOccurred,
    string RestartReason,
    IReadOnlyList<string> GigabyteProcesses,
    bool ExpectedStateMatches)
{
    public bool GigabyteReady => GigabyteProcesses.Count > 0;
}

internal static class VerificationService
{
    private const uint CmProbDisabled = 22;
    private const uint ConfigFlagDisabled = 0x00000001;

    public static readonly string[] GigabyteListenerProcessNames =
    [
        "GiMATE",
        "GimateServiceHelper",
        "GCC",
        "GCCService",
        "GService",
        "GIGABYTE Control Center"
    ];

    public static VerificationResult Check(AppConfig config)
    {
        if (config.Profile is null)
            return new VerificationResult(false, null, null, false, false, 0, config.IsRestartPending, config.RestartOccurred, config.RestartReason, FindGigabyteProcesses(), false);

        var status = DeviceManager.GetStatus(config.Profile.VendorInstanceId);
        var flags = DeviceManager.GetPersistentConfigFlags(config.Profile.VendorInstanceId);
        bool? hidDisabled = status is null ? null : status.Value.Problem == CmProbDisabled;
        bool? persistentDisable = flags is null ? null : (flags.Value & ConfigFlagDisabled) != 0;
        var mapping = ScancodeMap.GetSourceMapping(config.Profile.SourceScanCode);

        bool expectedMappingExists;
        ushort expectedDestination;
        bool expectedHidDisabled;

        if (config.RestartRequired)
        {
            expectedMappingExists = config.PendingExpectedMappingExists;
            expectedDestination = config.PendingExpectedMappingDestination;
            expectedHidDisabled = config.PendingExpectedHidDisabled;
        }
        else if (config.Applied)
        {
            expectedMappingExists = true;
            expectedDestination = ScancodeMap.Targets.TryGetValue(config.TargetKey, out var target) ? target : (ushort)0;
            expectedHidDisabled = true;
        }
        else
        {
            expectedMappingExists = config.OriginalMappingCaptured && config.HadOriginalSourceMapping;
            expectedDestination = config.OriginalMappingCaptured ? config.OriginalSourceDestination : (ushort)0;
            expectedHidDisabled = false;
        }

        var mappingMatches = expectedMappingExists
            ? mapping.Exists && mapping.Destination == expectedDestination
            : !mapping.Exists;

        // On the same boot where a persistent disable/enable was scheduled,
        // the current live HID state is allowed to differ until reboot.
        var hidMatches = config.IsRestartPending
            ? persistentDisable is not null && persistentDisable.Value == expectedHidDisabled
            : hidDisabled is not null && hidDisabled.Value == expectedHidDisabled;

        var expectedMatches = mappingMatches && hidMatches;
        var processes = FindGigabyteProcesses();

        AppLog.Info($"Verification: HidDisabledNow={hidDisabled?.ToString() ?? "unknown"}, PersistentDisableFlag={persistentDisable?.ToString() ?? "unknown"}, ExpectedHidDisabled={expectedHidDisabled}, MappingExists={mapping.Exists}, MappingDestination=0x{mapping.Destination:X4}, ExpectedMappingExists={expectedMappingExists}, ExpectedDestination=0x{expectedDestination:X4}, RestartPending={config.IsRestartPending}, RestartOccurred={config.RestartOccurred}, GigabyteReady={processes.Count > 0}, ExpectedStateMatches={expectedMatches}");

        return new VerificationResult(
            true,
            hidDisabled,
            persistentDisable,
            mappingMatches,
            mapping.Exists,
            mapping.Destination,
            config.IsRestartPending,
            config.RestartOccurred,
            config.RestartReason,
            processes,
            expectedMatches);
    }

    private static IReadOnlyList<string> FindGigabyteProcesses()
    {
        try
        {
            var names = new HashSet<string>(GigabyteListenerProcessNames, StringComparer.OrdinalIgnoreCase);
            return Process.GetProcesses()
                .Select(p =>
                {
                    try { return p.ProcessName; }
                    catch { return ""; }
                    finally { p.Dispose(); }
                })
                .Where(x => !string.IsNullOrWhiteSpace(x) && names.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Could not enumerate GIGABYTE listener processes", ex);
            return Array.Empty<string>();
        }
    }
}
