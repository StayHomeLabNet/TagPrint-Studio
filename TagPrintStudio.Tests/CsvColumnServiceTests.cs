using System.Text;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void CsvColumnService_AddColumnToAllFiles_AppendsHeaderAndRowsWithBackup()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteText("first.csv", "Code,Name\r\nA100,Apple\r\n", Encoding.UTF8);
        var secondPath = temp.WriteText("second.csv", "Code,Name\r\nB200,Banana\r\n", Encoding.UTF8);
        var files = new List<CsvFileItem>
        {
            CsvFile(firstPath, ["Code", "Name"], ["A100", "Apple"]),
            CsvFile(secondPath, ["Code", "Name"], ["B200", "Banana"])
        };

        var result = new CsvColumnService().AddColumnToAllFiles(files, "FontSize", hasHeaderRow: true);

        Assert.Equal("FontSize", result.ColumnName);
        Assert.Equal(2, result.ColumnIndex);
        Assert.Equal(2, result.UpdatedFileCount);
        Assert.Equal(["Code", "Name", "FontSize"], files[0].HeaderRow);
        Assert.Equal(["A100", "Apple", ""], files[0].Rows[0]);
        Assert.Equal(["Code", "Name", "FontSize"], files[1].HeaderRow);
        Assert.Equal(["B200", "Banana", ""], files[1].Rows[0]);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "second.csv.bak-*"));
        Assert.Contains("Code,Name,FontSize", File.ReadAllText(firstPath, Encoding.UTF8));
        Assert.Contains("A100,Apple,", File.ReadAllText(firstPath, Encoding.UTF8));
    }

    [Fact]
    public void CsvColumnService_AddColumnToAllFiles_RejectsDuplicateHeader()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,Name\r\nA100,Apple\r\n", Encoding.UTF8);
        var files = new List<CsvFileItem>
        {
            CsvFile(path, ["Code", "Name"], ["A100", "Apple"])
        };

        Assert.Throws<InvalidOperationException>(() =>
            new CsvColumnService().AddColumnToAllFiles(files, "name", hasHeaderRow: true));
    }
}
