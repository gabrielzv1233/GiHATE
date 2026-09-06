using System.Diagnostics;

namespace GiHATE;

internal sealed class PendingRestartWatcherForm : Form
{
    private readonly AppConfig _config;
    private readonly RawInputCapture _raw = new();
    private readonly NotifyIcon _notify = new();
    private readonly Mutex _mutex;
    private DateTimeOffset _lastNotice = DateTimeOffset.MinValue;

    public PendingRestartWatcherForm(AppConfig config)
    {
        _config = config;
        _mutex = new Mutex(true, "Global\\GiHATE.PendingRestartWatcher", out var createdNew);
        if (!createdNew)
        {
            Shown += (_, _) => Close();
            return;
        }

        Text = "GiHATE restart watcher";
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        Opacity = 0;

        _notify.Icon = SystemIcons.Information;
        _notify.Text = "GiHATE restart pending";
        _notify.Visible = true;

        _raw.Keyboard += OnKeyboard;
        Shown += (_, _) =>
        {
            if (!_config.IsRestartPending || _config.Profile is null)
            {
                AppLog.Info("Pending-restart watcher started without an active pending restart; exiting.");
                Close();
                return;
            }

            try
            {
                _raw.Register(Handle);
                AppLog.Info($"Pending-restart watcher active for scan 0x{_config.Profile.SourceScanCode:X2}.");
            }
            catch (Exception ex)
            {
                AppLog.Exception("Pending-restart watcher Raw Input registration failed", ex);
                Close();
            }
        };
    }

    protected override void WndProc(ref Message m)
    {
        _raw.ProcessMessage(m);
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _raw.Keyboard -= OnKeyboard;
        _notify.Visible = false;
        _notify.Dispose();
        try { _mutex.ReleaseMutex(); } catch { }
        _mutex.Dispose();
        base.OnFormClosed(e);
    }

    private void OnKeyboard(KeyboardRawEvent e)
    {
        if (!e.IsKeyDown || _config.Profile is null || e.ScanCode != _config.Profile.SourceScanCode)
            return;

        if (!_config.IsRestartPending)
        {
            BeginInvoke((MethodInvoker)(() => Close()));
            return;
        }

        if (DateTimeOffset.Now - _lastNotice < TimeSpan.FromSeconds(4))
            return;
        _lastNotice = DateTimeOffset.Now;

        AppLog.Info("GiMATE button pressed while a restart is pending. Showing restart notification.");
        _notify.BalloonTipTitle = "GiHATE needs a restart";
        _notify.BalloonTipText = "The GiMATE button change is saved but not active yet. Restart Windows before testing the button.";
        _notify.BalloonTipIcon = ToolTipIcon.Warning;
        _notify.ShowBalloonTip(6000);
    }

    public static void StartDetachedIfNeeded(AppConfig config)
    {
        if (!config.IsRestartPending || config.Profile is null)
            return;

        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the GiHATE executable path.");
            Process.Start(new ProcessStartInfo(exe, "--pending-watch")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            AppLog.Info("Started detached pending-restart button watcher.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Could not start pending-restart watcher", ex);
        }
    }
}
