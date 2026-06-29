using System.Drawing.Drawing2D;
using TagPrintStudio.Models;
using QRCoder;

namespace TagPrintStudio.Services;

public sealed partial class PreviewRenderService
{
    private static void DrawQrCode(Graphics graphics, LayoutElement element, string value, RectangleF rect, float scale)
    {
        var textHeight = element.BarcodeShowText
            ? Math.Min(rect.Height * 0.24f, Math.Max(8f, (float)(element.FontSize * scale * 0.72) + 4f))
            : 0f;
        var qrArea = new RectangleF(rect.Left, rect.Top, rect.Width, Math.Max(1f, rect.Height - textHeight));
        var size = Math.Max(1f, Math.Min(qrArea.Width, qrArea.Height));
        var qrRect = new RectangleF(
            qrArea.Left + (qrArea.Width - size) / 2f,
            qrArea.Top + (qrArea.Height - size) / 2f,
            size,
            size);

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(data);
            using var bitmap = qrCode.GetGraphic(
                pixelsPerModule: 20,
                darkColor: ParseColor(element.TextColor, Color.Black),
                lightColor: Color.White,
                drawQuietZones: true);

            var previousInterpolation = graphics.InterpolationMode;
            var previousSmoothing = graphics.SmoothingMode;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.DrawImage(bitmap, qrRect);
            graphics.InterpolationMode = previousInterpolation;
            graphics.SmoothingMode = previousSmoothing;
        }
        catch
        {
            DrawBarcodeWarning(graphics, element, rect, scale, LocalizationService.T("preview.barcode.qrInvalid"));
            return;
        }

        if (!element.BarcodeShowText || textHeight <= 0) return;

        var textRect = new RectangleF(rect.Left, qrArea.Bottom, rect.Width, textHeight);
        using var font = CreateSafeFont(element.FontFamily, Math.Max(1f, (float)(element.FontSize * scale * 0.72)), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ParseColor(element.TextColor, Color.Black));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        graphics.DrawString(value, font, brush, textRect, format);
    }
}
