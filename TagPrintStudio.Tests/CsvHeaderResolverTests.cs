using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Theory]
    [InlineData("tagPrintOrder")]
    [InlineData("tag_print_order")]
    [InlineData("tag print order")]
    [InlineData(" Tag Print Order ")]
    public void CsvHeaderResolver_FindIndex_UsesSharedNormalizedHeaderMatching(string lookupName)
    {
        var header = new[] { "Code", "Tag_Print_Order" };

        var index = CsvHeaderResolver.FindIndex(header, lookupName);

        Assert.Equal(1, index);
    }

    [Fact]
    public void CsvHeaderResolver_FindIndex_CanResolveCsvFileHeaders()
    {
        var file = new CsvFileItem
        {
            HeaderRow = ["Code", "tag_print"],
            Rows = [["A001", "true"]]
        };

        var index = CsvHeaderResolver.FindIndex(file, "tagPrint");

        Assert.Equal(1, index);
    }

    [Fact]
    public void PrintTargetOrderColumnResolver_UsesCsvHeaderResolverMatching()
    {
        var preset = new PresetFile();
        var file = new CsvFileItem
        {
            HeaderRow = ["Code", "tag_print_order"],
            Rows = [["A001", "1"]]
        };

        var index = PrintTargetOrderColumnResolver.FindColumnIndex(preset, file, "tagPrintOrder");

        Assert.Equal(1, index);
    }

    [Theory]
    [InlineData("ｔａｇ　ｐｒｉｎｔ　ｏｒｄｅｒ")]
    [InlineData("tag-print-order")]
    [InlineData("tag－print－order")]
    public void CsvHeaderResolver_FindIndex_NormalizesFullWidthSpacesAndHyphens(string lookupName)
    {
        var header = new[] { "Code", "Tag_Print_Order" };

        var index = CsvHeaderResolver.FindIndex(header, lookupName);

        Assert.Equal(1, index);
    }

}
