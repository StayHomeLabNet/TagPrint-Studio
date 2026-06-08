using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IPrintTargetSummaryService
{
    PrintTargetConfirmationData Build(
        LayoutFile layout,
        PresetFile preset,
        IEnumerable<CsvFileItem> csvFiles,
        OutputOptions options,
        bool hasHeaderRow,
        int rowsPerPage);
}

public sealed class PrintTargetSummaryService : IPrintTargetSummaryService
{
    public PrintTargetConfirmationData Build(
        LayoutFile layout,
        PresetFile preset,
        IEnumerable<CsvFileItem> csvFiles,
        OutputOptions options,
        bool hasHeaderRow,
        int rowsPerPage)
    {
        var entries = Entries(preset, csvFiles, options);
        if (entries.Count == 0)
        {
            return new PrintTargetConfirmationData { SortMode = options.PrintSortMode };
        }

        var safeRowsPerPage = Math.Max(1, rowsPerPage);
        var pageCount = (int)Math.Ceiling(entries.Count / (double)safeRowsPerPage);
        var sortField = SortField(preset, options);

        var orderedRows = entries
            .Select((entry, index) => new PrintTargetOrderRow
            {
                Order = index + 1,
                CsvName = entry.File.FileName,
                RowNumber = entry.RowIndex + (hasHeaderRow ? 2 : 1),
                SortValue = CellValue(entry.Row, sortField?.ColumnIndex),
                KeyValue = KeyValue(preset, entry.Row)
            })
            .ToList();

        var orderLookup = orderedRows
            .GroupBy(row => row.CsvName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Order).ToList(), StringComparer.CurrentCultureIgnoreCase);

        var csvSummaries = entries
            .GroupBy(entry => entry.File.FileName, StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                var orders = orderLookup[group.Key];
                var count = group.Count();
                return new PrintTargetCsvSummaryRow
                {
                    CsvName = group.Key,
                    RowCount = count,
                    TagCount = count,
                    FirstOrder = orders.Min(),
                    LastOrder = orders.Max()
                };
            })
            .OrderBy(row => row.FirstOrder)
            .ToList();

        return new PrintTargetConfirmationData
        {
            TotalTags = entries.Count,
            CsvCount = csvSummaries.Count,
            PageCount = pageCount,
            SortMode = options.PrintSortMode,
            CsvSummaries = csvSummaries,
            OrderedRows = orderedRows
        };
    }

    private static List<PrintableRowService.PrintableRowEntry> Entries(PresetFile preset, IEnumerable<CsvFileItem> csvFiles, OutputOptions options)
    {
        var validFiles = csvFiles.Where(file => file.Status == CsvFileStatus.Ok).ToList();
        var usesGlobalOrder =
            options.OutputFormat == OutputFormatType.Print
            || (options.OutputFormat == OutputFormatType.Pdf && options.OutputScope == OutputScopeType.SinglePdfAllCsv)
            || (options.OutputFormat == OutputFormatType.Png && options.OutputScope == OutputScopeType.IndividualPngPerRow);

        if (usesGlobalOrder)
        {
            return PrintableRowService.PrintableRowEntries(preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType);
        }

        return validFiles
            .SelectMany(file => PrintableRowService.PrintableRowEntries(preset, [file], options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType))
            .ToList();
    }

    private static FieldDefinition? SortField(PresetFile preset, OutputOptions options)
    {
        if (options.PrintSortMode == PrintSortMode.SelectedField)
        {
            return preset.Fields.FirstOrDefault(field =>
                field.Enabled
                && field.ColumnIndex is not null
                && string.Equals(field.FieldId, options.PrintSortFieldId, StringComparison.Ordinal));
        }

        if (options.PrintSortMode == PrintSortMode.ItemCode)
        {
            return preset.Fields
                .Where(field => field.Enabled && field.ColumnIndex is not null)
                .FirstOrDefault(field => IsItemCodeField(field.DisplayName))
                ?? preset.Fields
                    .Where(field => field.Enabled && field.ColumnIndex is not null)
                    .FirstOrDefault(field => IsItemCodeField(field.FieldId));
        }

        return null;
    }

    private static string KeyValue(PresetFile preset, string[] row)
    {
        var field = preset.Fields
            .Where(field => field.Enabled && field.ColumnIndex is not null)
            .FirstOrDefault(field => IsItemCodeField(field.DisplayName))
            ?? preset.Fields
                .Where(field => field.Enabled && field.ColumnIndex is not null)
                .FirstOrDefault(field => IsItemCodeField(field.FieldId))
            ?? preset.Fields.FirstOrDefault(field => field.Enabled && field.ColumnIndex is not null);

        return CellValue(row, field?.ColumnIndex);
    }

    private static bool IsItemCodeField(string value)
    {
        var normalized = value.Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
        return normalized.Contains("itemcode", StringComparison.OrdinalIgnoreCase);
    }

    private static string CellValue(string[] row, int? index)
    {
        return index is int columnIndex && columnIndex >= 0 && columnIndex < row.Length ? row[columnIndex] : "";
    }
}
