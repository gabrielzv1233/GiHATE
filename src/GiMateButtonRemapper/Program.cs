namespace GiHATE;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppLog.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Exception("Unhandled UI-thread exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Exception("Unhandled application exception", ex);
            else
                AppLog.Error($"Unhandled non-Exception object: {e.ExceptionObject}");
        };

        AppLog.Info("Starting main window");
        Application.Run(new MainForm());
        AppLog.Info("GiHATE exited normally");
    }
}
