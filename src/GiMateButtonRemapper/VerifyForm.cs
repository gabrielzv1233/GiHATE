namespace GiHATE;

internal sealed class VerifyForm : Form
{
    private readonly AppConfig _config;
    private readonly bool _fromTask;
    private readonly bool _debug;
    private readonly Label _hid = new();
    private readonly Label _mapping = new();
    private readonly Label _restart = new();
    private readonly Label _gigabyte = new();
    private readonly Label _summary = new();

    public VerifyForm(AppConfig config, bool fromTask, bool debug)
    {
        _config = config;
        _fromTask = fromTask;
        _debug = debug;

        Text = "GiHATE verification";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 390);
        MinimumSize = new Size(560, 390);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(28, 24, 28, 22)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        void Add(Control c, int top = 0)
        {
            var row = root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c.Margin = new Padding(0, top, 0, 0);
            root.Controls.Add(c, 0, row);
        }

        Add(new Label
        {
            Text = "GiHATE verification",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            AutoSize = true
        });

        _summary.AutoSize = true;
        _summary.MaximumSize = new Size(490, 0);
        _summary.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        Add(_summary, 14);

        foreach (var label in new[] { _hid, _mapping, _restart, _gigabyte })
        {
            label.AutoSize = true;
            label.MaximumSize = new Size(490, 0);
            Add(label, 12);
        }

        var ok = new Button
        {
            Text = "OK",
            AutoSize = true,
            MinimumSize = new Size(110, 38),
            Anchor = AnchorStyles.Right
        };
        ok.Click += (_, _) => Close();
        Add(ok, 24);

        Shown += (_, _) => RefreshVerification();
    }

    private void RefreshVerification()
    {
        var result = VerificationService.Check(_config);
        if (!result.HasProfile || _config.Profile is null)
        {
            _summary.Text = "GiHATE is not configured.";
            _hid.Text = "• No GiMATE HID profile is saved.";
            _mapping.Text = "• No scan-code mapping can be verified.";
            _restart.Text = "• Restart state: not applicable";
            _gigabyte.Text = FormatGigabyte(result);
            return;
        }

        _hid.Text = result.HidDisabled switch
        {
            true => "✓ GiMATE vendor HID: Disabled",
            false => "✕ GiMATE vendor HID: Enabled",
            null => "? GiMATE vendor HID: State unknown"
        };

        var source = _config.Profile.SourceScanCode;
        var expectedDestination = _config.RestartRequired
            ? _config.PendingExpectedMappingDestination
            : ScancodeMap.Targets.TryGetValue(_config.TargetKey, out var target) ? target : (ushort)0;
        var expectedName = ScancodeMap.Targets.FirstOrDefault(x => x.Value == expectedDestination).Key ?? $"0x{expectedDestination:X2}";
        _mapping.Text = result.MappingMatches
            ? $"✓ 0x{source:X2} -> {expectedName} mapped successfully"
            : $"✕ 0x{source:X2} mapping does not match expected {expectedName}";

        if (result.RestartPending)
            _restart.Text = $"⚠ Windows restart required: {_config.RestartReason}";
        else if (result.RestartOccurred)
            _restart.Text = "✓ Windows restart detected";
        else
            _restart.Text = "✓ Windows restart: not pending";

        _gigabyte.Text = FormatGigabyte(result);

        if (result.RestartPending)
        {
            _summary.Text = "Changes are saved, but this is still the same Windows boot. Restart before judging whether the GiMATE button is fixed.";
        }
        else if (result.ExpectedStateMatches)
        {
            _summary.Text = result.GigabyteReady
                ? "Configuration verified successfully. GIGABYTE software is running, so the result is meaningful."
                : "Configuration matches, but no known GIGABYTE listener process is running yet. A button test may be inconclusive until it starts.";

            if (!_debug && _config.RestartRequired && result.RestartOccurred)
            {
                _config.ClearRestartRequired();
                _config.Save();
            }
        }
        else
        {
            _summary.Text = "Verification found a configuration mismatch. Open Advanced -> Copy log file and attach the log to an issue.";
        }

        if (_fromTask && !_debug && result.RestartOccurred)
        {
            VerificationTask.Delete(string.IsNullOrWhiteSpace(_config.VerificationTaskName) ? null : _config.VerificationTaskName);
            if (!string.IsNullOrWhiteSpace(_config.VerificationTaskName))
            {
                _config.VerificationTaskName = "";
                _config.Save();
            }
        }
    }

    private static string FormatGigabyte(VerificationResult result)
    {
        return result.GigabyteReady
            ? $"✓ GIGABYTE listener detected: {string.Join(", ", result.GigabyteProcesses)}"
            : $"⚠ GIGABYTE listener not detected yet. Known names: {string.Join(", ", VerificationService.GigabyteListenerProcessNames)}";
    }
}
