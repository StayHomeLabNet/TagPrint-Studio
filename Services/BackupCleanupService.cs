using Microsoft.VisualBasic.FileIO;

namespace TagPrintStudio.Services;

public interface IBackupCleanupService
{
    BackupCleanupPreview Preview(string folderPath, int retentionDays, int maxCountPerCsv);
    BackupCleanupResult Cleanup(BackupCleanupPreview preview);
}

public sealed class BackupCleanupService : IBackupCleanupService
{
    public BackupCleanupPreview Preview(string folderPath, int retentionDays, int maxCountPerCsv)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        var cutoff = retentionDays > 0 ? DateTime.Now.AddDays(-retentionDays) : DateTime.MinValue;
        var files = Directory.GetFiles(folderPath, "*.csv.bak-*", System.IO.SearchOption.TopDirectoryOnly)
            .Select(path => new BackupFileCandidate
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                SourceCsvName = SourceCsvName(path),
                LastWriteTime = File.GetLastWriteTime(path),
                SizeBytes = new FileInfo(path).Length
            })
            .OrderByDescending(file => file.LastWriteTime)
            .ToList();

        var deletePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (retentionDays > 0)
        {
            foreach (var file in files.Where(file => file.LastWriteTime < cutoff))
            {
                deletePaths.Add(file.FilePath);
            }
        }

        if (maxCountPerCsv > 0)
        {
            foreach (var group in files.GroupBy(file => file.SourceCsvName, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var file in group.OrderByDescending(file => file.LastWriteTime).Skip(maxCountPerCsv))
                {
                    deletePaths.Add(file.FilePath);
                }
            }
        }

        foreach (var file in files)
        {
            file.DeleteCandidate = deletePaths.Contains(file.FilePath);
        }

        return new BackupCleanupPreview
        {
            FolderPath = folderPath,
            RetentionDays = retentionDays,
            MaxCountPerCsv = maxCountPerCsv,
            Files = files
        };
    }

    public BackupCleanupResult Cleanup(BackupCleanupPreview preview)
    {
        var deleted = 0;
        foreach (var file in preview.Files.Where(file => file.DeleteCandidate))
        {
            if (!File.Exists(file.FilePath))
            {
                continue;
            }

            FileSystem.DeleteFile(
                file.FilePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.DoNothing);
            deleted++;
        }

        return new BackupCleanupResult
        {
            DeletedCount = deleted
        };
    }

    private static string SourceCsvName(string backupPath)
    {
        var fileName = Path.GetFileName(backupPath);
        var marker = ".bak-";
        var index = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index > 0 ? fileName[..index] : fileName;
    }
}

public sealed class BackupCleanupPreview
{
    public string FolderPath { get; set; } = "";
    public int RetentionDays { get; set; }
    public int MaxCountPerCsv { get; set; }
    public List<BackupFileCandidate> Files { get; set; } = [];
    public int TotalCount => Files.Count;
    public int DeleteCount => Files.Count(file => file.DeleteCandidate);
    public long DeleteBytes => Files.Where(file => file.DeleteCandidate).Sum(file => file.SizeBytes);
}

public sealed class BackupFileCandidate
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourceCsvName { get; set; } = "";
    public DateTime LastWriteTime { get; set; }
    public long SizeBytes { get; set; }
    public bool DeleteCandidate { get; set; }
}

public sealed class BackupCleanupResult
{
    public int DeletedCount { get; set; }
}
