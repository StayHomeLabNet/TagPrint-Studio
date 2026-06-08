using System.Diagnostics;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class SaveCompleteDialog : Form
{
    private readonly CheckBox openFolderCheck = new();
    private readonly string folderPath;
    public bool OpenFolderChecked => openFolderCheck.Checked;

    public SaveCompleteDialog(string title, string message, string folderPath, bool initialChecked = false)
    {
        this.folderPath = folderPath;

        Text = title;
        ClientSize = new Size(360, 170);
        MinimumSize = new Size(360, 170);
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Font;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(31, 41, 55),
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular)
        }, 0, 0);

        openFolderCheck.Text = LocalizationService.T("saveComplete.openFolder");
        openFolderCheck.AutoSize = true;
        openFolderCheck.Dock = DockStyle.Left;
        openFolderCheck.Checked = initialChecked;
        root.Controls.Add(openFolderCheck, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        var okButton = DialogButton(LocalizationService.T("common.ok"), primary: true);
        okButton.Click += (_, _) => Accept();
        buttons.Controls.Add(okButton);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = okButton;
        CancelButton = okButton;
    }

    private void Accept()
    {
        if (openFolderCheck.Checked && Directory.Exists(folderPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
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
