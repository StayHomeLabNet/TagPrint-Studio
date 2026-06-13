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
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.folderNotFound"));
        }

        if (string.IsNullOrWhiteSpace(options.ParentCsvPath))
        {
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.parentRequired"));
        }

        var csvPaths = Directory.GetFiles(options.FolderPath, "*.csv")
            .Where(path => !CsvFileUtility.IsPrintQueueCsv(path))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (csvPaths.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.noCsvFiles"));
        }

        if (!csvPaths.Contains(options.ParentCsvPath, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.parentOutsideFolder"));
        }

        var loadResult = csvLoader.LoadFiles(csvPaths, preset, options.HasHeaderRow);
        var files = loadResult.Files
            .OrderBy(file => file.FilePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var parent = files.FirstOrDefault(file => string.Equals(file.FilePath, options.ParentCsvPath, StringComparison.OrdinalIgnoreCase));
        if (parent is null || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.parentLoadFailed"));
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
                Note = MergeNote(file, isParent, parent),
                Snapshot = SnapshotFor(file.FilePath)
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
            FileSnapshots = items.Select(item => item.Snapshot).ToList(),
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
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.noAppendFiles"));
        }

        var targetPaths = new[] { preview.ParentCsvPath }.Concat(preview.AppendFilePaths).ToList();
        EnsurePreviewedFilesUnchanged(preview, targetPaths);
        var loadResult = csvLoader.LoadFiles(targetPaths, preset, preview.HasHeaderRow);
        var files = loadResult.Files.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        if (!files.TryGetValue(preview.ParentCsvPath, out var parent) || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.parentLoadFailed"));
        }

        foreach (var childPath in preview.AppendFilePaths)
        {
            if (!files.TryGetValue(childPath, out var child) || !CanUseForMerge(child))
            {
                throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.childLoadFailed", Path.GetFileName(childPath)));
            }

            if (!HeadersMatch(parent.HeaderRow, child.HeaderRow))
            {
                throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.headerMismatch", Path.GetFileName(childPath)));
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
                LocalizationService.T("csvMerge.issue.deleteMergedSourceFailedRestored"),
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

    private static void EnsurePreviewedFilesUnchanged(CsvMergePreview preview, IEnumerable<string> targetPaths)
    {
        var snapshots = preview.FileSnapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.FilePath))
            .ToDictionary(snapshot => snapshot.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var targetPath in targetPaths)
        {
            if (!snapshots.TryGetValue(targetPath, out var snapshot))
            {
                throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.previewExpired", Path.GetFileName(targetPath)));
            }

            if (!SnapshotMatches(snapshot, targetPath))
            {
                throw new InvalidOperationException(LocalizationService.T("csvMerge.issue.fileChangedAfterPreview", Path.GetFileName(targetPath)));
            }
        }
    }

    private static bool SnapshotMatches(CsvFileSnapshot snapshot, string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return !snapshot.Exists;
        }

        return snapshot.Exists
            && snapshot.Length == fileInfo.Length
            && snapshot.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc;
    }

    private static CsvFileSnapshot SnapshotFor(string path)
    {
        var fileInfo = new FileInfo(path);
        return new CsvFileSnapshot
        {
            FilePath = path,
            Exists = fileInfo.Exists,
            Length = fileInfo.Exists ? fileInfo.Length : 0,
            LastWriteTimeUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue
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
            return LocalizationService.T("csvMerge.status.parent");
        }

        if (!CanUseForMerge(file))
        {
            return LocalizationService.T("csvMerge.status.loadError");
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.T("csvMerge.status.append"))
            : (LocalizationService.T("csvMerge.status.skip"));
    }

    private static string MergeNote(CsvFileItem file, bool isParent, CsvFileItem parent)
    {
        if (isParent)
        {
            return LocalizationService.T("csvMerge.note.parent");
        }

        if (!CanUseForMerge(file))
        {
            return string.IsNullOrWhiteSpace(file.Note)
                ? LocalizationService.T("csvMerge.note.loadFailed")
                : file.Note;
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.T("csvMerge.note.headersMatch"))
            : (LocalizationService.T("csvMerge.note.headersDoNotMatch"));
    }
}
