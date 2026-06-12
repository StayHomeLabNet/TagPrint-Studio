using System.Text;
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
        EnsureHeaderRowSupported(hasHeaderRow);

        var resolvedOrderColumnName = OrderColumnName(flagColumnName, orderColumnName);
        var workFiles = csvFiles.Select(CloneCsvFile).ToList();
        var workByPath = workFiles.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var file in workFiles)
        {
            EnsureFlagColumn(file, flagColumnName, hasHeaderRow);
            EnsureTextColumn(file, resolvedOrderColumnName, hasHeaderRow);
        }

        var nextOrder = MaxOrderValue(workFiles, resolvedOrderColumnName) + 1;
        foreach (var group in selections.GroupBy(row => row.File.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!workByPath.TryGetValue(group.Key, out var file)) continue;

            var flagIndex = CsvHeaderResolver.FindIndex(file, flagColumnName);
            var orderIndex = CsvHeaderResolver.FindIndex(file, resolvedOrderColumnName);
            if (flagIndex < 0 || orderIndex < 0) continue;

            foreach (var result in group)
            {
                if (result.RowIndex < 0 || result.RowIndex >= file.Rows.Count) continue;
                if (CsvFileUtility.IsBlankRow(file.Rows[result.RowIndex])) continue;

                file.Rows[result.RowIndex] = EnsureRowWidth(file.Rows[result.RowIndex], Math.Max(flagIndex, orderIndex) + 1);
                file.Rows[result.RowIndex][flagIndex] = result.Checked ? "true" : "false";
                if (!result.Checked)
                {
                    file.Rows[result.RowIndex][orderIndex] = "";
                }
                else if (string.IsNullOrWhiteSpace(file.Rows[result.RowIndex][orderIndex]))
                {
                    file.Rows[result.RowIndex][orderIndex] = nextOrder.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    nextOrder++;
                }
            }
        }

        var changedFiles = ChangedCsvFiles(csvFiles, workByPath, hasHeaderRow);
        WriteChangedCsvFilesTransactionally(changedFiles, hasHeaderRow);
        ApplyCsvFileState(changedFiles);
    }

    public TagFinderQueueCsvResult CreatePrintQueueCsv(
        List<TagFinderQueueSource> targets,
        int additionalQuantity,
        string flagColumnName,
        string orderColumnName,
        bool hasHeaderRow)
    {
        EnsureHeaderRowSupported(hasHeaderRow);

        if (additionalQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalQuantity), additionalQuantity, "Additional print quantity must be greater than zero.");
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException("No target rows were selected.");
        }

        var firstFile = targets[0].File;
        var sourceFolder = Path.GetDirectoryName(firstFile.FilePath)
            ?? throw new InvalidOperationException("Could not resolve the source CSV folder.");
        var resolvedOrderColumnName = OrderColumnName(flagColumnName, orderColumnName);
        var sourceFiles = targets
            .Select(target => target.File)
            .DistinctBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var workFiles = sourceFiles.Select(CloneCsvFile).ToList();
        var workByPath = workFiles.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var file in workFiles)
        {
            EnsureFlagColumn(file, flagColumnName, hasHeaderRow);
            EnsureTextColumn(file, resolvedOrderColumnName, hasHeaderRow);
        }

        var firstWorkFile = workByPath[firstFile.FilePath];
        var header = (firstWorkFile.HeaderRow ?? []).ToArray();
        var flagIndex = CsvHeaderResolver.FindIndex(header, flagColumnName);
        if (flagIndex < 0)
        {
            flagIndex = header.Length;
            header = [.. header, flagColumnName];
        }
        var orderIndex = CsvHeaderResolver.FindIndex(header, resolvedOrderColumnName);
        if (orderIndex < 0)
        {
            orderIndex = header.Length;
            header = [.. header, resolvedOrderColumnName];
        }
        var nextOrder = MaxOrderValue(workFiles, resolvedOrderColumnName) + 1;

        var lines = new List<string>();
        if (hasHeaderRow)
        {
            lines.Add(CsvFileUtility.ToCsvLine(header, firstWorkFile.Delimiter));
        }

        var queueRows = new List<string[]>();
        foreach (var target in targets)
        {
            if (!workByPath.TryGetValue(target.File.FilePath, out var targetFile)) continue;

            var sourceFlagIndex = CsvHeaderResolver.FindIndex(targetFile, flagColumnName);
            var sourceOrderIndex = CsvHeaderResolver.FindIndex(targetFile, resolvedOrderColumnName);
            if (sourceFlagIndex < 0 || sourceOrderIndex < 0) continue;

            if (target.RowIndex < 0 || target.RowIndex >= targetFile.Rows.Count) continue;
            if (CsvFileUtility.IsBlankRow(targetFile.Rows[target.RowIndex])) continue;

            var sourceRow = EnsureRowWidth(targetFile.Rows[target.RowIndex], Math.Max(sourceFlagIndex, sourceOrderIndex) + 1);
            sourceRow[sourceFlagIndex] = "true";
            if (string.IsNullOrWhiteSpace(sourceRow[sourceOrderIndex]))
            {
                sourceRow[sourceOrderIndex] = nextOrder.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nextOrder++;
            }
            targetFile.Rows[target.RowIndex] = sourceRow;

            var row = AlignRowToHeader(targetFile, sourceRow, header);
            row = EnsureRowWidth(row, Math.Max(flagIndex, orderIndex) + 1);
            row[flagIndex] = "true";
            row[orderIndex] = sourceRow[sourceOrderIndex];

            for (var i = 0; i < additionalQuantity; i++)
            {
                queueRows.Add(row.ToArray());
                lines.Add(CsvFileUtility.ToCsvLine(row, firstWorkFile.Delimiter));
            }
        }

        var path = NextQueueCsvPath(sourceFolder);
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        var changedFiles = ChangedCsvFiles(sourceFiles, workByPath, hasHeaderRow);

        WriteChangedCsvFilesAndQueueTransactionally(
            changedFiles,
            hasHeaderRow,
            path,
            content,
            CsvFileUtility.EncodingForCsv(firstWorkFile.EncodingName));
        ApplyCsvFileState(changedFiles);

        return new TagFinderQueueCsvResult
        {
            FilePath = path,
            EncodingName = firstWorkFile.EncodingName,
            Delimiter = firstWorkFile.Delimiter,
            Header = header,
            Rows = queueRows
        };
    }

    private sealed class CsvWritePlan
    {
        public CsvFileItem Original { get; init; } = new();
        public CsvFileItem Work { get; init; } = new();
        public string? BackupPath { get; set; }
        public bool TargetExisted { get; set; }
        public bool TargetWritten { get; set; }
    }

    private static CsvFileItem CloneCsvFile(CsvFileItem file)
    {
        return new CsvFileItem
        {
            FilePath = file.FilePath,
            FileName = file.FileName,
            EncodingName = file.EncodingName,
            Delimiter = file.Delimiter,
            RowCount = file.RowCount,
            ColumnCount = file.ColumnCount,
            Status = file.Status,
            Note = file.Note,
            HeaderRow = file.HeaderRow?.ToArray(),
            Rows = file.Rows.Select(row => row.ToArray()).ToList()
        };
    }

    private static List<CsvWritePlan> ChangedCsvFiles(
        IEnumerable<CsvFileItem> originals,
        IReadOnlyDictionary<string, CsvFileItem> workByPath,
        bool hasHeaderRow)
    {
        var changedFiles = new List<CsvWritePlan>();
        foreach (var original in originals)
        {
            if (!workByPath.TryGetValue(original.FilePath, out var work)) continue;
            if (!File.Exists(original.FilePath) && CsvFileUtility.IsPrintQueueCsv(original.FilePath)) continue;

            if (!string.Equals(
                CsvFileUtility.BuildCsvContent(original, hasHeaderRow),
                CsvFileUtility.BuildCsvContent(work, hasHeaderRow),
                StringComparison.Ordinal))
            {
                changedFiles.Add(new CsvWritePlan { Original = original, Work = work });
            }
        }

        return changedFiles;
    }

    private static void WriteChangedCsvFilesTransactionally(
        List<CsvWritePlan> changedFiles,
        bool hasHeaderRow)
    {
        WriteChangedCsvFilesAndQueueTransactionally(
            changedFiles,
            hasHeaderRow,
            queuePath: null,
            queueContent: null,
            queueEncoding: null);
    }

    private static void WriteChangedCsvFilesAndQueueTransactionally(
        List<CsvWritePlan> changedFiles,
        bool hasHeaderRow,
        string? queuePath,
        string? queueContent,
        Encoding? queueEncoding)
    {
        var queueWritten = false;

        try
        {
            foreach (var plan in changedFiles)
            {
                plan.TargetExisted = File.Exists(plan.Original.FilePath);
                if (plan.TargetExisted)
                {
                    plan.BackupPath = CsvBackupUtility.CreateTimestampedBackup(plan.Original.FilePath);
                }
            }

            foreach (var plan in changedFiles)
            {
                CsvFileUtility.WriteCsvFile(plan.Work, hasHeaderRow, createBackup: false);
                plan.TargetWritten = true;
            }

            if (!string.IsNullOrWhiteSpace(queuePath)
                && queueContent is not null
                && queueEncoding is not null)
            {
                CsvFileUtility.WriteAllTextAtomic(queuePath, queueContent, queueEncoding);
                queueWritten = true;
            }
        }
        catch (Exception ex)
        {
            var rollbackErrors = RollBackCsvWrites(changedFiles, queuePath, queueWritten);
            var message = BuildTransactionalSaveFailureMessage(changedFiles, ex, rollbackErrors);
            throw new IOException(message, ex);
        }
    }

    private static List<Exception> RollBackCsvWrites(
        IEnumerable<CsvWritePlan> changedFiles,
        string? queuePath,
        bool queueWritten)
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

        if (queueWritten && !string.IsNullOrWhiteSpace(queuePath))
        {
            try
            {
                if (File.Exists(queuePath))
                {
                    File.Delete(queuePath);
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
        IEnumerable<CsvWritePlan> changedFiles,
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
            "Tag Finder CSV save failed. Changes were written transactionally and rollback was attempted.",
            $"Original error: {saveException.Message}",
            attemptedFiles.Count == 0
                ? "CSV files planned for update: none"
                : "CSV files planned for update:" + Environment.NewLine + string.Join(Environment.NewLine, attemptedFiles.Select(path => $"- {path}")),
            writtenFiles.Count == 0
                ? "CSV files written before failure: none"
                : "CSV files written before failure:" + Environment.NewLine + string.Join(Environment.NewLine, writtenFiles.Select(path => $"- {path}")),
            backupFiles.Count == 0
                ? "Backup files created before failure: none"
                : "Backup files created before failure:" + Environment.NewLine + string.Join(Environment.NewLine, backupFiles.Select(path => $"- {path}"))
        };

        if (rollbackErrors.Count == 0)
        {
            lines.Add("Rollback completed. The in-memory CSV data was not changed.");
        }
        else
        {
            lines.Add("Rollback had errors. Please restore from the listed backup files if needed.");
            lines.AddRange(rollbackErrors.Select(error => $"Rollback error: {error.Message}"));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static void ApplyCsvFileState(IEnumerable<CsvWritePlan> changedFiles)
    {
        foreach (var plan in changedFiles)
        {
            plan.Original.HeaderRow = plan.Work.HeaderRow?.ToArray();
            plan.Original.Rows = plan.Work.Rows.Select(row => row.ToArray()).ToList();
            plan.Original.RowCount = plan.Work.RowCount;
            plan.Original.ColumnCount = plan.Work.ColumnCount;
            plan.Original.Status = plan.Work.Status;
            plan.Original.Note = plan.Work.Note;
        }
    }

    private static void EnsureHeaderRowSupported(bool hasHeaderRow)
    {
        if (!hasHeaderRow)
        {
            throw new InvalidOperationException("Tag Finder requires CSV files with a header row.");
        }
    }

    private static int EnsureFlagColumn(CsvFileItem file, string flagColumnName, bool hasHeaderRow)
    {
        var index = EnsureTextColumn(file, flagColumnName, hasHeaderRow);
        for (var i = 0; i < file.Rows.Count; i++)
        {
            if (CsvFileUtility.IsBlankRow(file.Rows[i])) continue;

            if (string.IsNullOrWhiteSpace(file.Rows[i][index]))
            {
                file.Rows[i][index] = "false";
            }
        }

        return index;
    }

    private static int EnsureTextColumn(CsvFileItem file, string columnName, bool hasHeaderRow)
    {
        var index = CsvHeaderResolver.FindIndex(file, columnName);
        if (index >= 0)
        {
            for (var i = 0; i < file.Rows.Count; i++)
            {
                if (CsvFileUtility.IsBlankRow(file.Rows[i])) continue;

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
            if (CsvFileUtility.IsBlankRow(file.Rows[i])) continue;

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
            var orderIndex = CsvHeaderResolver.FindIndex(file, orderColumnName);
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

    private static string NextQueueCsvPath(string sourceFolder)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(sourceFolder, $"print_queue_{timestamp}.csv");
        if (!File.Exists(path))
        {
            return path;
        }

        var suffix = 2;
        while (true)
        {
            path = Path.Combine(sourceFolder, $"print_queue_{timestamp}-{suffix}.csv");
            if (!File.Exists(path))
            {
                return path;
            }

            suffix++;
        }
    }

    private static string[] EnsureRowWidth(string[] row, int width)
    {
        if (row.Length >= width) return row;
        Array.Resize(ref row, width);
        return row;
    }

    private static string[] AlignRowToHeader(CsvFileItem sourceFile, string[] sourceRow, string[] targetHeader)
    {
        var result = new string[targetHeader.Length];
        var sourceHeader = sourceFile.HeaderRow ?? [];

        for (var i = 0; i < targetHeader.Length; i++)
        {
            var sourceIndex = CsvHeaderResolver.FindIndex(sourceHeader, targetHeader[i]);
            result[i] = sourceIndex >= 0 && sourceIndex < sourceRow.Length
                ? sourceRow[sourceIndex]
                : string.Empty;
        }

        return result;
    }

}
