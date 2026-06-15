using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public sealed partial class PreviewRenderService
{
    private static void DrawText(Graphics graphics, LayoutElement element, string text, RectangleF rect, float scale)
    {
        DrawText(graphics, element, text, rect, scale, element.FontSize);
    }

    private static void DrawText(Graphics graphics, LayoutElement element, string text, RectangleF rect, float scale, double fontSize)
    {
        using var font = new Font(element.FontFamily, (float)(fontSize * scale * 0.72), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ParseColor(element.TextColor, Color.Black));
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        format.Alignment = element.TextAlign switch
        {
            TextAlignType.Center => StringAlignment.Center,
            TextAlignType.Right => StringAlignment.Far,
            _ => StringAlignment.Near
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private static void DrawPriceValue(Graphics graphics, LayoutElement element, Dictionary<string, string> rowData, RectangleF rect, float scale)
    {
        DrawPriceValue(graphics, element, rowData, rect, scale, element.FontSize);
    }

    private static void DrawPriceValue(Graphics graphics, LayoutElement element, Dictionary<string, string> rowData, RectangleF rect, float scale, double fontSize)
    {
        var rawText = string.IsNullOrWhiteSpace(element.FieldId)
            ? element.Text
            : rowData.GetValueOrDefault(element.FieldId, "");

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        DrawPriceValue(graphics, element, rawText, rect, scale, fontSize);
    }

    private static void DrawPriceValue(Graphics graphics, LayoutElement element, string rawText, RectangleF rect, float scale)
    {
        DrawPriceValue(graphics, element, rawText, rect, scale, element.FontSize);
    }

    private static void DrawPriceValue(Graphics graphics, LayoutElement element, string rawText, RectangleF rect, float scale, double fontSize)
    {
        var normalized = NormalizePriceText(rawText, element.DecimalPlaces);
        if ((element.DecimalDisplayStyle != DecimalDisplayStyle.SmallFractionBottom &&
             element.DecimalDisplayStyle != DecimalDisplayStyle.SmallFractionTop) ||
            !TrySplitPrice(normalized, out var integerPart, out var fractionPart))
        {
            DrawText(graphics, element, normalized, rect, scale, fontSize);
            return;
        }

        using var wholeFont = new Font(element.FontFamily, (float)(fontSize * scale * 0.72), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var fractionFont = new Font(
            element.FontFamily,
            Math.Max(1f, (float)(fontSize * scale * 0.72 * element.FractionFontScale)),
            element.Bold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ParseColor(element.TextColor, Color.Black));
        using var format = new StringFormat(StringFormat.GenericTypographic);

        var wholeSize = graphics.MeasureString(integerPart, wholeFont, PointF.Empty, format);
        var fractionSize = graphics.MeasureString(fractionPart, fractionFont, PointF.Empty, format);
        var totalWidth = wholeSize.Width + fractionSize.Width;
        var x = rect.Left;
        if (element.TextAlign == TextAlignType.Center)
        {
            x = rect.Left + (rect.Width - totalWidth) / 2f;
        }
        else if (element.TextAlign == TextAlignType.Right)
        {
            x = rect.Right - totalWidth;
        }

        var wholeAscent = FontAscentPixels(wholeFont);
        var fractionAscent = FontAscentPixels(fractionFont);
        var wholeTop = rect.Top + (rect.Height - wholeSize.Height) / 2f;
        var baselineY = wholeTop + wholeAscent;
        var fractionTop = element.DecimalDisplayStyle switch
        {
            DecimalDisplayStyle.SmallFractionTop => wholeTop,
            DecimalDisplayStyle.SmallFractionBottom => wholeTop + wholeSize.Height - fractionSize.Height,
            _ => baselineY - fractionAscent
        };
        fractionTop += (float)(element.FractionYOffset * scale);

        graphics.DrawString(integerPart, wholeFont, brush, new PointF(x, wholeTop), format);
        graphics.DrawString(
            fractionPart,
            fractionFont,
            brush,
            new PointF(x + wholeSize.Width, fractionTop),
            format);
    }

    private static string NormalizePriceText(string rawText, int decimalPlaces)
    {
        var text = (rawText ?? "").Trim();
        if (decimalPlaces < 0) return text;

        if (!decimal.TryParse(text, NumberStyles.Number | NumberStyles.Currency, CultureInfo.InvariantCulture, out var value) &&
            !decimal.TryParse(text, NumberStyles.Number | NumberStyles.Currency, CultureInfo.CurrentCulture, out value))
        {
            return text;
        }

        var format = decimalPlaces == 0 ? "0" : "0." + new string('0', decimalPlaces);
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static bool TrySplitPrice(string text, out string integerPart, out string fractionPart)
    {
        var dotIndex = text.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= text.Length - 1)
        {
            integerPart = text;
            fractionPart = "";
            return false;
        }

        integerPart = text[..dotIndex];
        fractionPart = text[dotIndex..];
        return true;
    }

    private static float FontAscentPixels(Font font)
    {
        var family = font.FontFamily;
        var style = font.Style;
        var ascent = family.GetCellAscent(style);
        var emHeight = family.GetEmHeight(style);
        return emHeight == 0 ? font.Size : font.Size * ascent / emHeight;
    }

    private static string ResolveImagePath(LayoutFile layout, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        if (Path.IsPathRooted(imagePath)) return imagePath;

        var layoutDirectory = Path.GetDirectoryName(layout.FilePath);
        return string.IsNullOrWhiteSpace(layoutDirectory)
            ? imagePath
            : Path.GetFullPath(Path.Combine(layoutDirectory, imagePath));
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim().TrimStart('#');
        if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
        if (text.Length == 8 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return Color.FromArgb((argb >> 24) & 0xFF, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
        }
        return fallback;
    }
}
