using System.Reflection;
using System.Security.Cryptography;

namespace GiHATE;

internal sealed record BuildDetails(
    string Version,
    string InformationalVersion,
    string Commit,
    string ExecutablePath,
    string Sha256)
{
    private static readonly Lazy<BuildDetails> CurrentValue = new(Create);
    public static BuildDetails Current => CurrentValue.Value;

    private static BuildDetails Create()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? Application.ProductVersion;
        var version = informational.Split('+', 2)[0];
        var suffix = informational.Contains('+') ? informational.Split('+', 2)[1] : "";
        var commit = suffix.Length >= 7 ? suffix[..7] : string.IsNullOrWhiteSpace(suffix) ? "unknown" : suffix;
        var executable = Environment.ProcessPath ?? assembly.Location;
        var hash = "unavailable";

        try
        {
            using var stream = File.OpenRead(executable);
            hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch
        {
            // Build information should never prevent GiHATE from starting.
        }

        return new BuildDetails(version, informational, commit, executable, hash);
    }
}
