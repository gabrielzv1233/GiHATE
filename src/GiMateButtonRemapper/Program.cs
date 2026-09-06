namespace GiHATE;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var pendingWatch = args.Contains("--pending-watch", StringComparer.OrdinalIgnoreCase);
        var verifyDebug = args.Contains("--verify-debug", StringComparer.OrdinalIgnoreCase);
        var verify = verifyDebug || args.Contains("--verify", StringComparer.OrdinalIgnoreCase);
        var fromTask = args.Contains("--from-task", StringComparer.OrdinalIgnoreCase);
        var advanced = args.Contains("--advanced", StringComparer.OrdinalIgnoreCase);
        var mode = pendingWatch ? "pending-watch" : verifyDebug ? "verify-debug" : verify ? "verify" : advanced ? "advanced" : "main";

        AppLog.Initialize(rotate: !pendingWatch, mode: mode);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Exception("Unhandled UI-thread exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Exception("Unhandled application exception", ex);
            else
                AppLog.Error($"Unhandled non-Exception object: {e.ExceptionObject}");
        };

        var config = AppConfig.Load();
        AppLog.Info($"Launch arguments: {string.Join(" ", args)}");

        if (pendingWatch)
        {
            Application.Run(new PendingRestartWatcherForm(config));
            AppLog.Info("Pending-restart watcher exited.");
            return;
        }

        if (verify)
        {
            Application.Run(new VerifyForm(config, fromTask, verifyDebug));
            AppLog.Info("Verification UI exited.");
            return;
        }

        AppLog.Info("Starting main window");
        Application.Run(new MainForm(openAdvancedOnShown: advanced));
        AppLog.Info("GiHATE exited normally");
    }
}
