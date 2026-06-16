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

        Text = LocalizationService.T("backupCleanup.cleanUpBackupFiles");
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

        var browseButton = DialogButton(LocalizationService.T("backupCleanup.browse"));
        browseButton.Click += (_, _) => BrowseFolder();
        var previewButton = DialogButton(LocalizationService.T("backupCleanup.refresh"), primary: true);
        previewButton.Click += (_, _) => RefreshPreview();

        panel.Controls.Add(RowLabel(LocalizationService.T("backupCleanup.targetFolder")), 0, 0);
        panel.Controls.Add(folderBox, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        panel.Controls.Add(previewButton, 5, 0);

        panel.Controls.Add(RowLabel(LocalizationService.T("backupCleanup.keepDays")), 0, 1);
        panel.Controls.Add(retentionDaysBox, 1, 1);
        panel.Controls.Add(RowLabel(LocalizationService.T("backupCleanup.keepPerCsv")), 3, 1);
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
        closeButton.DialogResult = DialogResult.Cancel;
        closeButton.Click += (_, _) => Close();
        var saveButton = DialogButton(LocalizationService.T("backupCleanup.saveSettings"));
        saveButton.Click += (_, _) => SaveSettingsAndClose();
        var cleanupButton = DialogButton(LocalizationService.T("backupCleanup.cleanUp"), primary: true, width: 118);
        cleanupButton.Click += (_, _) => Cleanup();

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(saveButton);
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

        grid.Columns.Add(CheckColumn("DeleteCandidate", LocalizationService.T("backupCleanup.delete"), 58));
        grid.Columns.Add(TextColumn("FileName", LocalizationService.T("backupCleanup.fileName"), 260));
        grid.Columns.Add(TextColumn("SourceCsvName", LocalizationService.T("backupCleanup.sourceCsv"), 180));
        grid.Columns.Add(TextColumn("LastWriteTime", LocalizationService.T("backupCleanup.modified"), 150));
        grid.Columns.Add(TextColumn("SizeBytes", LocalizationService.T("backupCleanup.size"), 90));
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
            summaryLabel.Text = LocalizationService.T(
                "backupCleanup.previewSummary",
                preview.TotalCount,
                preview.DeleteCount,
                FormatBytes(preview.DeleteBytes));
        }
        catch (Exception ex)
        {
            preview = null;
            grid.DataSource = null;
            summaryLabel.Text = LocalizationService.T("backupCleanup.checkTheTargetFolder");
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
                LocalizationService.T("backupCleanup.thereAreNoBackupFilesToDelete"),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            LocalizationService.T("backupCleanup.confirmMoveToRecycleBin", preview.DeleteCount),
            LocalizationService.T("common.confirm"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = service.Cleanup(preview);
            MessageBox.Show(
                LocalizationService.T("backupCleanup.movedToRecycleBin", result.DeletedCount),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettingsAndClose()
    {
        DialogResult = DialogResult.OK;
        Close();
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

    private sealed class BackupCleanupGridRow(BackupFileCandidate file)
    {
        public bool DeleteCandidate { get; } = file.DeleteCandidate;
        public string FileName { get; } = file.FileName;
        public string SourceCsvName { get; } = file.SourceCsvName;
        public string LastWriteTime { get; } = file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
        public string SizeBytes { get; } = FormatBytes(file.SizeBytes);
    }
}
