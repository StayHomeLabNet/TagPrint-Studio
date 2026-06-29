using System.Drawing.Drawing2D;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public sealed partial class PreviewRenderService
{
    private static void DrawEan13Barcode(Graphics graphics, LayoutElement element, string ean13, RectangleF rect, float scale)
    {
        var textHeight = element.BarcodeShowText
            ? Math.Min(rect.Height * 0.28f, Math.Max(8f, (float)(element.FontSize * scale * 0.72) + 4f))
            : 0f;
        var barcodeRect = new RectangleF(rect.Left, rect.Top, rect.Width, Math.Max(1f, rect.Height - textHeight));
        var moduleWidth = barcodeRect.Width / 109f; // 95 data modules + 7 module quiet zone on each side
        var x = barcodeRect.Left + 7f * moduleWidth;

        var previousSmoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        using var brush = new SolidBrush(ParseColor(element.TextColor, Color.Black));

        void DrawBits(string bits, bool guard = false)
        {
            var barHeight = guard && element.BarcodeShowText
                ? Math.Min(barcodeRect.Height + textHeight * 0.28f, rect.Bottom - barcodeRect.Top)
                : barcodeRect.Height;
            foreach (var bit in bits)
            {
                if (bit == '1')
                {
                    graphics.FillRectangle(brush, x, barcodeRect.Top, Math.Max(1f, moduleWidth), Math.Max(1f, barHeight));
                }
                x += moduleWidth;
            }
        }

        DrawBits("101", guard: true);
        var parity = Ean13Parity[ean13[0] - '0'];
        for (var i = 1; i <= 6; i++)
        {
            var digit = ean13[i] - '0';
            DrawBits(parity[i - 1] == 'G' ? Ean13G[digit] : Ean13L[digit]);
        }

        DrawBits("01010", guard: true);
        for (var i = 7; i <= 12; i++)
        {
            DrawBits(Ean13R[ean13[i] - '0']);
        }
        DrawBits("101", guard: true);
        graphics.SmoothingMode = previousSmoothing;

        if (!element.BarcodeShowText || textHeight <= 0) return;

        var textRect = new RectangleF(rect.Left, barcodeRect.Bottom, rect.Width, textHeight);
        using var font = CreateSafeFont(element.FontFamily, Math.Max(1f, (float)(element.FontSize * scale * 0.72)), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        graphics.DrawString(ean13, font, brush, textRect, format);
    }

    private static bool TryNormalizeEan13(string value, out string ean13, out string reason)
    {
        ean13 = "";
        reason = "";
        var digits = value.Trim();
        if (digits.Length == 0)
        {
            reason = "empty";
            return false;
        }

        if (digits.Any(ch => !char.IsDigit(ch)))
        {
            reason = "digits";
            return false;
        }

        if (digits.Length == 12)
        {
            ean13 = digits + CalculateEan13CheckDigit(digits);
            return true;
        }

        if (digits.Length == 13)
        {
            var expected = CalculateEan13CheckDigit(digits[..12]);
            if (digits[12] != expected)
            {
                reason = "check";
                return false;
            }

            ean13 = digits;
            return true;
        }

        reason = "length";
        return false;
    }

    private static char CalculateEan13CheckDigit(string first12Digits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = first12Digits[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    private static readonly string[] Ean13Parity =
    [
        "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
        "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
    ];

    private static readonly string[] Ean13L =
    [
        "0001101", "0011001", "0010011", "0111101", "0100011",
        "0110001", "0101111", "0111011", "0110111", "0001011"
    ];

    private static readonly string[] Ean13G =
    [
        "0100111", "0110011", "0011011", "0100001", "0011101",
        "0111001", "0000101", "0010001", "0001001", "0010111"
    ];

    private static readonly string[] Ean13R =
    [
        "1110010", "1100110", "1101100", "1000010", "1011100",
        "1001110", "1010000", "1000100", "1001000", "1110100"
    ];

}
