using System.Drawing;
using System.IO.Compression;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    static ServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Theory]
    [InlineData(PrintConditionType.Number, PrintConditionOperator.GreaterThanOrEqual, "100", "100", true)]
    [InlineData(PrintConditionType.Number, PrintConditionOperator.LessThan, "100", "99", true)]
    [InlineData(PrintConditionType.Bool, PrintConditionOperator.IsTrue, "", "yes", true)]
    [InlineData(PrintConditionType.Bool, PrintConditionOperator.IsFalse, "", "0", true)]
    [InlineData(PrintConditionType.Date, PrintConditionOperator.OnOrAfter, "2026-04-01", "2026/4/25", true)]
    [InlineData(PrintConditionType.Date, PrintConditionOperator.OnOrBefore, "2026-04-01", "2026/4/25", false)]
    public void PrintConditionService_MatchesExpectedOperators(
        PrintConditionType type,
        PrintConditionOperator op,
        string compareValue,
        string actualValue,
        bool expected)
    {
        var preset = Preset(new FieldDefinition
        {
            FieldId = "target",
            DisplayName = "対象",
            ColumnIndex = 0,
            Enabled = true,
            PrintConditionEnabled = true,
            PrintConditionType = type,
            PrintConditionOperator = op,
            PrintConditionValue = compareValue
        });

        Assert.Equal(expected, PrintConditionService.MatchesAll(preset, [actualValue]));
    }

    [Fact]
    public void PreviewRenderer_CalculatesRowsPerPageFromLayout()
    {
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout(tagWidth: 50, tagHeight: 30, margin: 5, spacing: 5);

        Assert.Equal((3, 8), renderer.GetGrid(layout));
        Assert.Equal(24, renderer.GetRowsPerPage(layout));
    }

    [Fact]
    public void OutputService_PlansPagedPngPathsUsingRowsPerPageAndSafeFileNames()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("name", "商品名", 0));
        var csv = Csv("bad:name.csv", ["A"], ["B"], ["C"], ["D"], ["E"]);
        var options = Options(OutputFormatType.Png, OutputScopeType.SinglePdfAllCsv);

        var paths = service.GetPlannedOutputPaths(layout, preset, [csv], options)
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["bad_name_page_001.png", "bad_name_page_002.png", "bad_name_page_003.png"], paths);
    }

    [Fact]
    public void OutputService_PlansIndividualPngPathsOnlyForRowsMatchingCondition()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(10), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(new FieldDefinition
        {
            FieldId = "price",
            DisplayName = "価格",
            ColumnIndex = 0,
            Enabled = true,
            PrintConditionEnabled = true,
            PrintConditionType = PrintConditionType.Number,
            PrintConditionOperator = PrintConditionOperator.GreaterThanOrEqual,
            PrintConditionValue = "100"
        });
        var csv = Csv("items.csv", ["80"], ["100"], ["120"]);
        var options = Options(OutputFormatType.Png, OutputScopeType.IndividualPngPerRow);

        var paths = service.GetPlannedOutputPaths(layout, preset, [csv], options)
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["items_0001.png", "items_0002.png"], paths);
    }

    [Fact]
    public void OutputService_PlansPdfPathsForSelectedScope()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("name", "商品名", 0));
        var files = new List<CsvFileItem>
        {
            Csv("first.csv", ["A"]),
            Csv("second.csv", ["B"])
        };

        var separate = service.GetPlannedOutputPaths(layout, preset, files, Options(OutputFormatType.Pdf, OutputScopeType.SeparatePdfPerCsv))
            .Select(Path.GetFileName)
            .ToList();
        var combined = service.GetPlannedOutputPaths(layout, preset, files, Options(OutputFormatType.Pdf, OutputScopeType.SinglePdfAllCsv))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["first.pdf", "second.pdf"], separate);
        Assert.Equal(["TagPrintStudio_Output.pdf"], combined);
    }

    [Fact]
    public void SafeFileNameService_ReplacesInvalidCharactersAndHandlesBlankNames()
    {
        Assert.Equal("bad_name_", SafeFileNameService.ToSafeFileName(" bad:name? "));
        Assert.Equal("untitled", SafeFileNameService.ToSafeFileName("   "));
    }

    [Fact]
    public void OutputService_Pages_ReturnsLazyEnumerable()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"], ["B"], ["C"]);

        var pages = InvokePages(service, layout, preset, [csv]);

        Assert.False(pages is List<List<Dictionary<string, string>>>);
        Assert.Equal([["A", "B"], ["C"]], pages.Select(page => page.Select(row => row["name"]).ToList()).ToList());
    }

    [Fact]
    public void OutputService_Pages_PreservesSelectedFieldSort()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("item", "Item Code", 0), Field("style", "Style", 1));
        var csv = Csv("items.csv", ["A001", "Z"], ["A002", "A"], ["A003", "M"]);

        var pages = InvokePages(service, layout, preset, [csv], PrintSortMode.SelectedField, "style")
            .SelectMany(page => page)
            .Select(row => row["style"])
            .ToList();

        Assert.Equal(["A", "M", "Z"], pages);
    }

    [Fact]
    public void OutputService_Pages_ReturnsNoPagesForNoPrintableRows()
    {
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var field = Field("price", "Price", 0);
        field.PrintConditionEnabled = true;
        field.PrintConditionType = PrintConditionType.Number;
        field.PrintConditionOperator = PrintConditionOperator.GreaterThan;
        field.PrintConditionValue = "100";
        var preset = Preset(field);
        var csv = Csv("items.csv", ["10"], ["20"]);

        var pages = InvokePages(service, layout, preset, [csv]).ToList();

        Assert.Empty(pages);
    }

    [Fact]
    public void OutputService_ExportPdf_SavesIntoSpecifiedOutputFolder()
    {
        using var temp = new TempDirectory();
        var outputFolder = System.IO.Path.Combine(temp.DirectoryPath, "exports");
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"], ["B"]);
        var options = Options(OutputFormatType.Pdf, OutputScopeType.SinglePdfAllCsv);
        options.OutputFolder = outputFolder;

        service.ExportPdf(layout, preset, [csv], options);

        Assert.True(File.Exists(System.IO.Path.Combine(outputFolder, "TagPrintStudio_Output.pdf")));
    }

    [Fact]
    public void OutputService_ExportPng_RemovesCompletedFilesWhenCanceled()
    {
        using var temp = new TempDirectory();
        var outputFolder = System.IO.Path.Combine(temp.DirectoryPath, "exports");
        var service = new OutputService(new FixedRowsPerPageRenderer(2), new UnitConversionService());
        var layout = Layout();
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"], ["B"]);
        var options = Options(OutputFormatType.Png, OutputScopeType.IndividualPngPerRow);
        options.OutputFolder = outputFolder;
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterProgress(1, cancellation);

        Assert.Throws<OperationCanceledException>(() => service.ExportPng(layout, preset, [csv], options, progress, cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(outputFolder, "*.png"));
        Assert.Empty(Directory.EnumerateFiles(outputFolder, "*.partial"));
    }

    [Theory]
    [InlineData(0f, 1)]
    [InlineData(-1f, 1)]
    [InlineData(25.4f, 100)]
    public void OutputService_MmToHundredthsOfInchForSize_ClampsToPrintableSize(float millimeters, int expected)
    {
        Assert.Equal(expected, InvokeOutputServiceInt("MmToHundredthsOfInchForSize", millimeters));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(-25.4f, -100)]
    [InlineData(25.4f, 100)]
    public void OutputService_MmToHundredthsOfInchForOffset_AllowsZeroAndNegativeValues(float millimeters, int expected)
    {
        Assert.Equal(expected, InvokeOutputServiceInt("MmToHundredthsOfInchForOffset", millimeters));
    }

    [Fact]
    public void PrintableRowService_SelectFiles_UsesAllCsvOnlyForSinglePdfAllCsv()
    {
        var first = Csv("first.csv", ["A001"]);
        var second = Csv("second.csv", ["A002"]);

        var allFiles = PrintableRowService.SelectFiles(OutputScopeType.SinglePdfAllCsv, [first, second], first);
        var selectedFile = PrintableRowService.SelectFiles(OutputScopeType.SeparatePdfPerCsv, [first, second], first);

        Assert.Equal(["first.csv", "second.csv"], allFiles.Select(file => file.FileName));
        Assert.Equal(["first.csv"], selectedFile.Select(file => file.FileName));
    }

    [Fact]
    public void PrintableRowService_PreviewRows_PreservesCsvContextAfterFiltering()
    {
        var condition = Field("updated", "Updated", 1);
        condition.PrintConditionEnabled = true;
        condition.PrintConditionType = PrintConditionType.Date;
        condition.PrintConditionOperator = PrintConditionOperator.OnOrAfter;
        condition.PrintConditionValue = "25/04/26";
        var preset = Preset(Field("code", "Code", 0), condition);
        var first = Csv("first.csv", ["A001", "24/04/26"], ["A002", "25/04/26"]);
        var second = Csv("second.csv", ["B001", "26/04/26"]);

        var rows = PrintableRowService.PreviewRows(preset, [first, second], hasHeaderRow: true);

        Assert.Equal(2, rows.Count);
        Assert.Equal("first.csv", rows[0].FileName);
        Assert.Equal(1, rows[0].RowIndex);
        Assert.Equal("A002", rows[0].Data["code"]);
        Assert.Equal("second.csv", rows[1].FileName);
        Assert.Equal(0, rows[1].RowIndex);
        Assert.Equal("B001", rows[1].Data["code"]);
    }

    [Fact]
    public void PrintableRowService_PrintableRows_ItemCodeSort_GroupsQueueRowsAfterParentRows()
    {
        var preset = Preset(Field("product_name_1", "Item Code", 0));
        var parent = Csv("parent.csv", ["B002"], ["A001"]);
        var queue = Csv("print_queue_20260603_120000.csv", ["A001"], ["B002"]);

        var rows = PrintableRowService.PrintableRows(preset, [parent, queue], PrintSortMode.ItemCode);

        Assert.Equal(["A001", "A001", "B002", "B002"], rows.Select(row => row[0]));
        Assert.Same(parent.Rows[1], rows[0]);
        Assert.Same(queue.Rows[0], rows[1]);
        Assert.Same(parent.Rows[0], rows[2]);
        Assert.Same(queue.Rows[1], rows[3]);
    }

    [Fact]
    public void PrintableRowService_PrintableRows_SelectedFieldSort_UsesChosenField()
    {
        var preset = Preset(
            Field("item", "Item Code", 0),
            Field("style", "Style", 1));
        var csv = Csv("items.csv", ["A001", "Z"], ["A002", "A"], ["A003", "M"]);

        var rows = PrintableRowService.PrintableRows(preset, [csv], PrintSortMode.SelectedField, "style");

        Assert.Equal(["A", "M", "Z"], rows.Select(row => row[1]));
    }

    [Fact]
    public void PrintableRowService_PrintableRows_SelectedFieldSort_CanUseNaturalOrder()
    {
        var preset = Preset(Field("item", "Item Code", 0));
        var csv = Csv("items.csv", ["A1"], ["A10"], ["A2"]);

        var rows = PrintableRowService.PrintableRows(preset, [csv], PrintSortMode.SelectedField, "item", PrintSortValueType.Natural);

        Assert.Equal(["A1", "A2", "A10"], rows.Select(row => row[0]));
    }

    [Fact]
    public void PrintableRowService_PrintableRows_SelectedFieldSort_CanUseNumberOrder()
    {
        var preset = Preset(Field("price", "Price", 0));
        var csv = Csv("items.csv", ["10"], ["2"], ["100"]);

        var rows = PrintableRowService.PrintableRows(preset, [csv], PrintSortMode.SelectedField, "price", PrintSortValueType.Number);

        Assert.Equal(["2", "10", "100"], rows.Select(row => row[0]));
    }

    [Fact]
    public void PrintableRowService_PrintableRows_SelectedFieldSort_CanUseDateOrder()
    {
        var preset = Preset(Field("date", "Date", 0));
        var csv = Csv("items.csv", ["2026-04-10"], ["2026-04-02"], ["2026-04-30"]);

        var rows = PrintableRowService.PrintableRows(preset, [csv], PrintSortMode.SelectedField, "date", PrintSortValueType.Date);

        Assert.Equal(["2026-04-02", "2026-04-10", "2026-04-30"], rows.Select(row => row[0]));
    }

    [Fact]
    public void PrintableRowService_PrintableRows_PrintTargetOrderSort_UsesTagPrintOrderColumn()
    {
        var preset = Preset(
            Field("item", "Item Code", 0),
            Field("tag_print", "tagPrint", 1),
            Field("tag_print_order", "tagPrintOrder", 2));
        preset.Fields[1].PrintConditionEnabled = true;
        preset.Fields[1].PrintConditionType = PrintConditionType.Bool;
        preset.Fields[1].PrintConditionOperator = PrintConditionOperator.IsTrue;
        var csv = Csv("items.csv", ["A", "true", "3"], ["B", "true", "1"], ["C", "true", "2"], ["D", "false", "4"]);
        csv.HeaderRow = ["Item Code", "tagPrint", "tagPrintOrder"];

        var rows = PrintableRowService.PrintableRows(preset, [csv], PrintSortMode.PrintTargetOrder);

        Assert.Equal(["B", "C", "A"], rows.Select(row => row[0]));
    }

    [Fact]
    public void SettingsArchiveService_CreateBackup_IncludesSettingsFolders()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(paths.Layouts, "default.tpslayout"), "layout", Encoding.UTF8);
        File.WriteAllText(Path.Combine(paths.Presets, "default.tpspreset"), "preset", Encoding.UTF8);
        File.WriteAllText(Path.Combine(paths.Recent, "recent.json"), "recent", Encoding.UTF8);
        File.WriteAllText(Path.Combine(paths.Settings, "appsettings.json"), "settings", Encoding.UTF8);

        var zipPath = Path.Combine(temp.DirectoryPath, "backup.zip");
        new SettingsArchiveService(paths).CreateBackup(zipPath);

        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("layouts/default.tpslayout", entries);
        Assert.Contains("presets/default.tpspreset", entries);
        Assert.Contains("recent/recent.json", entries);
        Assert.Contains("settings/appsettings.json", entries);
    }

    [Fact]
    public void LayoutService_SaveAndLoad_PreservesBarcodeSettings()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();

        var service = new LayoutService(paths);
        var path = Path.Combine(paths.Layouts, "barcode-settings.tpslayout");
        var layout = Layout(tagWidth: 50, tagHeight: 50);

        layout.Elements.Add(new LayoutElement
        {
            ElementId = "barcode-1",
            Name = "QR Code",
            ElementType = LayoutElementType.Barcode,
            FieldId = "code",
            X = 5,
            Y = 5,
            Width = 30,
            Height = 20,
            BarcodeType = "QRCode",
            BarcodeShowText = false,
            Visible = true
        });

        service.Save(path, layout);

        var loaded = service.Load(path);
        var element = Assert.Single(loaded.Elements);

        Assert.Equal("QRCode", element.BarcodeType);
        Assert.False(element.BarcodeShowText);
    }

    [Fact]
    public void SettingsArchiveService_ImportBackup_ReplacesSettingsFolders()
    {
        using var sourceTemp = new TempDirectory();
        var sourcePaths = TestAppPaths(sourceTemp);
        sourcePaths.EnsureDirectories();
        File.WriteAllText(Path.Combine(sourcePaths.Layouts, "imported.tpslayout"), "layout", Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourcePaths.Presets, "imported.tpspreset"), "preset", Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourcePaths.Recent, "recent.json"), "recent", Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourcePaths.Settings, "appsettings.json"), "settings", Encoding.UTF8);
        var zipPath = Path.Combine(sourceTemp.DirectoryPath, "backup.zip");
        new SettingsArchiveService(sourcePaths).CreateBackup(zipPath);

        using var targetTemp = new TempDirectory();
        var targetPaths = TestAppPaths(targetTemp);
        targetPaths.EnsureDirectories();
        File.WriteAllText(Path.Combine(targetPaths.Layouts, "old.tpslayout"), "old", Encoding.UTF8);

        new SettingsArchiveService(targetPaths).ImportBackup(zipPath);

        Assert.False(File.Exists(Path.Combine(targetPaths.Layouts, "old.tpslayout")));
        Assert.Equal("layout", File.ReadAllText(Path.Combine(targetPaths.Layouts, "imported.tpslayout"), Encoding.UTF8));
        Assert.Equal("preset", File.ReadAllText(Path.Combine(targetPaths.Presets, "imported.tpspreset"), Encoding.UTF8));
        Assert.Equal("recent", File.ReadAllText(Path.Combine(targetPaths.Recent, "recent.json"), Encoding.UTF8));
        Assert.Equal("settings", File.ReadAllText(Path.Combine(targetPaths.Settings, "appsettings.json"), Encoding.UTF8));
    }

    [Fact]
    public void SettingsArchiveService_ImportBackup_KeepsExistingSettingsWhenZipIsInvalid()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();
        var existingLayoutPath = Path.Combine(paths.Layouts, "existing.tpslayout");
        var existingSettingsPath = Path.Combine(paths.Settings, "appsettings.json");
        File.WriteAllText(existingLayoutPath, "existing-layout", Encoding.UTF8);
        File.WriteAllText(existingSettingsPath, "existing-settings", Encoding.UTF8);

        var badZipPath = temp.WriteText("bad.zip", "not a zip file", Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => new SettingsArchiveService(paths).ImportBackup(badZipPath));
        Assert.Equal("existing-layout", File.ReadAllText(existingLayoutPath, Encoding.UTF8));
        Assert.Equal("existing-settings", File.ReadAllText(existingSettingsPath, Encoding.UTF8));
    }


    [Fact]
    public void SettingsArchiveService_ImportBackup_RejectsSingleOversizedFileAndKeepsExistingSettings()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();
        var existingLayoutPath = Path.Combine(paths.Layouts, "existing.tpslayout");
        File.WriteAllText(existingLayoutPath, "existing-layout", Encoding.UTF8);

        var zipPath = Path.Combine(temp.DirectoryPath, "oversized.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("layouts/large.tpslayout", CompressionLevel.NoCompression);
            using var stream = entry.Open();
            var buffer = new byte[1024 * 1024];
            for (var i = 0; i < 21; i++)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsArchiveService(paths).ImportBackup(zipPath));
        Assert.Contains("too large", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("existing-layout", File.ReadAllText(existingLayoutPath, Encoding.UTF8));
    }

    [Fact]
    public void SettingsArchiveService_ImportBackup_RejectsTooManyFilesAndKeepsExistingSettings()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();
        var existingSettingsPath = Path.Combine(paths.Settings, "appsettings.json");
        File.WriteAllText(existingSettingsPath, "existing-settings", Encoding.UTF8);

        var zipPath = Path.Combine(temp.DirectoryPath, "too-many-files.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            for (var i = 0; i < 5001; i++)
            {
                archive.CreateEntry($"settings/file-{i:D4}.json");
            }
        }

        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsArchiveService(paths).ImportBackup(zipPath));
        Assert.Contains("too many", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("existing-settings", File.ReadAllText(existingSettingsPath, Encoding.UTF8));
    }

    [Fact]
    public void BootstrapService_Initialize_DoesNotReplaceCustomizedDefaultFilesWhenMojibakeTextAppears()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();

        var presetPath = Path.Combine(paths.Presets, "default-price-tag.tpspreset");
        var customPreset = Preset(Field("name", "Name", 0));
        customPreset.PresetId = "custom-default-price-tag";
        customPreset.PresetName = "Customized Default";
        customPreset.Description = "User text that happens to contain 繝";
        JsonFile.Save(presetPath, customPreset);

        var layoutPath = Path.Combine(paths.Layouts, "default-price-tag.tpslayout");
        var customLayout = Layout();
        customLayout.LayoutId = "custom-default-price-tag-layout";
        customLayout.LayoutName = "Customized Layout";
        customLayout.Description = "User text that happens to contain 縺";
        JsonFile.Save(layoutPath, customLayout);

        new BootstrapService(paths).Initialize();

        var loadedPreset = JsonFile.Load<PresetFile>(presetPath);
        var loadedLayout = JsonFile.Load<LayoutFile>(layoutPath);
        Assert.Equal("custom-default-price-tag", loadedPreset.PresetId);
        Assert.Equal("Customized Default", loadedPreset.PresetName);
        Assert.Contains("繝", loadedPreset.Description);
        Assert.Equal("custom-default-price-tag-layout", loadedLayout.LayoutId);
        Assert.Equal("Customized Layout", loadedLayout.LayoutName);
        Assert.Contains("縺", loadedLayout.Description);
        Assert.Empty(Directory.GetFiles(paths.Presets, "default-price-tag.tpspreset.mojibake-backup-*"));
        Assert.Empty(Directory.GetFiles(paths.Layouts, "default-price-tag.tpslayout.mojibake-backup-*"));
    }

    [Fact]
    public void BootstrapService_Initialize_ReplacesDefaultMojibakeFilesAndCreatesBackups()
    {
        using var temp = new TempDirectory();
        var paths = TestAppPaths(temp);
        paths.EnsureDirectories();

        var presetPath = Path.Combine(paths.Presets, "default-price-tag.tpspreset");
        var mojibakePreset = Preset(Field("name", "Name", 0));
        mojibakePreset.PresetId = "default-price-tag";
        mojibakePreset.PresetName = "繝励Μ繧ｻ繝ット";
        mojibakePreset.Description = "繝壹Ξ繝｣繝ｼ";
        JsonFile.Save(presetPath, mojibakePreset);

        var layoutPath = Path.Combine(paths.Layouts, "default-price-tag.tpslayout");
        var mojibakeLayout = Layout();
        mojibakeLayout.LayoutId = "default-price-tag-layout";
        mojibakeLayout.LayoutName = "縺ゅｋ繝ｬ繧､繧｢繧ｦ繝ト";
        mojibakeLayout.Description = "縺ゅｋ隱ｬ譏";
        JsonFile.Save(layoutPath, mojibakeLayout);

        new BootstrapService(paths).Initialize();

        var loadedPreset = JsonFile.Load<PresetFile>(presetPath);
        var loadedLayout = JsonFile.Load<LayoutFile>(layoutPath);
        Assert.Equal("default-price-tag", loadedPreset.PresetId);
        Assert.Equal("Default Price Tag", loadedPreset.PresetName);
        Assert.DoesNotContain("繝", loadedPreset.Description);
        Assert.Equal("default-price-tag-layout", loadedLayout.LayoutId);
        Assert.Equal("Default Price Tag Layout", loadedLayout.LayoutName);
        Assert.DoesNotContain("縺", loadedLayout.Description);

        var presetBackup = Assert.Single(Directory.GetFiles(paths.Presets, "default-price-tag.tpspreset.mojibake-backup-*"));
        var layoutBackup = Assert.Single(Directory.GetFiles(paths.Layouts, "default-price-tag.tpslayout.mojibake-backup-*"));
        Assert.Contains("繝", File.ReadAllText(presetBackup, Encoding.UTF8));
        Assert.Contains("縺", File.ReadAllText(layoutBackup, Encoding.UTF8));
    }

    [Fact]
    public void JsonFile_Load_MovesBrokenJsonAsideAndReturnsDefault()
    {
        JsonFile.ClearRecoveryWarnings();
        using var temp = new TempDirectory();
        var path = temp.WriteText("appsettings.json", "{ broken json", Encoding.UTF8);

        var settings = JsonFile.Load<AppSettings>(path);

        Assert.NotNull(settings);
        Assert.False(File.Exists(path));
        var brokenPath = Assert.Single(Directory.GetFiles(temp.DirectoryPath, "appsettings.json.broken-*"));
        Assert.Contains("{ broken json", File.ReadAllText(brokenPath, Encoding.UTF8));
        var warning = Assert.Single(JsonFile.RecoveryWarnings);
        Assert.Equal(path, warning.Path);
        Assert.Equal(brokenPath, warning.BrokenPath);
        JsonFile.ClearRecoveryWarnings();
    }

    [Fact]
    public void CsvColumnService_AddColumnToAllFiles_PreservesTabDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Item Code\tStyle\r\nA001\tCasual\r\n", new UTF8Encoding(false));
        var loader = new CsvLoaderService();
        var import = loader.LoadFiles([path], Preset(), hasHeaderRow: true);
        var file = Assert.Single(import.Files);

        new CsvColumnService().AddColumnToAllFiles([file], "tagPrint", hasHeaderRow: true);

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Item Code\tStyle\ttagPrint", content);
        Assert.DoesNotContain("Item Code,Style,tagPrint", content);
    }

    [Fact]
    public void TagFinderCsvService_SaveFlags_PreservesSemicolonDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Item Code;tagPrint\r\nA001;false\r\n", new UTF8Encoding(false));
        var loader = new CsvLoaderService();
        var import = loader.LoadFiles([path], Preset(), hasHeaderRow: true);
        var file = Assert.Single(import.Files);

        new TagFinderCsvService().SaveFlags(
            [file],
            [new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true, WasChecked = false }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Item Code;tagPrint;tagPrintOrder", content);
        Assert.Contains("A001;true;1", content);
        Assert.DoesNotContain("Item Code,tagPrint,tagPrintOrder", content);
    }

    [Fact]
    public void TagFinderCsvService_SaveFlags_PreservesTabDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText(
            "items.csv",
            "Item Code\tName\ttagPrint\r\nA001\tApple\tfalse\r\n",
            new UTF8Encoding(false));
        var loader = new CsvLoaderService();
        var import = loader.LoadFiles([path], Preset(), hasHeaderRow: true);
        var file = Assert.Single(import.Files);

        Assert.Equal("\t", file.Delimiter);

        new TagFinderCsvService().SaveFlags(
            [file],
            [new TagFinderSaveSelection { File = file, RowIndex = 0, Checked = true, WasChecked = false }],
            "tagPrint",
            "tagPrintOrder",
            hasHeaderRow: true);

        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("Item Code\tName\ttagPrint\ttagPrintOrder", content);
        Assert.Contains("A001\tApple\ttrue\t1", content);
        Assert.DoesNotContain("Item Code,Name,tagPrint,tagPrintOrder", content);
        Assert.DoesNotContain("A001,Apple,true,1", content);
    }

    [Fact]
    public void CsvFileUtility_WriteAllTextAtomic_ReplacesExistingFileWithoutLeavingTempFiles()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "old", Encoding.UTF8);

        CsvFileUtility.WriteAllTextAtomic(path, "new", Encoding.UTF8);

        Assert.Equal("new", File.ReadAllText(path, Encoding.UTF8));
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "*.tmp-*"));
    }

    [Fact]
    public void CsvFileUtility_WriteAllTextAtomic_CreatesNewFileWithoutLeavingTempFiles()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.DirectoryPath, "new.csv");

        CsvFileUtility.WriteAllTextAtomic(path, "created", Encoding.UTF8);

        Assert.Equal("created", File.ReadAllText(path, Encoding.UTF8));
        Assert.Empty(Directory.GetFiles(temp.DirectoryPath, "*.tmp-*"));
    }



    [Fact]
    public void PreviewRenderer_RendersCode128BarcodeWithoutException()
    {
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout();
        layout.Elements.Add(Barcode("barcode", "Code128", width: 32, height: 16));
        var rowData = new Dictionary<string, string> { ["barcode"] = "ABC-TEST-001" };

        using var bitmap = renderer.RenderSingleTag(layout, Preset(Field("barcode", "Barcode", 0)), rowData, scale: 2f);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    [Theory]
    [InlineData("18", 18)]
    [InlineData("2", 6)]
    [InlineData("100", 48)]
    [InlineData("", 10)]
    [InlineData("bad", 10)]
    public void PreviewRenderer_ResolvesFontSizeFromCsvFieldWithFallbackAndClamp(string csvValue, double expected)
    {
        var element = new LayoutElement
        {
            FontSize = 10,
            FontSizeSource = FontSizeSourceType.CsvField,
            FontSizeFieldId = "font_size",
            MinFontSize = 6,
            MaxFontSize = 48
        };
        var rowData = new Dictionary<string, string> { ["font_size"] = csvValue };

        var actual = InvokeResolveFontSize(element, rowData);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PreviewRenderer_UsesFixedFontSizeWhenFontSizeFieldIsMissing()
    {
        var element = new LayoutElement
        {
            FontSize = 12,
            FontSizeSource = FontSizeSourceType.CsvField,
            FontSizeFieldId = "font_size",
            MinFontSize = 6,
            MaxFontSize = 48
        };

        var actual = InvokeResolveFontSize(element, []);

        Assert.Equal(12, actual);
    }

    [Fact]
    public void PreviewRenderer_RendersEan13TwelveDigitValueWithoutException()
    {
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout(tagWidth: 50, tagHeight: 30);
        layout.Elements.Add(Barcode("barcode", "EAN13", width: 32, height: 22));
        var rowData = new Dictionary<string, string> { ["barcode"] = "490123456789" };

        using var bitmap = renderer.RenderSingleTag(layout, Preset(Field("barcode", "Barcode", 0)), rowData, scale: 2f);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void PreflightCheck_AllowsEan13TwelveDigitValueByCalculatingCheckDigit()
    {
        var service = new PreflightCheckService();
        var preset = Preset(Field("barcode", "Barcode", 0));
        var layout = Layout(tagWidth: 50, tagHeight: 30);
        layout.Elements.Add(Barcode("barcode", "EAN13", width: 32, height: 22));
        var csv = Csv("ean.csv", ["490123456789"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.False(result.HasIssues);
        Assert.Equal(1, result.PrintableRowCount);
    }

    [Fact]
    public void PreflightCheck_ReportsInvalidEan13Value()
    {
        var service = new PreflightCheckService();
        var preset = Preset(Field("barcode", "Barcode", 0));
        var layout = Layout(tagWidth: 50, tagHeight: 30);
        layout.Elements.Add(Barcode("barcode", "EAN13", width: 32, height: 22));
        var csv = Csv("ean.csv", ["49012345678X"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasIssues);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("EAN-13", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("JAN-13", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreviewRenderer_RendersQrCodeWithoutException()
    {
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout(tagWidth: 50, tagHeight: 50);
        layout.Elements.Add(Barcode("qr", "QRCode", width: 22, height: 22, showText: false));
        var rowData = new Dictionary<string, string> { ["qr"] = "https://example.com/products/qr01" };

        using var bitmap = renderer.RenderSingleTag(layout, Preset(Field("qr", "QR", 0)), rowData, scale: 2f);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void PreflightCheck_ReportsUndersizedBarcode()
    {
        var service = new PreflightCheckService();
        var preset = Preset(Field("barcode", "Barcode", 0));
        var layout = Layout();
        layout.Elements.Add(Barcode("barcode", "Code128", width: 10, height: 5));
        var csv = Csv("small.csv", ["ABC-TEST-001"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasIssues);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("recommended", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("推奨", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("小さ", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void PreflightCheck_ReportsInvalidImageFile()
    {
        using var temp = new TempDirectory();
        var imagePath = temp.WriteText("broken-image.png", "this is not a real image", Encoding.UTF8);
        var service = new PreflightCheckService();
        var layout = Layout();
        layout.Elements.Add(ImageElement(imagePath));
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasBlockingIssues);
        Assert.Contains(result.Errors, issue =>
            issue.Contains("Invalid image", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("画像ファイル", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreflightCheck_ReportsOversizedImageFileBeforeLoadingImage()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var imagePath = Path.Combine(temp.DirectoryPath, "huge-image.png");
        using (var stream = File.Create(imagePath))
        {
            stream.SetLength(20L * 1024 * 1024 + 1);
        }

        var service = new PreflightCheckService();
        var layout = Layout();
        layout.Elements.Add(ImageElement(imagePath));
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasBlockingIssues);
        Assert.Contains(result.Errors, issue =>
            issue.Contains("too large", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("大きすぎ", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void PreflightCheck_ReportsOversizedImageDimensions()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var imagePath = Path.Combine(temp.DirectoryPath, "wide-image.png");
        using (var bitmap = new Bitmap(12001, 1))
        {
            bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
        }

        var service = new PreflightCheckService();
        var layout = Layout();
        layout.Elements.Add(ImageElement(imagePath));
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasBlockingIssues);
        Assert.Contains(result.Errors, issue =>
            issue.Contains("dimensions", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("画像サイズ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreviewRenderer_SkipsInvalidImageWithoutThrowing()
    {
        using var temp = new TempDirectory();
        var imagePath = temp.WriteText("broken-image.png", "this is not a real image", Encoding.UTF8);
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout(tagWidth: 50, tagHeight: 50);
        layout.Elements.Add(ImageElement(imagePath));

        using var bitmap = renderer.RenderSingleTag(layout, Preset(Field("name", "Name", 0)), new Dictionary<string, string>(), scale: 2f);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    private static FieldDefinition Field(string id, string name, int? columnIndex, bool required = false) => new()
    {
        FieldId = id,
        DisplayName = name,
        ColumnIndex = columnIndex,
        Required = required,
        Enabled = true
    };

    private static PresetFile Preset(params FieldDefinition[] fields) => new()
    {
        PresetName = "Test",
        Fields = fields.ToList()
    };

    private static LayoutFile Layout(double tagWidth = 40, double tagHeight = 30, double margin = 0, double spacing = 0) => new()
    {
        LayoutName = "Test",
        Paper = new PaperSettings { PaperSize = PaperSizeType.A4 },
        Tag = new TagSettings { Width = tagWidth, Height = tagHeight },
        Margin = new MarginSettings { Top = margin, Right = margin, Bottom = margin, Left = margin },
        Spacing = new SpacingSettings { Horizontal = spacing, Vertical = spacing }
    };




    private static LayoutElement ImageElement(string imagePath) => new()
    {
        ElementId = Guid.NewGuid().ToString("N"),
        Name = "Image",
        ElementType = LayoutElementType.Image,
        ImagePath = imagePath,
        X = 2,
        Y = 2,
        Width = 20,
        Height = 20,
        Visible = true
    };

    private static LayoutElement Barcode(string fieldId, string barcodeType, double width, double height, bool showText = true) => new()
    {
        ElementId = Guid.NewGuid().ToString("N"),
        Name = "Barcode",
        ElementType = LayoutElementType.Barcode,
        FieldId = fieldId,
        BarcodeType = barcodeType,
        BarcodeShowText = showText,
        X = 2,
        Y = 2,
        Width = width,
        Height = height,
        FontSize = 8,
        Visible = true
    };

    private static CsvFileItem Csv(string fileName, params string[][] rows) => new()
    {
        FileName = fileName,
        FilePath = fileName,
        Status = CsvFileStatus.Ok,
        Rows = rows.ToList(),
        RowCount = rows.Length,
        ColumnCount = rows.FirstOrDefault()?.Length ?? 0
    };

    private static CsvFileItem CsvFile(string path, string[] header, params string[][] rows) => new()
    {
        FileName = Path.GetFileName(path),
        FilePath = path,
        EncodingName = "UTF-8",
        Status = CsvFileStatus.Ok,
        HeaderRow = header,
        Rows = rows.ToList(),
        RowCount = rows.Length,
        ColumnCount = header.Length
    };

    private static OutputOptions Options(OutputFormatType format, OutputScopeType scope) => new()
    {
        OutputFormat = format,
        OutputScope = scope,
        OutputFolder = Path.Combine(Path.GetTempPath(), "TagPrintStudioTests"),
        ConfirmOverwrite = false
    };

    private static int InvokeOutputServiceInt(string methodName, float value)
    {
        var method = typeof(OutputService).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (int)method.Invoke(null, [value])!;
    }

    private static double InvokeResolveFontSize(LayoutElement element, Dictionary<string, string> rowData)
    {
        var method = typeof(PreviewRenderService).GetMethod(
            "ResolveFontSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (double)method.Invoke(null, [element, rowData])!;
    }

    private static IEnumerable<List<Dictionary<string, string>>> InvokePages(
        OutputService service,
        LayoutFile layout,
        PresetFile preset,
        IEnumerable<CsvFileItem> files,
        PrintSortMode sortMode = PrintSortMode.LoadOrder,
        string sortFieldId = "",
        PrintSortValueType sortValueType = PrintSortValueType.Text)
    {
        var method = typeof(OutputService)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Single(method =>
            {
                if (method.Name != "Pages") return false;
                var parameters = method.GetParameters();
                return parameters.Length == 6 && parameters[2].ParameterType == typeof(IEnumerable<CsvFileItem>);
            });

        var result = method.Invoke(service, [layout, preset, files, sortMode, sortFieldId, sortValueType]);
        Assert.NotNull(result);
        return (IEnumerable<List<Dictionary<string, string>>>)result;
    }

    private static AppPaths TestAppPaths(TempDirectory temp) => new(
        Path.Combine(temp.DirectoryPath, "roaming"),
        Path.Combine(temp.DirectoryPath, "local"),
        Path.Combine(temp.DirectoryPath, "exports"));

    private sealed class TempDirectory : IDisposable
    {
        private readonly string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TagPrintStudioTests", Guid.NewGuid().ToString("N"));
        public string DirectoryPath => tempPath;

        public string WriteText(string fileName, string content, Encoding encoding)
        {
            Directory.CreateDirectory(tempPath);
            var filePath = System.IO.Path.Combine(tempPath, fileName);
            File.WriteAllText(filePath, content, encoding);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    private sealed class FixedRowsPerPageRenderer(int rowsPerPage) : IPreviewRenderService
    {
        public Bitmap RenderSingleTag(LayoutFile layout, PresetFile preset, Dictionary<string, string> rowData, float scale = 4f) => new(1, 1);
        public Bitmap RenderPage(LayoutFile layout, PresetFile preset, List<Dictionary<string, string>> rows, float scale = 3f) => new(1, 1);
        public (int Columns, int Rows) GetGrid(LayoutFile layout) => (rowsPerPage, 1);
        public int GetRowsPerPage(LayoutFile layout) => rowsPerPage;
    }

    private sealed class CancelAfterProgress(int completedCount, CancellationTokenSource cancellationTokenSource) : IProgress<OutputProgress>
    {
        public void Report(OutputProgress value)
        {
            if (value.Current >= completedCount)
            {
                cancellationTokenSource.Cancel();
            }
        }
    }
}
