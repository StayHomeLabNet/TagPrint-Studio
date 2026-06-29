using System.Drawing.Drawing2D;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public sealed partial class PreviewRenderService
{
    private static void DrawBarcode(Graphics graphics, LayoutElement element, Dictionary<string, string> rowData, RectangleF rect, float scale, RenderLayerOptions layerOptions)
    {
        var rawText = string.IsNullOrWhiteSpace(element.FieldId)
            ? element.Text
            : rowData.GetValueOrDefault(element.FieldId, "");
        var value = (rawText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            DrawBarcodeWarning(graphics, element, rect, scale, LocalizationService.T("preview.barcode.empty"));
            return;
        }

        if (IsQrCode(element.BarcodeType))
        {
            DrawQrCode(graphics, element, value, rect, scale);
            return;
        }

        if (IsEan13Barcode(element.BarcodeType))
        {
            if (!TryNormalizeEan13(value, out var ean13, out _))
            {
                DrawBarcodeWarning(graphics, element, rect, scale, LocalizationService.T("preview.barcode.eanInvalid"));
                return;
            }

            DrawEan13Barcode(graphics, element, ean13, rect, scale);
            return;
        }

        if (!string.Equals(element.BarcodeType, "Code128", StringComparison.OrdinalIgnoreCase))
        {
            DrawBarcodeWarning(graphics, element, rect, scale, LocalizationService.T("preview.barcode.unsupportedType"));
            return;
        }

        var codes = EncodeCode128B(value);
        if (codes.Count == 0)
        {
            DrawBarcodeWarning(graphics, element, rect, scale, LocalizationService.T("preview.barcode.code128Invalid"));
            return;
        }

        var textHeight = element.BarcodeShowText
            ? Math.Min(rect.Height * 0.28f, Math.Max(8f, (float)(element.FontSize * scale * 0.72) + 4f))
            : 0f;
        var barcodeRect = new RectangleF(rect.Left, rect.Top, rect.Width, Math.Max(1f, rect.Height - textHeight));

        var patterns = codes.Select(code => Code128Patterns[code]).ToList();
        var moduleCount = patterns.Sum(pattern => pattern.Sum(ch => ch - '0')) + 20;
        var moduleWidth = barcodeRect.Width / Math.Max(1, moduleCount);
        var x = barcodeRect.Left + 10 * moduleWidth;

        var previousSmoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        using var brush = new SolidBrush(ParseColor(element.TextColor, Color.Black));
        foreach (var pattern in patterns)
        {
            var drawBar = true;
            foreach (var ch in pattern)
            {
                var width = (ch - '0') * moduleWidth;
                if (drawBar && width > 0)
                {
                    graphics.FillRectangle(brush, x, barcodeRect.Top, Math.Max(1f, width), barcodeRect.Height);
                }

                x += width;
                drawBar = !drawBar;
            }
        }
        graphics.SmoothingMode = previousSmoothing;

        if (!element.BarcodeShowText || !layerOptions.DrawRasterBarcodeText || textHeight <= 0) return;

        var textRect = new RectangleF(rect.Left, barcodeRect.Bottom, rect.Width, textHeight);
        using var font = CreateSafeFont(element.FontFamily, Math.Max(1f, (float)(element.FontSize * scale * 0.72)), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        graphics.DrawString(value, font, brush, textRect, format);
    }

    private static void DrawBarcodeWarning(Graphics graphics, LayoutElement element, RectangleF rect, float scale, string message)
    {
        using var brush = new SolidBrush(Color.FromArgb(130, 107, 114, 128));
        using var pen = new Pen(Color.FromArgb(110, 156, 163, 175), Math.Max(1f, scale * 0.12f))
        {
            DashStyle = DashStyle.Dash
        };
        using var font = CreateSafeFont(
            element.FontFamily,
            Math.Max(8f, (float)(element.FontSize * scale * 0.72)),
            FontStyle.Regular,
            GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        graphics.DrawString(message, font, brush, rect, format);
    }


    private static bool IsQrCode(string? barcodeType)
    {
        return string.Equals(barcodeType, "QRCode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR Code", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEan13Barcode(string? barcodeType)
    {
        return string.Equals(barcodeType, "EAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13 / JAN-13", StringComparison.OrdinalIgnoreCase);
    }
}
