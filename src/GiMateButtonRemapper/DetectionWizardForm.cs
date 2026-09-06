namespace GiHATE;

internal sealed class DetectionWizardForm : Form
{
    private readonly RawInputCapture _raw;
    private readonly IntPtr _returnHandle;
    private readonly DetectionSession _session = new();
    private readonly Label _step = new();
    private readonly Label _instruction = new();
    private readonly Label _counter = new();
    private readonly Label _detail = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _cancel = new();
    private DetectionStage _stage = DetectionStage.Button;
    private DetectedProfile? _candidate;
    private bool _safetyCheckPending;

    public DetectedProfile? ResultProfile { get; private set; }

    public DetectionWizardForm(RawInputCapture raw, IntPtr returnHandle)
    {
        _raw = raw;
        _returnHandle = returnHandle;

        Text = "Detect GiMATE button - GiHATE";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 390);
        MinimumSize = new Size(620, 390);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();

        _raw.Keyboard += OnKeyboard;
        _raw.Hid += OnHid;

        Shown += (_, _) =>
        {
            try { _raw.Register(Handle); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Raw Input registration failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
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
        _raw.Hid -= OnHid;
        try { if (_returnHandle != IntPtr.Zero) _raw.Register(_returnHandle); } catch { }
        base.OnFormClosed(e);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(28, 24, 28, 22)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _step.Text = "Step 1 of 2";
        _step.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _step.ForeColor = SystemColors.GrayText;
        _step.AutoSize = true;
        _step.Margin = new Padding(0, 0, 0, 8);

        _instruction.Text = "Press the GiMATE button 3 times";
        _instruction.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        _instruction.AutoSize = true;
        _instruction.MaximumSize = new Size(540, 0);
        _instruction.Margin = new Padding(0, 0, 0, 18);

        _counter.Text = "GiMATE presses detected: 0 / 3";
        _counter.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        _counter.AutoSize = true;
        _counter.Margin = new Padding(0, 0, 0, 8);

        _progress.Minimum = 0;
        _progress.Maximum = 3;
        _progress.Value = 0;
        _progress.Height = 22;
        _progress.Dock = DockStyle.Top;
        _progress.Margin = new Padding(0, 0, 0, 16);

        _detail.Text = "Press only the GiMATE button during this step. GiHATE waits for both the keyboard scan code and the matching vendor HID report from each press.";
        _detail.AutoSize = true;
        _detail.MaximumSize = new Size(540, 0);
        _detail.ForeColor = SystemColors.GrayText;
        _detail.Margin = new Padding(0);

        _cancel.Text = "Cancel";
        _cancel.AutoSize = true;
        _cancel.Padding = new Padding(12, 4, 12, 4);
        _cancel.Anchor = AnchorStyles.Right;
        _cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        root.Controls.Add(_step, 0, 0);
        root.Controls.Add(_instruction, 0, 1);
        root.Controls.Add(_counter, 0, 2);
        root.Controls.Add(_progress, 0, 3);
        root.Controls.Add(_detail, 0, 4);
        root.Controls.Add(_cancel, 0, 5);
    }

    private void OnKeyboard(KeyboardRawEvent e)
    {
        if (_stage == DetectionStage.Button)
        {
            _session.Add(e);
            UpdateButtonProgress();
            TryCompleteButtonStage();
            return;
        }

        if (_stage != DetectionStage.Safety || _candidate is null)
            return;

        _session.Add(e);

        if (!e.IsKeyDown || e.ScanCode == _candidate.SourceScanCode || _safetyCheckPending)
            return;

        _safetyCheckPending = true;
        BeginInvoke(() =>
        {
            var keyName = e.VKey == 0 ? $"scan 0x{e.ScanCode:X2}" : ((Keys)e.VKey).ToString();
            _counter.Text = $"Normal key detected: {keyName}";
            _detail.Text = "Verifying that this normal keyboard press does not also come from the vendor HID interface GiHATE plans to disable...";
            _progress.Style = ProgressBarStyle.Marquee;
        });
        _ = VerifySafetyAfterDelayAsync();
    }

    private void OnHid(HidRawEvent e)
    {
        if (_stage == DetectionStage.Done)
            return;

        _session.Add(e);

        if (_stage == DetectionStage.Button)
        {
            UpdateButtonProgress();
            TryCompleteButtonStage();
        }
    }

    private void UpdateButtonProgress()
    {
        var progress = _session.GetButtonPressProgress();
        BeginInvoke(() =>
        {
            _counter.Text = $"GiMATE presses detected: {progress} / 3";
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = progress;
        });
    }

    private void TryCompleteButtonStage()
    {
        if (_stage != DetectionStage.Button)
            return;

        var candidate = _session.FindButtonCandidate();
        if (candidate is null)
            return;

        _candidate = candidate;
        _session.Clear();
        _stage = DetectionStage.Safety;

        BeginInvoke(() =>
        {
            _step.Text = "Step 2 of 2";
            _instruction.Text = "Press one normal keyboard key";
            _counter.Text = "Waiting for A, Space, or another normal key";
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Maximum = 1;
            _progress.Value = 0;
            _detail.Text = $"GiMATE was detected after exactly 3 presses. Scan code: 0x{candidate.SourceScanCode:X2}. Vendor report: {candidate.VendorReportHex}.\n\nNow press one normal built-in keyboard key so GiHATE can verify it is not about to disable your keyboard.";
        });
    }

    private async Task VerifySafetyAfterDelayAsync()
    {
        await Task.Delay(400);

        if (_stage != DetectionStage.Safety || _candidate is null)
            return;

        var passed = _session.NormalKeySafetyPassed(_candidate);
        _safetyCheckPending = false;

        if (!passed)
        {
            _session.Clear();
            BeginInvoke(() =>
            {
                _counter.Text = "Safety check did not pass";
                _progress.Style = ProgressBarStyle.Blocks;
                _progress.Maximum = 1;
                _progress.Value = 0;
                _detail.Text = "That press did not verify cleanly. Press another normal built-in keyboard key. Nothing has been changed on your system yet.";
            });
            return;
        }

        if (_candidate.VendorUsagePage < 0xFF00 || string.IsNullOrWhiteSpace(_candidate.VendorInstanceId) || string.IsNullOrWhiteSpace(_candidate.KeyboardInstanceId))
        {
            BeginInvoke(() =>
            {
                MessageBox.Show(this, "The candidate failed GiHATE's vendor-HID safety checks. Nothing was changed.", "Detection failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
            });
            return;
        }

        ResultProfile = _candidate;
        _stage = DetectionStage.Done;

        BeginInvoke(() =>
        {
            _counter.Text = "Safety check passed";
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Maximum = 1;
            _progress.Value = 1;
            _detail.Text = "Detection is complete. No device or registry changes have been made yet.";
            DialogResult = DialogResult.OK;
            Close();
        });
    }

    private enum DetectionStage
    {
        Button,
        Safety,
        Done
    }
}
