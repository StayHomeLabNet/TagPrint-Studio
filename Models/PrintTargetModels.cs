using TagPrintStudio.Enums;

namespace TagPrintStudio.Models;

public sealed class PrintTargetConfirmationData
{
    public int TotalTags { get; init; }
    public int CsvCount { get; init; }
    public int PageCount { get; init; }
    public PrintSortMode SortMode { get; init; }
    public List<PrintTargetCsvSummaryRow> CsvSummaries { get; init; } = [];
    public List<PrintTargetOrderRow> OrderedRows { get; init; } = [];
}

public sealed class PrintTargetCsvSummaryRow
{
    public string CsvName { get; init; } = "";
    public int RowCount { get; init; }
    public int TagCount { get; init; }
    public int FirstOrder { get; init; }
    public int LastOrder { get; init; }
}

public sealed class PrintTargetOrderRow
{
    public int Order { get; init; }
    public string CsvName { get; init; } = "";
    public int RowNumber { get; init; }
    public string SortValue { get; init; } = "";
    public string KeyValue { get; init; } = "";
}
