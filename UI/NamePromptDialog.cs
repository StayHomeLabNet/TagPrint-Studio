using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class NamePromptDialog : Form
{
    private readonly TextBox nameBox = new();

    public string Value => nameBox.Text.Trim();

    public NamePromptDialog(string title, string label, string initialValue)
    {
        Text = title;
        ClientSize = new Size(420, 164);
        MinimumSize = new Size(420, 164);
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Font;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = Color.FromArgb(244, 246, 248) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold) }, 0, 0);

        nameBox.Dock = DockStyle.Fill;
        nameBox.BorderStyle = BorderStyle.FixedSingle;
        nameBox.Text = initialValue;
        nameBox.SelectAll();
        root.Controls.Add(nameBox, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        var okButton = DialogButton(LocalizationService.T("common.ok"), primary: true);
        var cancelButton = DialogButton(LocalizationService.T("common.cancel"));
        cancelButton.DialogResult = DialogResult.Cancel;
        okButton.Click += (_, _) => Accept();
        buttons.Controls.AddRange([cancelButton, okButton]);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            MessageBox.Show(LocalizationService.T("namePrompt.required"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button DialogButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = 80,
            Height = 30,
            Margin = new Padding(4, 0, 4, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(216, 222, 230);
        return button;
    }
}
