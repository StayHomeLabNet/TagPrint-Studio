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

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddDirectoryToArchive(archive, paths.Layouts, "layouts");
        AddDirectoryToArchive(archive, paths.Presets, "presets");
        AddDirectoryToArchive(archive, paths.Recent, "recent");
        AddDirectoryToArchive(archive, paths.Settings, "settings");
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
                var backupDir = Path.Combine(backupRoot, root);
                if (Directory.Exists(targetDir))
                {
                    Directory.Move(targetDir, backupDir);
                    movedTargets.Add((targetDir, backupDir));
                }

                if (Directory.Exists(sourceDir))
                {
                    Directory.Move(sourceDir, targetDir);
                }
                else
                {
                    Directory.CreateDirectory(targetDir);
                }
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
        var extractedFileCount = 0;
        var validatedEntryCount = 0;
        long totalUncompressedBytes = 0;

        using var archive = ZipFile.OpenRead(zipPath);
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
            if (!allowedRootSet.Contains(root) || string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            if (normalized.Length > MaxBackupEntryNameLength)
            {
                throw new InvalidOperationException("The ZIP contains a path that is too long.");
            }

            validatedEntryCount++;
            if (validatedEntryCount > MaxBackupEntryCount)
            {
                throw new InvalidOperationException("The ZIP contains too many files.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(extractRoot, root, relativePath));
            var allowedRoot = Path.GetFullPath(Path.Combine(extractRoot, root)) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The ZIP contains an invalid path.");
            }

            if (IsDirectoryEntry(entry))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            if (entry.Length > MaxBackupSingleFileBytes)
            {
                throw new InvalidOperationException("The ZIP contains a file that is too large.");
            }

            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > MaxBackupTotalUncompressedBytes)
            {
                throw new InvalidOperationException("The ZIP is too large to import.");
            }

            extractedFileCount++;

            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        if (extractedFileCount == 0 || !allowedRootSet.Any(root => Directory.Exists(Path.Combine(extractRoot, root))))
        {
            throw new InvalidOperationException("The ZIP does not contain layouts, presets, recent, or settings folders.");
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
                Directory.Move(backup, target);
            }
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
        if (!Directory.Exists(sourceDirectory)) return;

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath).Replace('\\', '/');
            archive.CreateEntryFromFile(filePath, $"{entryRoot}/{relativePath}", CompressionLevel.Optimal);
        }
    }

}
