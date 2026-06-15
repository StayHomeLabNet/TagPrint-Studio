using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void PreflightCheck_IgnoresHiddenInvalidImageElement()
    {
        using var temp = new TempDirectory();
        var imagePath = temp.WriteText("hidden-broken-image.png", "not an image", Encoding.UTF8);
        var service = new PreflightCheckService();
        var layout = Layout();
        var image = ImageElement(imagePath);
        image.Visible = false;
        layout.Elements.Add(image);
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.False(result.HasIssues);
    }

    [Fact]
    public void PreflightCheck_IgnoresHiddenOutOfBoundsElement()
    {
        var service = new PreflightCheckService();
        var layout = Layout(tagWidth: 40, tagHeight: 30);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "hidden-outside",
            Name = "Hidden Outside",
            ElementType = LayoutElementType.StaticText,
            X = 100,
            Y = 100,
            Width = 20,
            Height = 20,
            Visible = false
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.False(result.HasIssues);
    }


    [Fact]
    public void PreflightCheck_ReportsVisibleInvalidElementGeometry()
    {
        var service = new PreflightCheckService();
        var layout = Layout(tagWidth: 40, tagHeight: 30);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "visible-invalid",
            Name = "Visible Invalid",
            ElementType = LayoutElementType.StaticText,
            X = -1,
            Y = 2,
            Width = 0,
            Height = 10,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.True(result.HasBlockingIssues);
        Assert.Contains(result.Errors, issue =>
            issue.Contains("invalid size", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("サイズ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, issue =>
            issue.Contains("negative", StringComparison.OrdinalIgnoreCase) ||
            issue.Contains("負", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreflightCheck_WarnsWhenVisibleFontIsNotInstalled()
    {
        var service = new PreflightCheckService();
        var missingFont = "FontThatShouldNotExist_TagPrintStudio_12345";
        var layout = Layout(tagWidth: 40, tagHeight: 30);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "visible-text",
            Name = "Visible Text",
            ElementType = LayoutElementType.StaticText,
            X = 2,
            Y = 2,
            Width = 10,
            Height = 10,
            FontFamily = missingFont,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv]);

        Assert.False(result.HasBlockingIssues);
        Assert.Contains(result.Warnings, issue => issue.Contains(missingFont, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CsvLoader_RejectsOversizedCsvBeforeReadingContent()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var path = Path.Combine(temp.DirectoryPath, "huge.csv");
        using (var stream = File.Create(path))
        {
            stream.SetLength(100L * 1024 * 1024 + 1);
        }
        var preset = Preset(Field("name", "Name", 0));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.LoadFailed, file.Status);
        Assert.True(file.Note.Contains("too large", StringComparison.OrdinalIgnoreCase) || file.Note.Contains("大きすぎ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CsvLoader_RejectsWhenDataRowsExceedConfiguredLimit()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("limited.csv", "Code,Name\r\nA001,Apple\r\n\r\nA002,Banana\r\nA003,Cherry\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles(
            [path],
            preset,
            hasHeaderRow: true,
            new CsvLoadOptions { MaxDataRowsPerFile = 2 });
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.LoadFailed, file.Status);
        Assert.Contains("2", file.Note);
    }

    [Fact]
    public void SettingsArchiveService_ImportBackup_RejectsTooManyTotalEntriesEvenOutsideAllowedRoots()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var zipPath = Path.Combine(temp.DirectoryPath, "many-unrelated.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("layouts/");
            archive.CreateEntry("layouts/default.tpslayout");
            for (var i = 0; i < 5000; i++)
            {
                archive.CreateEntry($"unrelated/file-{i:D4}.txt");
            }
        }

        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsArchiveService(TestAppPaths(temp)).ImportBackup(zipPath));

        Assert.True(ex.Message.Contains("too many", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("多すぎ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CsvMerge_Apply_RejectsFilesChangedAfterPreview()
    {
        using var temp = new TempDirectory();
        var parentPath = temp.WriteText("parent.csv", "Code,Price\r\nA100,120\r\n", Encoding.UTF8);
        var childPath = temp.WriteText("child.csv", "Code,Price\r\nB200,80\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("price", "Price", 1));
        var service = new CsvMergeService();
        var preview = service.Preview(new CsvMergeOptions
        {
            FolderPath = temp.DirectoryPath,
            ParentCsvPath = parentPath,
            HasHeaderRow = true
        }, preset);
        File.AppendAllText(childPath, "C300,90\r\n", Encoding.UTF8);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Apply(preview, preset));

        Assert.True(ex.Message.Contains("preview", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("プレビュー", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CsvFileUtility_BuildCsvContent_PreservesLoadedLfNewlines()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("lf.csv", "Code,Name\nA001,Apple\nA002,Banana\n", new UTF8Encoding(false));
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));
        var file = Assert.Single(new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true).Files);

        var content = CsvFileUtility.BuildCsvContent(file, hasHeaderRow: true);

        Assert.Contains("Code,Name\nA001,Apple\nA002,Banana\n", content);
        Assert.DoesNotContain("\r\n", content);
    }

    [Fact]
    public void SimplePdfWriter_EmbedsPagesAsLosslessFlateImages()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var pdfPath = Path.Combine(temp.DirectoryPath, "lossless.pdf");
        using var bitmap = new Bitmap(2, 2);

        SimplePdfWriter.SaveImages(pdfPath, [bitmap], new SizeF(10, 10));
        var pdfText = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath));

        Assert.Contains("/FlateDecode", pdfText);
        Assert.DoesNotContain("/DCTDecode", pdfText);
    }

    [Fact]
    public void VectorTextPdfWriter_CreatesPdfWhenSelected()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var service = new OutputService(new FixedRowsPerPageRenderer(1), new UnitConversionService());
        var layout = Layout(tagWidth: 40, tagHeight: 20);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "name",
            Name = "Name",
            ElementType = LayoutElementType.FieldText,
            FieldId = "name",
            X = 2,
            Y = 2,
            Width = 30,
            Height = 8,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["Apple"]);
        var options = Options(OutputFormatType.Pdf, OutputScopeType.SinglePdfAllCsv);
        options.OutputFolder = temp.DirectoryPath;
        options.PdfRenderMode = PdfRenderMode.VectorText;

        service.ExportPdf(layout, preset, [csv], options);
        var pdfPath = Path.Combine(temp.DirectoryPath, "TagPrintStudio_Output.pdf");
        var signature = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath).Take(4).ToArray());

        Assert.Equal("%PDF", signature);
    }

    [Fact]
    public void OutputService_VectorTextPdf_RendersRasterBackgroundWithoutTextLayers()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var renderer = new RecordingRenderer(rowsPerPage: 1);
        var service = new OutputService(renderer, new UnitConversionService());
        var layout = Layout(tagWidth: 40, tagHeight: 20);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "name",
            Name = "Name",
            ElementType = LayoutElementType.FieldText,
            FieldId = "name",
            X = 2,
            Y = 2,
            Width = 30,
            Height = 8,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["Apple"]);
        var options = Options(OutputFormatType.Pdf, OutputScopeType.SinglePdfAllCsv);
        options.OutputFolder = temp.DirectoryPath;
        options.PdfRenderMode = PdfRenderMode.VectorText;

        service.ExportPdf(layout, preset, [csv], options);

        var layerOptions = Assert.Single(renderer.PageLayerOptions);
        Assert.NotNull(layerOptions);
        Assert.False(layerOptions!.DrawRasterText);
        Assert.False(layerOptions.DrawRasterPrice);
    }

    [Fact]
    public void VectorTextPdfWriter_EmbedsUnicodeFontForJapaneseText()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.DirectoryPath);
        var service = new OutputService(new FixedRowsPerPageRenderer(1), new UnitConversionService());
        var layout = Layout(tagWidth: 40, tagHeight: 20);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "name",
            Name = "Name",
            ElementType = LayoutElementType.FieldText,
            FieldId = "name",
            FontFamily = "Yu Gothic UI",
            FontSize = 10,
            X = 2,
            Y = 2,
            Width = 34,
            Height = 8,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["日本語ABC"]);
        var options = Options(OutputFormatType.Pdf, OutputScopeType.SinglePdfAllCsv);
        options.OutputFolder = temp.DirectoryPath;
        options.PdfRenderMode = PdfRenderMode.VectorText;

        service.ExportPdf(layout, preset, [csv], options);
        var pdfPath = Path.Combine(temp.DirectoryPath, "TagPrintStudio_Output.pdf");
        var pdfText = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath));

        Assert.Contains("/ToUnicode", pdfText);
    }

    [Fact]
    public void VectorTextPdfWriter_ConvertsLayoutFontSizeToPdfPoints()
    {
        var method = typeof(VectorTextPdfWriter).GetMethod("PdfFontPointSize", BindingFlags.NonPublic | BindingFlags.Static);

        var actual = Assert.IsType<double>(method!.Invoke(null, [10d]));

        Assert.Equal(PdfSharp.Drawing.XUnit.FromMillimeter(7.2).Point, actual, 3);
    }

    [Fact]
    public void VectorTextPdfWriter_PrefersJapaneseFontsForCjkText()
    {
        var method = typeof(VectorTextPdfWriter).GetMethod("FontCandidates", BindingFlags.NonPublic | BindingFlags.Static);

        var candidates = Assert.IsAssignableFrom<IEnumerable<string>>(method!.Invoke(null, ["Arial", "\u65e5\u672c\u8a9eABC"])).ToList();

        Assert.NotEmpty(candidates);
        Assert.NotEqual("Arial", candidates[0]);
        Assert.Contains("Noto", candidates[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(candidates, candidate => candidate.Contains("Noto", StringComparison.OrdinalIgnoreCase) || candidate.Contains("Gothic", StringComparison.OrdinalIgnoreCase) || candidate.Contains("Meiryo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowsPdfFontResolver_EmbedsRegisteredTtfFontDirectly()
    {
        var resolution = WindowsPdfFontResolver.DescribeFamilyResolution("Arial");

        Assert.True(resolution.IsDirectlyEmbeddable);
        Assert.True(resolution.UsesRequestedFont);
    }

    [Fact]
    public void PreflightCheck_WarnsWhenVectorPdfFontFallsBackFromTtc()
    {
        var service = new PreflightCheckService();
        var layout = Layout(tagWidth: 40, tagHeight: 30);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "jp",
            Name = "Japanese",
            ElementType = LayoutElementType.StaticText,
            Text = "\u65e5\u672c\u8a9e",
            FontFamily = "Yu Gothic UI",
            X = 2,
            Y = 2,
            Width = 30,
            Height = 10,
            Visible = true
        });
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"]);

        var result = service.Check(layout, preset, [csv], pdfRenderMode: PdfRenderMode.VectorText);

        Assert.Contains(result.Warnings, warning =>
            (warning.Contains("Vector PDF", StringComparison.OrdinalIgnoreCase) ||
             warning.Contains("ベクターPDF", StringComparison.OrdinalIgnoreCase)) &&
            warning.Contains("Yu Gothic UI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VectorTextPdfWriter_WrapsTextToFitRectWidth()
    {
        EnsureVectorTextFontResolver();
        using var document = new PdfSharp.Pdf.PdfDocument();
        var page = document.AddPage();
        using var graphics = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
        var font = new PdfSharp.Drawing.XFont("Arial", 12, PdfSharp.Drawing.XFontStyleEx.Regular, PdfSharp.Drawing.XPdfFontOptions.UnicodeDefault);
        var method = typeof(VectorTextPdfWriter).GetMethod("WrapText", BindingFlags.NonPublic | BindingFlags.Static);
        const double maxWidth = 70;

        var lines = Assert.IsAssignableFrom<List<string>>(method!.Invoke(null, [graphics, "Linden Hand Cream 100ML Pink Petal", font, maxWidth]));

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(graphics.MeasureString(line, font).Width <= maxWidth + 0.01));
    }

    [Fact]
    public void VectorTextPdfWriter_CalculatesPositiveFontAscent()
    {
        EnsureVectorTextFontResolver();
        var font = new PdfSharp.Drawing.XFont("Arial", 12, PdfSharp.Drawing.XFontStyleEx.Regular, PdfSharp.Drawing.XPdfFontOptions.UnicodeDefault);
        var method = typeof(VectorTextPdfWriter).GetMethod("FontAscentPoints", BindingFlags.NonPublic | BindingFlags.Static);

        var ascent = Assert.IsType<double>(method!.Invoke(null, [font]));

        Assert.InRange(ascent, 1, font.Size);
    }

    private static void EnsureVectorTextFontResolver()
    {
        var method = typeof(VectorTextPdfWriter).GetMethod("EnsureFontResolver", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, []);
    }

    [Fact]
    public void CsvFileUtility_SnapshotMatchesDetectsSameSizeSameTimestampContentChange()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "A001\n", Encoding.UTF8);
        var snapshot = CsvFileUtility.CreateSnapshot(path);

        File.WriteAllText(path, "B002\n", Encoding.UTF8);
        File.SetLastWriteTimeUtc(path, snapshot.LastWriteTimeUtc);

        Assert.False(CsvFileUtility.SnapshotMatches(snapshot, path));
    }

    [Fact]
    public void CsvFileUtility_WriteCsvFileUpdatesSnapshot()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("items.csv", "Code\r\nA001\r\n", Encoding.UTF8);
        var file = Assert.Single(new CsvLoaderService().LoadFiles([path], Preset(Field("code", "Code", 0)), hasHeaderRow: true).Files);
        var originalHash = file.Snapshot.Sha256;
        file.Rows[0][0] = "B002";

        CsvFileUtility.WriteCsvFile(file, hasHeaderRow: true, createBackup: false);

        Assert.False(string.IsNullOrWhiteSpace(file.Snapshot.Sha256));
        Assert.NotEqual(originalHash, file.Snapshot.Sha256);
        Assert.True(CsvFileUtility.SnapshotMatches(file.Snapshot, path));
    }

    [Fact]
    public void TagFinderService_SearchIgnoresNullCellsAndRows()
    {
        var csv = Csv("items.csv", ["A001", "false"], null!, [null!, "true"], ["B002", "true"]);
        csv.HeaderRow = ["Code", "tagPrint"];

        var matches = new TagFinderService().Search([csv], columnIndex: 0, query: "B002", flagColumnName: "tagPrint", autoCheckMatches: false);

        var match = Assert.Single(matches);
        Assert.Equal(3, match.RowIndex);
        Assert.True(match.Checked);
    }

    [Fact]
    public void TagFinderWorkflowService_FindUnusedQueueCsvFilesIgnoresNullCellsAndRows()
    {
        var queue = Csv("print_queue_20260603_120000.csv", ["A001", null!], null!);
        queue.HeaderRow = ["Code", "tagPrint"];

        var unused = new TagFinderWorkflowService().FindUnusedQueueCsvFiles([queue], "tagPrint").ToList();

        Assert.Equal(queue.FilePath, Assert.Single(unused));
    }

    [Fact]
    public void PrintableRowService_PreviewRowsHonorsConfiguredLimit()
    {
        var preset = Preset(Field("name", "Name", 0));
        var csv = Csv("items.csv", ["A"], ["B"], ["C"]);

        var rows = PrintableRowService.PreviewRows(preset, [csv], hasHeaderRow: true, maxRows: 2);

        Assert.Equal(2, rows.Count);
        Assert.Equal("A", rows[0].Values[0]);
        Assert.Equal("B", rows[1].Values[0]);
    }

    [Fact]
    public void OutputService_MapRow_CoercesNullCsvCellsToEmptyStrings()
    {
        var preset = Preset(Field("name", "Name", 0), Field("barcode", "Barcode", 1));

        var row = OutputService.MapRow(preset, ["Apple", null!]);

        Assert.Equal("Apple", row["name"]);
        Assert.Equal("", row["barcode"]);
    }

    [Fact]
    public void PreviewRenderer_DoesNotThrowWhenRowDataContainsNullValues()
    {
        var renderer = new PreviewRenderService(new UnitConversionService());
        var layout = Layout(tagWidth: 50, tagHeight: 50);
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "text",
            Name = "Text",
            ElementType = LayoutElementType.FieldText,
            FieldId = "name",
            X = 2,
            Y = 2,
            Width = 20,
            Height = 8,
            Visible = true
        });
        layout.Elements.Add(new LayoutElement
        {
            ElementId = "price",
            Name = "Price",
            ElementType = LayoutElementType.PriceValue,
            FieldId = "price",
            X = 2,
            Y = 12,
            Width = 20,
            Height = 8,
            Visible = true
        });
        layout.Elements.Add(Barcode("barcode", "Code128", width: 32, height: 16));
        var rowData = new Dictionary<string, string>
        {
            ["name"] = null!,
            ["price"] = null!,
            ["barcode"] = null!
        };

        using var bitmap = renderer.RenderSingleTag(
            layout,
            Preset(Field("name", "Name", 0), Field("price", "Price", 1), Field("barcode", "Barcode", 2)),
            rowData,
            scale: 2f);

        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }
}
