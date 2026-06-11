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
        deleteMergedSourcesCheck = TopCheckBox(L("マージ後に不要CSVをゴミ箱に移動", "Move merged source CSVs to Recycle Bin"), settings.LastMergeDeleteSourceFiles, 320);
        deleteMergedSourcesCheck.CheckedChanged += (_, _) => settings.LastMergeDeleteSourceFiles = deleteMergedSourcesCheck.Checked;
        deleteMergeBakCheck = TopCheckBox(L("マージ後にbakファイルをゴミ箱に移動", "Move bak file to Recycle Bin after merge"), settings.LastMergeDeleteBakFiles, 320);
        deleteMergeBakCheck.CheckedChanged += (_, _) => settings.LastMergeDeleteBakFiles = deleteMergeBakCheck.Checked;
        deleteMergeBakCheck.Margin = new Padding(25, 2, 12, 2);
        var browseFolderButton = Button(L("参照...", "Browse..."), (_, _) => BrowseMergeFolder());
        var reloadButton = Button(L("再読込", "Reload"), (_, _) => ReloadMergeFolderFiles());
        var previewButton = Button(L("プレビュー", "Preview"), async (_, _) => await PreviewMergeAsync(), primary: true);
        var mergeOptionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };
        mergeOptionRow.Controls.Add(deleteMergedSourcesCheck);
        mergeOptionRow.Controls.Add(deleteMergeBakCheck);

        top.Controls.Add(TopLabel(L("対象フォルダー", "Target Folder")), 0, 0);
        top.Controls.Add(mergeFolderBox, 1, 0);
        top.SetColumnSpan(mergeFolderBox, 3);
        top.Controls.Add(browseFolderButton, 4, 0);
        top.Controls.Add(reloadButton, 5, 0);
        top.Controls.Add(TopLabel(L("親CSV", "Parent CSV")), 0, 1);
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
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.RoleText), L("対象", "Role"), 70));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.FileName), L("ファイル名", "File Name"), 200));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.RowCount), L("行数", "Rows"), 70));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.HeaderMatchText), L("ヘッダー", "Header"), 100));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.Status), L("状態", "Status"), 110));
        mergeGrid.Columns.Add(TextColumn(nameof(CsvMergeCandidate.Note), L("備考", "Note"), 320));
        root.Controls.Add(mergeGrid, 0, 1);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.White, Padding = new Padding(10) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mergeSummaryLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 85, 99) };
        UpdateMergeSummary(null);
        var mergeApplyButton = Button(L("実行", "Run"), async (_, _) => await ApplyMergeAsync(), primary: true);
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
            MessageBox.Show(L("親CSVを選択してください。", "Please choose a parent CSV."), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(L("先にプレビューを実行してください。", "Please run preview first."), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (lastMergePreview.AppendFileCount == 0)
        {
            MessageBox.Show(L("追加対象のCSVがありません。", "There are no CSV files to append."), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var message = string.Format(
            L("{1}件のCSVから {2} 行を {0} に追加します。よろしいですか？", "Append {2} row(s) from {1} CSV file(s) into {0}?"),
            lastMergePreview.ParentFileName,
            lastMergePreview.AppendFileCount,
            lastMergePreview.AppendRowCount);
        if (lastMergePreview.SkipFileCount > 0)
        {
            message += Environment.NewLine + string.Format(L("{0}件のCSVはスキップされます。", "{0} CSV file(s) will be skipped."), lastMergePreview.SkipFileCount);
        }
        if (deleteMergedSourcesCheck.Checked)
        {
            var csvNames = string.Join(", ", lastMergePreview.AppendFilePaths.Select(Path.GetFileName));
            message += Environment.NewLine + string.Format(L("ゴミ箱へ移動するCSV: {0}", "CSV files moved to Recycle Bin: {0}"), csvNames);
        }
        if (deleteMergeBakCheck.Checked)
        {
            var bakName = Path.GetFileName(lastMergePreview.ParentCsvPath) + ".bak-yyyymmdd-hhmmss";
            message += Environment.NewLine + string.Format(L("ゴミ箱へ移動するbak: {0}", "bak file moved to Recycle Bin: {0}"), bakName);
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
            ? L("親CSVを選んでプレビューを実行してください。", "Choose a parent CSV and run preview.")
            : string.Format(
                L("親CSV: {0}  追加CSV: {1}  追加行数: {2}  スキップ: {3}  結果行数: {4}",
                    "Parent: {0}  Append Files: {1}  Append Rows: {2}  Skip: {3}  Result Rows: {4}"),
                preview.ParentFileName,
                preview.AppendFileCount,
                preview.AppendRowCount,
                preview.SkipFileCount,
                preview.ResultRowCount);
    }
}
