using System.Diagnostics;

namespace GiHATE;

internal static class VerificationTask
{
    public const string DefaultTaskName = "GiHATE Verify After Restart";

    public static bool Schedule(AppConfig config)
    {
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the GiHATE executable path.");
            var taskName = DefaultTaskName;
            var command = $"\"{exe}\" --verify --from-task --task-name \"{taskName}\"";

            var result = RunSchtasks(
                "/Create",
                "/TN", taskName,
                "/TR", command,
                "/SC", "ONLOGON",
                "/DELAY", "0000:10",
                "/RL", "HIGHEST",
                "/F");

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"schtasks /Create failed with exit code {result.ExitCode}: {result.Output}");

            config.VerificationTaskName = taskName;
            config.Save();
            AppLog.Info($"Scheduled one-shot verification task '{taskName}' for the next interactive logon. It self-deletes after a reboot is detected and verification runs.");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Failed to schedule post-reboot verification task", ex);
            return false;
        }
    }

    public static void Delete(string? taskName = null)
    {
        var name = string.IsNullOrWhiteSpace(taskName) ? DefaultTaskName : taskName;
        try
        {
            var result = RunSchtasks("/Delete", "/TN", name!, "/F");
            AppLog.Info($"Verification task delete result: exit={result.ExitCode}, output={result.Output}");
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Failed to delete verification task '{name}'", ex);
        }
    }

    public static bool Exists(string? taskName = null)
    {
        var name = string.IsNullOrWhiteSpace(taskName) ? DefaultTaskName : taskName;
        try
        {
            return RunSchtasks("/Query", "/TN", name!).ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim());
    }
}
