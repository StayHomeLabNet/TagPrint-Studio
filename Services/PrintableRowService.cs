using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using System.Globalization;

namespace TagPrintStudio.Services;

public static class PrintableRowService
{
    public sealed class PrintableRowEntry
    {
        public CsvFileItem File { get; init; } = null!;
        public string[] Row { get; init; } = [];
        public int FileIndex { get; init; }
        public int RowIndex { get; init; }
    }

    public static List<CsvFileItem> SelectFiles(OutputScopeType scope, IEnumerable<CsvFileItem> files, CsvFileItem? currentFile)
    {
        if (scope == OutputScopeType.SinglePdfAllCsv)
        {
            return files.Where(file => file.Status == CsvFileStatus.Ok).ToList();
        }

        return currentFile is { Status: CsvFileStatus.Ok } ? [currentFile] : [];
    }

    public static List<string[]> PrintableRows(PresetFile preset, IEnumerable<CsvFileItem> files, PrintSortMode sortMode = PrintSortMode.LoadOrder, string sortFieldId = "", PrintSortValueType sortValueType = PrintSortValueType.Text)
    {
        return PrintableRowEntries(preset, files, sortMode, sortFieldId, sortValueType)
            .Select(entry => entry.Row)
            .ToList();
    }

    public static List<PrintableRowEntry> PrintableRowEntries(PresetFile preset, IEnumerable<CsvFileItem> files, PrintSortMode sortMode = PrintSortMode.LoadOrder, string sortFieldId = "", PrintSortValueType sortValueType = PrintSortValueType.Text)
    {
        var entries = files
            .Select((file, fileIndex) => new { file, fileIndex })
            .Where(item => item.file.Status == CsvFileStatus.Ok)
            .SelectMany(item => item.file.Rows.Select((row, rowIndex) => new PrintableRowEntry
            {
                File = item.file,
                Row = row,
                FileIndex = item.fileIndex,
                RowIndex = rowIndex
            }))
            .Where(entry => PrintConditionService.MatchesAll(preset, entry.Row))
            .ToList();

        return SortEntries(preset, entries, sortMode, sortFieldId, sortValueType);
    }

    public static List<CsvPreviewRowContext> PreviewRows(PresetFile preset, IEnumerable<CsvFileItem> files, bool hasHeaderRow, PrintSortMode sortMode = PrintSortMode.LoadOrder, string sortFieldId = "", PrintSortValueType sortValueType = PrintSortValueType.Text)
    {
        var contexts = new List<CsvPreviewRowContext>();

        foreach (var entry in PrintableRowEntries(preset, files, sortMode, sortFieldId, sortValueType))
        {
            var csvFile = entry.File;
            contexts.Add(new CsvPreviewRowContext
            {
                FilePath = csvFile.FilePath,
                FileName = csvFile.FileName,
                EncodingName = csvFile.EncodingName,
                Delimiter = csvFile.Delimiter,
                HasHeaderRow = hasHeaderRow,
                HeaderRow = hasHeaderRow ? csvFile.HeaderRow : null,
                AllRows = csvFile.Rows,
                RowIndex = entry.RowIndex,
                Values = entry.Row,
                Data = OutputService.MapRow(preset, entry.Row)
            });
        }

        return contexts;
    }

    private static List<PrintableRowEntry> SortEntries(PresetFile preset, List<PrintableRowEntry> entries, PrintSortMode sortMode, string sortFieldId, PrintSortValueType sortValueType)
    {
        if (sortMode == PrintSortMode.LoadOrder) return entries;

        var sortField = sortMode switch
        {
            PrintSortMode.ItemCode => FindItemCodeField(preset),
            PrintSortMode.SelectedField => FindFieldById(preset, sortFieldId),
            PrintSortMode.PrintTargetOrder => FindPrintTargetOrderField(preset),
            _ => null
        };
        if (sortMode == PrintSortMode.PrintTargetOrder)
        {
            return SortByPrintTargetOrder(entries, sortField?.ColumnIndex);
        }

        if (sortField?.ColumnIndex is not int sortColumnIndex) return entries;

        var effectiveSortValueType = sortMode == PrintSortMode.ItemCode ? PrintSortValueType.Natural : sortValueType;
        return effectiveSortValueType switch
        {
            PrintSortValueType.Number => SortByNumber(entries, sortColumnIndex),
            PrintSortValueType.Date => SortByDate(entries, sortColumnIndex),
            PrintSortValueType.Natural => SortByNatural(entries, sortColumnIndex),
            _ => SortByText(entries, sortColumnIndex)
        };
    }

    private static List<PrintableRowEntry> SortByText(List<PrintableRowEntry> entries, int sortColumnIndex) =>
        entries
            .OrderBy(entry => CellValue(entry.Row, sortColumnIndex), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath))
            .ThenBy(entry => entry.FileIndex)
            .ThenBy(entry => entry.RowIndex)
            .ToList();

    private static List<PrintableRowEntry> SortByNatural(List<PrintableRowEntry> entries, int sortColumnIndex) =>
        entries
            .OrderBy(entry => CellValue(entry.Row, sortColumnIndex), NaturalStringComparer.Instance)
            .ThenBy(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath))
            .ThenBy(entry => entry.FileIndex)
            .ThenBy(entry => entry.RowIndex)
            .ToList();

    private static List<PrintableRowEntry> SortByNumber(List<PrintableRowEntry> entries, int sortColumnIndex) =>
        entries
            .OrderBy(entry => TryParseDecimal(CellValue(entry.Row, sortColumnIndex), out var value) ? 0 : 1)
            .ThenBy(entry => TryParseDecimal(CellValue(entry.Row, sortColumnIndex), out var value) ? value : 0)
            .ThenBy(entry => CellValue(entry.Row, sortColumnIndex), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath))
            .ThenBy(entry => entry.FileIndex)
            .ThenBy(entry => entry.RowIndex)
            .ToList();

    private static List<PrintableRowEntry> SortByDate(List<PrintableRowEntry> entries, int sortColumnIndex) =>
        entries
            .OrderBy(entry => TryParseDate(CellValue(entry.Row, sortColumnIndex), out var value) ? 0 : 1)
            .ThenBy(entry => TryParseDate(CellValue(entry.Row, sortColumnIndex), out var value) ? value : DateTime.MaxValue)
            .ThenBy(entry => CellValue(entry.Row, sortColumnIndex), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath))
            .ThenBy(entry => entry.FileIndex)
            .ThenBy(entry => entry.RowIndex)
            .ToList();

    private static List<PrintableRowEntry> SortByPrintTargetOrder(List<PrintableRowEntry> entries, int? presetColumnIndex) =>
        entries
            .OrderBy(entry => TryPrintTargetOrderValue(entry, presetColumnIndex, out var value) ? 0 : 1)
            .ThenBy(entry => TryPrintTargetOrderValue(entry, presetColumnIndex, out var value) ? value : int.MaxValue)
            .ThenBy(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath))
            .ThenBy(entry => entry.FileIndex)
            .ThenBy(entry => entry.RowIndex)
            .ToList();

    private static bool TryParseDecimal(string value, out decimal parsed) =>
        decimal.TryParse(value, NumberStyles.Number | NumberStyles.Currency, CultureInfo.CurrentCulture, out parsed)
        || decimal.TryParse(value, NumberStyles.Number | NumberStyles.Currency, CultureInfo.InvariantCulture, out parsed);

    private static bool TryParseDate(string value, out DateTime parsed) =>
        DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)
        || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);

    private static FieldDefinition? FindItemCodeField(PresetFile preset)
    {
        static bool IsItemCode(string value)
        {
            var normalized = value.Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
            return normalized.Contains("itemcode", StringComparison.OrdinalIgnoreCase);
        }

        return preset.Fields
            .Where(field => field.Enabled && field.ColumnIndex is not null)
            .FirstOrDefault(field => IsItemCode(field.DisplayName))
            ?? preset.Fields
                .Where(field => field.Enabled && field.ColumnIndex is not null)
                .FirstOrDefault(field => IsItemCode(field.FieldId));
    }

    private static FieldDefinition? FindFieldById(PresetFile preset, string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId)) return null;
        return preset.Fields.FirstOrDefault(field =>
            field.Enabled
            && field.ColumnIndex is not null
            && string.Equals(field.FieldId, fieldId, StringComparison.Ordinal));
    }

    private static FieldDefinition? FindPrintTargetOrderField(PresetFile preset) =>
        preset.Fields
            .Where(field => field.Enabled && field.ColumnIndex is not null)
            .FirstOrDefault(field => IsPrintTargetOrderName(field.DisplayName))
        ?? preset.Fields
            .Where(field => field.Enabled && field.ColumnIndex is not null)
            .FirstOrDefault(field => IsPrintTargetOrderName(field.FieldId));

    private static bool TryPrintTargetOrderValue(PrintableRowEntry entry, int? presetColumnIndex, out int value)
    {
        value = 0;
        var columnIndex = PrintTargetOrderColumnIndex(entry.File, presetColumnIndex);
        return columnIndex >= 0
            && columnIndex < entry.Row.Length
            && int.TryParse(entry.Row[columnIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static int PrintTargetOrderColumnIndex(CsvFileItem file, int? presetColumnIndex)
    {
        if (file.HeaderRow is { Length: > 0 } header)
        {
            for (var i = 0; i < header.Length; i++)
            {
                if (IsPrintTargetOrderName(header[i]))
                {
                    return i;
                }
            }
        }

        return presetColumnIndex ?? -1;
    }

    private static bool IsPrintTargetOrderName(string value)
    {
        var normalized = value.Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        return string.Equals(normalized, "tagprintorder", StringComparison.OrdinalIgnoreCase);
    }

    private static string CellValue(string[] row, int index)
    {
        return index >= 0 && index < row.Length ? row[index] : "";
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= "";
            y ??= "";
            var ix = 0;
            var iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    var numberCompare = CompareNumberRun(x, ref ix, y, ref iy);
                    if (numberCompare != 0)
                    {
                        return numberCompare;
                    }
                    continue;
                }

                var charCompare = char.ToUpper(x[ix], CultureInfo.CurrentCulture)
                    .CompareTo(char.ToUpper(y[iy], CultureInfo.CurrentCulture));
                if (charCompare != 0)
                {
                    return charCompare;
                }

                ix++;
                iy++;
            }

            return x.Length.CompareTo(y.Length);
        }

        private static int CompareNumberRun(string x, ref int ix, string y, ref int iy)
        {
            var startX = ix;
            var startY = iy;
            while (ix < x.Length && char.IsDigit(x[ix])) ix++;
            while (iy < y.Length && char.IsDigit(y[iy])) iy++;

            var rawX = x[startX..ix];
            var rawY = y[startY..iy];
            var trimmedX = rawX.TrimStart('0');
            var trimmedY = rawY.TrimStart('0');
            if (trimmedX.Length == 0) trimmedX = "0";
            if (trimmedY.Length == 0) trimmedY = "0";

            var lengthCompare = trimmedX.Length.CompareTo(trimmedY.Length);
            if (lengthCompare != 0)
            {
                return lengthCompare;
            }

            var valueCompare = string.CompareOrdinal(trimmedX, trimmedY);
            return valueCompare != 0 ? valueCompare : rawX.Length.CompareTo(rawY.Length);
        }
    }
}
