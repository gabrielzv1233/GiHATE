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

    public MainForm()
    {
        Text = "GiHATE";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 600);
        ClientSize = new Size(860, 650);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        Shown += (_, _) =>
        {
            try { _raw.Register(Handle); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Raw Input registration failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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

        var title = new Label
        {
            Text = "GiHATE",
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            AutoSize = true
        };
        AddRow(title);

        var subtitle = new Label
        {
            Text = "Turn the dedicated GiMATE button into a normal programmable Windows key while keeping GIGABYTE software installed.",
            AutoSize = true,
            MaximumSize = new Size(760, 0)
        };
        AddRow(subtitle, 2);

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
        _apply.Click += (_, _) => ApplyConfiguration();

        _restore.Text = "Restore GiMATE";
        _restore.AutoSize = true;
        _restore.MinimumSize = new Size(165, 42);
        _restore.Margin = new Padding(0);
        _restore.Click += (_, _) => RestoreConfiguration();

        actionFlow.Controls.Add(_apply);
        actionFlow.Controls.Add(_restore);
        AddRow(actionFlow, 22);

        var configButton = new Button
        {
            Text = "Open config folder",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4)
        };
        configButton.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppConfig.DirectoryPath);
            Process.Start(new ProcessStartInfo("explorer.exe", AppConfig.DirectoryPath) { UseShellExecute = true });
        };
        AddRow(configButton, 14);

        var footer = new Label
        {
            Text = $"Publisher metadata: {Application.CompanyName}    Version: {Application.ProductVersion}\nConfig: {AppConfig.FilePath}",
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5f)
        };
        AddRow(footer, 22);
    }

    private void RefreshUi()
    {
        if (_config.Profile is null)
        {
            _status.Text = "Status: Not configured";
            _detail.Text = "Run detection first. Detection opens in its own guided window with a live 0/3 press counter and a separate keyboard safety-check step.";
            _apply.Enabled = false;
            _restore.Enabled = _config.Applied;
            return;
        }

        var deviceState = DeviceManager.GetStatus(_config.Profile.VendorInstanceId);
        _status.Text = _config.Applied ? "Status: GiMATE override configured" : "Status: Device detected";
        _detail.Text = $"{_config.Profile.DisplayName}\nVendor interface: {_config.Profile.VendorInstanceId}\nVendor HID: usage 0x{_config.Profile.VendorUsagePage:X4}/0x{_config.Profile.VendorUsage:X2}, report {_config.Profile.VendorReportHex}" +
            (deviceState is null ? "" : $"\nPnP status bits: 0x{deviceState.Value.Status:X8}, problem: {deviceState.Value.Problem}");
        _apply.Enabled = true;
        _restore.Enabled = _config.Applied;
    }

    private void StartDetection()
    {
        using var wizard = new DetectionWizardForm(_raw, Handle);
        if (wizard.ShowDialog(this) != DialogResult.OK || wizard.ResultProfile is null)
            return;

        _config.Profile = wizard.ResultProfile;
        _config.Save();
        RefreshUi();

        MessageBox.Show(
            this,
            $"Detected scan code 0x{wizard.ResultProfile.SourceScanCode:X2} and vendor HID report {wizard.ResultProfile.VendorReportHex}.\n\nNothing has been changed yet. Choose a replacement key and click Apply when ready.",
            "Detection complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void AdoptKnownProfile()
    {
        var vendor = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_02&COL04\");
        var keyboard = DeviceManager.FindInstanceIds(@"HID\VID_0414&PID_8100&MI_00\");
        if (vendor.Count != 1 || keyboard.Count < 1)
        {
            MessageBox.Show(this, "The exact tested AORUS Master 16 device layout was not found unambiguously. Use guided detection instead.", "Known profile not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, "GiHATE found the exact VID/PID/interface layout used on the tested AORUS Master 16 AM6H.\n\nThis shortcut assumes scan 0x59 and vendor report 04 00 00 91. Continue?", "Adopt tested Master 16 profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

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
        RefreshUi();
    }

    private void ApplyConfiguration()
    {
        if (_config.Profile is null)
            return;

        var targetName = _target.SelectedItem?.ToString() ?? "F24";
        if (!ScancodeMap.Targets.TryGetValue(targetName, out var destination))
            return;

        try
        {
            if (!_config.Applied)
            {
                var original = ScancodeMap.GetSourceMapping(_config.Profile.SourceScanCode);
                _config.HadOriginalSourceMapping = original.Exists;
                _config.OriginalSourceDestination = original.Destination;
            }

            DeviceManager.DisablePersistent(_config.Profile.VendorInstanceId);
            ScancodeMap.SetMapping(_config.Profile.SourceScanCode, destination);
            _config.TargetKey = targetName;
            _config.Applied = true;
            _config.Save();

            using var prompt = new RestartPromptForm($"GiHATE disabled the vendor HID trigger and mapped scan 0x{_config.Profile.SourceScanCode:X2} to {targetName}.");
            if (prompt.ShowDialog(this) == DialogResult.OK && prompt.RestartNow)
                RestartWindows();
            else
                RefreshUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Apply failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreConfiguration()
    {
        if (_config.Profile is null)
            return;

        if (MessageBox.Show(this, "This will re-enable the GiMATE vendor HID interface and restore the previous mapping for this scan code. Continue?", "Restore GiMATE", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            DeviceManager.Enable(_config.Profile.VendorInstanceId);
            ScancodeMap.RestoreSource(_config.Profile.SourceScanCode, _config.HadOriginalSourceMapping, _config.OriginalSourceDestination);
            _config.Applied = false;
            _config.Save();

            using var prompt = new RestartPromptForm("GiHATE restored the original GiMATE device path and scan-code mapping.");
            if (prompt.ShowDialog(this) == DialogResult.OK && prompt.RestartNow)
                RestartWindows();
            else
                RefreshUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RestartWindows() => Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
}
