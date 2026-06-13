using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvColumnService
{
    CsvColumnAddResult AddColumnToAllFiles(List<CsvFileItem> csvFiles, string columnName, bool hasHeaderRow);
}

public sealed class CsvColumnService : ICsvColumnService
{
    private readonly Func<string, string> createBackup;
    private readonly Action<CsvFileItem, bool> writeCsvFile;

    public CsvColumnService()
        : this(
            CsvBackupUtility.CreateTimestampedBackup,
            (file, hasHeaderRow) => CsvFileUtility.WriteCsvFile(file, hasHeaderRow, createBackup: false))
    {
    }

    internal CsvColumnService(Func<string, string> createBackup, Action<CsvFileItem, bool> writeCsvFile)
    {
        this.createBackup = createBackup;
        this.writeCsvFile = writeCsvFile;
    }

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

            var header = file.HeaderRow ?? [];
            if (file.Status == Enums.CsvFileStatus.ColumnMismatch
                || (header.Length > 0 && file.Rows.Any(row => !CsvFileUtility.IsBlankRow(row) && row.Length != header.Length)))
            {
                throw new InvalidOperationException("Cannot add a column while CSV files have different column counts.");
            }

            if (CsvHeaderResolver.FindIndex(header, normalizedName) >= 0)
            {
                throw new InvalidOperationException($"Column already exists: {normalizedName}");
            }
        }

        var firstHeader = targets.First().HeaderRow ?? [];
        var columnIndex = firstHeader.Length;
        if (targets.Any(file => (file.HeaderRow ?? []).Length != columnIndex))
        {
            throw new InvalidOperationException("Cannot add a column when loaded CSV files have different header column counts.");
        }

        if (targets.Any(file => !HeadersMatch(firstHeader, file.HeaderRow ?? [])))
        {
            throw new InvalidOperationException("Cannot add a column when loaded CSV files have different header names.");
        }

        var writePlans = targets
            .Select(file => new CsvColumnWritePlan(file, CloneCsvFile(file)))
            .ToList();

        foreach (var plan in writePlans)
        {
            var file = plan.Work;
            var header = file.HeaderRow ?? CreateGeneratedHeader(file.ColumnCount);
            file.HeaderRow = [.. header, normalizedName];

            for (var i = 0; i < file.Rows.Count; i++)
            {
                if (!CsvFileUtility.IsBlankRow(file.Rows[i]))
                {
                    file.Rows[i] = [.. file.Rows[i], ""];
                }
            }

            file.ColumnCount = Math.Max(file.HeaderRow.Length, file.Rows.Where(row => !CsvFileUtility.IsBlankRow(row)).Select(row => row.Length).DefaultIfEmpty(0).Max());
            file.RowCount = file.Rows.Count;
            UpdateStatus(file);
        }

        WriteChangedCsvFilesTransactionally(writePlans, hasHeaderRow);
        ApplyCsvFileState(writePlans);

        return new CsvColumnAddResult
        {
            ColumnName = normalizedName,
            ColumnIndex = columnIndex,
            UpdatedFileCount = targets.Count
        };
    }


    private sealed class CsvColumnWritePlan(CsvFileItem original, CsvFileItem work)
    {
        public CsvFileItem Original { get; } = original;
        public CsvFileItem Work { get; } = work;
        public string? BackupPath { get; set; }
        public bool TargetExisted { get; set; }
        public bool TargetWritten { get; set; }
    }

    private static CsvFileItem CloneCsvFile(CsvFileItem file) => new()
    {
        FilePath = file.FilePath,
        FileName = file.FileName,
        EncodingName = file.EncodingName,
        Delimiter = file.Delimiter,
        NewLine = file.NewLine,
        Snapshot = file.Snapshot,
        RowCount = file.RowCount,
        ColumnCount = file.ColumnCount,
        Status = file.Status,
        Note = file.Note,
        HeaderRow = file.HeaderRow?.ToArray(),
        Rows = file.Rows.Select(row => row.ToArray()).ToList()
    };

    private void WriteChangedCsvFilesTransactionally(List<CsvColumnWritePlan> changedFiles, bool hasHeaderRow)
    {
        try
        {
            foreach (var plan in changedFiles)
            {
                plan.TargetExisted = File.Exists(plan.Original.FilePath);
                if (plan.TargetExisted)
                {
                    plan.BackupPath = createBackup(plan.Original.FilePath);
                }
            }

            foreach (var plan in changedFiles)
            {
                writeCsvFile(plan.Work, hasHeaderRow);
                plan.TargetWritten = true;
            }
        }
        catch (Exception ex)
        {
            var rollbackErrors = RollBackCsvWrites(changedFiles);
            throw new IOException(BuildTransactionalSaveFailureMessage(changedFiles, ex, rollbackErrors), ex);
        }
    }

    private static List<Exception> RollBackCsvWrites(IEnumerable<CsvColumnWritePlan> changedFiles)
    {
        var rollbackErrors = new List<Exception>();

        foreach (var plan in changedFiles.Reverse())
        {
            try
            {
                if (plan.BackupPath is not null && File.Exists(plan.BackupPath))
                {
                    File.Copy(plan.BackupPath, plan.Original.FilePath, overwrite: true);
                }
                else if (!plan.TargetExisted && plan.TargetWritten && File.Exists(plan.Original.FilePath))
                {
                    File.Delete(plan.Original.FilePath);
                }
            }
            catch (Exception rollbackEx)
            {
                rollbackErrors.Add(rollbackEx);
            }
        }

        return rollbackErrors;
    }

    private static string BuildTransactionalSaveFailureMessage(
        IEnumerable<CsvColumnWritePlan> changedFiles,
        Exception saveException,
        IReadOnlyCollection<Exception> rollbackErrors)
    {
        var attemptedFiles = changedFiles.Select(plan => plan.Original.FilePath).ToList();
        var backupFiles = changedFiles
            .Where(plan => !string.IsNullOrWhiteSpace(plan.BackupPath))
            .Select(plan => plan.BackupPath!)
            .ToList();
        var writtenFiles = changedFiles
            .Where(plan => plan.TargetWritten)
            .Select(plan => plan.Original.FilePath)
            .ToList();

        var lines = new List<string>
        {
            "CSV column addition save failed. Changes were written transactionally and rollback was attempted.",
            $"Original error: {saveException.Message}",
            "Attempted files:",
        };
        lines.AddRange(attemptedFiles.Select(path => "- " + path));

        if (backupFiles.Count > 0)
        {
            lines.Add("Backup files:");
            lines.AddRange(backupFiles.Select(path => "- " + path));
        }

        if (writtenFiles.Count > 0)
        {
            lines.Add("Files written before failure:");
            lines.AddRange(writtenFiles.Select(path => "- " + path));
        }

        if (rollbackErrors.Count > 0)
        {
            lines.Add("Rollback errors:");
            lines.AddRange(rollbackErrors.Select(error => "- " + error.Message));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void ApplyCsvFileState(IEnumerable<CsvColumnWritePlan> writePlans)
    {
        foreach (var plan in writePlans)
        {
            plan.Original.HeaderRow = plan.Work.HeaderRow?.ToArray();
            plan.Original.Rows = plan.Work.Rows.Select(row => row.ToArray()).ToList();
            plan.Original.RowCount = plan.Work.RowCount;
            plan.Original.ColumnCount = plan.Work.ColumnCount;
            plan.Original.Status = plan.Work.Status;
            plan.Original.Note = plan.Work.Note;
            plan.Original.NewLine = plan.Work.NewLine;
            plan.Original.Snapshot = plan.Work.Snapshot;
        }
    }

    private static void UpdateStatus(CsvFileItem file)
    {
        var expected = file.HeaderRow?.Length ?? file.ColumnCount;
        if (expected > 0 && file.Rows.Any(row => !CsvFileUtility.IsBlankRow(row) && row.Length != expected))
        {
            file.Status = Enums.CsvFileStatus.ColumnMismatch;
            file.Note = $"ColumnMismatch - Expected {expected}, actual {file.Rows.Where(row => !CsvFileUtility.IsBlankRow(row)).Select(row => row.Length).DefaultIfEmpty(0).Max()}";
            return;
        }

        file.Status = Enums.CsvFileStatus.Ok;
        file.Note = "";
    }

    private static bool HeadersMatch(string[] left, string[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (CsvHeaderResolver.FindIndex([left[i]], right[i]) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string[] CreateGeneratedHeader(int columnCount) =>
        Enumerable.Range(1, Math.Max(0, columnCount)).Select(index => $"Column {index}").ToArray();

}

public sealed class CsvColumnAddResult
{
    public string ColumnName { get; init; } = "";
    public int ColumnIndex { get; init; }
    public int UpdatedFileCount { get; init; }
}
