using System.IO.Compression;

namespace TagPrintStudio.Services;

public interface ISettingsArchiveService
{
    void CreateBackup(string zipPath);
    void ImportBackup(string zipPath);
}

public sealed class SettingsArchiveService(AppPaths paths) : ISettingsArchiveService
{
    private const int MaxBackupEntryCount = 5000;
    private const long MaxBackupTotalUncompressedBytes = 100L * 1024 * 1024;
    private const long MaxBackupSingleFileBytes = 20L * 1024 * 1024;
    private const int MaxBackupEntryNameLength = 260;

    public void CreateBackup(string zipPath)
    {
        paths.EnsureDirectories();

        var fullZipPath = Path.GetFullPath(zipPath);
        var zipDirectory = Path.GetDirectoryName(fullZipPath);
        if (!string.IsNullOrWhiteSpace(zipDirectory))
        {
            Directory.CreateDirectory(zipDirectory);
        }

        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(zipDirectory) ? Environment.CurrentDirectory : zipDirectory,
            $"{Path.GetFileName(fullZipPath)}.tmp-{Guid.NewGuid():N}");

        try
        {
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                AddDirectoryToArchive(archive, paths.Layouts, "layouts");
                AddDirectoryToArchive(archive, paths.Presets, "presets");
                AddDirectoryToArchive(archive, paths.Recent, "recent");
                AddDirectoryToArchive(archive, paths.Settings, "settings");
            }

            File.Move(tempPath, fullZipPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    public void ImportBackup(string zipPath)
    {
        paths.EnsureDirectories();

        var targetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["layouts"] = paths.Layouts,
            ["presets"] = paths.Presets,
            ["recent"] = paths.Recent,
            ["settings"] = paths.Settings
        };

        var tempRoot = CreateTempImportDirectory();
        var extractRoot = Path.Combine(tempRoot, "extract");
        var backupRoot = Path.Combine(tempRoot, "backup");
        var movedTargets = new List<(string Target, string Backup)>();

        try
        {
            Directory.CreateDirectory(extractRoot);
            Directory.CreateDirectory(backupRoot);
            ExtractAndValidateBackup(zipPath, extractRoot, targetMap.Keys);

            foreach (var (root, targetDir) in targetMap)
            {
                var sourceDir = Path.Combine(extractRoot, root);
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                var backupDir = Path.Combine(backupRoot, root);
                if (Directory.Exists(targetDir))
                {
                    CopyDirectory(targetDir, backupDir);
                    movedTargets.Add((targetDir, backupDir));
                    DeleteDirectory(targetDir);
                }

                CopyDirectory(sourceDir, targetDir);
            }

            foreach (var targetDir in targetMap.Values)
            {
                Directory.CreateDirectory(targetDir);
            }
        }
        catch
        {
            RestoreMovedTargets(movedTargets);
            throw;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempImportDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "TagPrintStudioSettingsImport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void ExtractAndValidateBackup(string zipPath, string extractRoot, IEnumerable<string> allowedRoots)
    {
        var allowedRootSet = new HashSet<string>(allowedRoots, StringComparer.OrdinalIgnoreCase);
        var validatedEntryCount = 0;
        long totalUncompressedBytes = 0;

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxBackupEntryCount)
        {
            throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.tooManyEntries"));
        }

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            var slashIndex = normalized.IndexOf('/');
            var root = slashIndex >= 0 ? normalized[..slashIndex] : normalized;
            var relativePath = slashIndex >= 0 ? normalized[(slashIndex + 1)..] : "";
            if (!allowedRootSet.Contains(root))
            {
                continue;
            }

            if (normalized.Length > MaxBackupEntryNameLength)
            {
                throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.pathTooLong"));
            }

            validatedEntryCount++;
            if (validatedEntryCount > MaxBackupEntryCount)
            {
                throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.tooManyEntries"));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                if (IsDirectoryEntry(entry))
                {
                    Directory.CreateDirectory(Path.Combine(extractRoot, root));
                }

                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(extractRoot, root, relativePath));
            var allowedRoot = Path.GetFullPath(Path.Combine(extractRoot, root)) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.invalidPath"));
            }

            if (IsDirectoryEntry(entry))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            if (entry.Length > MaxBackupSingleFileBytes)
            {
                throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.fileTooLarge"));
            }

            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > MaxBackupTotalUncompressedBytes)
            {
                throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.zipTooLarge"));
            }

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        if (validatedEntryCount == 0 || !allowedRootSet.Any(root => Directory.Exists(Path.Combine(extractRoot, root))))
        {
            throw new InvalidOperationException(LocalizationService.T("settingsArchive.issue.noSettingsFolders"));
        }
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
        entry.FullName.EndsWith("\\", StringComparison.Ordinal);

    private static void RestoreMovedTargets(List<(string Target, string Backup)> movedTargets)
    {
        foreach (var (target, backup) in movedTargets.AsEnumerable().Reverse())
        {
            TryDeleteDirectory(target);
            if (Directory.Exists(backup))
            {
                try
                {
                    CopyDirectory(backup, target);
                }
                catch
                {
                    // Best-effort restore. Preserve the original import error.
                }
            }
        }
    }


    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup. The backup operation has either succeeded or the original backup was left untouched.
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup. The import operation has already succeeded or restored the original settings.
        }
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string sourceDirectory, string entryRoot)
    {
        archive.CreateEntry($"{entryRoot}/");

        if (!Directory.Exists(sourceDirectory)) return;

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath).Replace('\\', '/');
            archive.CreateEntryFromFile(filePath, $"{entryRoot}/{relativePath}", CompressionLevel.Optimal);
        }
    }

}
