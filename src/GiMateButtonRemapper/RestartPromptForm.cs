namespace GiHATE;

internal sealed class RestartPromptForm : Form
{
    private readonly RadioButton _restartNow = new() { Text = "Restart now (recommended)", Checked = true, AutoSize = true };
    private readonly RadioButton _restartLater = new() { Text = "Restart later", AutoSize = true };
    public bool RestartNow => _restartNow.Checked;

    public RestartPromptForm(string action)
    {
        Text = "Restart required"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(500, 220);
        var title = new Label { Text = "A Windows restart is required", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Location = new Point(20, 20) };
        var body = new Label { Text = $"{action}\n\nThe scan-code change will not do anything until Windows restarts.", AutoSize = false, Size = new Size(455, 70), Location = new Point(20, 50) };
        _restartNow.Location = new Point(24, 125); _restartLater.Location = new Point(24, 150);
        var ok = new Button { Text = "Continue", DialogResult = DialogResult.OK, Width = 95, Location = new Point(380, 170) };
        Controls.AddRange([title, body, _restartNow, _restartLater, ok]); AcceptButton = ok;
    }
}
