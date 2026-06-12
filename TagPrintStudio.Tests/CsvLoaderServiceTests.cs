using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void CsvLoader_ParsesQuotedCsvAndHeaderRows()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("products.csv", "商品名,価格,コード\r\n\"りんご,大\",120,A001\r\n\"改行\r\n入り\",250,A002\r\n", Encoding.UTF8);
        var preset = Preset(
            Field("name", "商品名", 0, required: true),
            Field("price", "価格", 1, required: true),
            Field("code", "コード", 2));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.NotNull(file.HeaderRow);
        Assert.Equal(["商品名", "価格", "コード"], file.HeaderRow);
        Assert.Equal(2, file.RowCount);
        Assert.Equal("りんご,大", file.Rows[0][0]);
        Assert.Equal("改行\r\n入り", file.Rows[1][0]);
    }

    [Fact]
    public void CsvLoader_PreservesBlankRowsWhenLoadingAndSaving()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("blank-lines.csv", "Code,Name\r\nA001,Apple\r\n\r\nA002,Banana\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal(3, file.RowCount);
        Assert.Equal(["A001", "Apple"], file.Rows[0]);
        Assert.Empty(file.Rows[1]);
        Assert.Equal(["A002", "Banana"], file.Rows[2]);

        var content = CsvFileUtility.BuildCsvContent(file, hasHeaderRow: true);
        Assert.Contains($"A001,Apple{Environment.NewLine}{Environment.NewLine}A002,Banana", content);
    }

    [Fact]
    public void CsvLoader_BlankRowsAreNotTreatedAsPrintableDataRows()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("blank-print.csv", "Code,Name\r\nA001,Apple\r\n\r\nA002,Banana\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);
        var printableRows = PrintableRowService.PrintableRows(preset, [file]);

        Assert.Equal(2, printableRows.Count);
        Assert.DoesNotContain(printableRows, row => CsvFileUtility.IsBlankRow(row));
    }

    [Fact]
    public void CsvLoader_DetectsShiftJisAndReportsWarning()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("sjis.csv", "商品名,価格\r\nみかん,98\r\n", Encoding.GetEncoding(932));
        var preset = Preset(Field("name", "商品名", 0), Field("price", "価格", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal("Shift_JIS", file.EncodingName);
        Assert.Contains(result.Warnings, warning => warning.Contains("Shift_JIS"));
        Assert.Equal("みかん", file.Rows[0][0]);
    }

    [Fact]
    public void CsvLoader_DetectsUtf16LittleEndianBom()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("utf16le.csv", "Code,Name\r\nA001,りんご\r\n", Encoding.Unicode);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal("UTF-16 LE BOM", file.EncodingName);
        Assert.Equal(["Code", "Name"], file.HeaderRow);
        Assert.Equal("りんご", file.Rows[0][1]);
    }

    [Fact]
    public void CsvLoader_DetectsUtf16BigEndianBom()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("utf16be.csv", "Code,Name\r\nA001,りんご\r\n", Encoding.BigEndianUnicode);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal("UTF-16 BE BOM", file.EncodingName);
        Assert.Equal(["Code", "Name"], file.HeaderRow);
        Assert.Equal("りんご", file.Rows[0][1]);
    }

    [Fact]
    public void CsvFileUtility_WriteCsvFile_PreservesUtf16LittleEndianBom()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("utf16le-save.csv", "Code,Name\r\nA001,りんご\r\n", Encoding.Unicode);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));
        var file = Assert.Single(new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true).Files);

        CsvFileUtility.WriteCsvFile(file, hasHeaderRow: true, createBackup: false);

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 2);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);
        Assert.Equal("Code,Name", File.ReadLines(path, Encoding.Unicode).First());
    }

    [Fact]
    public void CsvFileUtility_WriteCsvFile_PreservesUtf16BigEndianBom()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("utf16be-save.csv", "Code,Name\r\nA001,りんご\r\n", Encoding.BigEndianUnicode);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));
        var file = Assert.Single(new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true).Files);

        CsvFileUtility.WriteCsvFile(file, hasHeaderRow: true, createBackup: false);

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 2);
        Assert.Equal(0xFE, bytes[0]);
        Assert.Equal(0xFF, bytes[1]);
        Assert.Equal("Code,Name", File.ReadLines(path, Encoding.BigEndianUnicode).First());
    }


    [Fact]
    public void CsvLoader_FlagsMissingRequiredColumn()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("missing.csv", "商品名\r\nりんご\r\n", Encoding.UTF8);
        var preset = Preset(
            Field("name", "商品名", 0, required: true),
            Field("price", "価格", 2, required: true));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.MissingRequiredField, file.Status);
        Assert.Contains("価格", file.Note);
    }

    [Theory]
    [InlineData("short.csv", "Code,Name,Price\r\nA001,Apple\r\n", 2)]
    [InlineData("long.csv", "Code,Name,Price\r\nB002,Banana,120,extra\r\n", 4)]
    public void CsvLoader_FlagsDataRowsWhoseColumnCountDoesNotMatchHeader(string fileName, string csvContent, int actualColumnCount)
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText(fileName, csvContent, Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1), Field("price", "Price", 2));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.ColumnMismatch, file.Status);
        Assert.Contains("期待値 3", file.Note);
        Assert.Contains($"実際 {actualColumnCount}", file.Note);
        Assert.Contains("行 2", file.Note);
    }

    [Fact]
    public void CsvLoader_AutoDetectsTabDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("tab.csv", "Code\tName\tPrice\r\nA001\tApple\t120\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1), Field("price", "Price", 2));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal(["Code", "Name", "Price"], file.HeaderRow);
        Assert.Equal(["A001", "Apple", "120"], file.Rows.Single());
        Assert.Equal("\t", file.Delimiter);
        Assert.Contains("Delimiter: Tab", file.EncodingDisplayName);
    }


    [Fact]
    public void CsvLoader_DetectsDelimiterFromMultipleRecordsWhenFirstRecordHasNoDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("semicolon-after-title.csv", "Title\r\nA;B;C\r\n1;2;3\r\n", Encoding.UTF8);
        var preset = Preset(Field("a", "A", 0), Field("b", "B", 1), Field("c", "C", 2));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(";", file.Delimiter);
    }

    [Fact]
    public void CsvLoader_IgnoresDelimitersInsideQuotesWhenDetectingDelimiter()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("quoted-commas-tab.csv", "Code\tName\tNote\r\nA001\tApple\t\"large, red\"\r\nA002\tBanana\t\"small, yellow\"\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1), Field("note", "Note", 2));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.Ok, file.Status);
        Assert.Equal("\t", file.Delimiter);
        Assert.Equal("large, red", file.Rows[0][2]);
    }

    [Fact]
    public void CsvLoader_ReportsMalformedQuotesWithLineNumber()
    {
        using var temp = new TempDirectory();
        var path = temp.WriteText("bad.csv", "Code,Name\r\nA001,\"Apple\r\n", Encoding.UTF8);
        var preset = Preset(Field("code", "Code", 0), Field("name", "Name", 1));

        var result = new CsvLoaderService().LoadFiles([path], preset, hasHeaderRow: true);
        var file = Assert.Single(result.Files);

        Assert.Equal(CsvFileStatus.LoadFailed, file.Status);
        Assert.Contains("line", file.Note, StringComparison.OrdinalIgnoreCase);
    }

}
