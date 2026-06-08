using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IUnitConversionService
{
    double Convert(double value, UnitType from, UnitType to, double dpi = 96.0);
    SizeF GetPaperSize(PaperSettings paper);
}

public sealed class UnitConversionService : IUnitConversionService
{
    public double Convert(double value, UnitType from, UnitType to, double dpi = 96.0)
    {
        var inches = from switch
        {
            UnitType.Mm => value / 25.4,
            UnitType.Px => value / dpi,
            UnitType.Inch => value,
            _ => value
        };
        return to switch
        {
            UnitType.Mm => inches * 25.4,
            UnitType.Px => inches * dpi,
            UnitType.Inch => inches,
            _ => value
        };
    }

    public SizeF GetPaperSize(PaperSettings paper)
    {
        if (paper.PaperSize == PaperSizeType.Custom)
        {
            var width = Convert(paper.CustomWidth ?? 210, paper.Unit, UnitType.Mm);
            var height = Convert(paper.CustomHeight ?? 297, paper.Unit, UnitType.Mm);
            return new SizeF((float)width, (float)height);
        }
        return paper.PaperSize switch
        {
            PaperSizeType.A5 => new SizeF(148, 210),
            PaperSizeType.Letter => new SizeF(215.9f, 279.4f),
            PaperSizeType.Postcard => new SizeF(100, 148),
            _ => new SizeF(210, 297)
        };
    }
}
