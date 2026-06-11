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

            var flagIndex = FindHeaderIndex(file, flagColumnName);
            var hasPrintableRow = flagIndex >= 0 && file.Rows.Any(row => IsTrue(ValueAt(row, flagIndex)));
            if (!hasPrintableRow)
            {
                yield return file.FilePath;
            }
        }
    }

    private static int FindHeaderIndex(CsvFileItem file, string columnName)
    {
        if (file.HeaderRow is not { Length: > 0 }) return -1;

        for (var i = 0; i < file.HeaderRow.Length; i++)
        {
            if (string.Equals(file.HeaderRow[i], columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string ValueAt(string[] row, int index) => index >= 0 && index < row.Length ? row[index] : "";

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
}
