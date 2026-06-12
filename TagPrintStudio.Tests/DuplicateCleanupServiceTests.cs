using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void DuplicateCleanup_Analyze_KeepsLargestNumberPerKeyAcrossCsvFiles()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("price", "Price", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", "120"], ["B200", "50"]),
            Csv("second.csv", ["A100", "180"], ["C300", "90"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "price",
            ValueType = DuplicateValueType.Number,
            Rule = DuplicateResolutionRule.KeepLargest
        });

        Assert.Equal(1, result.GroupCount);
        Assert.Equal(1, result.DeleteCount);
        Assert.Equal(1, result.TargetFileCount);

        var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Keep && item.FileName == "second.csv" && item.CompareValue == "180");
        Assert.Contains(items, item => !item.Keep && item.FileName == "first.csv" && item.CompareValue == "120");
    }

    [Fact]
    public void DuplicateCleanup_Analyze_UsesNonEmptyRuleForText()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("description", "Description", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", ""], ["B200", "Alpha"]),
            Csv("second.csv", ["A100", "Filled"], ["C300", "Beta"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "description",
            ValueType = DuplicateValueType.Text,
            Rule = DuplicateResolutionRule.KeepNonEmpty
        });

        var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Keep && item.FileName == "second.csv" && item.CompareValue == "Filled");
        Assert.Contains(items, item => !item.Keep && item.FileName == "first.csv" && item.CompareValue == "");
    }

    [Fact]
    public void DuplicateCleanup_Apply_RemovesDeletedRowsAndCreatesBackup()
    {
        using var temp = new TempDirectory();
        var file1Path = temp.WriteText("first.csv", "Code,Price\r\nA100,120\r\nB200,50\r\n", Encoding.UTF8);
        var file2Path = temp.WriteText("second.csv", "Code,Price\r\nA100,180\r\nC300,90\r\n", Encoding.UTF8);

        var file1 = new CsvLoaderService().LoadFiles([file1Path], Preset(Field("code", "Code", 0), Field("price", "Price", 1)), hasHeaderRow: true).Files.Single();
        var file2 = new CsvLoaderService().LoadFiles([file2Path], Preset(Field("code", "Code", 0), Field("price", "Price", 1)), hasHeaderRow: true).Files.Single();
        var files = new List<CsvFileItem> { file1, file2 };
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("price", "Price", 1));

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "price",
            ValueType = DuplicateValueType.Number,
            Rule = DuplicateResolutionRule.KeepLargest
        });

        var apply = service.Apply(files, result, hasHeaderRow: true);

        Assert.Equal(1, apply.UpdatedFileCount);
        Assert.Equal(1, apply.DeletedRowCount);
        Assert.Contains(file1Path, apply.UpdatedFiles);
        Assert.Single(Directory.GetFiles(temp.DirectoryPath, "first.csv.bak-*"));

        var rewritten = File.ReadAllText(file1Path, Encoding.UTF8);
        Assert.Contains("B200,50", rewritten);
        Assert.DoesNotContain("A100,120", rewritten);
        Assert.Contains("Code,Price", rewritten);
    }

    [Fact]
    public void DuplicateCleanup_Analyze_UsesFirstRowWhenValuesTie()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("price", "Price", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", "120"]),
            Csv("second.csv", ["A100", "120"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "price",
            ValueType = DuplicateValueType.Number,
            Rule = DuplicateResolutionRule.KeepLargest
        });

        var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Keep && item.FileName == "first.csv");
        Assert.Contains(items, item => !item.Keep && item.FileName == "second.csv");
    }

    [Fact]
    public void DuplicateCleanup_Analyze_KeepsLatestDatePerKey()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("updatedAt", "UpdatedAt", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", "2026-04-20"]),
            Csv("second.csv", ["A100", "2026-04-25"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "updatedAt",
            ValueType = DuplicateValueType.Date,
            Rule = DuplicateResolutionRule.KeepLargest
        });

        var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Keep && item.FileName == "second.csv" && item.CompareValue == "2026-04-25");
        Assert.Contains(items, item => !item.Keep && item.FileName == "first.csv" && item.CompareValue == "2026-04-20");
    }

    [Fact]
    public void DuplicateCleanup_Analyze_UsesDateSpecificReasonsForKeepLargest()
    {
        var previousLanguage = LocalizationService.CurrentLanguage;
        LocalizationService.SetLanguage(AppLanguage.English);
        try
        {
            var service = new DuplicateCleanupService();
            var preset = Preset(
                Field("code", "Code", 0),
                Field("updatedAt", "UpdatedAt", 1));
            var files = new List<CsvFileItem>
            {
                Csv("first.csv", ["A100", "2026-04-20"]),
                Csv("second.csv", ["A100", "2026-04-25"])
            };

            var result = service.Analyze(preset, files, new DuplicateCleanupOptions
            {
                KeyFieldId = "code",
                CompareFieldId = "updatedAt",
                ValueType = DuplicateValueType.Date,
                Rule = DuplicateResolutionRule.KeepLargest
            });

            var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
            Assert.Contains(items, item => item.Keep && item.Reason == "Keep: newer date");
            Assert.Contains(items, item => !item.Keep && item.Reason == "Delete: older date");
        }
        finally
        {
            LocalizationService.SetLanguage(previousLanguage);
        }
    }

    [Fact]
    public void DuplicateCleanup_Analyze_IgnoresEmptyKeysWhenOptionEnabled()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("price", "Price", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["", "120"], ["A100", "150"]),
            Csv("second.csv", ["", "180"], ["A100", "140"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "price",
            ValueType = DuplicateValueType.Number,
            Rule = DuplicateResolutionRule.KeepLargest,
            IgnoreEmptyKeys = true
        });

        Assert.Equal(1, result.GroupCount);
        Assert.All(result.Items, item => Assert.Equal("A100", item.KeyValue));
    }

    [Fact]
    public void DuplicateCleanup_Analyze_AlwaysIgnoresPhysicalBlankRows()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("price", "Price", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", Array.Empty<string>(), ["A100", "120"]),
            Csv("second.csv", Array.Empty<string>(), ["A100", "180"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "price",
            ValueType = DuplicateValueType.Number,
            Rule = DuplicateResolutionRule.KeepLargest,
            IgnoreEmptyKeys = false
        });

        Assert.Equal(1, result.GroupCount);
        Assert.All(result.Items, item => Assert.Equal("A100", item.KeyValue));
    }

    [Fact]
    public void DuplicateCleanup_Analyze_BuildsCarryAction_WhenKeepRowIsEmpty()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("updatedAt", "UpdatedAt", 1),
            Field("description", "Description", 2));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", "2026-04-20", "From first"]),
            Csv("second.csv", ["A100", "2026-04-25", ""])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "updatedAt",
            ValueType = DuplicateValueType.Date,
            Rule = DuplicateResolutionRule.KeepLargest,
            Carry = new DuplicateCarryOptions
            {
                Enabled = true,
                FieldIds = ["description"],
                CopyMode = DuplicateCopyMode.FillIfEmpty
            }
        });

        Assert.Single(result.CarryActions);
        Assert.Equal(1, result.CarryCellCount);
        var keepRow = Assert.Single(result.Items, item => item.Keep);
        Assert.Equal("Description", keepRow.CarrySummary);
    }

    [Fact]
    public void DuplicateCleanup_Apply_CopiesCarryValueBeforeDeletingRows()
    {
        using var temp = new TempDirectory();
        var file1Path = temp.WriteText("first.csv", "Code,UpdatedAt,Description\r\nA100,2026-04-20,From first\r\n", Encoding.UTF8);
        var file2Path = temp.WriteText("second.csv", "Code,UpdatedAt,Description\r\nA100,2026-04-25,\r\n", Encoding.UTF8);

        var preset = Preset(
            Field("code", "Code", 0),
            Field("updatedAt", "UpdatedAt", 1),
            Field("description", "Description", 2));
        var file1 = new CsvLoaderService().LoadFiles([file1Path], preset, hasHeaderRow: true).Files.Single();
        var file2 = new CsvLoaderService().LoadFiles([file2Path], preset, hasHeaderRow: true).Files.Single();
        var files = new List<CsvFileItem> { file1, file2 };
        var service = new DuplicateCleanupService();

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "updatedAt",
            ValueType = DuplicateValueType.Date,
            Rule = DuplicateResolutionRule.KeepLargest,
            Carry = new DuplicateCarryOptions
            {
                Enabled = true,
                FieldIds = ["description"],
                CopyMode = DuplicateCopyMode.FillIfEmpty
            }
        });

        var apply = service.Apply(files, result, hasHeaderRow: true);

        Assert.Equal(2, apply.UpdatedFileCount);
        Assert.Equal(1, apply.DeletedRowCount);
        Assert.Contains(file1Path, apply.UpdatedFiles);
        Assert.Contains(file2Path, apply.UpdatedFiles);

        var rewrittenSecond = File.ReadAllText(file2Path, Encoding.UTF8);
        Assert.Contains("A100,2026-04-25,From first", rewrittenSecond);
        Assert.DoesNotContain("A100,2026-04-20,From first", rewrittenSecond);
    }

    [Fact]
    public void DuplicateCleanup_Analyze_CarriesValuesFromAllDeletedRows()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("updatedAt", "UpdatedAt", 1),
            Field("description", "Description", 2),
            Field("note", "Note", 3));
        var files = new List<CsvFileItem>
        {
            Csv("latest.csv", ["A100", "2026-04-25", "", ""]),
            Csv("old1.csv", ["A100", "2026-04-20", "From old1", ""]),
            Csv("old2.csv", ["A100", "2026-04-15", "", "From old2"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "updatedAt",
            ValueType = DuplicateValueType.Date,
            Rule = DuplicateResolutionRule.KeepLargest,
            Carry = new DuplicateCarryOptions
            {
                Enabled = true,
                FieldIds = ["description", "note"],
                CopyMode = DuplicateCopyMode.FillIfEmpty
            }
        });

        Assert.Equal(2, result.CarryCellCount);
        Assert.Contains(result.CarryActions, action =>
            action.FieldId == "description" &&
            action.Value == "From old1" &&
            action.SourceFilePath == "old1.csv");
        Assert.Contains(result.CarryActions, action =>
            action.FieldId == "note" &&
            action.Value == "From old2" &&
            action.SourceFilePath == "old2.csv");

        var keepRow = Assert.Single(result.Items, item => item.Keep);
        Assert.Equal("Description, Note", keepRow.CarrySummary);
    }

    [Fact]
    public void DuplicateCleanup_Apply_RollsBackDiskAndKeepsMemoryUnchangedWhenLaterCsvSaveFails()
    {
        using var temp = new TempDirectory();
        var file1Path = temp.WriteText("first.csv", "Code,Price\r\nA100,120\r\nB200,50\r\n", Encoding.UTF8);
        var blockingFilePath = temp.WriteText("not-a-folder", "blocks directory creation", Encoding.UTF8);
        var file2Path = Path.Combine(blockingFilePath, "second.csv");

        var file1 = new CsvFileItem
        {
            FileName = "first.csv",
            FilePath = file1Path,
            EncodingName = "UTF-8",
            Status = CsvFileStatus.Ok,
            HeaderRow = ["Code", "Price"],
            Rows = [["A100", "120"], ["B200", "50"]],
            RowCount = 2,
            ColumnCount = 2
        };
        var file2 = new CsvFileItem
        {
            FileName = "second.csv",
            FilePath = file2Path,
            EncodingName = "UTF-8",
            Status = CsvFileStatus.Ok,
            HeaderRow = ["Code", "Price"],
            Rows = [["A100", "180"]],
            RowCount = 1,
            ColumnCount = 2
        };

        var result = new DuplicateCleanupResult
        {
            Items =
            [
                new DuplicateResolutionItem
                {
                    FilePath = file1Path,
                    FileName = "first.csv",
                    RowIndex = 0,
                    Keep = false
                },
                new DuplicateResolutionItem
                {
                    FilePath = file2Path,
                    FileName = "second.csv",
                    RowIndex = 0,
                    Keep = true
                }
            ],
            CarryActions =
            [
                new DuplicateCarryAction
                {
                    KeepFilePath = file2Path,
                    KeepRowIndex = 0,
                    ColumnIndex = 1,
                    Value = "999"
                }
            ]
        };

        var exception = Assert.Throws<IOException>(() =>
            new DuplicateCleanupService().Apply([file1, file2], result, hasHeaderRow: true));

        Assert.Contains("Duplicate cleanup CSV save failed", exception.Message);
        Assert.Collection(
            file1.Rows,
            row => Assert.Equal(["A100", "120"], row),
            row => Assert.Equal(["B200", "50"], row));
        var file2Row = Assert.Single(file2.Rows);
        Assert.Equal(["A100", "180"], file2Row);
        Assert.Equal("Code,Price\r\nA100,120\r\nB200,50\r\n", File.ReadAllText(file1Path, Encoding.UTF8));
    }


    [Fact]
    public void DuplicateCleanup_Analyze_UsesLastRowRule()
    {
        var service = new DuplicateCleanupService();
        var preset = Preset(
            Field("code", "Code", 0),
            Field("description", "Description", 1));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A100", "First"]),
            Csv("second.csv", ["A100", "Last"])
        };

        var result = service.Analyze(preset, files, new DuplicateCleanupOptions
        {
            KeyFieldId = "code",
            CompareFieldId = "description",
            ValueType = DuplicateValueType.Text,
            Rule = DuplicateResolutionRule.KeepLast
        });

        var items = result.Items.Where(item => item.KeyValue == "A100").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Keep && item.FileName == "second.csv" && item.CompareValue == "Last");
        Assert.Contains(items, item => !item.Keep && item.FileName == "first.csv" && item.CompareValue == "First");
    }

}