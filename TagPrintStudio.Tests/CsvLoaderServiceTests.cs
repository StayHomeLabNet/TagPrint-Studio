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
        Assert.Contains("Delimiter: Tab", file.EncodingName);
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

    [Theory]
    [InlineData(PrintConditionType.Number, PrintConditionOperator.GreaterThanOrEqual, "100", "100", true)]
    [InlineData(PrintConditionType.Number, PrintConditionOperator.LessThan, "100", "99", true)]
    [InlineData(PrintConditionType.Bool, PrintConditionOperator.IsTrue, "", "yes", true)]
}
