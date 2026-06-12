using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class StartupBackupCleanupDialog : Form
{
    private readonly CheckBox autoRunCheck = new();
    private readonly CheckBox disableCheck = new();
    private readonly DataGridView filesGrid;

    public bool EnableAutoRunChecked => autoRunCheck.Checked;
    public bool DisableStartupCleanupChecked => disableCheck.Checked;

    public StartupBackupCleanupDialog(
        string title,
        string message,
        string folderPath,
        int deleteCount,
        long deleteBytes,
        IReadOnlyList<BackupFileCandidate> filesToClean)
    {
        Text = title;
        ClientSize = new Size(780, 570);
        MinimumSize = new Size(720, 540);
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);
        FormBorderStyle = FormBorderStyle.Sizable;
        AutoScaleMode = AutoScaleMode.Font;

        filesGrid = BuildFilesGrid();
        filesGrid.DataSource = filesToClean.Select(file => new StartupBackupCleanupGridRow(file)).ToList();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            Padding = new Padding(14),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
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

        root.Controls.Add(new Label
        {
            Text = LocalizationService.T("backupCleanup.filesToClean"),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(31, 41, 55),
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold)
        }, 0, 2);
        root.Controls.Add(filesGrid, 0, 3);

        autoRunCheck.Text = LocalizationService.T("backupCleanup.startup.autoRunWithoutAsking");
        autoRunCheck.AutoSize = true;
        autoRunCheck.Dock = DockStyle.Left;
        autoRunCheck.Margin = new Padding(0, 2, 0, 4);
        autoRunCheck.CheckedChanged += (_, _) =>
        {
            if (autoRunCheck.Checked)
            {
                disableCheck.Checked = false;
            }
        };

        disableCheck.Text = LocalizationService.T("backupCleanup.startup.disableStartupCleanup");
        disableCheck.AutoSize = true;
        disableCheck.Dock = DockStyle.Left;
        disableCheck.Margin = new Padding(0, 4, 0, 2);
        disableCheck.CheckedChanged += (_, _) =>
        {
            if (disableCheck.Checked)
            {
                autoRunCheck.Checked = false;
            }
        };

        var optionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        optionsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        optionsPanel.Controls.Add(autoRunCheck, 0, 0);
        optionsPanel.Controls.Add(disableCheck, 0, 1);
        root.Controls.Add(optionsPanel, 0, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var yesButton = DialogButton(LocalizationService.T("common.yes"), primary: true);
        var noButton = DialogButton(LocalizationService.T("common.no"));
        yesButton.Click += (_, _) => Accept();
        noButton.Click += (_, _) => Decline();
        buttons.Controls.AddRange([noButton, yesButton]);
        root.Controls.Add(buttons, 0, 5);

        AcceptButton = yesButton;
        CancelButton = noButton;
    }


    private static DataGridView BuildFilesGrid()
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
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.Both
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(StartupBackupCleanupGridRow.FileName),
            DataPropertyName = nameof(StartupBackupCleanupGridRow.FileName),
            HeaderText = LocalizationService.T("backupCleanup.fileName"),
            Width = 300,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 52
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(StartupBackupCleanupGridRow.SourceCsvName),
            DataPropertyName = nameof(StartupBackupCleanupGridRow.SourceCsvName),
            HeaderText = LocalizationService.T("backupCleanup.sourceCsv"),
            Width = 210,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 32
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(StartupBackupCleanupGridRow.LastWriteTime),
            DataPropertyName = nameof(StartupBackupCleanupGridRow.LastWriteTime),
            HeaderText = LocalizationService.T("backupCleanup.modified"),
            Width = 150
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(StartupBackupCleanupGridRow.SizeBytes),
            DataPropertyName = nameof(StartupBackupCleanupGridRow.SizeBytes),
            HeaderText = LocalizationService.T("backupCleanup.size"),
            Width = 90
        });

        return grid;
    }

    private static string BuildDetailsText(string folderPath, int deleteCount, long deleteBytes)
    {
        var sizeText = FormatBytes(deleteBytes);
        return string.Join(Environment.NewLine,
            string.Format(LocalizationService.T("backupCleanup.details.deleteCount"), deleteCount),
            string.Format(LocalizationService.T("backupCleanup.details.deleteSize"), sizeText),
            string.Format(LocalizationService.T("backupCleanup.details.targetFolder"), folderPath));
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

    private sealed class StartupBackupCleanupGridRow(BackupFileCandidate file)
    {
        public string FileName { get; } = file.FileName;
        public string SourceCsvName { get; } = file.SourceCsvName;
        public string LastWriteTime { get; } = file.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
        public string SizeBytes { get; } = FormatBytes(file.SizeBytes);
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
