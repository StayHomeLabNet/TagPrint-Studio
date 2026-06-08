using System.Text;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void BackupCleanup_Preview_MarksOldBackupsAndExcessPerCsv()
    {
        using var temp = new TempDirectory();
        var oldPath = temp.WriteText("products.csv.bak-20260101-000000", "old", Encoding.UTF8);
        var newestPath = temp.WriteText("products.csv.bak-20260601-000002", "newest", Encoding.UTF8);
        var excessPath = temp.WriteText("products.csv.bak-20260601-000001", "excess", Encoding.UTF8);
        var otherPath = temp.WriteText("prices.csv.bak-20260601-000000", "other", Encoding.UTF8);

        File.SetLastWriteTime(oldPath, DateTime.Now.AddDays(-40));
        File.SetLastWriteTime(newestPath, DateTime.Now.AddDays(-1));
        File.SetLastWriteTime(excessPath, DateTime.Now.AddDays(-2));
        File.SetLastWriteTime(otherPath, DateTime.Now.AddDays(-3));

        var preview = new BackupCleanupService().Preview(temp.DirectoryPath, retentionDays: 30, maxCountPerCsv: 1);

        Assert.Equal(4, preview.TotalCount);
        Assert.Equal(2, preview.DeleteCount);
        Assert.Contains(preview.Files, file => file.FilePath == oldPath && file.DeleteCandidate);
        Assert.Contains(preview.Files, file => file.FilePath == excessPath && file.DeleteCandidate);
        Assert.Contains(preview.Files, file => file.FilePath == newestPath && !file.DeleteCandidate);
        Assert.Contains(preview.Files, file => file.FilePath == otherPath && !file.DeleteCandidate);
    }
}
