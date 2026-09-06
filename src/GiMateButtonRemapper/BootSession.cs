namespace GiHATE;

internal static class BootSession
{
    public static string CurrentId
    {
        get
        {
            var bootUtc = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            var roundedTicks = bootUtc.UtcDateTime.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute;
            return new DateTimeOffset(roundedTicks, TimeSpan.Zero).ToString("yyyyMMddHHmm");
        }
    }

    public static bool IsSameBoot(string? id) => !string.IsNullOrWhiteSpace(id) && string.Equals(id, CurrentId, StringComparison.Ordinal);
}
