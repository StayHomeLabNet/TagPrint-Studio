using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;
using Microsoft.VisualBasic.FileIO;

namespace TagPrintStudio.UI;

public sealed partial class MainForm
{
    private Control BuildCsvImportSection()
    {
        var csvTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = HeaderBackColor };
        recentCsvCombo = new ComboBox
        {
            Size = new Size(240, 30),
            AutoSize = false,
            Height = 30,
            IntegralHeight = false,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0)
        };
        recentCsvCombo.SelectedIndexChanged += (_, _) => OpenRecentCsvSelection();

        const int csvToolHeight = 30;
        var selectCsvButton = Button("main.button.selectCsv", (_, _) => SelectCsvFiles(), primary: true);
        var selectFolderButton = Button("フォルダー選択", (_, _) => SelectCsvFolder());
        duplicateCleanupButton = Button("重複整理", (_, _) => OpenDuplicateCleanup());
        tagFinderButton = Button("タグ検索", (_, _) => OpenTagFinder());
        MatchToolbarHeight(selectCsvButton, csvToolHeight);
        MatchToolbarHeight(selectFolderButton, csvToolHeight);
        MatchToolbarHeight(duplicateCleanupButton, csvToolHeight);
        MatchToolbarHeight(tagFinderButton, csvToolHeight);
        csvTools.Controls.AddRange([selectCsvButton, selectFolderButton, duplicateCleanupButton, tagFinderButton, recentCsvCombo]);

        csvGrid = Grid(csvRows);
        csvGrid.AutoGenerateColumns = false;
        csvGrid.Columns.Add(TextColumn("FileName", "ファイル名", 150));
        csvGrid.Columns.Add(TextColumn("FilePath", "パス", 300));
        csvGrid.Columns.Add(TextColumn("RowCount", "行数", 55));
        csvGrid.Columns.Add(TextColumn("ColumnCount", "列数", 55));
        csvGrid.Columns.Add(TextColumn("Status", "状態", 120));
        csvGrid.Columns.Add(TextColumn("Note", "備考", 240));
        csvGrid.Columns.Add(TextColumn("EncodingName", "Encoding", 92));
        if (csvGrid.Columns["RowCount"] is DataGridViewColumn rowCountColumn)
        {
            rowCountColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            rowCountColumn.MinimumWidth = 60;
        }
        if (csvGrid.Columns["ColumnCount"] is DataGridViewColumn columnCountColumn)
        {
            columnCountColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            columnCountColumn.MinimumWidth = 60;
        }

        csvGrid.SelectionChanged += (_, _) =>
        {
            ResetPreviewPage();
            RedrawPreview();
        };
        csvGrid.CellMouseDown += CsvGrid_CellMouseDown;
        csvGrid.AllowDrop = true;
        csvGrid.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        csvGrid.DragDrop += (_, e) =>
        {
            var paths = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (paths is not null) LoadCsvPaths(paths.SelectMany(ExpandCsvPath), append: true);
        };

        return Section("CSV読み込み", csvGrid, csvTools);
    }

    private void CsvGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.RowIndex >= csvRows.Count) return;

        var item = csvRows[e.RowIndex];
        if (!CsvFileUtility.IsPrintQueueCsv(item.FilePath)) return;

        csvGrid.ClearSelection();
        csvGrid.Rows[e.RowIndex].Selected = true;
        csvGrid.CurrentCell = csvGrid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];

        var menu = new ContextMenuStrip();
        menu.Items.Add(
            LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "一覧から削除" : "Remove from List",
            null,
            (_, _) => RemoveCsvFromList(item));
        menu.Items.Add(
            LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "CSVを削除" : "Delete CSV",
            null,
            (_, _) => DeleteQueueCsv(item));
        menu.Show(csvGrid, csvGrid.PointToClient(Cursor.Position));
    }

    private void RemoveCsvFromList(CsvFileItem item)
    {
        csvRows.Remove(item);
        ResetPreviewPage();
        RedrawPreview();
    }

    private void DeleteQueueCsv(CsvFileItem item)
    {
        if (!CsvFileUtility.IsPrintQueueCsv(item.FilePath)) return;

        var message = LocalizationService.CurrentLanguage == AppLanguage.Japanese
            ? $"このキューCSVを削除しますか？\n\n{item.FileName}"
            : $"Delete this queue CSV?\n\n{item.FileName}";
        if (MessageBox.Show(message, LocalizationService.T("common.appTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (File.Exists(item.FilePath))
            {
                FileSystem.DeleteFile(item.FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            RemoveCsvFromList(item);
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.outputTitle"), ex.Message, ex);
        }
    }

    private void RememberRecentCsvPaths(IEnumerable<string> paths)
    {
        var changed = false;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path) && string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)))
        {
            changed |= RememberRecentPath(settings.RecentCsvEntries, path);
        }

        if (!changed) return;
        RefreshRecentCsvCombo();
        if (!loadingSettings) SaveCurrentSettings();
    }

    private void RefreshRecentCsvCombo()
    {
        if (recentCsvCombo is null) return;
        loadingSettings = true;
        recentCsvCombo.BeginUpdate();
        recentCsvCombo.Items.Clear();
        recentCsvCombo.Items.Add(RecentPathItem.Placeholder(LocalizationService.T("main.placeholder.recentCsv")));
        foreach (var entry in settings.RecentCsvEntries.OrderByDescending(entry => entry.LastOpenedAt))
        {
            recentCsvCombo.Items.Add(RecentPathItem.FromPath(entry.Path));
        }
        recentCsvCombo.EndUpdate();
        recentCsvCombo.SelectedIndex = 0;
        loadingSettings = false;
    }

    private void SelectCsvFiles()
    {
        using var dialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv", Multiselect = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadCsvPaths(dialog.FileNames, append: true);
    }

    private void SelectCsvFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadCsvPaths(Directory.GetFiles(dialog.SelectedPath, "*.csv"));
    }

    private void OpenDuplicateCleanup()
    {
        if (csvRows.All(row => row.Status != CsvFileStatus.Ok))
        {
            MessageBox.Show(LocalizationService.T("main.message.noValidCsv"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        currentPreset.Fields = fieldRows.ToList();
        using var dialog = new DuplicateCleanupDialog(currentPreset, fieldRows.ToList(), csvRows.ToList(), headerCheck.Checked, settings);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Applied)
        {
            var reloadPaths = dialog.ReloadCsvPaths.Count > 0 ? dialog.ReloadCsvPaths : csvRows.Select(c => c.FilePath).ToList();
            LoadCsvPaths(reloadPaths);
        }
        SaveCurrentSettings();
    }

    private void OpenTagFinder()
    {
        if (csvRows.All(row => row.Status != CsvFileStatus.Ok))
        {
            MessageBox.Show(LocalizationService.T("main.message.noValidCsv"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        currentPreset.Fields = fieldRows.ToList();
        using var dialog = new TagFinderDialog(fieldRows.ToList(), csvRows.ToList(), currentLayout, headerCheck.Checked, settings, SaveCurrentSettings, LoadQueueCsvFolder);
        dialog.ShowDialog(this);
        if (dialog.LayoutDesignerPreviewRows is { Count: > 0 } previewRows)
        {
            OpenLayoutDesigner(previewRows, dialog.LayoutDesignerInitialPreviewIndex);
        }

        if (dialog.CsvSaved)
        {
            ReloadCsvWithCurrentFiles();
        }
        SaveCurrentSettings();
    }

    private void LoadQueueCsvFolder(string queueCsvPath)
    {
        if (string.IsNullOrWhiteSpace(queueCsvPath)) return;

        if (Directory.Exists(queueCsvPath))
        {
            LoadCsvPaths(Directory.GetFiles(queueCsvPath, "*.csv").OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase));
            return;
        }

        if (!File.Exists(queueCsvPath)) return;

        var queueFolder = Path.GetDirectoryName(queueCsvPath);
        if (!string.IsNullOrWhiteSpace(queueFolder) && Directory.Exists(queueFolder))
        {
            LoadCsvPaths(Directory.GetFiles(queueFolder, "*.csv").OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase));
            return;
        }

        LoadCsvPaths([queueCsvPath]);
    }

    private void OpenRecentCsvSelection()
    {
        if (loadingSettings || recentCsvCombo.SelectedItem is not RecentPathItem item) return;
        if (string.IsNullOrWhiteSpace(item.Path)) return;
        if (!File.Exists(item.Path))
        {
            MessageBox.Show(LocalizationService.T("main.message.recentCsvMissing", item.Path), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LoadCsvPaths([item.Path]);
    }

    private void LoadStartupSourceIfConfigured()
    {
        if (!string.IsNullOrWhiteSpace(settings.StartupCsvPath))
        {
            if (!File.Exists(settings.StartupCsvPath))
            {
                MessageBox.Show(
                    LocalizationService.T("main.message.startupCsvMissing", settings.StartupCsvPath),
                    LocalizationService.T("common.appTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            LoadCsvPaths([settings.StartupCsvPath]);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.StartupCsvFolderPath)) return;
        if (!Directory.Exists(settings.StartupCsvFolderPath))
        {
            MessageBox.Show(
                LocalizationService.T("main.message.startupFolderMissing", settings.StartupCsvFolderPath),
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        LoadCsvPaths(Directory.GetFiles(settings.StartupCsvFolderPath, "*.csv").OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase));
    }

    private void ReloadCsvWithCurrentFiles()
    {
        if (csvRows.Count == 0)
        {
            UpdateHeaderNames();
            return;
        }
        LoadCsvPaths(csvRows.Select(c => c.FilePath).ToList());
    }

    private void LoadCsvPaths(IEnumerable<string> filePaths, bool append = false)
    {
        var csvPaths = NormalizeCsvPaths(filePaths).ToList();
        if (append)
        {
            csvPaths = MergeCsvPaths(csvRows.Select(row => row.FilePath), csvPaths).ToList();
        }

        RememberRecentCsvPaths(csvPaths);
        currentPreset.Fields = fieldRows.ToList();
        var result = csvLoader.LoadFiles(csvPaths, currentPreset, headerCheck.Checked);
        csvRows.Clear();
        foreach (var file in result.Files) csvRows.Add(file);
        if (ApplySavedCsvFieldMapping())
        {
            currentPreset.Fields = fieldRows.ToList();
            result = csvLoader.LoadFiles(csvPaths, currentPreset, headerCheck.Checked);
            csvRows.Clear();
            foreach (var file in result.Files) csvRows.Add(file);
        }

        if (SyncFieldsToCsvHeaders())
        {
            currentPreset.Fields = fieldRows.ToList();
            result = csvLoader.LoadFiles(csvPaths, currentPreset, headerCheck.Checked);
            csvRows.Clear();
            foreach (var file in result.Files) csvRows.Add(file);
            SaveCurrentCsvHeaderMapping();
            MarkPresetDirty();
        }

        if (AutoMapCsvFields())
        {
            currentPreset.Fields = fieldRows.ToList();
            result = csvLoader.LoadFiles(csvPaths, currentPreset, headerCheck.Checked);
            csvRows.Clear();
            foreach (var file in result.Files) csvRows.Add(file);
            SaveCurrentCsvHeaderMapping();
            MarkPresetDirty();
        }

        UpdateHeaderNames();
        ResetPreviewPage();
        RedrawPreview();
        ShowCsvImportDetails(result);
    }

    private static IEnumerable<string> NormalizeCsvPaths(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) continue;

            var normalizedPath = Path.GetFullPath(path);
            if (seen.Add(normalizedPath)) yield return normalizedPath;
        }
    }

    private static IEnumerable<string> MergeCsvPaths(IEnumerable<string> existingPaths, IEnumerable<string> newPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in existingPaths.Concat(newPaths))
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var normalizedPath = Path.GetFullPath(path);
            if (seen.Add(normalizedPath)) yield return normalizedPath;
        }
    }

    private IEnumerable<string> ExpandCsvPath(string path)
    {
        if (Directory.Exists(path)) return Directory.GetFiles(path, "*.csv");
        return string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) ? [path] : [];
    }

}
