using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void CsvMerge_Preview_UsesParentAndSkipsHeaderMismatch()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        _ = temp.WriteText("child1.csv", "Code,Price\r\nB200,80\r\nC300,90\r\n", Encoding.UTF8);
        _ = temp.WriteText("child2.csv", "Code,Name\r\nD400,Other\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));

        var preview = new CsvMergeService().Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true,
            DeleteMergedSourceFiles = true
        }, preset);

        Assert.Equal("parent.csv", preview.ParentFileName);
        Assert.True(preview.DeleteMergedSourceFiles);
        Assert.Equal(1, preview.AppendFileCount);
        Assert.Equal(2, preview.AppendRowCount);
        Assert.Equal(1, preview.SkipFileCount);
        Assert.Equal(3, preview.ResultRowCount);
        Assert.Contains(preview.Items, item => item.IsParent && item.FileName == "parent.csv");
        Assert.Contains(preview.Items, item => !item.IsParent && item.FileName == "child1.csv" && item.HeaderMatches);
        Assert.Contains(preview.Items, item => !item.IsParent && item.FileName == "child2.csv" && !item.HeaderMatches);
    }

    [Fact]
    public void CsvMerge_Preview_AllowsParentEvenWhenEarlierFileHasDifferentColumnCount()
    {
        using var temp = new TempDirectory();
        _ = temp.WriteText("a-child-extra.csv", "Code,Price,Tag\r\nX100,50,true\r\n", Encoding.UTF8);
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        _ = temp.WriteText("z-child-match.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));

        var preview = new CsvMergeService().Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true
        }, preset);

        Assert.Equal("parent.csv", preview.ParentFileName);
        Assert.Equal(1, preview.AppendFileCount);
        Assert.Equal(1, preview.AppendRowCount);
        Assert.Equal(1, preview.SkipFileCount);
        Assert.Contains(preview.Items, item => item.IsParent && item.FileName == "parent.csv");
        Assert.Contains(preview.Items, item => !item.IsParent && item.FileName == "a-child-extra.csv" && !item.HeaderMatches);
        Assert.Contains(preview.Items, item => !item.IsParent && item.FileName == "z-child-match.csv" && item.HeaderMatches);
    }

    [Fact]
    public void CsvMerge_Apply_AppendsRowsToParentAndCreatesBackup()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        _ = temp.WriteText("child1.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        _ = temp.WriteText("child2.csv", "Code,Price\r\nC300,90\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));
        var service = new CsvMergeService();

        var preview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true
        }, preset);

        var result = service.Apply(preview, preset);
        var merged = File.ReadAllText(parentPath, Encoding.UTF8);

        Assert.Equal(parentPath, result.ParentCsvPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.StartsWith(parentPath + ".bak-", result.BackupPath, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "parent.csv.bak-*"));
        Assert.Equal(2, result.AppendedFileCount);
        Assert.Equal(2, result.AppendedRowCount);
        Assert.Contains("A100,120", merged);
        Assert.Contains("B200,80", merged);
        Assert.Contains("C300,90", merged);
    }

    [Fact]
    public void CsvMerge_Apply_CanMoveMergedSourceFilesOutOfFolder()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        var child1Path = temp.WriteText("child1.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        var child2Path = temp.WriteText("child2.csv", "Code,Price\r\nC300,90\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));
        var service = new CsvMergeService();

        var preview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true,
            DeleteMergedSourceFiles = true
        }, preset);

        var result = service.Apply(preview, preset);

        Assert.Equal(2, result.DeletedSourceFileCount);
        Assert.False(File.Exists(child1Path));
        Assert.False(File.Exists(child2Path));
        Assert.True(File.Exists(parentPath));
    }

    [Fact]
    public void CsvMerge_Apply_CanMoveBakFileOutOfFolder()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        _ = temp.WriteText("child1.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));
        var service = new CsvMergeService();

        var preview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true,
            DeleteBakFileAfterMerge = true
        }, preset);

        var result = service.Apply(preview, preset);

        Assert.Equal(1, result.DeletedBakFileCount);
        Assert.False(File.Exists(result.BackupPath));
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "parent.csv.bak-*"));
        Assert.True(File.Exists(parentPath));
    }

    [Fact]
    public void CsvMerge_Apply_CreatesSeparateBackupsAcrossRuns()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        _ = temp.WriteText("child1.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));
        var service = new CsvMergeService();

        var firstPreview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true
        }, preset);
        var first = service.Apply(firstPreview, preset);

        var secondPreview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true
        }, preset);
        var second = service.Apply(secondPreview, preset);

        Assert.NotEqual(first.BackupPath, second.BackupPath);
        Assert.True(File.Exists(first.BackupPath));
        Assert.True(File.Exists(second.BackupPath));
        Assert.Equal(2, Directory.GetFiles(temp.DirectoryPath, "parent.csv.bak-*").Length);
    }
}
