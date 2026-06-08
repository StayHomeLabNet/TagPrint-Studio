using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class BackupCleanupDialog : Form
{
    private readonly IBackupCleanupService service;
    private readonly TextBox folderBox;
    private readonly NumericUpDown retentionDaysBox;
    private readonly NumericUpDown maxCountBox;
    private readonly DataGridView grid;
    private readonly Label summaryLabel;
    private BackupCleanupPreview? preview;

    public string FolderPath => folderBox.Text.Trim();
    public int RetentionDays => (int)retentionDaysBox.Value;
    public int MaxCountPerCsv => (int)maxCountBox.Value;

    public BackupCleanupDialog(IBackupCleanupService service, string initialFolder, int retentionDays, int maxCountPerCsv)
    {
        this.service = service;

        Text = L("バックアップファイル整理", "Clean Up Backup Files");
        Width = 900;
        Height = 620;
        MinimumSize = new Size(760, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Yu Gothic UI", 9F);
        AppIcon.Apply(this);

        folderBox = new TextBox
        {
            Text = initialFolder,
            Dock = DockStyle.Fill,
            Height = 30,
            Margin = new Padding(0, 0, 8, 0)
        };
        retentionDaysBox = NumberBox(Math.Max(1, retentionDays), 1, 9999);
        maxCountBox = NumberBox(Math.Max(1, maxCountPerCsv), 1, 999);
        summaryLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(55, 65, 81),
            Padding = new Padding(8, 0, 0, 0)
        };
        grid = BuildGrid();

        Controls.Add(BuildRoot());
        Shown += (_, _) => RefreshPreview();
    }

    private Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(18, 16, 18, 14),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(summaryLabel, 0, 1);
        root.Controls.Add(grid, 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);
        return root;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            BackColor = Color.White
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        var browseButton = DialogButton(L("参照...", "Browse..."));
        browseButton.Click += (_, _) => BrowseFolder();
        var previewButton = DialogButton(L("再検出", "Refresh"), primary: true);
        previewButton.Click += (_, _) => RefreshPreview();

        panel.Controls.Add(RowLabel(L("対象フォルダー", "Target Folder")), 0, 0);
        panel.Controls.Add(folderBox, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        panel.Controls.Add(previewButton, 5, 0);

        panel.Controls.Add(RowLabel(L("保持日数", "Keep Days")), 0, 1);
        panel.Controls.Add(retentionDaysBox, 1, 1);
        panel.Controls.Add(RowLabel(L("CSVごとの保持数", "Keep per CSV")), 3, 1);
        panel.Controls.Add(maxCountBox, 4, 1);
        return panel;
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.White
        };
        var closeButton = DialogButton(LocalizationService.T("common.close"));
        closeButton.Click += (_, _) => Close();
        var cleanupButton = DialogButton(L("整理を実行", "Clean Up"), primary: true, width: 118);
        cleanupButton.Click += (_, _) => Cleanup();

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(cleanupButton);
        return buttons;
    }

    private static DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.Columns.Add(CheckColumn("DeleteCandidate", L("削除", "Delete"), 58));
        grid.Columns.Add(TextColumn("FileName", L("ファイル名", "File Name"), 260));
        grid.Columns.Add(TextColumn("SourceCsvName", L("元CSV", "Source CSV"), 180));
        grid.Columns.Add(TextColumn("LastWriteTime", L("更新日時", "Modified"), 150));
        grid.Columns.Add(TextColumn("SizeBytes", L("サイズ", "Size"), 90));
        return grid;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(FolderPath) ? FolderPath : ""
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        folderBox.Text = dialog.SelectedPath;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            preview = service.Preview(FolderPath, RetentionDays, MaxCountPerCsv);
            grid.DataSource = preview.Files.Select(file => new BackupCleanupGridRow(file)).ToList();
            summaryLabel.Text = L(
                $"検出: {preview.TotalCount} 件 / 削除候補: {preview.DeleteCount} 件 / {FormatBytes(preview.DeleteBytes)}",
                $"Found: {preview.TotalCount} / To delete: {preview.DeleteCount} / {FormatBytes(preview.DeleteBytes)}");
        }
        catch (Exception ex)
        {
            preview = null;
            grid.DataSource = null;
            summaryLabel.Text = L("対象フォルダーを確認してください。", "Check the target folder.");
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Cleanup()
    {
        if (preview is null)
        {
            RefreshPreview();
        }

        if (preview is null || preview.DeleteCount == 0)
        {
            MessageBox.Show(
                L("削除候補はありません。", "There are no backup files to delete."),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            L(
                $"{preview.DeleteCount} 件のバックアップファイルをゴミ箱へ移動します。続行しますか？",
                $"Move {preview.DeleteCount} backup file(s) to the Recycle Bin?"),
            LocalizationService.T("common.confirm"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = service.Cleanup(preview);
            MessageBox.Show(
                L($"{result.DeletedCount} 件をゴミ箱へ移動しました。", $"Moved {result.DeletedCount} file(s) to the Recycle Bin."),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            RefreshPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label RowLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true
    };

    private static NumericUpDown NumberBox(int value, int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        Width = 90,
        Height = 30,
        Margin = new Padding(0, 2, 0, 0)
    };

    private static Button DialogButton(string text, bool primary = false, int width = 96) => new()
    {
        Text = text,
        Width = width,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
        ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55),
        Margin = new Padding(6, 0, 0, 0)
    };

    private static DataGridViewTextBoxColumn TextColumn(string propertyName, string header, int width) => new()
    {
        Name = propertyName,
        DataPropertyName = propertyName,
        HeaderText = header,
        Width = width
    };

    private static DataGridViewCheckBoxColumn CheckColumn(string propertyName, string header, int width) => new()
    {
        Name = propertyName,
        DataPropertyName = propertyName,
        HeaderText = header,
        Width = width
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:0.#} KB";
        return $"{kb / 1024d:0.#} MB";
    }

    private static string L(string japanese, string english) =>
        LocalizationService.CurrentLanguage == TagPrintStudio.Enums.AppLanguage.Japanese ? japanese : english;

    private sealed class BackupCleanupGridRow(BackupFileCandidate file)
    {
        public bool DeleteCandidate { get; } = file.DeleteCandidate;
        public string FileName { get; } = file.FileName;
        public string SourceCsvName { get; } = file.SourceCsvName;
        public string LastWriteTime { get; } = file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
        public string SizeBytes { get; } = FormatBytes(file.SizeBytes);
    }
}
