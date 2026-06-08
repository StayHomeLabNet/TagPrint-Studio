using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private static Dictionary<string, string> DefaultPreviewData(IEnumerable<FieldDefinition> fields) =>
        fields.ToDictionary(
            field => field.FieldId,
            field => string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName);

    private void ConfigurePreviewRowCombo()
    {
        previewRowCombo.Items.Clear();
        if (previewRows.Count == 0)
        {
            previewRowCombo.Items.Add(LocalizationService.T("layout.message.fieldNames"));
            previewRowCombo.SelectedIndex = 0;
            previewRowCombo.Enabled = false;
            UpdatePreviewRowNavigationButtons();
            return;
        }

        for (var i = 0; i < previewRows.Count; i++)
        {
            previewRowCombo.Items.Add(PreviewRowLabel(previewRows[i]));
        }

        previewRowCombo.SelectedIndex = 0;
        previewRowCombo.Enabled = previewRows.Count > 1;
        UpdatePreviewRowNavigationButtons();
    }

    private void SelectPreviewRow()
    {
        if (previewRowCombo.SelectedIndex < 0 || previewRowCombo.SelectedIndex >= previewRows.Count)
        {
            UpdatePreviewRowNavigationButtons();
            return;
        }

        previewData.Clear();
        foreach (var (key, value) in previewRows[previewRowCombo.SelectedIndex].Data)
        {
            previewData[key] = value;
        }

        UpdateCsvValueEditor();
        RedrawPreview();
        UpdatePreviewRowNavigationButtons();
    }

    private void MovePreviewRow(int delta)
    {
        if (previewRows.Count == 0 || previewRowCombo.Items.Count == 0)
        {
            return;
        }

        var currentIndex = previewRowCombo.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = Math.Clamp(currentIndex + delta, 0, previewRows.Count - 1);
        if (nextIndex == previewRowCombo.SelectedIndex)
        {
            return;
        }

        previewRowCombo.SelectedIndex = nextIndex;
    }

    private void UpdatePreviewRowNavigationButtons()
    {
        if (previousPreviewRowButton is null || nextPreviewRowButton is null || previewRowCombo is null)
        {
            return;
        }

        var hasMultipleRows = previewRows.Count > 1 && previewRowCombo.SelectedIndex >= 0;
        previousPreviewRowButton.Enabled = hasMultipleRows && previewRowCombo.SelectedIndex > 0;
        nextPreviewRowButton.Enabled = hasMultipleRows && previewRowCombo.SelectedIndex < previewRows.Count - 1;
    }

    private static string PreviewNavigationButtonText(bool previous) =>
        previous
            ? LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "前へ" : "Previous"
            : LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "次へ" : "Next";

    private void ConfigureCsvColumnEditor()
    {
        csvColumnCombo.Items.Clear();

        foreach (var field in fieldDefinitions)
        {
            csvColumnCombo.Items.Add(new CsvColumnComboItem(field));
        }

        var hasColumns = csvColumnCombo.Items.Count > 0 && previewRows.Count > 0;
        csvColumnCombo.Enabled = hasColumns;
        csvValueBox.Enabled = hasColumns;

        if (csvColumnCombo.Items.Count > 0)
        {
            csvColumnCombo.SelectedIndex = 0;
        }

        UpdateCsvValueEditor();
    }

    private void UpdateCsvValueEditor()
    {
        if (csvValueBox is null) return;

        updatingCsvValueEditor = true;
        var fieldId = SelectedCsvFieldId();
        csvValueBox.Text = string.IsNullOrWhiteSpace(fieldId)
            ? ""
            : previewData.GetValueOrDefault(fieldId, "");
        updatingCsvValueEditor = false;
    }

    private void ApplyCsvValueEditor()
    {
        if (updatingCsvValueEditor) return;
        var fieldId = SelectedCsvFieldId();
        if (string.IsNullOrWhiteSpace(fieldId)) return;
        SetCsvFieldValue(fieldId, csvValueBox.Text);
    }

    private string SelectedCsvFieldId()
    {
        if (csvColumnCombo.SelectedItem is CsvColumnComboItem item)
        {
            return item.Field.FieldId;
        }

        var text = csvColumnCombo.Text.Trim();
        var separator = text.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? text[..separator] : text;
    }

    private void SetCsvFieldValue(string fieldId, string value)
    {
        previewData[fieldId] = value;
        var context = CurrentPreviewContext();
        if (context is null) return;

        context.Data[fieldId] = value;
        if (string.Equals(SelectedCsvFieldId(), fieldId, StringComparison.Ordinal))
        {
            updatingCsvValueEditor = true;
            csvValueBox.Text = value;
            updatingCsvValueEditor = false;
        }
        var field = fieldDefinitions.FirstOrDefault(field => field.FieldId == fieldId);
        if (field?.ColumnIndex is int columnIndex && columnIndex >= 0)
        {
            EnsureRowWidth(context, columnIndex + 1);
            context.Values[columnIndex] = value;
            if (context.RowIndex >= 0 && context.RowIndex < context.AllRows.Count)
            {
                context.AllRows[context.RowIndex] = context.Values;
            }
        }

        MarkCsvDirty(context);
    }

    private string CurrentPreviewRowLabel() =>
        previewRows.Count == 0 || previewRowCombo.SelectedIndex < 0
            ? LocalizationService.T("layout.message.fieldNames")
            : LocalizationService.T("layout.message.previewRowCount", previewRowCombo.SelectedIndex + 1, previewRows.Count);

    private static string PreviewRowLabel(CsvPreviewRowContext row)
    {
        return string.IsNullOrWhiteSpace(row.FileName)
            ? LocalizationService.T("layout.message.previewRowLabelNoFile", row.RowIndex + 1, PreviewRowSummary(row.Data))
            : LocalizationService.T("layout.message.previewRowLabel", row.RowIndex + 1, row.FileName, PreviewRowSummary(row.Data));
    }

    private static string PreviewRowSummary(Dictionary<string, string> row)
    {
        var value = row.Values.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? "";
        if (string.IsNullOrWhiteSpace(value)) return LocalizationService.T("layout.message.previewEmpty");
        return value.Length <= 28 ? value : value[..28] + "...";
    }

    private CsvPreviewRowContext? CurrentPreviewContext()
    {
        if (previewRowCombo.SelectedIndex < 0 || previewRowCombo.SelectedIndex >= previewRows.Count) return null;
        return previewRows[previewRowCombo.SelectedIndex];
    }

    private void MarkCsvDirty(CsvPreviewRowContext context)
    {
        context.IsDirty = true;
        csvPreviewDirty = true;
        UpdateCsvSaveState();
        RedrawPreview();
    }

    private void UpdateCsvSaveState()
    {
        if (saveCsvButton is null) return;
        saveCsvButton.Enabled = previewRows.Any(row => row.IsDirty && !string.IsNullOrWhiteSpace(row.FilePath));
    }

    private void SaveCsvChanges()
    {
        var dirtyRows = previewRows.Where(row => row.IsDirty && !string.IsNullOrWhiteSpace(row.FilePath)).ToList();
        if (dirtyRows.Count == 0) return;

        try
        {
            foreach (var group in dirtyRows.GroupBy(row => row.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                var first = group.First();
                foreach (var row in group)
                {
                    if (row.RowIndex >= 0 && row.RowIndex < row.AllRows.Count)
                    {
                        row.AllRows[row.RowIndex] = row.Values;
                    }
                }

                var lines = new List<string>();
                if (first.HasHeaderRow && first.HeaderRow is { Length: > 0 })
                {
                    lines.Add(ToCsvLine(first.HeaderRow));
                }
                lines.AddRange(first.AllRows.Select(ToCsvLine));

                var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
                CsvBackupUtility.CreateTimestampedBackup(first.FilePath);
                File.WriteAllText(first.FilePath, content, CsvFileUtility.EncodingForCsv(first));
            }

            foreach (var row in dirtyRows) row.IsDirty = false;
            csvPreviewDirty = false;
            CsvSaved = true;
            UpdateCsvSaveState();
            RedrawPreview();
            statusLabel.Text = LocalizationService.T("layout.message.statusCsvSaved", dirtyRows.Select(row => row.FileName).Distinct().Count());
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.T("layout.message.csvSaveFailed", ex.Message), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ToCsvLine(IEnumerable<string> cells) => string.Join(",", cells.Select(EscapeCsvCell));

    private static string EscapeCsvCell(string? value)
    {
        var text = value ?? "";
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n')) return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static void EnsureRowWidth(CsvPreviewRowContext context, int width)
    {
        if (context.Values.Length >= width) return;
        var values = context.Values;
        Array.Resize(ref values, width);
        context.Values = values;
    }
}
