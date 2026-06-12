using Microsoft.VisualBasic.FileIO;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class TagFinderDialog
{
    private void Save()
    {
        CommitGridEdit();
        SaveFinderSettings();

        try
        {
            if (!SaveFlagsToCsv())
            {
                return;
            }

            if ((int)printQuantityBox.Value > 1 && QueueTargets().Count > 0)
            {
                CreateQueue(flagsAlreadySaved: true);
            }

            ResetPrintQuantity();
        }
        catch (Exception ex)
        {
            MessageBox.Show(CsvFileUtility.BuildUserFriendlySaveErrorMessage(ex), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveAndNext()
    {
        Save();
        printQuantityBox.Value = 1;
        searchTextBox.SelectAll();
        searchTextBox.Focus();
    }

    private void CreateQueue(bool flagsAlreadySaved = false)
    {
        CommitGridEdit();
        SaveFinderSettings();

        if ((int)printQuantityBox.Value <= 1)
        {
            Save();
            return;
        }

        var targets = QueueTargets();
        if (targets.Count == 0)
        {
            Save();
            return;
        }

        try
        {
            if (!flagsAlreadySaved && !SaveFlagsToCsv())
            {
                return;
            }

            var queueCsv = tagFinderCsvService.CreatePrintQueueCsv(
                targets
                    .Select(row => new TagFinderQueueSource { File = row.File, RowIndex = row.RowIndex })
                    .ToList(),
                (int)printQuantityBox.Value - 1,
                FlagColumnName(),
                OrderColumnName(),
                hasHeaderRow);
            QueueCsvPath = queueCsv.FilePath;
            AddOrReplaceQueueCsv(queueCsv);
            statusLabel.Text = TF("tagFinder.message.queueCreated", Path.GetFileName(QueueCsvPath));
            queueCreated(QueueCsvPath);
            ResetPrintQuantity();
        }
        catch (Exception ex)
        {
            MessageBox.Show(CsvFileUtility.BuildUserFriendlySaveErrorMessage(ex), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool SaveFlagsToCsv()
    {
        if (csvFiles.Count == 0)
        {
            statusLabel.Text = T("tagFinder.message.noCsvToSave");
            return false;
        }

        if (!ConfirmCsvColumnAlignmentBeforeSave())
        {
            return false;
        }

        tagFinderCsvService.SaveFlags(
            csvFiles,
            resultRows.Select(row => new TagFinderSaveSelection
            {
                File = row.File,
                RowIndex = row.RowIndex,
                Checked = row.Checked
            }),
            FlagColumnName(),
            OrderColumnName(),
            hasHeaderRow);

        CsvSaved = true;
        statusLabel.Text = T("tagFinder.message.flagsSaved");
        return true;
    }

    private bool ConfirmCsvColumnAlignmentBeforeSave()
    {
        var flagColumnName = FlagColumnName();
        var orderColumnName = OrderColumnName();
        var filesMissingRequiredColumns = csvFiles
            .Where(file => !HasColumn(file, flagColumnName) || !HasColumn(file, orderColumnName))
            .ToList();

        if (filesMissingRequiredColumns.Count == 0)
        {
            return true;
        }

        var fileList = BuildFileList(filesMissingRequiredColumns);
        var message = TF(
            "tagFinder.confirm.saveAddsColumnsToAllCsv",
            csvFiles.Count,
            flagColumnName,
            orderColumnName,
            fileList);

        return MessageBox.Show(
            message,
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private static bool HasColumn(CsvFileItem file, string columnName)
    {
        if (file.HeaderRow is not { Length: > 0 })
        {
            return false;
        }

        return file.HeaderRow.Any(header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildFileList(IReadOnlyList<CsvFileItem> files)
    {
        const int maxDisplayFiles = 5;
        var names = files
            .Take(maxDisplayFiles)
            .Select(file => $"- {file.FileName}")
            .ToList();

        if (files.Count > maxDisplayFiles)
        {
            names.Add(TF("tagFinder.message.moreFiles", files.Count - maxDisplayFiles));
        }

        return string.Join(Environment.NewLine, names);
    }

    private List<TagFinderResultRow> QueueTargets()
    {
        return tagFinderWorkflowService.ResolveQueueTargets(
            resultRows,
            showingCheckedOnlyRows,
            SelectedResultRow(),
            queueTargetFromDoubleClick);
    }

    private TagFinderResultRow? SelectedResultRow()
    {
        if (resultsGrid.CurrentRow?.DataBoundItem is TagFinderResultRow current)
        {
            return current;
        }

        return resultsGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem)
            .OfType<TagFinderResultRow>()
            .FirstOrDefault();
    }

    private void ResetPrintQuantity()
    {
        if (printQuantityBox.Value != 1)
        {
            printQuantityBox.Value = 1;
        }
    }

    private void CreateQueueAndNext()
    {
        CreateQueue();
        ResetPrintQuantity();
        searchTextBox.SelectAll();
        searchTextBox.Focus();
    }

    private void DeleteAllQueueCsvs()
    {
        var queueFiles = csvFiles
            .Select(file => Path.GetDirectoryName(file.FilePath))
            .Where(folder => !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(folder => Directory.GetFiles(folder!, "print_queue_*.csv"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (queueFiles.Count == 0)
        {
            statusLabel.Text = T("tagFinder.message.noQueueCsvToDelete");
            return;
        }

        var message = TF("tagFinder.confirm.deleteQueueCsvs", queueFiles.Count);
        if (MessageBox.Show(message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var path in queueFiles)
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            RemoveDeletedQueueCsvs(queueFiles);

            statusLabel.Text = TF("tagFinder.message.deletedQueueCsvs", queueFiles.Count);

            foreach (var folder in queueFiles
                         .Select(Path.GetDirectoryName)
                         .Where(folder => !string.IsNullOrWhiteSpace(folder))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                queueCreated(folder!);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloseWithUnusedQueueCsvCleanup()
    {
        Close();
    }

    private void TagFinderDialog_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (closeCleanupChecked)
        {
            return;
        }

        closeCleanupChecked = true;
        if (!TryCleanupUnusedQueueCsvsBeforeClose())
        {
            e.Cancel = true;
            closeCleanupChecked = false;
        }
    }

    private bool TryCleanupUnusedQueueCsvsBeforeClose()
    {
        var unusedQueueFiles = UnusedQueueCsvFiles().ToList();
        if (unusedQueueFiles.Count == 0)
        {
            return true;
        }

        var fileList = string.Join(Environment.NewLine, unusedQueueFiles.Take(10).Select(Path.GetFileName));
        var more = unusedQueueFiles.Count > 10
            ? Environment.NewLine + TF("tagFinder.message.moreFiles", unusedQueueFiles.Count - 10)
            : "";
        var message = TF("tagFinder.confirm.deleteUnusedQueueCsvs", fileList + more);

        if (MessageBox.Show(message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return true;
        }

        try
        {
            foreach (var path in unusedQueueFiles)
            {
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }

            RemoveDeletedQueueCsvs(unusedQueueFiles);
            foreach (var folder in unusedQueueFiles
                         .Select(Path.GetDirectoryName)
                         .Where(folder => !string.IsNullOrWhiteSpace(folder))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                queueCreated(folder!);
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private IEnumerable<string> UnusedQueueCsvFiles()
    {
        return tagFinderWorkflowService.FindUnusedQueueCsvFiles(csvFiles, FlagColumnName());
    }

    private void RemoveDeletedQueueCsvs(IEnumerable<string> deletedPaths)
    {
        var deleted = deletedPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = csvFiles.Count - 1; i >= 0; i--)
        {
            if (deleted.Contains(Path.GetFullPath(csvFiles[i].FilePath)))
            {
                csvFiles.RemoveAt(i);
            }
        }

        for (var i = resultRows.Count - 1; i >= 0; i--)
        {
            if (deleted.Contains(Path.GetFullPath(resultRows[i].File.FilePath)))
            {
                resultRows.RemoveAt(i);
            }
        }

        UpdateTargetHeaderCheckState();
        RefreshTargetCheckColumn();
        SelectFirstResult();
        UpdateTagPreview();
    }

    private void SaveFinderSettings()
    {
        if (searchFieldCombo.SelectedItem is FieldComboItem selected)
        {
            settings.LastTagFinderSearchFieldId = selected.Field.FieldId;
        }

        settings.LastTagFinderFlagColumnName = FlagColumnName();
        settings.LastTagFinderOrderColumnName = OrderColumnName();
        settings.LastTagFinderAutoCheckAfterSearch = autoCheckAfterSearchCheck.Checked;
        settings.LastTagFinderPreviewVisible = tagPreviewVisibleCheck.Checked;
        saveSettings();
    }

    private string FlagColumnName() => string.IsNullOrWhiteSpace(flagColumnBox.Text) ? "tagPrint" : flagColumnBox.Text.Trim();

    private static bool IsRowChecked(CsvFileItem file, int rowIndex, string flagColumnName)
    {
        var flagIndex = CsvHeaderResolver.FindIndex(file, flagColumnName);
        return flagIndex >= 0
            && rowIndex >= 0
            && rowIndex < file.Rows.Count
            && flagIndex < file.Rows[rowIndex].Length
            && IsTrue(file.Rows[rowIndex][flagIndex]);
    }

    private void AddOrReplaceQueueCsv(TagFinderQueueCsvResult queueCsv)
    {
        var path = queueCsv.FilePath;
        var fullPath = Path.GetFullPath(path);
        csvFiles.RemoveAll(file => string.Equals(Path.GetFullPath(file.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
        csvFiles.Add(new CsvFileItem
        {
            FilePath = fullPath,
            FileName = Path.GetFileName(fullPath),
            EncodingName = queueCsv.EncodingName,
            Delimiter = queueCsv.Delimiter,
            RowCount = queueCsv.Rows.Count,
            ColumnCount = queueCsv.Header.Length,
            Status = CsvFileStatus.Ok,
            Rows = queueCsv.Rows,
            HeaderRow = hasHeaderRow ? queueCsv.Header : null
        });
    }

}
