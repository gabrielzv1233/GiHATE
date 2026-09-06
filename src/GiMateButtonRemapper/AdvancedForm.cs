using System.Diagnostics;

namespace GiHATE;

internal sealed class AdvancedForm : Form
{
    private readonly AppConfig _config;
    private readonly Func<Task> _restoreAction;
    private readonly Action _stateChanged;
    private readonly Label _systemStatus = new();

    public AdvancedForm(AppConfig config, Func<Task> restoreAction, Action stateChanged)
    {
        _config = config;
        _restoreAction = restoreAction;
        _stateChanged = stateChanged;

        Text = "Advanced - GiHATE";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 600);
        MinimumSize = new Size(720, 560);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        tabs.TabPages.Add(BuildSystemTab());
        tabs.TabPages.Add(BuildBackupTab());
        tabs.TabPages.Add(BuildDiagnosticsTab());
        Controls.Add(tabs);

        Shown += (_, _) => RefreshSystemStatus();
    }

    private TabPage BuildSystemTab()
    {
        var page = new TabPage("System");
        var panel = CreatePanel();
        page.Controls.Add(panel);

        Add(panel, Heading("Current state"));
        _systemStatus.AutoSize = true;
        _systemStatus.MaximumSize = new Size(670, 0);
        Add(panel, _systemStatus, 10);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var verify = Button("Verify now");
        verify.Click += (_, _) =>
        {
            using var form = new VerifyForm(_config, fromTask: false, debug: true);
            form.ShowDialog(this);
            RefreshSystemStatus();
        };
        var restore = Button("Restore GiMATE");
        restore.Click += async (_, _) =>
        {
            await _restoreAction();
            RefreshSystemStatus();
            _stateChanged();
        };
        buttons.Controls.Add(verify);
        buttons.Controls.Add(restore);
        Add(panel, buttons, 18);

        Add(panel, Heading("Build information"), 26);
        var build = BuildDetails.Current;
        Add(panel, new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            Text = $"Product: {Application.ProductName}\nPublisher metadata: {Application.CompanyName}\nVersion: {build.Version}\nCommit: {build.Commit}\nExecutable SHA256: {build.Sha256}\nExecutable: {build.ExecutablePath}"
        }, 8);

        return page;
    }

    private TabPage BuildBackupTab()
    {
        var page = new TabPage("Backup & Restore");
        var panel = CreatePanel();
        page.Controls.Add(panel);

        Add(panel, Heading("Configuration backup"));
        Add(panel, new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            Text = "A GiHATE backup includes the machine-wide config, the complete Windows Scancode Map value, and the vendor HID persistent disable flag. Importing one requires a restart."
        }, 8);

        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var export = Button("Export backup");
        export.Click += (_, _) => ExportBackup();
        var import = Button("Import backup");
        import.Click += (_, _) => ImportBackup();
        flow.Controls.Add(export);
        flow.Controls.Add(import);
        Add(panel, flow, 20);

        Add(panel, new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            ForeColor = SystemColors.GrayText,
            Text = "Import restores the full saved Scancode Map, not only the GiMATE entry. This is intentional so the backup is a real system-state backup."
        }, 10);

        return page;
    }

    private TabPage BuildDiagnosticsTab()
    {
        var page = new TabPage("Diagnostics");
        var panel = CreatePanel();
        page.Controls.Add(panel);

        Add(panel, Heading("Issue data"));
        Add(panel, new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            Text = $"Latest log: {AppLog.LogPath}\nConfig: {AppConfig.FilePath}\nOld logs are archived by timestamp in {AppLog.LogsDirectory}."
        }, 8);

        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var openLatest = Button("Open latest log");
        openLatest.Click += (_, _) => AppLog.OpenInExplorer();
        var copyLog = Button("Copy log file");
        copyLog.Click += (_, _) =>
        {
            try
            {
                AppLog.CopyLatestFileToClipboard();
                MessageBox.Show(this, "latest.log was copied to the clipboard as a file.", "Log copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Copy log file failed", ex);
                MessageBox.Show(this, ex.Message, "Could not copy log", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        var openLogs = Button("Open logs folder");
        openLogs.Click += (_, _) => AppLog.OpenLogsFolder();
        var openConfig = Button("Open config folder");
        openConfig.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppConfig.DirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe", AppConfig.DirectoryPath) { UseShellExecute = true });
        };
        flow.Controls.Add(openLatest);
        flow.Controls.Add(copyLog);
        flow.Controls.Add(openLogs);
        flow.Controls.Add(openConfig);
        Add(panel, flow, 18);

        Add(panel, Heading("Developer launch modes"), 28);
        Add(panel, new Label
        {
            AutoSize = true,
            MaximumSize = new Size(670, 0),
            Text = "--verify          Show the compact verification UI\n--verify-debug    Verify without clearing pending verification/task state\n--pending-watch   Run only the restart-required button watcher\n--advanced        Open directly to this Advanced window"
        }, 8);

        return page;
    }

    private void RefreshSystemStatus()
    {
        var result = VerificationService.Check(_config);
        if (_config.Profile is null)
        {
            _systemStatus.Text = "Status: Not configured";
            return;
        }

        var hid = result.HidDisabled switch { true => "Disabled", false => "Enabled", null => "Unknown" };
        var mapping = result.MappingExists ? $"0x{_config.Profile.SourceScanCode:X2} -> 0x{result.MappingDestination:X2}" : $"0x{_config.Profile.SourceScanCode:X2} -> no mapping";
        var restart = result.RestartPending ? $"Required ({_config.RestartReason})" : result.RestartOccurred ? "Restart occurred; verification pending/completed" : "Not pending";
        var gigabyte = result.GigabyteReady ? string.Join(", ", result.GigabyteProcesses) : "No known listener detected";

        _systemStatus.Text = $"GiMATE vendor HID: {hid}\nScancode mapping: {mapping}\nRestart: {restart}\nGIGABYTE listener: {gigabyte}\nVerification task: {(VerificationTask.Exists(_config.VerificationTaskName) ? "Scheduled" : "Not scheduled")}";
    }

    private void ExportBackup()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export GiHATE backup",
            Filter = "GiHATE backup (*.gihate.json)|*.gihate.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"GiHATE-backup-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.gihate.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            BackupManager.Export(dialog.FileName, _config);
            MessageBox.Show(this, "Backup exported successfully.", "Backup exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Backup export failed", ex);
            MessageBox.Show(this, ex.Message, "Backup export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import GiHATE backup",
            Filter = "GiHATE backup (*.gihate.json)|*.gihate.json|JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Importing restores the backup's complete Scancode Map and GiMATE HID persistent state. A Windows restart is required. Continue?",
                "Import backup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            var imported = BackupManager.Import(dialog.FileName);
            _config.CopyFrom(imported);
            VerificationTask.Schedule(_config);
            _config.Save();
            _stateChanged();
            RefreshSystemStatus();

            using var prompt = new RestartPromptForm("GiHATE imported the backup. Windows must restart before the restored keyboard/HID state is active.");
            var result = prompt.ShowDialog(this);
            if (result == DialogResult.OK && prompt.RestartNow)
                Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
            else
                PendingRestartWatcherForm.StartDetachedIfNeeded(_config);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Backup import failed", ex);
            MessageBox.Show(this, $"{ex.Message}\n\nSee {AppLog.LogPath} for details.", "Backup import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static TableLayoutPanel CreatePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(24, 20, 24, 20)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }

    private static void Add(TableLayoutPanel panel, Control control, int top = 0)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, top, 0, 0);
        panel.Controls.Add(control, 0, row);
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 16, FontStyle.Bold)
    };

    private static Button Button(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(10, 4, 10, 4),
        Margin = new Padding(0, 0, 10, 8)
    };
}
