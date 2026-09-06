using System.Collections.Specialized;
using System.Diagnostics;
using System.Security.Principal;

namespace GiHATE;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static bool _initialized;

    public static string LogsDirectory => Path.Combine(AppConfig.DirectoryPath, "logs");
    public static string LogPath => Path.Combine(LogsDirectory, "latest.log");

    public static void Initialize(bool rotate = true, string mode = "main")
    {
        if (_initialized) return;
        _initialized = true;

        Directory.CreateDirectory(LogsDirectory);
        if (rotate)
            RotateLatest();

        WriteRaw(new string('=', 96));
        Info($"GiHATE starting. Mode={mode}");

        var build = BuildDetails.Current;
        Info($"App version: {build.Version}");
        Info($"Informational version: {build.InformationalVersion}");
        Info($"Commit: {build.Commit}");
        Info($"Executable SHA256: {build.Sha256}");
        Info($"Executable: {build.ExecutablePath}");
        Info($"OS: {Environment.OSVersion}");
        Info($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Info($"64-bit OS: {Environment.Is64BitOperatingSystem}; 64-bit process: {Environment.Is64BitProcess}");
        Info($"Administrator: {IsAdministrator()}");
        Info($"Boot session: {BootSession.CurrentId}");
        Info($"Config path: {AppConfig.FilePath}");
        Info($"Latest log: {LogPath}");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Exception(string context, Exception exception) => Write("ERROR", $"{context}: {exception}");

    public static void OpenInExplorer()
    {
        EnsureLatestExists();
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LogPath}\"") { UseShellExecute = true });
    }

    public static void OpenLogsFolder()
    {
        Directory.CreateDirectory(LogsDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", LogsDirectory) { UseShellExecute = true });
    }

    public static void CopyLatestFileToClipboard()
    {
        EnsureLatestExists();
        var files = new StringCollection { LogPath };
        Clipboard.SetFileDropList(files);
        Info("Latest log file copied to the Windows clipboard as a file.");
    }

    private static void RotateLatest()
    {
        try
        {
            if (!File.Exists(LogPath) || new FileInfo(LogPath).Length == 0)
                return;

            var stamp = File.GetLastWriteTime(LogPath).ToString("yyyy-MM-dd_HH-mm-ss");
            var archive = Path.Combine(LogsDirectory, $"{stamp}.log");
            var suffix = 1;
            while (File.Exists(archive))
                archive = Path.Combine(LogsDirectory, $"{stamp}_{suffix++}.log");

            File.Move(LogPath, archive);
        }
        catch
        {
            // Never make startup depend on log rotation.
        }
    }

    private static void EnsureLatestExists()
    {
        Directory.CreateDirectory(LogsDirectory);
        if (!File.Exists(LogPath))
            File.WriteAllText(LogPath, "GiHATE log file\r\n");
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
                Directory.CreateDirectory(LogsDirectory);
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
