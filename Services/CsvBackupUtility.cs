namespace TagPrintStudio.Services;

internal static class CsvBackupUtility
{
    public static string CreateTimestampedBackup(string csvPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{csvPath}.bak-{timestamp}";
        var suffix = 2;
        while (File.Exists(backupPath))
        {
            backupPath = $"{csvPath}.bak-{timestamp}-{suffix}";
            suffix++;
        }

        File.Copy(csvPath, backupPath, overwrite: false);
        return backupPath;
    }
}
