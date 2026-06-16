using TagPrintStudio.Models;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private sealed record LayoutHistorySnapshot(
        LayoutFile Layout,
        List<CsvPreviewRowContext> PreviewRows,
        Dictionary<string, string> PreviewData,
        int PreviewRowIndex,
        bool CsvPreviewDirty);

    private void CaptureUndo()
    {
        if (loading || applyingHistory) return;
        undoStack.Push(CreateHistorySnapshot());
        redoStack.Clear();
        UpdateHistoryButtons();
    }

    private void Undo()
    {
        if (undoStack.Count == 0) return;
        redoStack.Push(CreateHistorySnapshot());
        RestoreLayout(undoStack.Pop());
        MarkDirty();
    }

    private void Redo()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push(CreateHistorySnapshot());
        RestoreLayout(redoStack.Pop());
        MarkDirty();
    }

    private LayoutHistorySnapshot CreateHistorySnapshot() => new(
        CloneCurrentLayout(),
        ClonePreviewRows(previewRows),
        new Dictionary<string, string>(previewData, StringComparer.Ordinal),
        previewRowCombo?.SelectedIndex ?? -1,
        csvPreviewDirty);

    private void RestoreLayout(LayoutHistorySnapshot snapshot)
    {
        var selectedId = SelectedElement()?.ElementId;
        applyingHistory = true;
        loading = true;
        currentLayout = CloneLayout(snapshot.Layout);
        elementRows.Clear();
        foreach (var element in currentLayout.Elements.OrderBy(e => e.ZIndex)) elementRows.Add(element);
        currentLayout.Elements = elementRows.ToList();
        RestorePreviewRows(snapshot);
        loading = false;
        applyingHistory = false;

        var selected = elementRows.FirstOrDefault(e => e.ElementId == selectedId) ?? elementRows.FirstOrDefault();
        if (selected is not null) SelectElement(selected);
        LoadSelectedElementIntoEditor();
        UpdateHistoryButtons();
        RedrawPreview();
    }

    private void RestorePreviewRows(LayoutHistorySnapshot snapshot)
    {
        previewRows.Clear();
        previewRows.AddRange(ClonePreviewRows(snapshot.PreviewRows));
        previewData.Clear();
        foreach (var (key, value) in snapshot.PreviewData)
        {
            previewData[key] = value;
        }

        csvPreviewDirty = snapshot.CsvPreviewDirty;
        if (previewRowCombo is not null)
        {
            previewRowCombo.Items.Clear();
            if (previewRows.Count == 0)
            {
                previewRowCombo.Items.Add(TagPrintStudio.Services.LocalizationService.T("layout.message.fieldNames"));
                previewRowCombo.SelectedIndex = 0;
                previewRowCombo.Enabled = false;
            }
            else
            {
                foreach (var row in previewRows)
                {
                    previewRowCombo.Items.Add(PreviewRowLabel(row));
                }

                previewRowCombo.SelectedIndex = Math.Clamp(snapshot.PreviewRowIndex, 0, previewRows.Count - 1);
                previewRowCombo.Enabled = previewRows.Count > 1;
            }
        }

        UpdateCsvValueEditor();
        UpdateCsvSaveState();
        UpdatePreviewRowNavigationButtons();
    }

    private static List<CsvPreviewRowContext> ClonePreviewRows(IEnumerable<CsvPreviewRowContext> rows)
    {
        var allRowsByFile = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CsvPreviewRowContext>();
        var index = 0;
        foreach (var row in rows)
        {
            var allRowsKey = string.IsNullOrWhiteSpace(row.FilePath)
                ? $"__row_{index}"
                : row.FilePath;
            if (!allRowsByFile.TryGetValue(allRowsKey, out var allRows))
            {
                allRows = row.AllRows.Select(CloneCsvRow).ToList();
                allRowsByFile[allRowsKey] = allRows;
            }

            var values = row.RowIndex >= 0 && row.RowIndex < allRows.Count
                ? allRows[row.RowIndex]
                : CloneCsvRow(row.Values);

            result.Add(new CsvPreviewRowContext
            {
                FilePath = row.FilePath,
                FileName = row.FileName,
                EncodingName = row.EncodingName,
                Delimiter = row.Delimiter,
                NewLine = row.NewLine,
                HasHeaderRow = row.HasHeaderRow,
                HeaderRow = row.HeaderRow is null ? null : CloneCsvRow(row.HeaderRow),
                AllRows = allRows,
                RowIndex = row.RowIndex,
                Values = values,
                Data = new Dictionary<string, string>(row.Data, StringComparer.Ordinal),
                IsDirty = row.IsDirty
            });
            index++;
        }

        return result;
    }

    private static string[] CloneCsvRow(string[] row) => row.ToArray();

    private void UpdateHistoryButtons()
    {
        if (undoButton is null || redoButton is null) return;
        undoButton.Enabled = undoStack.Count > 0;
        redoButton.Enabled = redoStack.Count > 0;
    }

}
