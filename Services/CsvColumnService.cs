using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvColumnService
{
    CsvColumnAddResult AddColumnToAllFiles(List<CsvFileItem> csvFiles, string columnName, bool hasHeaderRow);
}

public sealed class CsvColumnService : ICsvColumnService
{
    public CsvColumnAddResult AddColumnToAllFiles(List<CsvFileItem> csvFiles, string columnName, bool hasHeaderRow)
    {
        if (!hasHeaderRow)
        {
            throw new InvalidOperationException("CSV column addition requires header rows.");
        }

        var normalizedName = columnName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Column name is required.");
        }

        var targets = csvFiles
            .Where(file => file.Status != Enums.CsvFileStatus.LoadFailed)
            .Where(file => !string.IsNullOrWhiteSpace(file.FilePath))
            .ToList();
        if (targets.Count == 0)
        {
            throw new InvalidOperationException("No CSV files are available.");
        }

        foreach (var file in targets)
        {
            if (!File.Exists(file.FilePath))
            {
                throw new FileNotFoundException("CSV file was not found.", file.FilePath);
            }

            if (IndexOfHeader(file.HeaderRow ?? [], normalizedName) >= 0)
            {
                throw new InvalidOperationException($"Column already exists: {normalizedName}");
            }
        }

        var firstHeader = targets.First().HeaderRow ?? [];
        var columnIndex = firstHeader.Length;

        foreach (var file in targets)
        {
            var header = file.HeaderRow ?? CreateGeneratedHeader(file.ColumnCount);
            file.HeaderRow = [.. header, normalizedName];

            for (var i = 0; i < file.Rows.Count; i++)
            {
                file.Rows[i] = [.. file.Rows[i], ""];
            }

            file.ColumnCount = Math.Max(file.HeaderRow.Length, file.Rows.Select(row => row.Length).DefaultIfEmpty(0).Max());
            file.RowCount = file.Rows.Count;
            UpdateStatus(file);
        }

        foreach (var file in targets)
        {
            CsvFileUtility.WriteCsvFile(file, hasHeaderRow);
        }

        return new CsvColumnAddResult
        {
            ColumnName = normalizedName,
            ColumnIndex = columnIndex,
            UpdatedFileCount = targets.Count
        };
    }

    private static void UpdateStatus(CsvFileItem file)
    {
        var expected = file.HeaderRow?.Length ?? file.ColumnCount;
        if (expected > 0 && file.Rows.Any(row => row.Length != expected))
        {
            file.Status = Enums.CsvFileStatus.ColumnMismatch;
            file.Note = $"ColumnMismatch - Expected {expected}, actual {file.Rows.Select(row => row.Length).DefaultIfEmpty(0).Max()}";
            return;
        }

        file.Status = Enums.CsvFileStatus.Ok;
        file.Note = "";
    }

    private static string[] CreateGeneratedHeader(int columnCount) =>
        Enumerable.Range(1, Math.Max(0, columnCount)).Select(index => $"Column {index}").ToArray();

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
}

public sealed class CsvColumnAddResult
{
    public string ColumnName { get; init; } = "";
    public int ColumnIndex { get; init; }
    public int UpdatedFileCount { get; init; }
}
