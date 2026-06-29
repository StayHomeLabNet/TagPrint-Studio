using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IPreviewRenderService
{
    Bitmap RenderSingleTag(LayoutFile layout, PresetFile preset, Dictionary<string, string> rowData, float scale = 4f, RenderLayerOptions? layerOptions = null);
    Bitmap RenderPage(LayoutFile layout, PresetFile preset, List<Dictionary<string, string>> rows, float scale = 3f, RenderLayerOptions? layerOptions = null);
    (int Columns, int Rows) GetGrid(LayoutFile layout);
    int GetRowsPerPage(LayoutFile layout);
}

public sealed record RenderLayerOptions(
    bool DrawRasterText = true,
    bool DrawRasterPrice = true,
    bool DrawRasterBarcodeText = true);

public sealed partial class PreviewRenderService(IUnitConversionService units) : IPreviewRenderService
{
    public Bitmap RenderSingleTag(LayoutFile layout, PresetFile preset, Dictionary<string, string> rowData, float scale = 4f, RenderLayerOptions? layerOptions = null)
    {
        layerOptions ??= new RenderLayerOptions();
        var width = Math.Max(1, (int)Math.Ceiling(layout.Tag.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(layout.Tag.Height * scale));
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        Prepare(graphics);
        graphics.Clear(Color.White);
        DrawTag(graphics, layout, rowData, new RectangleF(0, 0, width, height), scale, layerOptions);
        graphics.DrawRectangle(Pens.Gray, 0, 0, width - 1, height - 1);
        return bitmap;
    }

    public Bitmap RenderPage(LayoutFile layout, PresetFile preset, List<Dictionary<string, string>> rows, float scale = 3f, RenderLayerOptions? layerOptions = null)
    {
        layerOptions ??= new RenderLayerOptions();
        var paper = units.GetPaperSize(layout.Paper);
        var width = Math.Max(1, (int)Math.Ceiling(paper.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(paper.Height * scale));
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        Prepare(graphics);
        graphics.Clear(Color.White);
        var (columns, pageRows) = CalculateGrid(layout, paper);
        var index = 0;
        for (var y = 0; y < pageRows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (index >= rows.Count) return bitmap;
                var left = (float)((layout.Margin.Left + x * (layout.Tag.Width + layout.Spacing.Horizontal)) * scale);
                var top = (float)((layout.Margin.Top + y * (layout.Tag.Height + layout.Spacing.Vertical)) * scale);
                var rect = new RectangleF(left, top, (float)(layout.Tag.Width * scale), (float)(layout.Tag.Height * scale));
                DrawTag(graphics, layout, rows[index], rect, scale, layerOptions);
                graphics.DrawRectangle(Pens.LightGray, rect.X, rect.Y, rect.Width, rect.Height);
                index++;
            }
        }
        return bitmap;
    }

    public (int Columns, int Rows) CalculateGrid(LayoutFile layout, SizeF paper)
    {
        if (!layout.Grid.AutoCalculate && layout.Grid.Columns > 0 && layout.Grid.Rows > 0)
        {
            return (layout.Grid.Columns, layout.Grid.Rows);
        }
        var availableWidth = paper.Width - layout.Margin.Left - layout.Margin.Right + layout.Spacing.Horizontal;
        var availableHeight = paper.Height - layout.Margin.Top - layout.Margin.Bottom + layout.Spacing.Vertical;
        var columns = Math.Max(1, (int)Math.Floor(availableWidth / (layout.Tag.Width + layout.Spacing.Horizontal)));
        var rows = Math.Max(1, (int)Math.Floor(availableHeight / (layout.Tag.Height + layout.Spacing.Vertical)));
        return (columns, rows);
    }

    public int GetRowsPerPage(LayoutFile layout)
    {
        var (columns, rows) = GetGrid(layout);
        return Math.Max(1, columns * rows);
    }

    public (int Columns, int Rows) GetGrid(LayoutFile layout)
    {
        var paper = units.GetPaperSize(layout.Paper);
        return CalculateGrid(layout, paper);
    }

    private static void Prepare(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    }

    private static Font CreateSafeFont(string? family, float size, FontStyle style, GraphicsUnit unit)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(family))
            {
                return new Font(family, size, style, unit);
            }
        }
        catch
        {
            // Missing fonts are common when layouts move between PCs.
        }

        try
        {
            return new Font("Yu Gothic UI", size, style, unit);
        }
        catch
        {
            return new Font(FontFamily.GenericSansSerif, size, style, unit);
        }
    }

    private static void DrawTag(Graphics graphics, LayoutFile layout, Dictionary<string, string> rowData, RectangleF tagRect, float scale, RenderLayerOptions layerOptions)
    {
        foreach (var element in layout.Elements.Where(e => e.Visible).OrderBy(e => e.ZIndex))
        {
            var rect = new RectangleF(
                tagRect.Left + (float)(element.X * scale),
                tagRect.Top + (float)(element.Y * scale),
                (float)(element.Width * scale),
                (float)(element.Height * scale));

            FillBackground(graphics, element, rect);

            if (element.ElementType == LayoutElementType.Image)
            {
                DrawImage(graphics, layout, element, rect);
            }
            else if (element.ElementType == LayoutElementType.Barcode)
            {
                DrawBarcode(graphics, element, rowData, rect, scale, layerOptions);
            }
            else if (element.ElementType == LayoutElementType.PriceValue)
            {
                if (layerOptions.DrawRasterPrice)
                {
                    DrawPriceValue(graphics, element, rowData, rect, scale, ResolveFontSize(element, rowData));
                }
            }
            else
            {
                var text = element.ElementType == LayoutElementType.FieldText
                    ? rowData.GetValueOrDefault(element.FieldId, "")
                    : element.Text;
                text ??= "";
                var fontSize = ResolveFontSize(element, rowData);
                if (element.DecimalDisplayStyle != DecimalDisplayStyle.Normal || element.DecimalPlaces >= 0)
                {
                    if (layerOptions.DrawRasterPrice)
                    {
                        DrawPriceValue(graphics, element, text, rect, scale, fontSize);
                    }
                }
                else
                {
                    if (layerOptions.DrawRasterText)
                    {
                        DrawText(graphics, element, text, rect, scale, fontSize);
                    }
                }
            }

            DrawBorder(graphics, element, rect, scale);
        }
    }

    private static double ResolveFontSize(LayoutElement element, Dictionary<string, string> rowData)
    {
        if (element.FontSizeSource != FontSizeSourceType.CsvField || string.IsNullOrWhiteSpace(element.FontSizeFieldId))
        {
            return Math.Max(1, element.FontSize);
        }

        if (!rowData.TryGetValue(element.FontSizeFieldId, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return Math.Max(1, element.FontSize);
        }

        var text = rawValue.Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return Math.Max(1, element.FontSize);
        }

        var min = element.MinFontSize > 0 ? element.MinFontSize : 1;
        var max = element.MaxFontSize >= min ? element.MaxFontSize : Math.Max(min, element.FontSize);
        return Math.Max(min, Math.Min(max, value));
    }

    private static void FillBackground(Graphics graphics, LayoutElement element, RectangleF rect)
    {
        var color = ParseColor(element.BackgroundColor, Color.Transparent);
        if (color.A == 0) return;
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, rect);
    }

    private static void DrawBorder(Graphics graphics, LayoutElement element, RectangleF rect, float scale)
    {
        if (element.BorderWidth <= 0) return;
        var color = ParseColor(element.BorderColor, Color.Black);
        if (color.A == 0) return;
        using var pen = new Pen(color, Math.Max(1f, (float)(element.BorderWidth * scale)));
        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static void DrawImage(Graphics graphics, LayoutFile layout, LayoutElement element, RectangleF rect)
    {
        var imagePath = ResolveImagePath(layout, element.ImagePath);
        if (!File.Exists(imagePath)) return;

        try
        {
            using var image = Image.FromFile(imagePath);
            if (!element.KeepAspectRatio)
            {
                graphics.DrawImage(image, rect);
                return;
            }

            var ratio = Math.Min(rect.Width / image.Width, rect.Height / image.Height);
            var width = image.Width * ratio;
            var height = image.Height * ratio;
            var left = rect.Left + (rect.Width - width) / 2;
            var top = rect.Top + (rect.Height - height) / 2;
            graphics.DrawImage(image, left, top, width, height);
        }
        catch
        {
            // Preflight should report invalid image files, but rendering must not crash
            // if an image becomes unreadable after preflight or preview is called directly.
            return;
        }
    }
}
