using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
{
    private void AddField()
    {
        var next = fieldRows.Count + 1;
        var displayName = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "新しい項目" : "New Field";
        fieldRows.Add(new FieldDefinition
        {
            FieldId = UniqueFieldId(displayName, fieldRows),
            DisplayName = displayName,
            DisplayOrder = next,
            Enabled = true
        });
        MarkPresetDirty();
        SaveCurrentCsvHeaderMapping();
        fieldsGrid.CurrentCell = fieldsGrid.Rows[^1].Cells["DisplayName"];
        fieldsGrid.BeginEdit(true);
    }

    private void AddCsvColumn()
    {
        if (!headerCheck.Checked)
        {
            MessageBox.Show(LocalizationService.T("main.message.csvColumnRequiresHeader"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (csvRows.Count == 0 || csvRows.All(row => row.Status == CsvFileStatus.LoadFailed))
        {
            MessageBox.Show(LocalizationService.T("main.message.noValidCsv"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new NamePromptDialog(
            LocalizationService.T("main.title.addCsvColumn"),
            LocalizationService.T("main.label.csvColumnName"),
            LocalizationService.T("main.default.newCsvColumn"));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var columnName = dialog.Value.Trim();
        if (CurrentCsvHeaders()?.Any(header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase)) == true)
        {
            MessageBox.Show(LocalizationService.T("main.message.csvColumnExists", columnName), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var result = csvColumnService.AddColumnToAllFiles(csvRows.ToList(), columnName, headerCheck.Checked);
            var next = fieldRows.Count + 1;
            fieldRows.Add(new FieldDefinition
            {
                FieldId = UniqueFieldId(result.ColumnName, fieldRows),
                DisplayName = result.ColumnName,
                ColumnIndex = result.ColumnIndex,
                DisplayOrder = next,
                Enabled = true
            });

            fieldsGrid.Refresh();
            csvGrid.Refresh();
            UpdateHeaderNames();
            MarkPresetDirty();
            SaveCurrentCsvHeaderMapping();
            RefreshPrintSortFieldCombo();
            ResetPreviewPage();
            RedrawPreview();

            MessageBox.Show(
                LocalizationService.T("main.message.csvColumnAdded", result.ColumnName, result.UpdatedFileCount),
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.csvColumnTitle"), LocalizationService.T("main.error.csvColumnSummary"), ex);
        }
    }

    private void FieldsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || fieldsGrid.Columns[e.ColumnIndex] is not DataGridViewButtonColumn) return;
        if (MessageBox.Show(LocalizationService.T("main.message.deleteField"), LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            fieldRows.RemoveAt(e.RowIndex);
            MarkPresetDirty();
            SaveCurrentCsvHeaderMapping();
            RedrawPreview();
        }
    }


    private void UpdateHeaderNames()
    {
        var source = csvRows.FirstOrDefault(c => c.Status == CsvFileStatus.Ok && c.HeaderRow is not null)
            ?? csvRows.FirstOrDefault(c => c.HeaderRow is not null);
        foreach (DataGridViewRow row in fieldsGrid.Rows)
        {
            if (row.DataBoundItem is not FieldDefinition field) continue;
            var header = field.ColumnIndex is int index && index >= 0 && source?.HeaderRow is { } headers && index < headers.Length
                ? headers[index]
                : "";
            row.Cells["HeaderName"].Value = header;
        }
    }

    private bool SyncFieldsToCsvHeaders()
    {
        if (!headerCheck.Checked) return false;

        var headers = csvRows.FirstOrDefault(c => c.Status == CsvFileStatus.Ok && c.HeaderRow is not null)?.HeaderRow
            ?? csvRows.FirstOrDefault(c => c.HeaderRow is not null)?.HeaderRow;
        if (headers is null || headers.Length == 0) return false;

        var existing = fieldRows.OrderBy(f => f.DisplayOrder).ToList();
        var nextFields = new List<FieldDefinition>();
        var consumedFields = new HashSet<FieldDefinition>();
        for (var i = 0; i < headers.Length; i++)
        {
            var header = string.IsNullOrWhiteSpace(headers[i]) ? $"Column {i + 1}" : headers[i].Trim();
            var source = i < existing.Count ? existing[i] : null;
            if (source is not null) consumedFields.Add(source);
            nextFields.Add(new FieldDefinition
            {
                FieldId = source?.FieldId ?? UniqueFieldId(header, nextFields),
                DisplayName = header,
                ColumnIndex = i,
                Required = source?.Required ?? false,
                DisplayOrder = i + 1,
                Enabled = source?.Enabled ?? true,
                PrintConditionEnabled = source?.PrintConditionEnabled ?? false,
                PrintConditionType = source?.PrintConditionType ?? PrintConditionType.None,
                PrintConditionOperator = source?.PrintConditionOperator ?? PrintConditionOperator.None,
                PrintConditionValue = source?.PrintConditionValue ?? ""
            });
        }

        foreach (var extraField in existing.Where(field => !consumedFields.Contains(field)))
        {
            var clone = CloneFieldDefinition(extraField);
            clone.DisplayOrder = nextFields.Count + 1;
            nextFields.Add(clone);
        }

        if (FieldsEqual(existing, nextFields)) return false;

        fieldRows.Clear();
        foreach (var field in nextFields) fieldRows.Add(field);
        fieldsGrid.Refresh();
        return true;
    }

    private static bool FieldsEqual(List<FieldDefinition> left, List<FieldDefinition> right)
    {
        if (left.Count != right.Count) return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].FieldId != right[i].FieldId ||
                left[i].DisplayName != right[i].DisplayName ||
                left[i].ColumnIndex != right[i].ColumnIndex ||
                left[i].DisplayOrder != right[i].DisplayOrder ||
                left[i].Required != right[i].Required ||
                left[i].Enabled != right[i].Enabled ||
                left[i].PrintConditionEnabled != right[i].PrintConditionEnabled ||
                left[i].PrintConditionType != right[i].PrintConditionType ||
                left[i].PrintConditionOperator != right[i].PrintConditionOperator ||
                left[i].PrintConditionValue != right[i].PrintConditionValue)
            {
                return false;
            }
        }

        return true;
    }

    private static string UniqueFieldId(string header, IEnumerable<FieldDefinition> fields)
    {
        var baseId = SafeFileNameService.ToSafeFileName(NormalizeMappingText(header));
        if (string.IsNullOrWhiteSpace(baseId) || baseId == "untitled") baseId = "field";
        if (char.IsDigit(baseId[0])) baseId = $"field_{baseId}";

        var used = fields.Select(f => f.FieldId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = baseId;
        var index = 2;
        while (used.Contains(candidate))
        {
            candidate = $"{baseId}_{index}";
            index++;
        }

        return candidate;
    }

    private bool ApplySavedCsvFieldMapping()
    {
        if (!headerCheck.Checked) return false;

        var headers = CurrentCsvHeaders();
        if (headers is null || headers.Length == 0) return false;

        var signature = CsvHeaderSignature(headers);
        var presetKey = CurrentPresetMappingKey();
        var profile = settings.CsvHeaderMappings
            .Where(mapping => string.Equals(mapping.PresetKey, presetKey, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(mapping => string.Equals(mapping.HeaderSignature, signature, StringComparison.Ordinal));
        if (profile is null || profile.Fields.Count == 0) return false;

        var existing = fieldRows.OrderBy(f => f.DisplayOrder).ToList();
        var mapped = profile.Fields
            .Select(CloneFieldDefinition)
            .OrderBy(field => field.DisplayOrder)
            .ToList();
        if (FieldsEqual(existing, mapped)) return false;

        fieldRows.Clear();
        foreach (var field in mapped)
        {
            fieldRows.Add(field);
        }

        fieldsGrid.Refresh();
        return true;
    }

    private bool AutoMapCsvFields()
    {
        if (!headerCheck.Checked) return false;

        var headers = CurrentCsvHeaders();
        if (headers is null || headers.Length == 0) return false;

        var changed = false;
        var usedIndexes = new HashSet<int>();
        var orderedFields = fieldRows.OrderBy(f => f.DisplayOrder).ToList();

        foreach (var field in orderedFields)
        {
            var currentScore = ScoreHeaderMatch(field, HeaderAt(headers, field.ColumnIndex));
            if (field.ColumnIndex is int index && index >= 0 && index < headers.Length && currentScore >= 70)
            {
                usedIndexes.Add(index);
            }
        }

        foreach (var field in orderedFields.Where(f => f.Enabled))
        {
            var currentIndex = field.ColumnIndex;
            var currentScore = ScoreHeaderMatch(field, HeaderAt(headers, currentIndex));
            if (currentIndex is int existingIndex && existingIndex >= 0 && existingIndex < headers.Length && currentScore >= 85)
            {
                continue;
            }

            var bestIndex = -1;
            var bestScore = 0;
            for (var i = 0; i < headers.Length; i++)
            {
                if (usedIndexes.Contains(i) && i != currentIndex) continue;

                var score = ScoreHeaderMatch(field, headers[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestScore < 70 || bestScore <= currentScore || currentIndex == bestIndex) continue;

            if (currentIndex is int oldIndex)
            {
                usedIndexes.Remove(oldIndex);
            }

            field.ColumnIndex = bestIndex;
            usedIndexes.Add(bestIndex);
            changed = true;
        }

        if (changed) fieldsGrid.Refresh();
        return changed;
    }

    private string[]? CurrentCsvHeaders()
    {
        return csvRows.FirstOrDefault(c => c.Status == CsvFileStatus.Ok && c.HeaderRow is not null)?.HeaderRow
            ?? csvRows.FirstOrDefault(c => c.HeaderRow is not null)?.HeaderRow;
    }

    private void SaveCurrentCsvHeaderMapping()
    {
        if (loadingPreset || loadingSettings || !headerCheck.Checked) return;

        var headers = CurrentCsvHeaders();
        if (headers is null || headers.Length == 0) return;

        var signature = CsvHeaderSignature(headers);
        var presetKey = CurrentPresetMappingKey();
        var profile = settings.CsvHeaderMappings
            .Where(mapping => string.Equals(mapping.PresetKey, presetKey, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(mapping => string.Equals(mapping.HeaderSignature, signature, StringComparison.Ordinal));

        var snapshot = fieldRows
            .OrderBy(field => field.DisplayOrder)
            .Select(CloneFieldDefinition)
            .ToList();

        if (profile is null)
        {
            settings.CsvHeaderMappings.Add(new CsvHeaderMappingProfile
            {
                PresetKey = presetKey,
                HeaderSignature = signature,
                Headers = headers.Select(header => header ?? "").ToList(),
                Fields = snapshot,
                UpdatedAt = DateTime.Now
            });
        }
        else
        {
            profile.Headers = headers.Select(header => header ?? "").ToList();
            profile.Fields = snapshot;
            profile.UpdatedAt = DateTime.Now;
        }

        SaveCurrentSettings();
    }

    private string CurrentPresetMappingKey()
    {
        if (currentPresetItem is not null)
        {
            return Path.GetFileName(currentPresetItem.Path);
        }

        if (!string.IsNullOrWhiteSpace(currentPreset.PresetId))
        {
            return currentPreset.PresetId;
        }

        return "default";
    }

    private static string CsvHeaderSignature(IEnumerable<string> headers)
    {
        return string.Join("|", headers.Select(header => NormalizeMappingText(header ?? "")));
    }

    private static string HeaderAt(string[] headers, int? index) =>
        index is int i && i >= 0 && i < headers.Length ? headers[i] : "";

    private static int ScoreHeaderMatch(FieldDefinition field, string? header)
    {
        var normalizedHeader = NormalizeMappingText(header ?? "");
        if (normalizedHeader.Length == 0) return 0;

        var normalizedDisplay = NormalizeMappingText(field.DisplayName);
        var normalizedId = NormalizeMappingText(field.FieldId);
        var normalizedField = normalizedDisplay + normalizedId;
        var score = 0;

        if (normalizedDisplay.Length > 0)
        {
            if (normalizedHeader == normalizedDisplay) score = Math.Max(score, 120);
            else if (normalizedHeader.Contains(normalizedDisplay) || normalizedDisplay.Contains(normalizedHeader)) score = Math.Max(score, 95);
        }

        score = Math.Max(score, ScoreHeaderKind(normalizedField, normalizedHeader));

        var fieldNumber = LastDigit(normalizedField);
        var headerNumber = LastDigit(normalizedHeader);
        if (fieldNumber is not null && headerNumber is not null)
        {
            score += fieldNumber == headerNumber ? 20 : -25;
        }

        return Math.Max(0, score);
    }

    private static int ScoreHeaderKind(string normalizedField, string normalizedHeader)
    {
        if (ContainsAny(normalizedField, ["product", "item", "goods", "商品", "品名", "名称", "name"]))
        {
            return ContainsAny(normalizedHeader, ["productname", "itemname", "goodsname", "product", "item", "goods", "商品名", "品名", "商品", "名称", "名前", "name"]) ? 75 : 0;
        }

        if (ContainsAny(normalizedField, ["price", "amount", "cost", "価格", "値段", "金額", "単価"]))
        {
            return ContainsAny(normalizedHeader, ["price", "amount", "cost", "yen", "価格", "値段", "売価", "金額", "単価", "税込", "税抜"]) ? 85 : 0;
        }

        if (ContainsAny(normalizedField, ["code", "jan", "sku", "barcode", "コード", "品番"]))
        {
            return ContainsAny(normalizedHeader, ["code", "jan", "sku", "barcode", "itemcode", "productcode", "コード", "品番", "型番", "バーコード", "商品コード"]) ? 85 : 0;
        }

        return 0;
    }

    private static string NormalizeMappingText(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool ContainsAny(string value, IEnumerable<string> candidates) =>
        candidates.Select(NormalizeMappingText).Any(candidate => candidate.Length > 0 && value.Contains(candidate));

    private static FieldDefinition CloneFieldDefinition(FieldDefinition field) => new()
    {
        FieldId = field.FieldId,
        DisplayName = field.DisplayName,
        ColumnIndex = field.ColumnIndex,
        Required = field.Required,
        DisplayOrder = field.DisplayOrder,
        Enabled = field.Enabled,
        PrintConditionEnabled = field.PrintConditionEnabled,
        PrintConditionType = field.PrintConditionType,
        PrintConditionOperator = field.PrintConditionOperator,
        PrintConditionValue = field.PrintConditionValue
    };

    private static char? LastDigit(string value)
    {
        for (var i = value.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(value[i])) return value[i];
        }

        return null;
    }


    private static string PrintConditionTypeText(PrintConditionType value) => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? value switch
        {
            PrintConditionType.None => "なし",
            PrintConditionType.Bool => "真偽",
            PrintConditionType.Date => "日付",
            PrintConditionType.Number => "数値",
            PrintConditionType.Text => "文字列",
            _ => value.ToString()
        }
        : value switch
        {
            PrintConditionType.Text => "Text",
            _ => value.ToString()
        };

    private static string PrintConditionOperatorText(PrintConditionOperator value) => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? value switch
        {
            PrintConditionOperator.None => "なし",
            PrintConditionOperator.IsTrue => "True",
            PrintConditionOperator.IsFalse => "False",
            PrintConditionOperator.Equal => "一致",
            PrintConditionOperator.NotEqual => "不一致",
            PrintConditionOperator.GreaterThan => "より大きい",
            PrintConditionOperator.GreaterThanOrEqual => "以上",
            PrintConditionOperator.LessThan => "より小さい",
            PrintConditionOperator.LessThanOrEqual => "以下",
            PrintConditionOperator.OnOrAfter => "以降",
            PrintConditionOperator.OnOrBefore => "以前",
            _ => value.ToString()
        }
        : value.ToString();


}
