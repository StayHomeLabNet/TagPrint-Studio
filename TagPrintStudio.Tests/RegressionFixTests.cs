using System.Drawing;
using System.IO.Compression;
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
}
