using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void TagFinderCsvService_SaveFlags_AddsFlagColumnToAllCsvFiles()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteText("first.csv", "Code,Name\r\nA100,Apple\r\n", Encoding.UTF8);
        var secondPath = temp.WriteText("second.csv", "Code,Name\r\nB200,Banana\r\n", Encoding.UTF8);
        var files = new List<CsvFileItem>
        {
            CsvFile(firstPath, ["Code", "Name"], ["A100", "Apple"]),
            CsvFile(secondPath, ["Code", "Name"], ["B200", "Banana"])
        };

        new TagFinderCsvService().SaveFlags(
            files,
            [new TagFinderSaveSelection { File = files[0], RowIndex = 0, Checked = true }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], files[0].HeaderRow);
        Assert.Equal(["A100", "Apple", "true", "1"], files[0].Rows[0]);
        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], files[1].HeaderRow);
        Assert.Equal(["B200", "Banana", "false", ""], files[1].Rows[0]);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "second.csv.bak-*"));
        Assert.Contains("A100,Apple,true,1", File.ReadAllText(firstPath, Encoding.UTF8));
        Assert.Contains("B200,Banana,false,", File.ReadAllText(secondPath, Encoding.UTF8));
    }

    [Fact]
    public void TagFinderCsvService_SaveFlags_AssignsNextOrderOnlyForNewlyCheckedRows()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,tagPrint,tagPrintOrder\r\nA,true,1\r\nB,false,\r\nC,true,5\r\n", Encoding.UTF8);
        var file = CsvFile(path, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", "1"], ["B", "false", ""], ["C", "true", "5"]);

        new TagFinderCsvService().SaveFlags(
            [file],
            [
                new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true, WasChecked = true },
                new TagFinderSaveSelection { File = file, RowIndex = 1, Checked = true, WasChecked = false },
                new TagFinderSaveSelection { File = file, RowIndex = 2, Checked = false, WasChecked = true }
            ],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["A", "true", "1"], file.Rows[0]);
        Assert.Equal(["B", "true", "6"], file.Rows[1]);
        Assert.Equal(["C", "false", ""], file.Rows[2]);
    }

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_WritesAdditionalRowsOnly()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Name,tagPrint\r\nA100,Apple,true\r\n", Encoding.UTF8);
        var parent = CsvFile(parentPath, ["Code", "Name", "tagPrint"], ["A100", "Apple", "true"]);

        var result = new TagFinderCsvService().CreatePrintQueueCsv(
            [new TagFinderQueueSource { File = parent, RowIndex = 0 }],
            additionalQuantity: 2,
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal(["A100", "Apple", "true", "1"], row));
        Assert.True(File.Exists(result.FilePath));
        var lines = File.ReadAllLines(result.FilePath, Encoding.UTF8);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Code,Name,tagPrint,tagPrintOrder", lines[0]);
        Assert.Equal("A100,Apple,true,1", lines[1]);
        Assert.Equal("A100,Apple,true,1", lines[2]);
    }
}
