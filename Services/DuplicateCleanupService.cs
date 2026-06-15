using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IDuplicateCleanupService
{
    DuplicateCleanupResult Analyze(PresetFile preset, List<CsvFileItem> csvFiles, DuplicateCleanupOptions options);
    DuplicateApplyResult Apply(List<CsvFileItem> csvFiles, DuplicateCleanupResult result, bool hasHeaderRow);
}

public sealed class DuplicateCleanupService : IDuplicateCleanupService
{
    private sealed class DuplicateCandidateRow
    {
        public required CsvFileItem CsvFile { get; init; }
        public required int RowIndex { get; init; }
        public required int SourceOrder { get; init; }
        public required string KeyValue { get; init; }
        public required string CompareValue { get; init; }
    }

    public DuplicateCleanupResult Analyze(PresetFile preset, List<CsvFileItem> csvFiles, DuplicateCleanupOptions options)
    {
        var keyField = ResolveField(preset, options.KeyFieldId);
        var compareField = ResolveField(preset, options.CompareFieldId);
        var carryFields = ResolveCarryFields(preset, options, keyField.FieldId);

        var candidates = FlattenRows(csvFiles, keyField, compareField, options);
        var duplicateGroups = candidates
            .GroupBy(row => row.KeyValue, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        var items = new List<DuplicateResolutionItem>();
        var carryActions = new List<DuplicateCarryAction>();
        var groupIndex = 0;

        foreach (var group in duplicateGroups)
        {
            var orderedRows = OrderRowsByPriority(group.ToList(), options);
            var keep = orderedRows[0];
            var carrySources = options.Carry.Enabled ? orderedRows.Skip(1).ToList() : [];
            var carrySummary = BuildCarryActions(carryActions, carryFields, options, keep, carrySources);

            foreach (var row in orderedRows)
            {
                items.Add(new DuplicateResolutionItem
                {
                    GroupIndex = groupIndex,
                    IsFirstInGroup = ReferenceEquals(row, orderedRows[0]),
                    KeyValue = row.KeyValue,
                    FilePath = row.CsvFile.FilePath,
                    FileName = row.CsvFile.FileName,
                    RowIndex = row.RowIndex,
                    CompareValue = row.CompareValue,
                    Keep = ReferenceEquals(row, keep),
                    CarrySummary = ReferenceEquals(row, keep) ? carrySummary : "",
                    Reason = BuildReason(row, keep, options)
                });
            }

            groupIndex++;
        }

        return new DuplicateCleanupResult
        {
            Items = items
                .OrderBy(item => item.GroupIndex)
                .ThenByDescending(item => item.Keep)
                .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RowIndex)
                .ToList(),
            CarryActions = carryActions,
            FileSnapshots = csvFiles
                .Where(file => file.Status == CsvFileStatus.Ok && !CsvFileUtility.IsPrintQueueCsv(file.FilePath))
                .Select(file => CsvFileUtility.CreateSnapshot(file.FilePath))
                .ToList(),
            GroupCount = duplicateGroups.Count,
            DeleteCount = items.Count(item => !item.Keep),
            CarryCellCount = carryActions.Count,
            TargetFileCount = items.Where(item => !item.Keep).Select(item => item.FilePath).Concat(carryActions.Select(action => action.KeepFilePath)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        };
    }

    public DuplicateApplyResult Apply(List<CsvFileItem> csvFiles, DuplicateCleanupResult result, bool hasHeaderRow)
    {
        var deletesByFile = result.Items
            .Where(item => !item.Keep)
            .GroupBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RowIndex).Distinct().OrderByDescending(index => index).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var carryByFile = result.CarryActions
            .GroupBy(action => action.KeepFilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(action => action.KeepRowIndex).ThenBy(action => action.ColumnIndex).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var targetPaths = deletesByFile.Keys
            .Concat(carryByFile.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        EnsureAnalyzedFilesUnchanged(result, targetPaths);

        var writePlans = csvFiles
            .Where(file => targetPaths.Contains(file.FilePath))
            .Select(file => new DuplicateCsvWritePlan(file, CloneCsvFile(file)))
            .ToList();

        var deletedRowCount = 0;

        foreach (var plan in writePlans)
        {
            var work = plan.Work;

            if (carryByFile.TryGetValue(work.FilePath, out var carryActions))
            {
                foreach (var action in carryActions)
                {
                    if (action.KeepRowIndex < 0 || action.KeepRowIndex >= work.Rows.Count)
                    {
                        continue;
                    }

                    var row = work.Rows[action.KeepRowIndex];
                    if (action.ColumnIndex < 0 || action.ColumnIndex >= row.Length)
                    {
                        continue;
                    }

                    row[action.ColumnIndex] = action.Value;
                }
            }

            if (deletesByFile.TryGetValue(work.FilePath, out var deleteIndexes))
            {
                foreach (var rowIndex in deleteIndexes)
                {
                    if (rowIndex < 0 || rowIndex >= work.Rows.Count)
                    {
                        continue;
                    }

                    work.Rows.RemoveAt(rowIndex);
                    deletedRowCount++;
                }
            }

            work.RowCount = work.Rows.Count;
            work.ColumnCount = Math.Max(work.HeaderRow?.Length ?? 0, work.Rows.Count == 0 ? 0 : work.Rows.Max(row => row.Length));
        }

        WriteChangedCsvFilesTransactionally(writePlans, hasHeaderRow);
        ApplyCsvFileState(writePlans);

        var updatedFiles = writePlans.Select(plan => plan.Original.FilePath).ToList();

        return new DuplicateApplyResult
        {
            UpdatedFileCount = updatedFiles.Count,
            DeletedRowCount = deletedRowCount,
            UpdatedFiles = updatedFiles
        };
    }

    private sealed class DuplicateCsvWritePlan(CsvFileItem original, CsvFileItem work)
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

    private static void WriteChangedCsvFilesTransactionally(List<DuplicateCsvWritePlan> changedFiles, bool hasHeaderRow)
    {
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
        }
        catch (Exception ex)
        {
            var rollbackErrors = RollBackCsvWrites(changedFiles);
            var message = BuildTransactionalSaveFailureMessage(changedFiles, ex, rollbackErrors);
            throw new IOException(message, ex);
        }
    }

    private static List<Exception> RollBackCsvWrites(IEnumerable<DuplicateCsvWritePlan> changedFiles)
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
        IEnumerable<DuplicateCsvWritePlan> changedFiles,
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
            "Duplicate cleanup CSV save failed. Changes were written transactionally and rollback was attempted.",
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

    private static void ApplyCsvFileState(IEnumerable<DuplicateCsvWritePlan> changedFiles)
    {
        foreach (var plan in changedFiles)
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

    private static void EnsureAnalyzedFilesUnchanged(DuplicateCleanupResult result, IEnumerable<string> targetPaths)
    {
        if (result.FileSnapshots.Count == 0)
        {
            return;
        }

        var snapshots = result.FileSnapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.FilePath))
            .ToDictionary(snapshot => snapshot.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (!snapshots.TryGetValue(targetPath, out var snapshot) || !CsvFileUtility.SnapshotMatches(snapshot, targetPath))
            {
                throw new InvalidOperationException(LocalizationService.T("duplicateCleanup.issue.fileChangedAfterAnalysis", Path.GetFileName(targetPath)));
            }
        }
    }

    private static FieldDefinition ResolveField(PresetFile preset, string fieldId)
    {
        var field = preset.Fields.FirstOrDefault(f => string.Equals(f.FieldId, fieldId, StringComparison.Ordinal));
        if (field is null)
        {
            throw new InvalidOperationException($"Field not found: {fieldId}");
        }

        if (field.ColumnIndex is null || field.ColumnIndex < 0)
        {
            throw new InvalidOperationException($"Field is not mapped to a CSV column: {fieldId}");
        }

        return field;
    }

    private static List<FieldDefinition> ResolveCarryFields(PresetFile preset, DuplicateCleanupOptions options, string keyFieldId)
    {
        if (!options.Carry.Enabled || options.Carry.FieldIds.Count == 0)
        {
            return [];
        }

        var requestedFieldIds = options.Carry.FieldIds
            .Where(fieldId => !string.Equals(fieldId, keyFieldId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        return preset.Fields
            .Where(field => requestedFieldIds.Contains(field.FieldId) && field.ColumnIndex is >= 0)
            .ToList();
    }

    private static List<DuplicateCandidateRow> FlattenRows(List<CsvFileItem> csvFiles, FieldDefinition keyField, FieldDefinition compareField, DuplicateCleanupOptions options)
    {
        var rows = new List<DuplicateCandidateRow>();
        var sourceOrder = 0;

        foreach (var file in csvFiles.Where(file => file.Status == CsvFileStatus.Ok && !CsvFileUtility.IsPrintQueueCsv(file.FilePath)))
        {
            for (var rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
            {
                var row = file.Rows[rowIndex];
                if (CsvFileUtility.IsBlankRow(row))
                {
                    continue;
                }

                var keyValue = ValueAt(row, keyField.ColumnIndex).Trim();
                if (options.IgnoreEmptyKeys && string.IsNullOrWhiteSpace(keyValue))
                {
                    continue;
                }

                rows.Add(new DuplicateCandidateRow
                {
                    CsvFile = file,
                    RowIndex = rowIndex,
                    SourceOrder = sourceOrder++,
                    KeyValue = keyValue,
                    CompareValue = ValueAt(row, compareField.ColumnIndex).Trim()
                });
            }
        }

        return rows;
    }

    private static List<DuplicateCandidateRow> OrderRowsByPriority(List<DuplicateCandidateRow> rows, DuplicateCleanupOptions options)
    {
        return options.Rule switch
        {
            DuplicateResolutionRule.KeepLargest => OrderByOrder(rows, options.ValueType, descending: true),
            DuplicateResolutionRule.KeepSmallest => OrderByOrder(rows, options.ValueType, descending: false),
            DuplicateResolutionRule.KeepNonEmpty => rows
                .OrderByDescending(row => !string.IsNullOrWhiteSpace(row.CompareValue))
                .ThenBy(row => row.SourceOrder)
                .ToList(),
            DuplicateResolutionRule.KeepLast => rows.OrderByDescending(row => row.SourceOrder).ToList(),
            _ => rows.OrderBy(row => row.SourceOrder).ToList()
        };
    }

    private static List<DuplicateCandidateRow> OrderByOrder(List<DuplicateCandidateRow> rows, DuplicateValueType valueType, bool descending)
    {
        return valueType switch
        {
            DuplicateValueType.Number => OrderByNumber(rows, descending),
            DuplicateValueType.Date => OrderByDate(rows, descending),
            _ => rows.OrderBy(row => row.SourceOrder).ToList()
        };
    }

    private static List<DuplicateCandidateRow> OrderByNumber(List<DuplicateCandidateRow> rows, bool descending)
    {
        var valid = rows
            .Select(row => new
            {
                Row = row,
                Ok = TryParseDecimal(row.CompareValue, out var value),
                Value = ParseDecimal(row.CompareValue)
            })
            .ToList();

        var ordered = descending
            ? valid.OrderByDescending(item => item.Ok).ThenByDescending(item => item.Value).ThenBy(item => item.Row.SourceOrder)
            : valid.OrderByDescending(item => item.Ok).ThenBy(item => item.Value).ThenBy(item => item.Row.SourceOrder);
        return ordered.Select(item => item.Row).ToList();
    }

    private static List<DuplicateCandidateRow> OrderByDate(List<DuplicateCandidateRow> rows, bool descending)
    {
        var valid = rows
            .Select(row => new
            {
                Row = row,
                Ok = TryParseDate(row.CompareValue, out var value),
                Value = ParseDate(row.CompareValue)
            })
            .ToList();

        var ordered = descending
            ? valid.OrderByDescending(item => item.Ok).ThenByDescending(item => item.Value).ThenBy(item => item.Row.SourceOrder)
            : valid.OrderByDescending(item => item.Ok).ThenBy(item => item.Value).ThenBy(item => item.Row.SourceOrder);
        return ordered.Select(item => item.Row).ToList();
    }

    private static string BuildCarryActions(
        List<DuplicateCarryAction> carryActions,
        List<FieldDefinition> carryFields,
        DuplicateCleanupOptions options,
        DuplicateCandidateRow keep,
        List<DuplicateCandidateRow> carrySources)
    {
        if (!options.Carry.Enabled || carrySources.Count == 0 || carryFields.Count == 0)
        {
            return "";
        }

        var carriedNames = new List<string>();
        foreach (var carryField in carryFields)
        {
            var columnIndex = carryField.ColumnIndex ?? -1;
            if (columnIndex < 0)
            {
                continue;
            }

            var keepValue = ValueAt(keep.CsvFile.Rows[keep.RowIndex], columnIndex);
            var carrySource = FindCarrySource(carrySources, columnIndex, keepValue, options.Carry.CopyMode);
            if (carrySource is null)
            {
                continue;
            }

            var sourceValue = ValueAt(carrySource.CsvFile.Rows[carrySource.RowIndex], columnIndex);
            carryActions.Add(new DuplicateCarryAction
            {
                KeepFilePath = keep.CsvFile.FilePath,
                KeepRowIndex = keep.RowIndex,
                SourceFilePath = carrySource.CsvFile.FilePath,
                SourceRowIndex = carrySource.RowIndex,
                FieldId = carryField.FieldId,
                ColumnIndex = columnIndex,
                Value = sourceValue
            });

            carriedNames.Add(string.IsNullOrWhiteSpace(carryField.DisplayName) ? carryField.FieldId : carryField.DisplayName);
        }

        return carriedNames.Count == 0 ? "" : string.Join(", ", carriedNames.Distinct(StringComparer.Ordinal));
    }

    private static DuplicateCandidateRow? FindCarrySource(
        List<DuplicateCandidateRow> carrySources,
        int columnIndex,
        string keepValue,
        DuplicateCopyMode copyMode)
    {
        if (copyMode == DuplicateCopyMode.FillIfEmpty && !string.IsNullOrWhiteSpace(keepValue))
        {
            return null;
        }

        foreach (var source in carrySources)
        {
            var sourceValue = ValueAt(source.CsvFile.Rows[source.RowIndex], columnIndex);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                continue;
            }

            if (copyMode == DuplicateCopyMode.Overwrite && string.Equals(keepValue, sourceValue, StringComparison.Ordinal))
            {
                continue;
            }

            return source;
        }

        return null;
    }

    private static string BuildReason(DuplicateCandidateRow row, DuplicateCandidateRow keep, DuplicateCleanupOptions options)
    {
        var isKeep = ReferenceEquals(row, keep);
        if (options.Rule == DuplicateResolutionRule.KeepLargest)
        {
            return BuildKeepLargestReason(isKeep, options.ValueType);
        }

        if (options.Rule == DuplicateResolutionRule.KeepSmallest)
        {
            return BuildKeepSmallestReason(isKeep, options.ValueType);
        }
        return options.Rule switch
        {
            DuplicateResolutionRule.KeepLargest => isKeep ? T("残す: 最大値", "Keep: largest value") : T("削除: より小さい値", "Delete: smaller value"),
            DuplicateResolutionRule.KeepSmallest => isKeep ? T("残す: 最小値", "Keep: smallest value") : T("削除: より大きい値", "Delete: larger value"),
            DuplicateResolutionRule.KeepNonEmpty => isKeep ? T("残す: 空でない値", "Keep: non-empty value") : T("削除: 優先度が低い", "Delete: lower priority"),
            DuplicateResolutionRule.KeepLast => isKeep ? T("残す: 後勝ち", "Keep: last row wins") : T("削除: 先に見つかった重複", "Delete: earlier duplicate"),
            _ => isKeep ? T("残す: 先勝ち", "Keep: first row wins") : T("削除: 後から見つかった重複", "Delete: later duplicate")
        };
    }

    private static string BuildKeepLargestReason(bool isKeep, DuplicateValueType valueType)
    {
        return valueType switch
        {
            DuplicateValueType.Date => isKeep ? T("残す: 新しい日付", "Keep: newer date") : T("削除: 古い日付", "Delete: older date"),
            _ => isKeep ? T("残す: 最大値", "Keep: largest value") : T("削除: より小さい値", "Delete: smaller value")
        };
    }

    private static string BuildKeepSmallestReason(bool isKeep, DuplicateValueType valueType)
    {
        return valueType switch
        {
            DuplicateValueType.Date => isKeep ? T("残す: 古い日付", "Keep: older date") : T("削除: 新しい日付", "Delete: newer date"),
            _ => isKeep ? T("残す: 最小値", "Keep: smallest value") : T("削除: より大きい値", "Delete: larger value")
        };
    }

    private static string T(string japanese, string english) =>
        LocalizationService.CurrentLanguage == AppLanguage.Japanese ? japanese : english;

    private static string ValueAt(string[] row, int? columnIndex)
    {
        if (columnIndex is not int index || index < 0 || index >= row.Length)
        {
            return "";
        }

        return row[index] ?? "";
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number | NumberStyles.Currency, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number | NumberStyles.Currency, CultureInfo.CurrentCulture, out value);
    }

    private static decimal ParseDecimal(string text) => TryParseDecimal(text, out var value) ? value : 0m;

    private static bool TryParseDate(string text, out DateTime value)
    {
        return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out value)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static DateTime ParseDate(string text) => TryParseDate(text, out var value) ? value : DateTime.MinValue;
}
