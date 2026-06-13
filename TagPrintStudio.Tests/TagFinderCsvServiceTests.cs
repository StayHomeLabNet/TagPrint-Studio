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

        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], files[0].HeaderRow!);
        Assert.Equal(["A100", "Apple", "true", "1"], files[0].Rows[0]);
        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], files[1].HeaderRow!);
        Assert.Equal(["B200", "Banana", "false", ""], files[1].Rows[0]);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "second.csv.bak-*"));
        Assert.Contains("A100,Apple,true,1", File.ReadAllText(firstPath, Encoding.UTF8));
        Assert.Contains("B200,Banana,false,", File.ReadAllText(secondPath, Encoding.UTF8));
    }


    [Fact]
    public void TagFinderCsvService_SaveFlags_DoesNotWriteUnchangedFiles()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteText("first.csv", "Code,tagPrint,tagPrintOrder\r\nA,false,\r\n", Encoding.UTF8);
        var secondPath = temp.WriteText("second.csv", "Code,tagPrint,tagPrintOrder\r\nB,false,\r\n", Encoding.UTF8);
        var files = new List<CsvFileItem>
        {
            CsvFile(firstPath, ["Code", "tagPrint", "tagPrintOrder"], ["A", "false", ""]),
            CsvFile(secondPath, ["Code", "tagPrint", "tagPrintOrder"], ["B", "false", ""])
        };

        new TagFinderCsvService().SaveFlags(
            files,
            [new TagFinderSaveSelection { File = files[0], RowIndex = 0, Checked = true }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["A", "true", "1"], files[0].Rows[0]);
        Assert.Equal(["B", "false", ""], files[1].Rows[0]);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "second.csv.bak-*"));
        Assert.Contains("A,true,1", File.ReadAllText(firstPath, Encoding.UTF8));
        Assert.Contains("B,false,", File.ReadAllText(secondPath, Encoding.UTF8));
    }

    [Fact]
    public void TagFinderCsvService_SaveFlags_RollsBackWrittenCsvAndKeepsMemoryUnchangedWhenLaterCsvFails()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteText("first.csv", "Code,Name\r\nA100,Apple\r\n", Encoding.UTF8);
        var failingPath = Path.Combine(temp.DirectoryPath, "second.csv");
        Directory.CreateDirectory(failingPath);
        var files = new List<CsvFileItem>
        {
            CsvFile(firstPath, ["Code", "Name"], ["A100", "Apple"]),
            CsvFile(failingPath, ["Code", "Name"], ["B200", "Banana"])
        };

        var ex = Assert.Throws<IOException>(() =>
            new TagFinderCsvService().SaveFlags(
                files,
                [new TagFinderSaveSelection { File = files[0], RowIndex = 0, Checked = true }],
                "tagPrint",
                "tagPrintOrder",
                hasHeaderRow: true));

        Assert.Contains("Tag Finder CSV save failed", ex.Message);
        Assert.Contains(firstPath, ex.Message);
        Assert.Contains(failingPath, ex.Message);
        Assert.Equal("Code,Name\r\nA100,Apple\r\n", File.ReadAllText(firstPath, Encoding.UTF8));
        Assert.Equal(["Code", "Name"], files[0].HeaderRow!);
        Assert.Equal(["A100", "Apple"], files[0].Rows[0]);
        Assert.Equal(["Code", "Name"], files[1].HeaderRow!);
        Assert.Equal(["B200", "Banana"], files[1].Rows[0]);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));
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
                new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true },
                new TagFinderSaveSelection { File = file, RowIndex = 1, Checked = true },
                new TagFinderSaveSelection { File = file, RowIndex = 2, Checked = false }
            ],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["A", "true", "1"], file.Rows[0]);
        Assert.Equal(["B", "true", "6"], file.Rows[1]);
        Assert.Equal(["C", "false", ""], file.Rows[2]);
    }

    [Fact]
    public void TagFinderCsvService_SaveFlags_AssignsOrderWhenAlreadyCheckedRowHasBlankOrder()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,tagPrint,tagPrintOrder\r\nA,true,\r\nB,true,3\r\n", Encoding.UTF8);
        var file = CsvFile(path, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", ""], ["B", "true", "3"]);

        new TagFinderCsvService().SaveFlags(
            [file],
            [
                new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true },
                new TagFinderSaveSelection { File = file, RowIndex = 1, Checked = true }
            ],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["A", "true", "4"], file.Rows[0]);
        Assert.Equal(["B", "true", "3"], file.Rows[1]);
    }


    [Fact]
    public void TagFinderCsvService_SaveFlags_AssignsOrderWhenCheckedRowHasBlankOrder()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText(
            "items.csv",
            "Code,tagPrint,tagPrintOrder\r\nA,true,\r\n",
            Encoding.UTF8);
        var file = CsvFile(path, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", ""]);

        new TagFinderCsvService().SaveFlags(
            [file],
            [new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal("1", file.Rows[0][2]);
        Assert.Contains("A,true,1", File.ReadAllText(path, Encoding.UTF8));
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

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_AlignsRowsByHeaderWhenSourceCsvHeaderOrderDiffers()
    {
        using var temp = new TempDirectory();
        var firstPath = temp.WriteText(
            "first.csv",
            "Code,Name,tagPrint,tagPrintOrder\r\nA001,Apple,true,1\r\n",
            Encoding.UTF8);
        var secondPath = temp.WriteText(
            "second.csv",
            "Name,Code,tagPrint,tagPrintOrder\r\nBanana,B002,true,2\r\n",
            Encoding.UTF8);
        var first = CsvFile(firstPath, ["Code", "Name", "tagPrint", "tagPrintOrder"], ["A001", "Apple", "true", "1"]);
        var second = CsvFile(secondPath, ["Name", "Code", "tagPrint", "tagPrintOrder"], ["Banana", "B002", "true", "2"]);

        var result = new TagFinderCsvService().CreatePrintQueueCsv(
            [
                new TagFinderQueueSource { File = first, RowIndex = 0 },
                new TagFinderQueueSource { File = second, RowIndex = 0 }
            ],
            additionalQuantity: 1,
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], result.Header);
        Assert.Equal(["A001", "Apple", "true", "1"], result.Rows[0]);
        Assert.Equal(["B002", "Banana", "true", "2"], result.Rows[1]);

        var lines = File.ReadAllLines(result.FilePath, Encoding.UTF8);
        Assert.Equal("Code,Name,tagPrint,tagPrintOrder", lines[0]);
        Assert.Equal("A001,Apple,true,1", lines[1]);
        Assert.Equal("B002,Banana,true,2", lines[2]);
    }

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_FillsBlankOrderOnParentCsvRow()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,tagPrint,tagPrintOrder\r\nA,true,\r\nB,true,3\r\n", Encoding.UTF8);
        var parent = CsvFile(parentPath, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", ""], ["B", "true", "3"]);

        var result = new TagFinderCsvService().CreatePrintQueueCsv(
            [new TagFinderQueueSource { File = parent, RowIndex = 0 }],
            additionalQuantity: 1,
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["A", "true", "4"], parent.Rows[0]);
        Assert.Equal(["B", "true", "3"], parent.Rows[1]);
        Assert.Single(result.Rows);
        Assert.Equal(["A", "true", "4"], result.Rows[0]);
        var parentContent = File.ReadAllText(parentPath, Encoding.UTF8);
        Assert.Contains("A,true,4", parentContent);
        Assert.Contains("B,true,3", parentContent);
    }

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_PrintTargetOrderKeepsParentBeforeQueueRows()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,tagPrint,tagPrintOrder\r\nA,true,\r\n", Encoding.UTF8);
        var parent = CsvFile(parentPath, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", ""]);

        var queueResult = new TagFinderCsvService().CreatePrintQueueCsv(
            [new TagFinderQueueSource { File = parent, RowIndex = 0 }],
            additionalQuantity: 2,
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);
        var queue = CsvFile(queueResult.FilePath, queueResult.Header, queueResult.Rows.ToArray());

        var preset = Preset(
            Field("code", "Code", 0),
            Field("tag_print", "tagPrint", 1),
            Field("tag_print_order", "tagPrintOrder", 2));
        preset.Fields[1].PrintConditionEnabled = true;
        preset.Fields[1].PrintConditionType = PrintConditionType.Bool;
        preset.Fields[1].PrintConditionOperator = PrintConditionOperator.IsTrue;

        var entries = PrintableRowService.PrintableRowEntries(preset, [queue, parent], PrintSortMode.PrintTargetOrder);

        Assert.Equal(3, entries.Count);
        Assert.Same(parent, entries[0].File);
        Assert.Equal(["A", "true", "1"], parent.Rows[0]);
        Assert.All(entries, entry => Assert.Equal("1", entry.Row[2]));
        Assert.True(entries.Skip(1).All(entry => CsvFileUtility.IsPrintQueueCsv(entry.File.FilePath)));
    }




    [Fact]
    public void TagFinderCsvService_SaveFlags_RejectsNoHeaderCsv()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "A100,Apple\r\n", Encoding.UTF8);
        var file = new CsvFileItem
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            EncodingName = "UTF-8",
            Status = CsvFileStatus.Ok,
            HeaderRow = null,
            Rows = [["A100", "Apple"]],
            RowCount = 1,
            ColumnCount = 2
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new TagFinderCsvService().SaveFlags(
                [file],
                [new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true }],
                "tagPrint",
                "tagPrintOrder",
                hasHeaderRow: false));

        Assert.Equal("Tag Finder requires CSV files with a header row.", ex.Message);
        Assert.Null(file.HeaderRow);
        Assert.Equal(["A100", "Apple"], file.Rows[0]);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TagFinderCsvService_CreatePrintQueueCsv_RejectsNonPositiveAdditionalQuantity(int additionalQuantity)
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,tagPrint,tagPrintOrder\r\nA,true,1\r\n", Encoding.UTF8);
        var file = CsvFile(path, ["Code", "tagPrint", "tagPrintOrder"], ["A", "true", "1"]);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TagFinderCsvService().CreatePrintQueueCsv(
                [new TagFinderQueueSource { File = file, RowIndex = 0 }],
                additionalQuantity,
                "tagPrint",
                "tagPrintOrder",
                hasHeaderRow: true));

        Assert.Equal("additionalQuantity", ex.ParamName);
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "print_queue_*.csv"));
        Assert.Equal(["A", "true", "1"], file.Rows[0]);
        Assert.Equal("Code,tagPrint,tagPrintOrder\r\nA,true,1\r\n", File.ReadAllText(path, Encoding.UTF8));
    }

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_RejectsCsvWithoutHeaderRow()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "A100,Apple\r\n", Encoding.UTF8);
        var file = new CsvFileItem
        {
            FileName = Path.GetFileName(path),
            FilePath = path,
            EncodingName = "UTF-8",
            Status = CsvFileStatus.Ok,
            HeaderRow = null,
            Rows = [["A100", "Apple"]],
            RowCount = 1,
            ColumnCount = 2
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new TagFinderCsvService().CreatePrintQueueCsv(
                [new TagFinderQueueSource { File = file, RowIndex = 0 }],
                additionalQuantity: 1,
                "tagPrint",
                "tagPrintOrder",
                hasHeaderRow: false));

        Assert.Equal("Tag Finder requires CSV files with a header row.", ex.Message);
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "print_queue_*.csv"));
        Assert.Null(file.HeaderRow);
        Assert.Equal(["A100", "Apple"], file.Rows[0]);
    }


    [Fact]
    public void TagFinderCsvService_SaveFlags_PreservesPhysicalBlankRows()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,Name\r\nA100,Apple\r\n\r\nB200,Banana\r\n", Encoding.UTF8);
        var file = CsvFile(path, ["Code", "Name"], ["A100", "Apple"], Array.Empty<string>(), ["B200", "Banana"]);

        new TagFinderCsvService().SaveFlags(
            [file],
            [new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Equal(["Code", "Name", "tagPrint", "tagPrintOrder"], file.HeaderRow!);
        Assert.Equal(["A100", "Apple", "true", "1"], file.Rows[0]);
        Assert.Empty(file.Rows[1]);
        Assert.Equal(["B200", "Banana", "false", ""], file.Rows[2]);

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        Assert.Equal("Code,Name,tagPrint,tagPrintOrder", lines[0]);
        Assert.Equal("A100,Apple,true,1", lines[1]);
        Assert.Equal("", lines[2]);
        Assert.Equal("B200,Banana,false,", lines[3]);
    }

    [Fact]
    public void TagFinderCsvService_CreatePrintQueueCsv_PreservesSourcePhysicalBlankRows()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code,tagPrint,tagPrintOrder\r\nA,false,\r\n\r\nB,true,\r\n", Encoding.UTF8);
        var file = CsvFile(path, ["Code", "tagPrint", "tagPrintOrder"], ["A", "false", ""], Array.Empty<string>(), ["B", "true", ""]);

        var result = new TagFinderCsvService().CreatePrintQueueCsv(
            [new TagFinderQueueSource { File = file, RowIndex = 2 }],
            additionalQuantity: 1,
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        Assert.Empty(file.Rows[1]);
        Assert.Equal(["B", "true", "1"], file.Rows[2]);
        Assert.Single(result.Rows);
        Assert.Equal(["B", "true", "1"], result.Rows[0]);

        var sourceLines = File.ReadAllLines(path, Encoding.UTF8);
        Assert.Equal("", sourceLines[2]);
    }

}
