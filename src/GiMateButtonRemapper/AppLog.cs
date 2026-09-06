using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace GiHATE;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static bool _initialized;

    public static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GiHATE.log");

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        WriteRaw("");
        WriteRaw(new string('=', 88));
        Info("GiHATE starting");
        Info($"Version: {Application.ProductVersion}");
        Info($"Executable: {Environment.ProcessPath}");
        Info($"OS: {Environment.OSVersion}");
        Info($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Info($"64-bit OS: {Environment.Is64BitOperatingSystem}; 64-bit process: {Environment.Is64BitProcess}");
        Info($"Administrator: {IsAdministrator()}");
        Info($"Config path: {AppConfig.FilePath}");
        Info($"Log path: {LogPath}");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Exception(string context, Exception exception)
    {
        Write("ERROR", $"{context}: {exception}");
    }

    public static void OpenInExplorer()
    {
        try
        {
            if (!File.Exists(LogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.WriteAllText(LogPath, "GiHATE log file\r\n");
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LogPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Exception("Failed to open log in Explorer", ex);
            throw;
        }
    }

    private static void Write(string level, string message)
    {
        var lines = message.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
            WriteRaw($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {line}");
    }

    private static void WriteRaw(string line)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never prevent the configurator from running.
            }
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
