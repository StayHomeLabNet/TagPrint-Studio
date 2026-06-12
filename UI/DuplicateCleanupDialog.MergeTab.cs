using System.ComponentModel;
using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed partial class DuplicateCleanupDialog : Form
{
    private Control BuildMergeTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, BackColor = Color.White, Padding = new Padding(10) };
        top.RowCount = 3;
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        mergeFolderBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(4, 2, 8, 0) };
        parentCsvCombo = Combo();
        deleteMergedSourcesCheck = TopCheckBox(LocalizationService.T("duplicateCleanup.moveMergedSourceCsvsToRecycleBin"), settings.LastMergeDeleteSourceFiles, 320);
        deleteMergedSourcesCheck.CheckedChanged += (_, _) => settings.LastMergeDeleteSourceFiles = deleteMergedSourcesCheck.Checked;
        deleteMergeBakCheck = TopCheckBox(LocalizationService.T("duplicateCleanup.moveBakFileToRecycleBinAfterMerge"), settings.LastMergeDeleteBakFiles, 320);
        deleteMergeBakCheck.CheckedChanged += (_, _) => settings.LastMergeDeleteBakFiles = deleteMergeBakCheck.Checked;
        deleteMergeBakCheck.Margin = new Padding(25, 2, 12, 2);
        var browseFolderButton = Button(LocalizationService.T("duplicateCleanup.browse"), (_, _) => BrowseMergeFolder());
        var reloadButton = Button(LocalizationService.T("duplicateCleanup.reload"), (_, _) => ReloadMergeFolderFiles());
        var previewButton = Button(LocalizationService.T("duplicateCleanup.preview"), async (_, _) => await PreviewMergeAsync(), primary: true);
        var mergeOptionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };
        mergeOptionRow.Controls.Add(deleteMergedSourcesCheck);
        mergeOptionRow.Controls.Add(deleteMergeBakCheck);

        top.Controls.Add(TopLabel(LocalizationService.T("duplicateCleanup.targetFolder")), 0, 0);
        top.Controls.Add(mergeFolderBox, 1, 0);
        top.SetColumnSpan(mergeFolderBox, 3);
        top.Controls.Add(browseFolderButton, 4, 0);
        top.Controls.Add(reloadButton, 5, 0);
        top.Controls.Add(TopLabel(LocalizationService.T("duplicateCleanup.parentCsv")), 0, 1);
        top.Controls.Add(parentCsvCombo, 1, 1);
        top.SetColumnSpan(parentCsvCombo, 5);
        top.Controls.Add(previewButton, 7, 1);
        top.Controls.Add(mergeOptionRow, 1, 2);
        top.SetColumnSpan(mergeOptionRow, 5);
        root.Controls.Add(top, 0, 0);

        mergeGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = mergeRows,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToResizeRows = false
        };
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.RoleText), LocalizationService.T("duplicateCleanup.role"), 70));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.FileName), LocalizationService.T("duplicateCleanup.fileName"), 200));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.RowCount), LocalizationService.T("duplicateCleanup.rows"), 70));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.HeaderMatchText), LocalizationService.T("duplicateCleanup.header"), 100));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.Status), LocalizationService.T("duplicateCleanup.status"), 110));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.Note), LocalizationService.T("duplicateCleanup.note"), 320));
        root.Controls.Add(mergeGrid, 0, 1);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.White, Padding = new Padding(10) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mergeSummaryLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 85, 99) };
        UpdateMergeSummary(null);
        var mergeApplyButton = Button(LocalizationService.T("duplicateCleanup.run"), async (_, _) => await ApplyMergeAsync(), primary: true);
        var closeButton = Button(LocalizationService.T("common.close"), (_, _) => CloseDialog());
        bottom.Controls.Add(mergeSummaryLabel, 0, 0);
        bottom.Controls.Add(mergeApplyButton, 1, 0);
        bottom.Controls.Add(closeButton, 2, 0);
        root.Controls.Add(bottom, 0, 2);

        return root;
    }

    private void InitializeMergeTab()
    {
        var folder = Directory.Exists(settings.LastMergeFolderPath) ? settings.LastMergeFolderPath : DetectInitialMergeFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            mergeFolderBox.Text = folder;
            ReloadMergeFolderFiles();
        }
    }

    private string DetectInitialMergeFolder()
    {
        var folders = csvFiles
            .Select(file => Path.GetDirectoryName(file.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return folders.Count == 1 ? folders[0]! : folders.FirstOrDefault() ?? "";
    }

    private void BrowseMergeFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = mergeFolderBox.Text };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        mergeFolderBox.Text = dialog.SelectedPath;
        settings.LastMergeFolderPath = dialog.SelectedPath;
        ReloadMergeFolderFiles();
    }

    private void ReloadMergeFolderFiles()
    {
        var folder = mergeFolderBox.Text.Trim();
        parentCsvCombo.BeginUpdate();
        parentCsvCombo.Items.Clear();
        mergeRows.Clear();
        lastMergePreview = null;
        UpdateMergeSummary(null);

        if (Directory.Exists(folder))
        {
            var csvPaths = Directory.GetFiles(folder, "*.csv")
                .Where(path => !CsvFileUtility.IsPrintQueueCsv(path))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            foreach (var path in csvPaths)
            {
                parentCsvCombo.Items.Add(new CsvPathItem { Path = path, DisplayName = Path.GetFileName(path) });
            }

            if (parentCsvCombo.Items.Count > 0)
            {
                var preferred = SelectLargestCsvPath(csvPaths);
                var preferredIndex = preferred is null
                    ? -1
                    : parentCsvCombo.Items.Cast<CsvPathItem>().ToList().FindIndex(item => string.Equals(item.Path, preferred, StringComparison.OrdinalIgnoreCase));
                parentCsvCombo.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
            }
        }

        parentCsvCombo.EndUpdate();
    }

    private static string? SelectLargestCsvPath(IEnumerable<string> csvPaths)
    {
        return csvPaths
            .OrderByDescending(path =>
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch
                {
                    return -1L;
                }
            })
            .ThenBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private async Task PreviewMergeAsync()
    {
        if (parentCsvCombo.SelectedItem is not CsvPathItem parent)
        {
            MessageBox.Show(LocalizationService.T("duplicateCleanup.pleaseChooseAParentCsv"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            settings.LastMergeFolderPath = mergeFolderBox.Text.Trim();
            settings.LastMergeParentCsvPath = parent.Path;
            var options = new CsvMergeOptions
            {
                FolderPath = mergeFolderBox.Text.Trim(),
                ParentCsvPath = parent.Path,
                HasHeaderRow = hasHeaderRow,
                DeleteMergedSourceFiles = deleteMergedSourcesCheck.Checked,
                DeleteBakFileAfterMerge = deleteMergeBakCheck.Checked
            };
            CsvMergePreview? preview = null;
            await RunBusyAsync(async () => preview = await Task.Run(() => mergeService.Preview(options, preset)));
            if (preview is null)
            {
                return;
            }

            lastMergePreview = preview;

            mergeRows.Clear();
            foreach (var item in lastMergePreview.Items)
            {
                mergeRows.Add(item);
            }

            UpdateMergeSummary(lastMergePreview);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ApplyMergeAsync()
    {
        if (lastMergePreview is null)
        {
            MessageBox.Show(LocalizationService.T("duplicateCleanup.pleaseRunPreviewFirst"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (lastMergePreview.AppendFileCount == 0)
        {
            MessageBox.Show(LocalizationService.T("duplicateCleanup.thereAreNoCsvFilesToAppend"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var message = string.Format(
            LocalizationService.T("duplicateCleanup.append2RowSFrom1CsvFileSInto0"),
            lastMergePreview.ParentFileName,
            lastMergePreview.AppendFileCount,
            lastMergePreview.AppendRowCount);
        if (lastMergePreview.SkipFileCount > 0)
        {
            message += Environment.NewLine + string.Format(LocalizationService.T("duplicateCleanup.0CsvFileSWillBeSkipped"), lastMergePreview.SkipFileCount);
        }
        if (deleteMergedSourcesCheck.Checked)
        {
            var csvNames = string.Join(", ", lastMergePreview.AppendFilePaths.Select(Path.GetFileName));
            message += Environment.NewLine + string.Format(LocalizationService.T("duplicateCleanup.csvFilesMovedToRecycleBin0"), csvNames);
        }
        if (deleteMergeBakCheck.Checked)
        {
            var bakName = Path.GetFileName(lastMergePreview.ParentCsvPath) + ".bak-yyyymmdd-hhmmss";
            message += Environment.NewLine + string.Format(LocalizationService.T("duplicateCleanup.bakFileMovedToRecycleBin0"), bakName);
        }

        if (MessageBox.Show(message, LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            settings.LastMergeDeleteSourceFiles = deleteMergedSourcesCheck.Checked;
            settings.LastMergeDeleteBakFiles = deleteMergeBakCheck.Checked;
            lastMergePreview.DeleteMergedSourceFiles = deleteMergedSourcesCheck.Checked;
            lastMergePreview.DeleteBakFileAfterMerge = deleteMergeBakCheck.Checked;
            await RunBusyAsync(() => Task.Run(() => mergeService.Apply(lastMergePreview, preset)));
            reloadCsvPaths.Clear();
            reloadCsvPaths.AddRange(Directory.GetFiles(lastMergePreview.FolderPath, "*.csv")
                .Where(path => !CsvFileUtility.IsPrintQueueCsv(path))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase));
            Applied = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(CsvFileUtility.BuildUserFriendlySaveErrorMessage(ex), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateMergeSummary(CsvMergePreview? preview)
    {
        mergeSummaryLabel.Text = preview is null
            ? LocalizationService.T("duplicateCleanup.chooseAParentCsvAndRunPreview")
            : string.Format(
                LocalizationService.T("duplicateCleanup.parent0AppendFiles1AppendRows2Skip3ResultRows4"),
                preview.ParentFileName,
                preview.AppendFileCount,
                preview.AppendRowCount,
                preview.SkipFileCount,
                preview.ResultRowCount);
    }
}
