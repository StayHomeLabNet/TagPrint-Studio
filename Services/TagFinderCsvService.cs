using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ITagFinderCsvService
{
    void SaveFlags(
        List<CsvFileItem> csvFiles,
        IEnumerable<TagFinderSaveSelection> selections,
        string flagColumnName,
        string orderColumnName,
        bool hasHeaderRow);

    TagFinderQueueCsvResult CreatePrintQueueCsv(
        List<TagFinderQueueSource> targets,
        int additionalQuantity,
        string flagColumnName,
        string orderColumnName,
        bool hasHeaderRow);
}

public sealed class TagFinderCsvService : ITagFinderCsvService
{
    public void SaveFlags(
        List<CsvFileItem> csvFiles,
        IEnumerable<TagFinderSaveSelection> selections,
        string flagColumnName,
        string orderColumnName,
        bool hasHeaderRow)
    {
        foreach (var file in csvFiles)
        {
            EnsureFlagColumn(file, flagColumnName, hasHeaderRow);
            EnsureTextColumn(file, OrderColumnName(flagColumnName, orderColumnName), hasHeaderRow);
        }

        var nextOrder = MaxOrderValue(csvFiles, OrderColumnName(flagColumnName, orderColumnName)) + 1;
        foreach (var group in selections.GroupBy(row => row.File.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var file = group.First().File;
            var flagIndex = FindHeaderIndex(file, flagColumnName);
            var orderIndex = FindHeaderIndex(file, OrderColumnName(flagColumnName, orderColumnName));
            if (flagIndex < 0 || orderIndex < 0) continue;

            foreach (var result in group)
            {
                file.Rows[result.RowIndex] = EnsureRowWidth(file.Rows[result.RowIndex], Math.Max(flagIndex, orderIndex) + 1);
                file.Rows[result.RowIndex][flagIndex] = result.Checked ? "true" : "false";
                if (!result.Checked)
                {
                    file.Rows[result.RowIndex][orderIndex] = "";
                }
                else if (!result.WasChecked && string.IsNullOrWhiteSpace(file.Rows[result.RowIndex][orderIndex]))
                {
                    file.Rows[result.RowIndex][orderIndex] = nextOrder.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    nextOrder++;
                }
            }
        }

        foreach (var file in csvFiles)
        {
            if (!File.Exists(file.FilePath) && CsvFileUtility.IsPrintQueueCsv(file.FilePath))
            {
                continue;
            }

            WriteCsvFile(file, hasHeaderRow);
        }
    }

    public TagFinderQueueCsvResult CreatePrintQueueCsv(
        List<TagFinderQueueSource> targets,
        int additionalQuantity,
        string flagColumnName,
        string orderColumnName,
        bool hasHeaderRow)
    {
        if (targets.Count == 0)
        {
            throw new InvalidOperationException("No target rows were selected.");
        }

        var firstFile = targets[0].File;
        var sourceFolder = Path.GetDirectoryName(firstFile.FilePath)
            ?? throw new InvalidOperationException("Could not resolve the source CSV folder.");
        var header = (firstFile.HeaderRow ?? []).ToArray();
        var flagIndex = IndexOfHeader(header, flagColumnName);
        if (flagIndex < 0)
        {
            flagIndex = header.Length;
            header = [.. header, flagColumnName];
        }
        var resolvedOrderColumnName = OrderColumnName(flagColumnName, orderColumnName);
        var orderIndex = IndexOfHeader(header, resolvedOrderColumnName);
        if (orderIndex < 0)
        {
            orderIndex = header.Length;
            header = [.. header, resolvedOrderColumnName];
        }
        var nextOrder = MaxOrderValue(targets.Select(target => target.File).Distinct(), resolvedOrderColumnName) + 1;

        var lines = new List<string>();
        if (hasHeaderRow)
        {
            lines.Add(ToCsvLine(header));
        }

        var queueRows = new List<string[]>();
        foreach (var target in targets)
        {
            var sourceRow = target.File.Rows[target.RowIndex];
            var row = EnsureRowWidth(sourceRow.ToArray(), Math.Max(flagIndex, orderIndex) + 1);
            row[flagIndex] = "true";
            if (string.IsNullOrWhiteSpace(row[orderIndex]))
            {
                row[orderIndex] = nextOrder.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nextOrder++;
            }

            for (var i = 0; i < additionalQuantity; i++)
            {
                queueRows.Add(row.ToArray());
                lines.Add(ToCsvLine(row));
            }
        }

        var path = Path.Combine(sourceFolder, $"print_queue_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        File.WriteAllText(path, content, CsvFileUtility.EncodingForCsv(firstFile.EncodingName));

        return new TagFinderQueueCsvResult
        {
            FilePath = path,
            EncodingName = firstFile.EncodingName,
            Header = header,
            Rows = queueRows
        };
    }

    private static int EnsureFlagColumn(CsvFileItem file, string flagColumnName, bool hasHeaderRow)
    {
        var index = EnsureTextColumn(file, flagColumnName, hasHeaderRow);
        for (var i = 0; i < file.Rows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(file.Rows[i][index]))
            {
                file.Rows[i][index] = "false";
            }
        }

        return index;
    }

    private static int EnsureTextColumn(CsvFileItem file, string columnName, bool hasHeaderRow)
    {
        var index = FindHeaderIndex(file, columnName);
        if (index >= 0)
        {
            for (var i = 0; i < file.Rows.Count; i++)
            {
                file.Rows[i] = EnsureRowWidth(file.Rows[i], index + 1);
            }

            file.ColumnCount = Math.Max(file.ColumnCount, index + 1);
            return index;
        }

        index = file.HeaderRow?.Length ?? file.ColumnCount;
        if (hasHeaderRow)
        {
            file.HeaderRow = [.. (file.HeaderRow ?? []), columnName];
        }

        for (var i = 0; i < file.Rows.Count; i++)
        {
            file.Rows[i] = EnsureRowWidth(file.Rows[i], index + 1);
            file.Rows[i][index] = "";
        }

        file.ColumnCount = Math.Max(file.ColumnCount, index + 1);
        return index;
    }

    private static int MaxOrderValue(IEnumerable<CsvFileItem> csvFiles, string orderColumnName)
    {
        var max = 0;
        foreach (var file in csvFiles)
        {
            var orderIndex = FindHeaderIndex(file, orderColumnName);
            if (orderIndex < 0) continue;

            foreach (var row in file.Rows)
            {
                if (orderIndex >= row.Length) continue;
                if (int.TryParse(row[orderIndex], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    max = Math.Max(max, value);
                }
            }
        }

        return max;
    }

    private static string OrderColumnName(string flagColumnName, string orderColumnName)
    {
        if (!string.IsNullOrWhiteSpace(orderColumnName)) return orderColumnName.Trim();
        var flag = string.IsNullOrWhiteSpace(flagColumnName) ? "tagPrint" : flagColumnName.Trim();
        return $"{flag}Order";
    }

    private static void WriteCsvFile(CsvFileItem file, bool hasHeaderRow)
    {
        CsvBackupUtility.CreateTimestampedBackup(file.FilePath);
        var lines = new List<string>();
        if (hasHeaderRow && file.HeaderRow is { Length: > 0 })
        {
            lines.Add(ToCsvLine(file.HeaderRow));
        }

        lines.AddRange(file.Rows.Select(ToCsvLine));
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        File.WriteAllText(file.FilePath, content, CsvFileUtility.EncodingForCsv(file.EncodingName));
    }

    private static string[] EnsureRowWidth(string[] row, int width)
    {
        if (row.Length >= width) return row;
        Array.Resize(ref row, width);
        return row;
    }

    private static int FindHeaderIndex(CsvFileItem file, string columnName)
    {
        if (file.HeaderRow is not { Length: > 0 }) return -1;
        return IndexOfHeader(file.HeaderRow, columnName);
    }

    private static int IndexOfHeader(string[] header, string columnName)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (string.Equals(header[i], columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string ToCsvLine(IEnumerable<string> cells) => string.Join(",", cells.Select(EscapeCsvCell));

    private static string EscapeCsvCell(string? value)
    {
        var text = value ?? "";
        return text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
