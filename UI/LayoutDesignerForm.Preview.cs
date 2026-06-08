using System.Drawing.Drawing2D;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private void RedrawPreview()
    {
        if (loading || previewBox is null || currentLayout.Tag.Width <= 0 || currentLayout.Tag.Height <= 0) return;
        using var rendered = previewMode == DesignerPreviewMode.Page
            ? RenderPagePreview()
            : renderer.RenderSingleTag(currentLayout, new PresetFile(), previewData, PreviewScale);
        var bitmap = new Bitmap(rendered);
        if (previewMode == DesignerPreviewMode.SingleTag)
        {
            if (showGridCheck.Checked) DrawGrid(bitmap);
            DrawSelection(bitmap);
        }

        previewBox.Image?.Dispose();
        previewBox.Image = bitmap;
        var csvStatus = csvPreviewDirty ? LocalizationService.T("layout.message.statusCsvEdited") : "";
        var modeText = previewMode == DesignerPreviewMode.Page
            ? LocalizationService.T("layout.preview.page")
            : LocalizationService.T("layout.preview.single");
        statusLabel.Text = LocalizationService.T("layout.message.previewStatus", currentLayout.LayoutName, CurrentPreviewRowLabel(), $"{csvStatus} / {modeText}", currentLayout.Tag.Width, currentLayout.Tag.Height, elementRows.Count);
    }

    private Bitmap RenderPagePreview()
    {
        var rowsPerPage = renderer.GetRowsPerPage(currentLayout);
        var pageRows = new List<Dictionary<string, string>>();
        var startIndex = previewRows.Count == 0 || previewRowCombo.SelectedIndex < 0 ? 0 : previewRowCombo.SelectedIndex;

        for (var i = 0; i < rowsPerPage; i++)
        {
            if (previewRows.Count > 0)
            {
                var sourceIndex = Math.Min(startIndex + i, previewRows.Count - 1);
                pageRows.Add(previewRows[sourceIndex].Data.ToDictionary());
            }
            else
            {
                pageRows.Add(previewData.ToDictionary());
            }
        }

        return renderer.RenderPage(currentLayout, new PresetFile(), pageRows, PreviewScale);
    }

    private void DrawGrid(Bitmap bitmap)
    {
        var step = (float)Math.Max(0.1, (double)gridSizeBox.Value * PreviewScale);
        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.FromArgb(80, Color.SteelBlue), 1) { DashStyle = DashStyle.Dot };
        for (float x = step; x < bitmap.Width; x += step) graphics.DrawLine(pen, x, 0, x, bitmap.Height);
        for (float y = step; y < bitmap.Height; y += step) graphics.DrawLine(pen, 0, y, bitmap.Width, y);
    }

    private void DrawSelection(Bitmap bitmap)
    {
        var selected = SelectedElement();
        if (selected is null) return;

        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(selected.Locked ? Color.DarkOrange : Color.DodgerBlue, 2);
        var rect = new RectangleF(
            (float)(selected.X * PreviewScale),
            (float)(selected.Y * PreviewScale),
            (float)(selected.Width * PreviewScale),
            (float)(selected.Height * PreviewScale));
        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        if (selected.Locked) return;

        using var brush = new SolidBrush(Color.White);
        using var border = new Pen(Color.DodgerBlue, 1);
        foreach (var handle in SelectionHandlesPx(rect))
        {
            graphics.FillRectangle(brush, handle);
            graphics.DrawRectangle(border, handle);
        }
    }

    private static IEnumerable<RectangleF> SelectionHandlesPx(RectangleF rect)
    {
        const float size = 8;
        var half = size / 2;
        yield return new RectangleF(rect.Left - half, rect.Top - half, size, size);
        yield return new RectangleF(rect.Right - half, rect.Top - half, size, size);
        yield return new RectangleF(rect.Left - half, rect.Bottom - half, size, size);
        yield return new RectangleF(rect.Right - half, rect.Bottom - half, size, size);
    }

    private static IEnumerable<(DragMode Mode, RectangleF Rect)> SelectionHandlesMm(RectangleF rect)
    {
        const float size = 1.2f;
        var half = size / 2;
        yield return (DragMode.ResizeTopLeft, new RectangleF(rect.Left - half, rect.Top - half, size, size));
        yield return (DragMode.ResizeTopRight, new RectangleF(rect.Right - half, rect.Top - half, size, size));
        yield return (DragMode.ResizeBottomLeft, new RectangleF(rect.Left - half, rect.Bottom - half, size, size));
        yield return (DragMode.ResizeBottomRight, new RectangleF(rect.Right - half, rect.Bottom - half, size, size));
    }
}

