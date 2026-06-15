using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ITagFinderWorkflowService
{
    List<TagFinderResultRow> ResolveQueueTargets(
        IEnumerable<TagFinderResultRow> resultRows,
        bool showingCheckedOnlyRows,
        TagFinderResultRow? selectedRow,
        TagFinderResultRow? doubleClickTarget);

    IEnumerable<string> FindUnusedQueueCsvFiles(IEnumerable<CsvFileItem> csvFiles, string flagColumnName);
}

public sealed class TagFinderWorkflowService : ITagFinderWorkflowService
{
    public List<TagFinderResultRow> ResolveQueueTargets(
        IEnumerable<TagFinderResultRow> resultRows,
        bool showingCheckedOnlyRows,
        TagFinderResultRow? selectedRow,
        TagFinderResultRow? doubleClickTarget)
    {
        var rows = resultRows.ToList();
        if (doubleClickTarget is { Checked: true } target && rows.Contains(target))
        {
            return [target];
        }

        if (showingCheckedOnlyRows)
        {
            return selectedRow is { Checked: true } ? [selectedRow] : [];
        }

        return rows.Where(row => row.Checked).ToList();
    }

    public IEnumerable<string> FindUnusedQueueCsvFiles(IEnumerable<CsvFileItem> csvFiles, string flagColumnName)
    {
        foreach (var file in csvFiles)
        {
            if (!CsvFileUtility.IsPrintQueueCsv(file.FilePath)) continue;

            var flagIndex = CsvHeaderResolver.FindIndex(file, flagColumnName);
            var hasPrintableRow = flagIndex >= 0 && file.Rows.Any(row => row is not null && IsTrue(ValueAt(row, flagIndex)));
            if (!hasPrintableRow)
            {
                yield return file.FilePath;
            }
        }
    }


    private static string ValueAt(string[] row, int index) => index >= 0 && index < row.Length ? row[index] ?? "" : "";

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
}
