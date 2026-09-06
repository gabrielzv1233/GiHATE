using System.Diagnostics;

namespace GiHATE;

public sealed class MainForm : Form
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly RawInputCapture _raw = new();
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly ComboBox _target = new();
    private readonly Button _detect = new();
    private readonly Button _knownProfile = new();
    private readonly Button _apply = new();
    private readonly Button _restore = new();
    private readonly Button _openLog = new();
    private readonly Button _revert = new();
    private bool _busy;

    public MainForm()
    {
        Text = "GiHATE";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 600);
        ClientSize = new Size(860, 650);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        AppLog.Info("Constructing main window");
        BuildUi();
        Shown += (_, _) =>
        {
            try
            {
                _raw.Register(Handle);
                AppLog.Info($"Raw Input registered to main window handle 0x{Handle.ToInt64():X}.");
            }
            catch (Exception ex)
            {
                AppLog.Exception("Raw Input registration failed", ex);
                MessageBox.Show(this, ex.Message, "Raw Input registration failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshUi();
        };
    }

    protected override void WndProc(ref Message m)
    {
        _raw.ProcessMessage(m);
        base.WndProc(ref m);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(32, 24, 32, 24),
            BackColor = SystemColors.Control
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        void AddRow(Control control, int topMargin = 0)
        {
            var row = root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, topMargin, 0, 0);
            root.Controls.Add(control, 0, row);
        }

        AddRow(new Label
        {
            Text = "GiHATE",
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            AutoSize = true
        });

        AddRow(new Label
        {
            Text = "Turn the dedicated GiMATE button into a normal programmable Windows key while keeping GIGABYTE software installed.",
            AutoSize = true,
            MaximumSize = new Size(760, 0)
        }, 2);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _status.Font = new Font(Font, FontStyle.Bold);
        _status.AutoSize = true;
        _status.Dock = DockStyle.Fill;
        _status.Margin = new Padding(0);

        _detail.AutoSize = true;
        _detail.Dock = DockStyle.Fill;
        _detail.MaximumSize = new Size(760, 0);
        _detail.Margin = new Padding(0, 6, 0, 0);

        statusPanel.Controls.Add(_status, 0, 0);
        statusPanel.Controls.Add(_detail, 0, 1);
        AddRow(statusPanel, 26);

        var setupGroup = new GroupBox
        {
            Text = "Setup",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 10, 14, 14)
        };
        var setupFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _detect.Text = "Detect / verify GiMATE button";
        _detect.AutoSize = true;
        _detect.Padding = new Padding(12, 5, 12, 5);
        _detect.Margin = new Padding(0, 0, 10, 0);
        _detect.Click += (_, _) => StartDetection();

        _knownProfile.Text = "Use tested Master 16 profile";
        _knownProfile.AutoSize = true;
        _knownProfile.Padding = new Padding(12, 5, 12, 5);
        _knownProfile.Margin = new Padding(0);
        _knownProfile.Click += (_, _) => AdoptKnownProfile();

        setupFlow.Controls.Add(_detect);
        setupFlow.Controls.Add(_knownProfile);
        setupGroup.Controls.Add(setupFlow);
        AddRow(setupGroup, 24);

        var replacementGroup = new GroupBox
        {
            Text = "Replacement key",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 10, 14, 14)
        };
        var replacementLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        replacementLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        replacementLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        replacementLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var picker = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        var targetLabel = new Label
        {
            Text = "Map the GiMATE button to:",
            AutoSize = true,
            Margin = new Padding(0, 7, 10, 0)
        };
        _target.DropDownStyle = ComboBoxStyle.DropDownList;
        _target.Items.AddRange(ScancodeMap.Targets.Keys.ToArray());
        _target.SelectedItem = ScancodeMap.Targets.ContainsKey(_config.TargetKey) ? _config.TargetKey : "F24";
        _target.Width = 150;
        _target.Margin = new Padding(0);
        picker.Controls.Add(targetLabel);
        picker.Controls.Add(_target);

        var hint = new Label
        {
            Text = "F24 is recommended. After reboot, PowerToys can remap F24 to Shift+F24, then map Shift+F24 to an app, command, or URL.",
            AutoSize = true,
            MaximumSize = new Size(740, 0),
            Margin = new Padding(0, 10, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        replacementLayout.Controls.Add(picker, 0, 0);
        replacementLayout.Controls.Add(hint, 0, 1);
        replacementGroup.Controls.Add(replacementLayout);
        AddRow(replacementGroup, 16);

        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };
        _apply.Text = "Apply";
        _apply.AutoSize = true;
        _apply.MinimumSize = new Size(140, 42);
        _apply.Margin = new Padding(0, 0, 10, 0);
        _apply.Click += async (_, _) => await ApplyConfigurationAsync();

        _restore.Text = "Restore GiMATE";
        _restore.AutoSize = true;
        _restore.MinimumSize = new Size(165, 42);
        _restore.Margin = new Padding(0);
        _restore.Click += async (_, _) => await RestoreConfigurationAsync();

        actionFlow.Controls.Add(_apply);
        actionFlow.Controls.Add(_restore);
        AddRow(actionFlow, 22);

        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var utilityFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };

        var configButton = new Button
        {
            Text = "Open config folder",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 10, 0)
        };
        configButton.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(AppConfig.DirectoryPath);
                AppLog.Info("Opening config folder in Explorer.");
                Process.Start(new ProcessStartInfo("explorer.exe", AppConfig.DirectoryPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to open config folder", ex);
                MessageBox.Show(this, ex.Message, "Could not open config folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _openLog.Text = "Open log";
        _openLog.AutoSize = true;
        _openLog.Padding = new Padding(10, 4, 10, 4);
        _openLog.Margin = new Padding(0);
        _openLog.Click += (_, _) =>
        {
            try { AppLog.OpenInExplorer(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open log", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };

        _revert.Text = "Revert changes";
        _revert.AutoSize = true;
        _revert.MinimumSize = new Size(150, 38);
        _revert.Padding = new Padding(10, 4, 10, 4);
        _revert.Margin = new Padding(16, 0, 0, 0);
        _revert.Anchor = AnchorStyles.Right;
        _revert.Click += async (_, _) => await RevertChangesAsync();

        utilityFlow.Controls.Add(configButton);
        utilityFlow.Controls.Add(_openLog);
        bottomBar.Controls.Add(utilityFlow, 0, 0);
        bottomBar.Controls.Add(_revert, 1, 0);
        AddRow(bottomBar, 14);

        AddRow(new Label
        {
            Text = $"Publisher metadata: {Application.CompanyName}    Version: {Application.ProductVersion}\nConfig: {AppConfig.FilePath}\nIssue log: {AppLog.LogPath}",
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5f)
        }, 22);
    }

    private void RefreshUi()
    {
        if (_busy) return;

        if (_config.Profile is null)
        {
            _status.Text = "Status: Not configured";
            _detail.Text = "Run detection first. Detection opens in its own guided window with a live 0/3 press counter and a separate keyboard safety-check step.";
            _apply.Enabled = false;
            _restore.Enabled = false;
            _revert.Enabled = false;
            _detect.Enabled = true;
            _knownProfile.Enabled = true;
            _target.Enabled = true;
            return;
        }

        var deviceState = DeviceManager.GetStatus(_config.Profile.VendorInstanceId);
        _status.Text = _config.Applied ? "Status: GiMATE override configured" : "Status: Device detected";
        _detail.Text = $"{_config.Profile.DisplayName}\nVendor interface: {_config.Profile.VendorInstanceId}\nVendor HID: usage 0x{_config.Profile.VendorUsagePage:X4}/0x{_config.Profile.VendorUsage:X2}, report {_config.Profile.VendorReportHex}" +
            (deviceState is null ? "" : $"\nPnP status bits: 0x{deviceState.Value.Status:X8}, problem: {deviceState.Value.Problem}");
        _apply.Enabled = true;
        _restore.Enabled = _config.Applied;
        _revert.Enabled = true;
        _detect.Enabled = true;
        _knownProfile.Enabled = true;
        _target.Enabled = true;
    }

    private void StartDetection()
    {
        AppLog.Info("Guided detection requested.");
        using var wizard = new DetectionWizardForm(_raw, Handle);
        if (wizard.ShowDialog(this) != DialogResult.OK || wizard.ResultProfile is null)
        {
            AppLog.Info("Guided detection cancelled or did not produce a profile.");
            return;
        }

        _config.Profile = wizard.ResultProfile;
        _config.Save();
        RefreshUi();

        AppLog.Info($"Detection accepted: scan=0x{wizard.ResultProfile.SourceScanCode:X2}, vendor={wizard.ResultProfile.VendorInstanceId}, report={wizard.ResultProfile.VendorReportHex}");
        MessageBox.Show(
            this,
            $"Detected scan code 0x{wizard.ResultProfile.SourceScanCode:X2} and vendor HID report {wizard.ResultProfile.VendorReportHex}.\n\nNothing has been changed yet. Choose a replacement key and click Apply when ready.",
            "Detection complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void AdoptKnownProfile()
    {
        AppLog.Info("Tested Master 16 profile adoption requested.");
        var vendor = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_02&COL04\");
        var keyboard = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_00\");
        if (vendor.Count != 1 || keyboard.Count < 1)
        {
            AppLog.Warn($"Known profile unavailable. vendor matches={vendor.Count}, keyboard matches={keyboard.Count}.");
            MessageBox.Show(this, "The exact tested AORUS Master 16 device layout was not found unambiguously. Use guided detection instead.", "Known profile not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, "GiHATE found the exact VID/PID/interface layout used on the tested AORUS Master 16 AM6H.\n\nThis shortcut assumes scan 0x59 and vendor report 04 00 00 91. Continue?", "Adopt tested Master 16 profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            AppLog.Info("Known profile adoption cancelled by user.");
            return;
        }

        _config.Profile = new DetectedProfile
        {
            KeyboardInstanceId = keyboard[0],
            VendorInstanceId = vendor[0],
            SourceScanCode = 0x59,
            VendorId = 0x0414,
            ProductId = 0x8100,
            VendorUsagePage = 0xFF02,
            VendorUsage = 0x01,
            VendorReportHex = "04 00 00 91"
        };
        _config.Save();
        AppLog.Info($"Known profile adopted. Keyboard={keyboard[0]}, Vendor={vendor[0]}");
        RefreshUi();
    }

    private async Task ApplyConfigurationAsync()
    {
        if (_config.Profile is null || _busy) return;

        var targetName = _target.SelectedItem?.ToString() ?? "F24";
        if (!ScancodeMap.Targets.TryGetValue(targetName, out var destination)) return;

        SetBusy(true, "Applying...");
        await Task.Yield();

        try
        {
            AppLog.Info($"===== APPLY START ===== target={targetName}, sourceScan=0x{_config.Profile.SourceScanCode:X2}");
            AppLog.Info($"Keyboard instance: {_config.Profile.KeyboardInstanceId}");
            AppLog.Info($"Vendor instance: {_config.Profile.VendorInstanceId}");

            if (!_config.OriginalMappingCaptured)
            {
                var original = ScancodeMap.GetSourceMapping(_config.Profile.SourceScanCode);
                _config.HadOriginalSourceMapping = original.Exists;
                _config.OriginalSourceDestination = original.Destination;
                _config.OriginalMappingCaptured = true;
                _config.Save();
                AppLog.Info($"Captured and persisted original source mapping. Exists={original.Exists}, Destination=0x{original.Destination:X4}");
            }

            var deviceResult = DeviceManager.DisablePersistent(_config.Profile.VendorInstanceId);
            AppLog.Info($"Vendor HID disable accepted via {deviceResult.Method}. Pending-reboot path={deviceResult.RestartRequired}");

            ScancodeMap.SetMapping(_config.Profile.SourceScanCode, destination);
            _config.TargetKey = targetName;
            _config.Applied = true;
            _config.Save();
            AppLog.Info("===== APPLY SUCCESS =====");

            var pendingText = deviceResult.RestartRequired
                ? " The vendor HID disable has been scheduled for the reboot."
                : "";
            using var prompt = new RestartPromptForm($"GiHATE configured scan 0x{_config.Profile.SourceScanCode:X2} as {targetName}.{pendingText}");
            var dialogResult = prompt.ShowDialog(this);
            AppLog.Info($"Restart prompt closed. DialogResult={dialogResult}, RestartNow={prompt.RestartNow}");
            if (dialogResult == DialogResult.OK && prompt.RestartNow)
            {
                AppLog.Info("Restart now selected. Requesting Windows restart.");
                RestartWindows();
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Apply failed", ex);
            MessageBox.Show(
                this,
                $"{ex.Message}\n\nUse Revert changes if you want GiHATE to clear any partial or pending changes.\n\nFull diagnostic details were written to:\n{AppLog.LogPath}",
                "Apply failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshUi();
        }
    }

    private async Task RestoreConfigurationAsync()
    {
        if (_config.Profile is null || _busy) return;

        if (MessageBox.Show(this, "This will re-enable the GiMATE vendor HID interface and restore the previous mapping for this scan code. Continue?", "Restore GiMATE", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            AppLog.Info("Restore cancelled by user.");
            return;
        }

        await RevertInternalAsync("Restoring...", "GiHATE restored the original GiMATE device path and scan-code mapping.", "RESTORE");
    }

    private async Task RevertChangesAsync()
    {
        if (_config.Profile is null || _busy) return;

        if (MessageBox.Show(
                this,
                "Revert any GiHATE changes or pending device changes?\n\nThis clears the persistent vendor-HID disable, re-enables the GiMATE interface, and restores the scan-code mapping only if GiHATE previously captured it. A restart is required to fully settle pending PnP changes.",
                "Revert GiHATE changes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            AppLog.Info("Revert changes cancelled by user.");
            return;
        }

        await RevertInternalAsync("Reverting...", "GiHATE cleared applied or pending changes and restored the pre-Apply state.", "REVERT");
    }

    private async Task RevertInternalAsync(string busyText, string restartMessage, string logName)
    {
        if (_config.Profile is null) return;

        SetBusy(true, busyText);
        await Task.Yield();

        try
        {
            AppLog.Info($"===== {logName} START =====");
            var deviceResult = DeviceManager.Enable(_config.Profile.VendorInstanceId);
            AppLog.Info($"Vendor HID enable accepted via {deviceResult.Method}. Pending-reboot path={deviceResult.RestartRequired}");

            if (_config.OriginalMappingCaptured)
            {
                ScancodeMap.RestoreSource(_config.Profile.SourceScanCode, _config.HadOriginalSourceMapping, _config.OriginalSourceDestination);
                AppLog.Info("Restored captured pre-Apply scan-code mapping.");
            }
            else
            {
                AppLog.Info("No captured pre-Apply scan-code mapping exists; scancode registry state was left untouched.");
            }

            _config.Applied = false;
            _config.OriginalMappingCaptured = false;
            _config.HadOriginalSourceMapping = false;
            _config.OriginalSourceDestination = 0;
            _config.Save();
            AppLog.Info($"===== {logName} SUCCESS =====");

            using var prompt = new RestartPromptForm(restartMessage);
            var dialogResult = prompt.ShowDialog(this);
            AppLog.Info($"{logName} restart prompt closed. DialogResult={dialogResult}, RestartNow={prompt.RestartNow}");
            if (dialogResult == DialogResult.OK && prompt.RestartNow)
            {
                AppLog.Info($"Restart now selected after {logName.ToLowerInvariant()}. Requesting Windows restart.");
                RestartWindows();
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception($"{logName} failed", ex);
            MessageBox.Show(
                this,
                $"{ex.Message}\n\nFull diagnostic details were written to:\n{AppLog.LogPath}",
                $"{logName} failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshUi();
        }
    }

    private void SetBusy(bool busy, string? applyText = null)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _apply.Enabled = !busy && _config.Profile is not null;
        _restore.Enabled = !busy && _config.Applied;
        _revert.Enabled = !busy && _config.Profile is not null;
        _detect.Enabled = !busy;
        _knownProfile.Enabled = !busy;
        _target.Enabled = !busy;
        _openLog.Enabled = true;
        _apply.Text = busy ? applyText ?? "Working..." : "Apply";
    }

    private static void RestartWindows()
    {
        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
    }
}
