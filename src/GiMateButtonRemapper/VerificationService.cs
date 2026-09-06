using System.Diagnostics;

namespace GiHATE;

internal sealed record VerificationResult(
    bool HasProfile,
    bool? HidDisabled,
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
            return new VerificationResult(false, null, false, false, 0, config.IsRestartPending, config.RestartOccurred, config.RestartReason, FindGigabyteProcesses(), false);

        var hidDisabled = DeviceManager.IsDisabled(config.Profile.VendorInstanceId);
        var mapping = ScancodeMap.GetSourceMapping(config.Profile.SourceScanCode);
        var expectedMappingExists = config.RestartRequired
            ? config.PendingExpectedMappingExists
            : config.Applied;
        var expectedDestination = config.RestartRequired
            ? config.PendingExpectedMappingDestination
            : ScancodeMap.Targets.TryGetValue(config.TargetKey, out var target) ? target : (ushort)0;
        var expectedHidDisabled = config.RestartRequired ? config.PendingExpectedHidDisabled : config.Applied;

        var mappingMatches = expectedMappingExists
            ? mapping.Exists && mapping.Destination == expectedDestination
            : !mapping.Exists;
        var hidMatches = hidDisabled is not null && hidDisabled.Value == expectedHidDisabled;
        var expectedMatches = mappingMatches && hidMatches;
        var processes = FindGigabyteProcesses();

        AppLog.Info($"Verification: HidDisabled={hidDisabled?.ToString() ?? "unknown"}, ExpectedHidDisabled={expectedHidDisabled}, MappingExists={mapping.Exists}, MappingDestination=0x{mapping.Destination:X4}, ExpectedMappingExists={expectedMappingExists}, ExpectedDestination=0x{expectedDestination:X4}, RestartPending={config.IsRestartPending}, RestartOccurred={config.RestartOccurred}, GigabyteReady={processes.Count > 0}, ExpectedStateMatches={expectedMatches}");

        return new VerificationResult(
            true,
            hidDisabled,
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
