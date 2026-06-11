using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class TagFinderDialog
{
    private void OpenCurrentPreviewSetInLayoutDesigner()
    {
        if (CurrentResultRow() is not { } result)
        {
            statusLabel.Text = T("tagFinder.message.selectRowForDesigner");
            return;
        }

        try
        {
            var target = ResolveLayoutDesignerTarget(result);
            if (target is null)
            {
                return;
            }

            var previewRows = PrintableRowService.PreviewRows(
                previewPreset,
                csvFiles,
                hasHeaderRow,
                settings.LastPrintSortMode,
                settings.LastPrintSortFieldId,
                settings.LastPrintSortValueType);

            if (previewRows.Count == 0)
            {
                statusLabel.Text = T("tagFinder.message.noPreviewRows");
                return;
            }

            LayoutDesignerPreviewRows = previewRows;
            LayoutDesignerInitialPreviewIndex = previewRows.FindIndex(row =>
                string.Equals(Path.GetFullPath(row.FilePath), Path.GetFullPath(target.File.FilePath), StringComparison.OrdinalIgnoreCase)
                && row.RowIndex == target.RowIndex);

            if (LayoutDesignerInitialPreviewIndex < 0)
            {
                LayoutDesignerInitialPreviewIndex = 0;
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private TagFinderResultRow? ResolveLayoutDesignerTarget(TagFinderResultRow current)
    {
        if (!CsvFileUtility.IsPrintQueueCsv(current.File.FilePath))
        {
            return current;
        }

        if (searchFieldCombo.SelectedItem is not FieldComboItem selected || selected.Field.ColumnIndex is not int columnIndex)
        {
            statusLabel.Text = T("tagFinder.message.selectSearchFieldFirst");
            return null;
        }

        var queueRow = current.RowIndex >= 0 && current.RowIndex < current.File.Rows.Count
            ? current.File.Rows[current.RowIndex]
            : null;
        var matchValue = queueRow is not null && columnIndex < queueRow.Length
            ? queueRow[columnIndex]
            : "";

        if (string.IsNullOrWhiteSpace(matchValue))
        {
            MessageBox.Show(
                T("tagFinder.message.couldNotDetermineParent"),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return null;
        }

        var parentMatch = resultRows.FirstOrDefault(row =>
            !CsvFileUtility.IsPrintQueueCsv(row.File.FilePath)
            && row.RowIndex >= 0
            && row.RowIndex < row.File.Rows.Count
            && columnIndex < row.File.Rows[row.RowIndex].Length
            && string.Equals(row.File.Rows[row.RowIndex][columnIndex], matchValue, StringComparison.OrdinalIgnoreCase));

        if (parentMatch is not null)
        {
            return parentMatch;
        }

        foreach (var file in csvFiles.Where(file => !CsvFileUtility.IsPrintQueueCsv(file.FilePath)))
        {
            for (var rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
            {
                if (columnIndex >= file.Rows[rowIndex].Length)
                {
                    continue;
                }

                if (string.Equals(file.Rows[rowIndex][columnIndex], matchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return new TagFinderResultRow
                    {
                        File = file,
                        RowIndex = rowIndex,
                        Checked = IsRowChecked(file, rowIndex, FlagColumnName())
                    };
                }
            }
        }

        MessageBox.Show(
            T("tagFinder.message.couldNotFindParent"),
            Text,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return null;
    }
}
