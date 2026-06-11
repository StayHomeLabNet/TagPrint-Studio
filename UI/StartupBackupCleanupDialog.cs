using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class StartupBackupCleanupDialog : Form
{
    private readonly CheckBox autoRunCheck = new();
    private readonly CheckBox disableCheck = new();

    public bool EnableAutoRunChecked => autoRunCheck.Checked;
    public bool DisableStartupCleanupChecked => disableCheck.Checked;

    public StartupBackupCleanupDialog(string title, string message, string folderPath, int deleteCount, long deleteBytes)
    {
        Text = title;
        ClientSize = new Size(500, 260);
        MinimumSize = new Size(500, 260);
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Font;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(14),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

        var detailsLabel = new Label
        {
            Text = BuildDetailsText(folderPath, deleteCount, deleteBytes),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Color.FromArgb(75, 85, 99),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(detailsLabel, 0, 1);

        autoRunCheck.Text = LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
            ? "今後は確認せず自動実行する"
            : "Run automatically next time without asking";
        autoRunCheck.AutoSize = true;
        autoRunCheck.Dock = DockStyle.Left;
        autoRunCheck.CheckedChanged += (_, _) =>
        {
            if (autoRunCheck.Checked)
            {
                disableCheck.Checked = false;
            }
        };

        disableCheck.Text = LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
            ? "今後は起動時整理をしない"
            : "Do not run cleanup on startup anymore";
        disableCheck.AutoSize = true;
        disableCheck.Dock = DockStyle.Left;
        disableCheck.Margin = new Padding(0, 22, 0, 0);
        disableCheck.CheckedChanged += (_, _) =>
        {
            if (disableCheck.Checked)
            {
                autoRunCheck.Checked = false;
            }
        };

        var optionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        optionsPanel.Controls.Add(autoRunCheck);
        optionsPanel.Controls.Add(disableCheck);
        root.Controls.Add(optionsPanel, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var yesButton = DialogButton(LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese ? "はい" : "Yes", primary: true);
        var noButton = DialogButton(LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese ? "いいえ" : "No");
        yesButton.Click += (_, _) => Accept();
        noButton.Click += (_, _) => Decline();
        buttons.Controls.AddRange([noButton, yesButton]);
        root.Controls.Add(buttons, 0, 3);

        AcceptButton = yesButton;
        CancelButton = noButton;
    }

    private static string BuildDetailsText(string folderPath, int deleteCount, long deleteBytes)
    {
        var sizeText = FormatBytes(deleteBytes);
        if (LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese)
        {
            return $"対象件数: {deleteCount} 件{Environment.NewLine}削除予定サイズ: {sizeText}{Environment.NewLine}対象フォルダー: {folderPath}";
        }

        return $"Files to clean: {deleteCount}{Environment.NewLine}Estimated size to remove: {sizeText}{Environment.NewLine}Target folder: {folderPath}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private void Accept()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Decline()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static Button DialogButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = 96,
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
