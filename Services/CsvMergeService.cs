using Microsoft.VisualBasic.FileIO;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvMergeService
{
    CsvMergePreview Preview(CsvMergeOptions options, PresetFile preset);
    CsvMergeApplyResult Apply(CsvMergePreview preview, PresetFile preset);
}

public sealed class CsvMergeService : ICsvMergeService
{
    private readonly ICsvLoaderService csvLoader;
    private readonly Action<string> deleteFile;

    public CsvMergeService()
        : this(new CsvLoaderService(), DeleteFileToRecycleBin)
    {
    }

    internal CsvMergeService(ICsvLoaderService csvLoader, Action<string> deleteFile)
    {
        this.csvLoader = csvLoader;
        this.deleteFile = deleteFile;
    }

    public CsvMergePreview Preview(CsvMergeOptions options, PresetFile preset)
    {
        if (string.IsNullOrWhiteSpace(options.FolderPath) || !Directory.Exists(options.FolderPath))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "対象フォルダーが見つかりません。"
                : "The target folder could not be found.");
        }

        if (string.IsNullOrWhiteSpace(options.ParentCsvPath))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVを選択してください。"
                : "Please choose a parent CSV.");
        }

        var csvPaths = Directory.GetFiles(options.FolderPath, "*.csv")
            .Where(path => !CsvFileUtility.IsPrintQueueCsv(path))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (csvPaths.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "対象フォルダーにCSVがありません。"
                : "No CSV files were found in the target folder.");
        }

        if (!csvPaths.Contains(options.ParentCsvPath, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVが対象フォルダー内にありません。"
                : "The parent CSV is not inside the target folder.");
        }

        var loadResult = csvLoader.LoadFiles(csvPaths, preset, options.HasHeaderRow);
        var files = loadResult.Files
            .OrderBy(file => file.FilePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var parent = files.FirstOrDefault(file => string.Equals(file.FilePath, options.ParentCsvPath, StringComparison.OrdinalIgnoreCase));
        if (parent is null || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVの読み込みに失敗しました。"
                : "The parent CSV could not be loaded.");
        }

        var items = new List<CsvMergeCandidate>();
        var appendFilePaths = new List<string>();
        var appendRowCount = 0;
        var skipFileCount = 0;

        foreach (var file in files)
        {
            var isParent = string.Equals(file.FilePath, options.ParentCsvPath, StringComparison.OrdinalIgnoreCase);
            var candidate = new CsvMergeCandidate
            {
                FilePath = file.FilePath,
                FileName = file.FileName,
                IsParent = isParent,
                RowCount = file.RowCount,
                HeaderMatches = isParent || HeadersMatch(parent.HeaderRow, file.HeaderRow),
                Status = MergeStatus(file, isParent, parent),
                Note = MergeNote(file, isParent, parent)
            };

            if (!isParent && CanUseForMerge(file) && candidate.HeaderMatches)
            {
                appendFilePaths.Add(file.FilePath);
                appendRowCount += file.RowCount;
            }
            else if (!isParent)
            {
                skipFileCount++;
            }

            items.Add(candidate);
        }

        return new CsvMergePreview
        {
            FolderPath = options.FolderPath,
            ParentCsvPath = options.ParentCsvPath,
            ParentFileName = parent.FileName,
            HasHeaderRow = options.HasHeaderRow,
            DeleteMergedSourceFiles = options.DeleteMergedSourceFiles,
            DeleteBakFileAfterMerge = options.DeleteBakFileAfterMerge,
            Items = items,
            AppendFilePaths = appendFilePaths,
            AppendFileCount = appendFilePaths.Count,
            AppendRowCount = appendRowCount,
            SkipFileCount = skipFileCount,
            ResultRowCount = parent.RowCount + appendRowCount
        };
    }

    public CsvMergeApplyResult Apply(CsvMergePreview preview, PresetFile preset)
    {
        if (preview.AppendFilePaths.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "追記対象のCSVがありません。"
                : "There are no CSV files to append.");
        }

        var targetPaths = new[] { preview.ParentCsvPath }.Concat(preview.AppendFilePaths).ToList();
        var loadResult = csvLoader.LoadFiles(targetPaths, preset, preview.HasHeaderRow);
        var files = loadResult.Files.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        if (!files.TryGetValue(preview.ParentCsvPath, out var parent) || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVの読み込みに失敗しました。"
                : "The parent CSV could not be loaded.");
        }

        foreach (var childPath in preview.AppendFilePaths)
        {
            if (!files.TryGetValue(childPath, out var child) || !CanUseForMerge(child))
            {
                throw new InvalidOperationException(string.Format(
                    LocalizationService.CurrentLanguage == AppLanguage.Japanese
                        ? "子CSVの読み込みに失敗しました: {0}"
                        : "A child CSV could not be loaded: {0}",
                    Path.GetFileName(childPath)));
            }

            if (!HeadersMatch(parent.HeaderRow, child.HeaderRow))
            {
                throw new InvalidOperationException(string.Format(
                    LocalizationService.CurrentLanguage == AppLanguage.Japanese
                        ? "ヘッダーが一致しないためマージできません: {0}"
                        : "Cannot merge because headers do not match: {0}",
                    Path.GetFileName(childPath)));
            }
        }

        var backupPath = CsvBackupUtility.CreateTimestampedBackup(preview.ParentCsvPath);
        List<CsvMergeSourceBackup> sourceBackups = preview.DeleteMergedSourceFiles
            ? CreateSourceBackups(preview.AppendFilePaths)
            : [];

        try
        {
            foreach (var childPath in preview.AppendFilePaths)
            {
                parent.Rows.AddRange(files[childPath].Rows.Select(row => row.ToArray()));
            }

            CsvFileUtility.WriteCsvFile(parent, preview.HasHeaderRow, createBackup: false);
        }
        catch
        {
            RestoreFileFromBackup(backupPath, preview.ParentCsvPath);
            RestoreSourceFiles(sourceBackups);
            CleanupSourceBackups(sourceBackups);
            throw;
        }

        var deletedSourceFileCount = 0;
        try
        {
            if (preview.DeleteMergedSourceFiles)
            {
                foreach (var childPath in preview.AppendFilePaths)
                {
                    if (!File.Exists(childPath))
                    {
                        continue;
                    }

                    deleteFile(childPath);
                    deletedSourceFileCount++;
                }
            }
        }
        catch (Exception ex)
        {
            RestoreFileFromBackup(backupPath, preview.ParentCsvPath);
            RestoreSourceFiles(sourceBackups);
            CleanupSourceBackups(sourceBackups);

            throw new InvalidOperationException(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "親CSVへの追記後に、マージ済み子CSVの削除に失敗したため、親CSVをバックアップから復元しました。削除に失敗した子CSVを再マージすると重複する可能性があるため、ファイル状態を確認してください。"
                    : "After appending rows to the parent CSV, deleting a merged child CSV failed, so the parent CSV was restored from backup. Check the file state before merging again because retrying with an undeleted child CSV could create duplicate rows.",
                ex);
        }
        finally
        {
            CleanupSourceBackups(sourceBackups);
        }

        var deletedBakFileCount = 0;
        if (preview.DeleteBakFileAfterMerge && File.Exists(backupPath))
        {
            deleteFile(backupPath);
            deletedBakFileCount = 1;
        }

        return new CsvMergeApplyResult
        {
            ParentCsvPath = preview.ParentCsvPath,
            BackupPath = backupPath,
            AppendedFileCount = preview.AppendFileCount,
            AppendedRowCount = preview.AppendRowCount,
            DeletedSourceFileCount = deletedSourceFileCount,
            DeletedBakFileCount = deletedBakFileCount
        };
    }

    private static void DeleteFileToRecycleBin(string path)
    {
        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.DoNothing);
    }

    private static List<CsvMergeSourceBackup> CreateSourceBackups(IEnumerable<string> sourcePaths)
    {
        var backupRoot = Path.Combine(Path.GetTempPath(), "TagPrintStudio", "CsvMerge", Guid.NewGuid().ToString("N"));
        var backups = new List<CsvMergeSourceBackup>();

        try
        {
            Directory.CreateDirectory(backupRoot);

            foreach (var sourcePath in sourcePaths)
            {
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var backupPath = Path.Combine(backupRoot, backups.Count.ToString("D4") + "_" + Path.GetFileName(sourcePath));
                File.Copy(sourcePath, backupPath, overwrite: false);
                backups.Add(new CsvMergeSourceBackup(sourcePath, backupPath));
            }

            return backups;
        }
        catch
        {
            CleanupSourceBackups(backups);
            throw;
        }
    }

    private static void RestoreFileFromBackup(string backupPath, string destinationPath)
    {
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, destinationPath, overwrite: true);
        }
    }

    private static void RestoreSourceFiles(IEnumerable<CsvMergeSourceBackup> backups)
    {
        foreach (var backup in backups)
        {
            if (File.Exists(backup.BackupPath))
            {
                File.Copy(backup.BackupPath, backup.SourcePath, overwrite: true);
            }
        }
    }

    private static void CleanupSourceBackups(IEnumerable<CsvMergeSourceBackup> backups)
    {
        foreach (var backup in backups)
        {
            var directory = Path.GetDirectoryName(backup.BackupPath);

            try
            {
                if (File.Exists(backup.BackupPath))
                {
                    File.Delete(backup.BackupPath);
                }

                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Temporary transaction backups are best-effort cleanup only.
            }
        }
    }

    private sealed record CsvMergeSourceBackup(string SourcePath, string BackupPath);

    private static bool HeadersMatch(string[]? left, string[]? right)
    {
        if (left is null || right is null) return false;
        return left.Length == right.Length && left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool CanUseForMerge(CsvFileItem file)
    {
        if (file.Status == CsvFileStatus.LoadFailed || file.HeaderRow is not { Length: > 0 } header)
        {
            return false;
        }

        return file.Rows.All(row => CsvFileUtility.IsBlankRow(row) || row.Length == header.Length);
    }

    private static string MergeStatus(CsvFileItem file, bool isParent, CsvFileItem parent)
    {
        if (isParent)
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "親CSV" : "Parent";
        }

        if (!CanUseForMerge(file))
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "読込エラー" : "Load Error";
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "追記対象" : "Append")
            : (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "スキップ" : "Skip");
    }

    private static string MergeNote(CsvFileItem file, bool isParent, CsvFileItem parent)
    {
        if (isParent)
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "親CSV" : "Parent CSV";
        }

        if (!CanUseForMerge(file))
        {
            return string.IsNullOrWhiteSpace(file.Note)
                ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "読み込みに失敗しました" : "The file could not be loaded.")
                : file.Note;
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ヘッダー一致" : "Headers match")
            : (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ヘッダー不一致" : "Headers do not match");
    }
}
