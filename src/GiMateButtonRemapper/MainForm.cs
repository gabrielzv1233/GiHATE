using System.Diagnostics;

namespace GiHATE;

public sealed class MainForm : Form
{
    private readonly AppConfig _config = AppConfig.Load();
    private readonly RawInputCapture _raw = new();
    private readonly DetectionSession _detection = new();
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly ComboBox _target = new();
    private readonly Button _detect = new();
    private readonly Button _knownProfile = new();
    private readonly Button _apply = new();
    private readonly Button _restore = new();
    private DetectionMode _mode;
    private DetectedProfile? _pendingProfile;
    private bool _safetyCheckPending;

    public MainForm()
    {
        Text = "GiHATE"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(720, 500); ClientSize = new Size(760, 520); Font = new Font("Segoe UI", 10);
        BuildUi();
        _raw.Keyboard += OnKeyboard; _raw.Hid += OnHid;
        Shown += (_, _) => { try { _raw.Register(Handle); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Raw Input registration failed", MessageBoxButtons.OK, MessageBoxIcon.Error); } RefreshUi(); };
    }

    protected override void WndProc(ref Message m) { _raw.ProcessMessage(m); base.WndProc(ref m); }

    private void BuildUi()
    {
        var title = new Label { Text = "GiHATE", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) };
        var subtitle = new Label { Text = "Turn the dedicated GiMATE button into a normal programmable key without uninstalling GIGABYTE software.", AutoSize = false, Size = new Size(700, 50), Location = new Point(27, 70) };
        _status.Font = new Font(Font, FontStyle.Bold); _status.AutoSize = true; _status.Location = new Point(28, 130);
        _detail.AutoSize = false; _detail.Size = new Size(700, 90); _detail.Location = new Point(28, 160);
        _detect.Text = "Detect / verify GiMATE button"; _detect.Size = new Size(240, 38); _detect.Location = new Point(28, 265); _detect.Click += (_, _) => StartDetection();
        _knownProfile.Text = "Use tested Master 16 profile"; _knownProfile.Size = new Size(240, 34); _knownProfile.Location = new Point(275, 267); _knownProfile.Click += (_, _) => AdoptKnownProfile();
        var targetLabel = new Label { Text = "Replacement key:", AutoSize = true, Location = new Point(28, 325) };
        _target.DropDownStyle = ComboBoxStyle.DropDownList; _target.Items.AddRange(ScancodeMap.Targets.Keys.ToArray()); _target.SelectedItem = ScancodeMap.Targets.ContainsKey(_config.TargetKey) ? _config.TargetKey : "F24"; _target.Size = new Size(150, 32); _target.Location = new Point(165, 320);
        var hint = new Label { Text = "F24 is recommended. After reboot, use PowerToys Keyboard Manager or another remapper to bind F24 to something useful.", AutoSize = false, Size = new Size(420, 55), Location = new Point(330, 315) };
        _apply.Text = "Apply"; _apply.Size = new Size(140, 42); _apply.Location = new Point(28, 390); _apply.Click += (_, _) => ApplyConfiguration();
        _restore.Text = "Restore GiMATE"; _restore.Size = new Size(165, 42); _restore.Location = new Point(180, 390); _restore.Click += (_, _) => RestoreConfiguration();
        var configButton = new Button { Text = "Open config folder", Size = new Size(170, 34), Location = new Point(28, 455) };
        configButton.Click += (_, _) => { Directory.CreateDirectory(AppConfig.DirectoryPath); Process.Start(new ProcessStartInfo("explorer.exe", AppConfig.DirectoryPath) { UseShellExecute = true }); };
        Controls.AddRange([title, subtitle, _status, _detail, _detect, _knownProfile, targetLabel, _target, hint, _apply, _restore, configButton]);
    }

    private void RefreshUi()
    {
        if (_config.Profile is null)
        {
            _status.Text = "Status: Not configured"; _detail.Text = "Run detection first. GiHATE will ask you to press the GiMATE button three times, then press a normal keyboard key as a safety check."; _apply.Enabled = false; _restore.Enabled = _config.Applied; return;
        }
        var deviceState = DeviceManager.GetStatus(_config.Profile.VendorInstanceId);
        _status.Text = _config.Applied ? "Status: GiMATE override configured" : "Status: Device detected";
        _detail.Text = $"{_config.Profile.DisplayName}\nVendor interface: {_config.Profile.VendorInstanceId}\nVendor HID: usage 0x{_config.Profile.VendorUsagePage:X4}/0x{_config.Profile.VendorUsage:X2}, report {_config.Profile.VendorReportHex}" + (deviceState is null ? "" : $"\nPnP status bits: 0x{deviceState.Value.Status:X8}, problem: {deviceState.Value.Problem}");
        _apply.Enabled = true; _restore.Enabled = _config.Applied;
    }

    private void AdoptKnownProfile()
    {
        var vendor = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_02&COL04\");
        var keyboard = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_00\");
        if (vendor.Count != 1 || keyboard.Count < 1) { MessageBox.Show(this, "The exact tested AORUS Master 16 device layout was not found unambiguously. Use guided detection instead.", "Known profile not available", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show(this, "GiHATE found the exact VID/PID/interface layout used on the tested AORUS Master 16 AM6H.\n\nThis shortcut assumes scan 0x59 and vendor report 04 00 00 91. Continue?", "Adopt tested Master 16 profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _config.Profile = new DetectedProfile { KeyboardInstanceId = keyboard[0], VendorInstanceId = vendor[0], SourceScanCode = 0x59, VendorId = 0x0414, ProductId = 0x8100, VendorUsagePage = 0xFF02, VendorUsage = 0x01, VendorReportHex = "04 00 00 91" };
        _config.Save(); RefreshUi();
    }

    private void StartDetection()
    {
        _detection.Clear(); _pendingProfile = null; _safetyCheckPending = false; _mode = DetectionMode.Button; _detect.Enabled = false; _apply.Enabled = false;
        _status.Text = "Detection: press the GiMATE button 3 times"; _detail.Text = "Do not press other keys during this step. GiHATE is looking for a repeated keyboard scan code plus a correlated vendor-defined HID report.";
    }

    private void OnKeyboard(KeyboardRawEvent e)
    {
        if (_mode == DetectionMode.None) return;
        _detection.Add(e);
        if (_mode == DetectionMode.Button)
        {
            var candidate = _detection.FindButtonCandidate(); if (candidate is null) return;
            _pendingProfile = candidate; _detection.Clear(); _mode = DetectionMode.Safety;
            BeginInvoke(() => { _status.Text = "Safety check: press a NORMAL key"; _detail.Text = "Press A, Space, or another normal built-in keyboard key once. This verifies that the vendor HID collection GiHATE plans to disable is not your keyboard."; });
        }
        else if (_mode == DetectionMode.Safety && _pendingProfile is not null && !_safetyCheckPending && e.IsKeyDown && e.ScanCode != _pendingProfile.SourceScanCode)
        {
            _safetyCheckPending = true; _ = VerifySafetyAfterDelayAsync(_pendingProfile);
        }
    }

    private void OnHid(HidRawEvent e) { if (_mode != DetectionMode.None) _detection.Add(e); }

    private async Task VerifySafetyAfterDelayAsync(DetectedProfile profile)
    {
        await Task.Delay(350);
        var passed = _mode == DetectionMode.Safety && _detection.NormalKeySafetyPassed(profile);
        _safetyCheckPending = false;
        if (!passed)
        {
            BeginInvoke(() => { _status.Text = "Safety check did not pass"; _detail.Text = "The candidate vendor HID also produced traffic, or the normal key did not come from the expected keyboard interface. Press another normal built-in key, or restart detection."; });
            return;
        }
        _mode = DetectionMode.None; BeginInvoke(() => FinishDetection(profile));
    }

    private void FinishDetection(DetectedProfile profile)
    {
        if (profile.VendorUsagePage < 0xFF00 || string.IsNullOrWhiteSpace(profile.VendorInstanceId) || string.IsNullOrWhiteSpace(profile.KeyboardInstanceId)) { MessageBox.Show(this, "The candidate failed GiHATE's vendor-HID safety checks.", "Detection failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); _detect.Enabled = true; RefreshUi(); return; }
        _config.Profile = profile; _config.Save(); _detect.Enabled = true; RefreshUi();
        MessageBox.Show(this, $"Detected scan code 0x{profile.SourceScanCode:X2} and vendor HID report {profile.VendorReportHex}.\n\nSaved system-wide in {AppConfig.FilePath}.", "Detection complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ApplyConfiguration()
    {
        if (_config.Profile is null) return;
        var targetName = _target.SelectedItem?.ToString() ?? "F24"; if (!ScancodeMap.Targets.TryGetValue(targetName, out var destination)) return;
        try
        {
            if (!_config.Applied) { var original = ScancodeMap.GetSourceMapping(_config.Profile.SourceScanCode); _config.HadOriginalSourceMapping = original.Exists; _config.OriginalSourceDestination = original.Destination; }
            DeviceManager.DisablePersistent(_config.Profile.VendorInstanceId); ScancodeMap.SetMapping(_config.Profile.SourceScanCode, destination); _config.TargetKey = targetName; _config.Applied = true; _config.Save();
            using var prompt = new RestartPromptForm($"GiHATE disabled the vendor HID trigger and mapped scan 0x{_config.Profile.SourceScanCode:X2} to {targetName}.");
            if (prompt.ShowDialog(this) == DialogResult.OK && prompt.RestartNow) RestartWindows(); else RefreshUi();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.ToString(), "Apply failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RestoreConfiguration()
    {
        if (_config.Profile is null) return;
        if (MessageBox.Show(this, "This will re-enable the GiMATE vendor HID interface and restore the previous mapping for this scan code. Continue?", "Restore GiMATE", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            DeviceManager.Enable(_config.Profile.VendorInstanceId); ScancodeMap.RestoreSource(_config.Profile.SourceScanCode, _config.HadOriginalSourceMapping, _config.OriginalSourceDestination); _config.Applied = false; _config.Save();
            using var prompt = new RestartPromptForm("GiHATE restored the original GiMATE device path and scan-code mapping.");
            if (prompt.ShowDialog(this) == DialogResult.OK && prompt.RestartNow) RestartWindows(); else RefreshUi();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.ToString(), "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void RestartWindows() => Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
    private enum DetectionMode { None, Button, Safety }
}
