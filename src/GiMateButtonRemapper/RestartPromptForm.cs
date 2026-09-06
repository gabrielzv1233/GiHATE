namespace GiHATE;

internal sealed class RestartPromptForm : Form
{
    private readonly RadioButton _restartNow = new() { Text = "Restart now (recommended)", Checked = true, AutoSize = true };
    private readonly RadioButton _restartLater = new() { Text = "Restart later", AutoSize = true };

    public bool RestartNow => _restartNow.Checked;

    public RestartPromptForm(string action)
    {
        Text = "Restart required";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 300);
        MinimumSize = new Size(540, 300);
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(24, 22, 24, 20)
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
            Text = "A Windows restart is required",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true
        });

        Add(new Label
        {
            Text = $"{action}\n\nThe HID/scancode change will not be active until Windows restarts. GiHATE also schedules a one-shot verification window for the next interactive logon.",
            AutoSize = true,
            MaximumSize = new Size(480, 0)
        }, 12);

        Add(_restartNow, 18);
        Add(_restartLater, 6);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var ok = new Button
        {
            Text = "Continue",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(100, 38)
        };
        buttons.Controls.Add(ok);
        Add(buttons, 20);
        AcceptButton = ok;
    }
}
