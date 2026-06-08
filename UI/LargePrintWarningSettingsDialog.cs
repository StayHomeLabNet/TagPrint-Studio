using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class LargePrintWarningSettingsDialog : Form
{
    private readonly CheckBox enabledCheck;
    private readonly NumericUpDown thresholdBox;

    public int Threshold => enabledCheck.Checked ? (int)thresholdBox.Value : 0;

    public LargePrintWarningSettingsDialog(int currentThreshold)
    {
        Text = LocalizationService.T("main.largePrintSettings.title");
        Width = 420;
        Height = 205;
        MinimumSize = new Size(420, 205);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.White;

        enabledCheck = new CheckBox
        {
            Text = LocalizationService.T("main.largePrintSettings.enabled"),
            Checked = currentThreshold > 0,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };
        thresholdBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 9999,
            Value = Math.Max(1, currentThreshold > 0 ? currentThreshold : 100),
            Width = 100,
            Height = 30,
            Margin = new Padding(0, 0, 0, 0)
        };
        enabledCheck.CheckedChanged += (_, _) => thresholdBox.Enabled = enabledCheck.Checked;
        thresholdBox.Enabled = enabledCheck.Checked;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(18, 16, 18, 14),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var thresholdRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        thresholdRow.Controls.Add(new Label
        {
            Text = LocalizationService.T("main.largePrintSettings.threshold"),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 12, 0)
        });
        thresholdRow.Controls.Add(thresholdBox);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        var okButton = DialogButton(LocalizationService.T("common.ok"), primary: true);
        var cancelButton = DialogButton(LocalizationService.T("common.cancel"));
        okButton.DialogResult = DialogResult.OK;
        cancelButton.DialogResult = DialogResult.Cancel;
        AcceptButton = okButton;
        CancelButton = cancelButton;
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        root.Controls.Add(enabledCheck, 0, 0);
        root.Controls.Add(thresholdRow, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
    }

    private static Button DialogButton(string text, bool primary = false) => new()
    {
        Text = text,
        Width = 96,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
        ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55),
        Margin = new Padding(6, 0, 0, 0)
    };
}
