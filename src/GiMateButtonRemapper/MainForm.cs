using System.Diagnostics;

namespace GiHATE;

public sealed class MainForm : Form
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly RawInputCapture _raw = new();
    private readonly bool _openAdvancedOnShown;
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly ComboBox _target = new();
    private readonly Button _detect = new();
    private readonly Button _revert = new();
    private readonly Button _apply = new();
    private readonly Button _advanced = new();
    private bool _busy;
    private bool _revertStaged;

    public MainForm(bool openAdvancedOnShown = false)
    {
        _openAdvancedOnShown = openAdvancedOnShown;
        Text = "GiHATE";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 540);
        ClientSize = new Size(820, 580);
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
            PendingRestartWatcherForm.StartDetachedIfNeeded(_config);
            if (_openAdvancedOnShown)
                BeginInvoke((MethodInvoker)(() => OpenAdvanced()));
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
            Padding = new Padding(32, 24, 32, 24)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        void Add(Control control, int top = 0)
        {
            var row = root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, top, 0, 0);
            root.Controls.Add(control, 0, row);
        }

        Add(new Label
        {
            Text = "GiHATE",
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            AutoSize = true
        });

        Add(new Label
        {
            Text = "Turn the dedicated GiMATE button into a normal programmable Windows key while keeping GIGABYTE software installed.",
            AutoSize = true,
            MaximumSize = new Size(730, 0)
        }, 2);

        var statusBox = new GroupBox
        {
            Text = "Status",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 12, 16, 16)
        };
        var statusLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _status.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _status.AutoSize = true;
        _detail.AutoSize = true;
        _detail.MaximumSize = new Size(690, 0);
        _detail.Margin = new Padding(0, 8, 0, 0);
        statusLayout.Controls.Add(_status, 0, 0);
        statusLayout.Controls.Add(_detail, 0, 1);
        statusBox.Controls.Add(statusLayout);
        Add(statusBox, 24);

        var setup = new GroupBox
        {
            Text = "Setup",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 10, 14, 14)
        };
        var setupFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        _detect.Text = "Detect / verify GiMATE button";
        _detect.AutoSize = true;
        _detect.Padding = new Padding(12, 5, 12, 5);
        _detect.Margin = new Padding(0, 0, 10, 0);
        _detect.Click += (_, _) => StartDetection();
        _revert.Text = "Revert changes";
        _revert.AutoSize = true;
        _revert.Padding = new Padding(12, 5, 12, 5);
        _revert.Click += (_, _) => StageRevert();
        setupFlow.Controls.Add(_detect);
        setupFlow.Controls.Add(_revert);
        setup.Controls.Add(setupFlow);
        Add(setup, 16);

        var replacement = new GroupBox
        {
            Text = "Replacement key",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 10, 14, 14)
        };
        var replacementLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        replacementLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var picker = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        picker.Controls.Add(new Label { Text = "Map the GiMATE button to:", AutoSize = true, Margin = new Padding(0, 7, 10, 0) });
        _target.DropDownStyle = ComboBoxStyle.DropDownList;
        _target.Items.AddRange(ScancodeMap.Targets.Keys.ToArray());
        _target.SelectedItem = ScancodeMap.Targets.ContainsKey(_config.TargetKey) ? _config.TargetKey : "F24";
        _target.Width = 150;
        picker.Controls.Add(_target);
        replacementLayout.Controls.Add(picker, 0, 0);
        replacementLayout.Controls.Add(new Label
        {
            Text = "F24 is recommended. PowerToys can map F24 -> Shift+F24, then bind Shift+F24 to an app, command, or URL.",
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 10, 0, 0)
        }, 0, 1);
        replacement.Controls.Add(replacementLayout);
        Add(replacement, 16);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        _apply.Text = "Apply";
        _apply.AutoSize = true;
        _apply.MinimumSize = new Size(140, 42);
        _apply.Margin = new Padding(0, 0, 10, 0);
        _apply.Click += async (_, _) => await ApplySelectedActionAsync();
        _advanced.Text = "Advanced";
        _advanced.AutoSize = true;
        _advanced.MinimumSize = new Size(140, 42);
        _advanced.Click += (_, _) => OpenAdvanced();
        actions.Controls.Add(_apply);
        actions.Controls.Add(_advanced);
        Add(actions, 22);
    }

    private void RefreshUi()
    {
        if (_busy) return;

        _detect.Enabled = true;
        _revert.Enabled = _config.Profile is not null || HasKnownMaster16Layout();
        _target.Enabled = !_revertStaged;
        _advanced.Enabled = true;

        if (_revertStaged)
        {
            _status.Text = "Revert staged";
            _status.ForeColor = SystemColors.ControlText;
            _detail.Text = "No system changes have been made yet. Click Apply to restore the GiMATE HID path and the pre-GiHATE scan-code state. A restart will be required.";
            _apply.Text = "Apply revert";
            _apply.Enabled = true;
            return;
        }

        _apply.Text = "Apply";
        if (_config.Profile is null)
        {
            _status.Text = "Not configured";
            _status.ForeColor = SystemColors.ControlText;
            _detail.Text = "Run detection first. GiHATE will identify the GiMATE keyboard scan code and its separate vendor HID trigger, then perform a normal-key safety check.";
            _apply.Enabled = false;
            return;
        }

        var verification = VerificationService.Check(_config);
        _apply.Enabled = true;

        if (_config.IsRestartPending)
        {
            _status.Text = "Restart required";
            _status.ForeColor = Color.DarkOrange;
            var target = _config.PendingExpectedMappingExists
                ? ScancodeMap.Targets.FirstOrDefault(x => x.Value == _config.PendingExpectedMappingDestination).Key ?? $"0x{_config.PendingExpectedMappingDestination:X2}"
                : "original / none";
            var live = FormatHid(verification.HidDisabled);
            var scheduled = verification.PersistentDisableFlagSet switch { true => "disable scheduled", false => "enable scheduled / disable cleared", null => "persistent state unknown" };
            _detail.Text = $"Changes are saved but are not active on this Windows boot. GiMATE HID now: {live}; {scheduled}. Pending mapping: scan 0x{_config.Profile.SourceScanCode:X2} -> {target}. Restart Windows before testing the button.";
            return;
        }

        if (_config.RestartOccurred)
        {
            if (!verification.ExpectedStateMatches)
            {
                _status.Text = "Restart completed - configuration mismatch";
                _status.ForeColor = Color.DarkRed;
                _detail.Text = "Windows has restarted, but the expected HID or scan-code state does not match. Open Advanced -> Verify now and attach latest.log if it remains broken.";
            }
            else if (!verification.GigabyteReady)
            {
                _status.Text = "Restart completed - waiting for GIGABYTE";
                _status.ForeColor = Color.DarkOrange;
                _detail.Text = "The HID and scan-code state match, but no known GIGABYTE/GiMATE listener process is running yet. A button test is not conclusive until that software is ready.";
            }
            else
            {
                _status.Text = "Restart completed - ready to verify";
                _status.ForeColor = Color.DarkGreen;
                _detail.Text = $"The expected HID and scan-code state is present and GIGABYTE is running ({string.Join(", ", verification.GigabyteProcesses)}). The one-shot verifier can now give a meaningful result.";
            }
            return;
        }

        if (_config.Applied)
        {
            if (!verification.ExpectedStateMatches)
            {
                _status.Text = "Configured - state mismatch";
                _status.ForeColor = Color.DarkRed;
            }
            else if (!verification.GigabyteReady)
            {
                _status.Text = "Configured - GIGABYTE not ready";
                _status.ForeColor = Color.DarkOrange;
            }
            else
            {
                _status.Text = "Configured and healthy";
                _status.ForeColor = Color.DarkGreen;
            }

            _detail.Text = $"GiMATE vendor HID: {FormatHid(verification.HidDisabled)}. Mapping: 0x{_config.Profile.SourceScanCode:X2} -> {_config.TargetKey}. GIGABYTE listener: {(verification.GigabyteReady ? string.Join(", ", verification.GigabyteProcesses) : "not detected yet; button behavior is not fully testable yet")}.";
            return;
        }

        _status.Text = "Device detected - ready to apply";
        _status.ForeColor = SystemColors.ControlText;
        _detail.Text = $"{_config.Profile.DisplayName}. Vendor HID report: {_config.Profile.VendorReportHex}. Choose a replacement key and click Apply.";
    }

    private static string FormatHid(bool? disabled) => disabled switch { true => "Disabled", false => "Enabled", null => "Unknown" };

    private void StartDetection()
    {
        _revertStaged = false;
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
        MessageBox.Show(this,
            $"Detected scan code 0x{wizard.ResultProfile.SourceScanCode:X2} and vendor HID report {wizard.ResultProfile.VendorReportHex}.\n\nNothing has been changed yet. Choose a replacement key and click Apply.",
            "Detection complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StageRevert()
    {
        if (_config.Profile is null && !TryLoadKnownMaster16Profile())
        {
            MessageBox.Show(this, "GiHATE does not have enough saved information to stage a revert on this machine. Run detection first.", "Cannot stage revert", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _revertStaged = true;
        AppLog.Info("Revert staged from the main window. No system changes made yet.");
        RefreshUi();
    }

    private async Task ApplySelectedActionAsync()
    {
        if (_busy) return;
        if (_revertStaged)
        {
            await RevertInternalAsync("REVERT", showConfirmation: false);
            _revertStaged = false;
            RefreshUi();
            return;
        }

        await ApplyConfigurationAsync();
    }

    private async Task ApplyConfigurationAsync()
    {
        if (_config.Profile is null) return;
        var targetName = _target.SelectedItem?.ToString() ?? "F24";
        if (!ScancodeMap.Targets.TryGetValue(targetName, out var destination)) return;

        SetBusy(true, "Applying...");
        await Task.Yield();

        try
        {
            AppLog.Info($"===== APPLY START ===== target={targetName}, sourceScan=0x{_config.Profile.SourceScanCode:X2}");
            if (!_config.OriginalMappingCaptured)
            {
                var original = ScancodeMap.GetSourceMapping(_config.Profile.SourceScanCode);
                _config.HadOriginalSourceMapping = original.Exists;
                _config.OriginalSourceDestination = original.Destination;
                _config.OriginalMappingCaptured = true;
                _config.Save();
            }

            var deviceResult = DeviceManager.DisablePersistent(_config.Profile.VendorInstanceId);
            ScancodeMap.SetMapping(_config.Profile.SourceScanCode, destination);
            _config.TargetKey = targetName;
            _config.Applied = true;
            _config.MarkRestartRequired("Apply GiHATE override", true, true, destination);
            VerificationTask.Schedule(_config);
            _config.Save();
            AppLog.Info($"===== APPLY SUCCESS ===== DeviceMethod={deviceResult.Method}");

            await PromptForRestartAsync($"GiHATE configured scan 0x{_config.Profile.SourceScanCode:X2} as {targetName}. A Windows restart is required before the new button behavior is active.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Apply failed", ex);
            MessageBox.Show(this, $"{ex.Message}\n\nOpen Advanced -> Copy log file for the full diagnostic log.", "Apply failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshUi();
        }
    }

    internal async Task RestoreConfigurationAsync()
    {
        if (_config.Profile is null || _busy) return;
        if (MessageBox.Show(this,
                "Restore the original GiMATE HID path and the scan-code state GiHATE captured before its first Apply? A restart is required.",
                "Restore GiMATE", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        await RevertInternalAsync("RESTORE", showConfirmation: false);
    }

    private async Task RevertInternalAsync(string logName, bool showConfirmation)
    {
        if (_config.Profile is null) return;
        if (showConfirmation && MessageBox.Show(this, "Revert GiHATE changes?", "Revert", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

        SetBusy(true, "Reverting...");
        await Task.Yield();

        try
        {
            AppLog.Info($"===== {logName} START =====");
            var currentMapping = ScancodeMap.GetSourceMapping(_config.Profile.SourceScanCode);
            var expectedExists = _config.OriginalMappingCaptured ? _config.HadOriginalSourceMapping : currentMapping.Exists;
            var expectedDestination = _config.OriginalMappingCaptured ? _config.OriginalSourceDestination : currentMapping.Destination;

            var deviceResult = DeviceManager.Enable(_config.Profile.VendorInstanceId);
            if (_config.OriginalMappingCaptured)
                ScancodeMap.RestoreSource(_config.Profile.SourceScanCode, _config.HadOriginalSourceMapping, _config.OriginalSourceDestination);
            else
                AppLog.Warn("No captured original scan-code state exists; revert left the current Scancode Map untouched.");

            _config.Applied = false;
            _config.MarkRestartRequired($"{logName} GiMATE state", false, expectedExists, expectedDestination);
            VerificationTask.Schedule(_config);
            _config.Save();
            AppLog.Info($"===== {logName} SUCCESS ===== DeviceMethod={deviceResult.Method}");

            await PromptForRestartAsync("GiHATE restored the GiMATE device path and pre-GiHATE scan-code state. Windows must restart before the restored behavior is active.");
        }
        catch (Exception ex)
        {
            AppLog.Exception($"{logName} failed", ex);
            MessageBox.Show(this, $"{ex.Message}\n\nOpen Advanced -> Copy log file for details.", $"{logName} failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshUi();
        }
    }

    private async Task PromptForRestartAsync(string message)
    {
        using var prompt = new RestartPromptForm(message);
        var result = prompt.ShowDialog(this);
        AppLog.Info($"Restart prompt closed. DialogResult={result}, RestartNow={prompt.RestartNow}");
        if (result == DialogResult.OK && prompt.RestartNow)
        {
            AppLog.Info("Restart now selected.");
            RestartWindows();
            return;
        }

        PendingRestartWatcherForm.StartDetachedIfNeeded(_config);
        await Task.CompletedTask;
    }

    private void OpenAdvanced()
    {
        using var advanced = new AdvancedForm(_config, RestoreConfigurationAsync, RefreshUi);
        advanced.ShowDialog(this);
        RefreshUi();
    }

    private bool HasKnownMaster16Layout()
    {
        if (_config.Profile is not null) return true;
        return DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_02&COL04\").Count == 1;
    }

    private bool TryLoadKnownMaster16Profile()
    {
        var vendor = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_02&COL04\");
        var keyboard = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_00\");
        if (vendor.Count != 1 || keyboard.Count < 1) return false;

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
        AppLog.Info("Loaded the tested Master 16 hardware profile only to support a staged revert.");
        return true;
    }

    private void SetBusy(bool busy, string? applyText = null)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _apply.Enabled = !busy && (_config.Profile is not null || _revertStaged);
        _detect.Enabled = !busy;
        _revert.Enabled = !busy;
        _target.Enabled = !busy && !_revertStaged;
        _advanced.Enabled = !busy;
        _apply.Text = busy ? applyText ?? "Working..." : _revertStaged ? "Apply revert" : "Apply";
    }

    private static void RestartWindows() => Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
}
