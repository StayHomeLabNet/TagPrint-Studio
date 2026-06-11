using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
{
    private void RedrawPreview()
    {
        if (previewBox is null || previewScrollPanel is null || currentLayout.Tag.Width <= 0 || fieldRows.Count == 0)
        {
            ShowPreviewImage(null, isPageMode: previewMode == PreviewMode.Page);
            UpdatePreviewNavigation(0);
            return;
        }

        var scrollPosition = CurrentPreviewScrollPosition();
        previewBox.Image?.Dispose();
        currentPreset.Fields = fieldRows.ToList();
        var scope = CurrentOutputScope();
        var allMode = scope == OutputScopeType.SinglePdfAllCsv;
        var file = allMode ? null : CurrentCsv();
        var files = PrintableRowService.SelectFiles(scope, csvRows, file);
        var printableRows = PrintableRowService.PrintableRows(currentPreset, files, CurrentPrintSortMode(), SelectedPrintSortFieldId(), CurrentPrintSortValueType());
        if (printableRows.Count == 0)
        {
            ShowPreviewImage(null, isPageMode: previewMode == PreviewMode.Page);
            UpdatePreviewNavigation(0);
            previewInfo.Text = csvRows.Count == 0 ? LocalizationService.T("main.preview.noCsv") : LocalizationService.T("main.preview.noPrintableRows");
            return;
        }

        var rowData = printableRows.Select(r => OutputService.MapRow(currentPreset, r)).ToList();
        var rowsPerPage = renderer.GetRowsPerPage(currentLayout);
        var pageCount = Math.Max(1, (int)Math.Ceiling(rowData.Count / (double)rowsPerPage));

        if (previewMode == PreviewMode.SingleTag)
        {
            previewPageIndex = Math.Clamp(previewPageIndex, 0, rowData.Count - 1);
            ShowPreviewImage(
                renderer.RenderSingleTag(currentLayout, currentPreset, rowData[previewPageIndex], SingleTagPreviewScale()),
                isPageMode: false);
            UpdatePreviewNavigation(rowData.Count);
        }
        else
        {
            previewPageIndex = Math.Clamp(previewPageIndex, 0, pageCount - 1);
            var pageRows = rowData
                .Skip(previewPageIndex * rowsPerPage)
                .Take(rowsPerPage)
                .ToList();
            ShowPreviewImage(
                renderer.RenderPage(currentLayout, currentPreset, pageRows, PagePreviewRenderScale()),
                isPageMode: true,
                desiredScrollPosition: scrollPosition);
            UpdatePreviewNavigation(pageCount);
        }

        var paper = units.GetPaperSize(currentLayout.Paper);
        var (columns, rows) = renderer.GetGrid(currentLayout);
        var pageText = previewMode == PreviewMode.Page ? LocalizationService.T("main.preview.pagePart", previewPageIndex + 1, pageCount) : "";
        previewInfo.Text = allMode
            ? LocalizationService.T("main.preview.allCsv", pageText, printableRows.Count, currentLayout.Paper.PaperSize, paper.Width, paper.Height, currentLayout.Tag.Width, currentLayout.Tag.Height, columns, rows)
            : LocalizationService.T("main.preview.singleCsv", file!.FileName, pageText, printableRows.Count, currentLayout.Paper.PaperSize, paper.Width, paper.Height, currentLayout.Tag.Width, currentLayout.Tag.Height, columns, rows);
    }

    private void ShowPreviewImage(Image? image, bool isPageMode, Point? desiredScrollPosition = null)
    {
        previewBox.Image = image;
        ApplyPreviewImageLayout(isPageMode, desiredScrollPosition);
        UpdatePreviewZoomControls();
    }

    private void ApplyPreviewImageLayout(bool isPageMode, Point? desiredScrollPosition = null)
    {
        if (previewBox is null || previewScrollPanel is null || previewCanvasHost is null)
        {
            return;
        }

        var viewport = previewScrollPanel.ClientSize;
        var viewportWidth = Math.Max(1, viewport.Width);
        var viewportHeight = Math.Max(1, viewport.Height);

        if (previewBox.Image is null)
        {
            previewCanvasHost.Size = new Size(viewportWidth, viewportHeight);
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewBox.Bounds = new Rectangle(0, 0, viewportWidth, viewportHeight);
            PositionPreviewImage(isPageMode);
            PositionPreviewZoomTools();
            return;
        }

        if (!isPageMode)
        {
            previewCanvasHost.Size = new Size(viewportWidth, viewportHeight);
            previewBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewBox.Bounds = new Rectangle(0, 0, viewportWidth, viewportHeight);
            PositionPreviewImage(isPageMode);
            PositionPreviewZoomTools();
            return;
        }

        previewBox.SizeMode = PictureBoxSizeMode.StretchImage;
        var scale = PagePreviewScale(previewBox.Image.Size, viewport);
        var imageWidth = Math.Max(1, (int)Math.Round(previewBox.Image.Width * scale));
        var imageHeight = Math.Max(1, (int)Math.Round(previewBox.Image.Height * scale));
        previewCanvasHost.Size = new Size(Math.Max(viewportWidth, imageWidth), Math.Max(viewportHeight, imageHeight));
        previewBox.Size = new Size(imageWidth, imageHeight);
        PositionPreviewImage(isPageMode);
        PositionPreviewZoomTools();

        if (desiredScrollPosition.HasValue)
        {
            SetPreviewScrollPosition(desiredScrollPosition.Value);
        }
    }

    private void PositionPreviewImage(bool isPageMode)
    {
        if (previewBox is null || previewCanvasHost is null)
        {
            return;
        }

        if (previewBox.Image is null)
        {
            previewBox.Location = Point.Empty;
            return;
        }

        if (!isPageMode)
        {
            previewBox.Location = Point.Empty;
            return;
        }

        var left = Math.Max(0, (previewCanvasHost.ClientSize.Width - previewBox.Width) / 2);
        var top = Math.Max(0, (previewCanvasHost.ClientSize.Height - previewBox.Height) / 2);
        previewBox.Location = new Point(left, top);
    }

    private float PagePreviewScale(Size imageSize, Size viewport)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return 1f;
        }

        var fitScale = Math.Min(
            Math.Max(1, viewport.Width) / (float)imageSize.Width,
            Math.Max(1, viewport.Height) / (float)imageSize.Height);
        if (!float.IsFinite(fitScale) || fitScale <= 0)
        {
            fitScale = 1f;
        }

        var zoomMultiplier = Math.Max(0.1f, 1f + pagePreviewZoomPercent / 100f);
        return fitScale * zoomMultiplier;
    }

    private float PagePreviewRenderScale()
    {
        var zoomMultiplier = Math.Max(1f, 1f + pagePreviewZoomPercent / 100f);
        return Math.Min(MaxPagePreviewRenderScale, BasePagePreviewRenderScale * zoomMultiplier);
    }

    private void AdjustPagePreviewZoom(int deltaPercent)
    {
        pagePreviewZoomPercent = Math.Clamp(
            pagePreviewZoomPercent + deltaPercent,
            MinPagePreviewZoomPercent,
            MaxPagePreviewZoomPercent);
        RedrawPreview();
    }

    private void ResetPagePreviewZoom()
    {
        pagePreviewZoomPercent = 0;
        RedrawPreview();
    }

    private Point CurrentPreviewScrollPosition()
    {
        if (previewScrollPanel is null)
        {
            return Point.Empty;
        }

        var position = previewScrollPanel.AutoScrollPosition;
        return new Point(Math.Abs(position.X), Math.Abs(position.Y));
    }

    private void SetPreviewScrollPosition(Point location)
    {
        if (previewScrollPanel is null)
        {
            return;
        }

        previewScrollPanel.AutoScrollPosition = new Point(
            Math.Max(0, location.X),
            Math.Max(0, location.Y));
    }

    private void PositionPreviewZoomTools()
    {
        if (previewViewportHost is null || previewZoomTools is null)
        {
            return;
        }

        previewZoomTools.Left = Math.Max(0, previewViewportHost.ClientSize.Width - previewZoomTools.Width - 12);
        previewZoomTools.Top = Math.Max(0, previewViewportHost.ClientSize.Height - previewZoomTools.Height - 12);
        previewZoomTools.BringToFront();
    }

    private void UpdatePreviewZoomControls()
    {
        if (previewZoomTools is null || previewZoomInButton is null || previewZoomResetButton is null || previewZoomOutButton is null)
        {
            return;
        }

        var enabled = previewMode == PreviewMode.Page && previewBox.Image is not null;
        previewZoomTools.Visible = enabled;
        previewZoomInButton.Enabled = enabled && pagePreviewZoomPercent < MaxPagePreviewZoomPercent;
        previewZoomResetButton.Enabled = enabled && pagePreviewZoomPercent != 0;
        previewZoomOutButton.Enabled = enabled && pagePreviewZoomPercent > MinPagePreviewZoomPercent;
        UpdatePreviewPanCursor();
    }

    private void PreviewSurface_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !IsPreviewPannable())
        {
            return;
        }

        previewPanning = true;
        previewPanStartCursor = Cursor.Position;
        previewPanStartScroll = CurrentPreviewScrollPosition();
        if (sender is Control control) control.Capture = true;
        UpdatePreviewPanCursor();
    }

    private void PreviewSurface_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!previewPanning || previewScrollPanel is null)
        {
            return;
        }

        var currentCursor = Cursor.Position;
        var deltaX = currentCursor.X - previewPanStartCursor.X;
        var deltaY = currentCursor.Y - previewPanStartCursor.Y;
        SetPreviewScrollPosition(new Point(
            Math.Max(0, previewPanStartScroll.X - deltaX),
            Math.Max(0, previewPanStartScroll.Y - deltaY)));
    }

    private void PreviewSurface_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (sender is Control control) control.Capture = false;
            previewPanning = false;
            UpdatePreviewPanCursor();
        }
    }

    private void PreviewSurface_MouseLeave(object? sender, EventArgs e)
    {
        if (!previewPanning)
        {
            UpdatePreviewPanCursor();
        }
    }

    private bool IsPreviewPannable()
    {
        return previewMode == PreviewMode.Page
            && previewBox.Image is not null
            && previewCanvasHost is not null
            && previewScrollPanel is not null
            && (previewCanvasHost.Width > previewScrollPanel.ClientSize.Width || previewCanvasHost.Height > previewScrollPanel.ClientSize.Height);
    }

    private void UpdatePreviewPanCursor()
    {
        if (previewViewportHost is null || previewBox is null)
        {
            return;
        }

        var cursor = previewPanning
            ? Cursors.SizeAll
            : IsPreviewPannable() ? Cursors.Hand : Cursors.Default;
        previewViewportHost.Cursor = cursor;
        previewBox.Cursor = cursor;
    }

    private float SingleTagPreviewScale()
    {
        var viewport = previewScrollPanel?.ClientSize ?? previewBox.ClientSize;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return MinSingleTagPreviewScale;
        }

        var fitScale = Math.Min(
            viewport.Width / (float)currentLayout.Tag.Width,
            viewport.Height / (float)currentLayout.Tag.Height);
        if (!float.IsFinite(fitScale) || fitScale <= 0)
        {
            return MinSingleTagPreviewScale;
        }

        return Math.Clamp(fitScale * 1.5f, MinSingleTagPreviewScale, MaxSingleTagPreviewScale);
    }

    private void MovePreviewPage(int direction)
    {
        previewPageIndex = Math.Max(0, previewPageIndex + direction);
        RedrawPreview();
    }

    private void SwitchPreviewMode(PreviewMode mode)
    {
        previewMode = mode;
        ResetPreviewPage();
        RedrawPreview();
    }

    private void ResetPreviewPage()
    {
        previewPageIndex = 0;
    }

    private void UpdatePreviewNavigation(int pageCount)
    {
        if (previousPageButton is null || nextPageButton is null) return;

        var enabled = pageCount > 1;
        previousPageButton.Enabled = enabled && previewPageIndex > 0;
        nextPageButton.Enabled = enabled && previewPageIndex < pageCount - 1;
    }

    private CsvFileItem? CurrentCsv()
    {
        if (csvGrid.CurrentRow?.DataBoundItem is CsvFileItem selected && selected.Status == CsvFileStatus.Ok) return selected;
        return csvRows.FirstOrDefault(c => c.Status == CsvFileStatus.Ok);
    }

    private void ShowCsvImportDetails(CsvImportResult result)
    {
        var details = new List<string>();
        details.AddRange(result.Warnings.Select(w => $"Warning: {w}"));
        details.AddRange(result.Errors.Select(e => $"Error: {e}"));
        details.AddRange(result.Files
            .Where(file => file.Status != CsvFileStatus.Ok)
            .Select(file => $"{file.FileName}: {file.Status} - {file.Note}"));

        if (details.Count == 0) return;

        using var dialog = new DetailsDialog(
            LocalizationService.T("main.details.csvImportTitle"),
            LocalizationService.T("main.details.csvImportSummary"),
            details);
        dialog.ShowDialog(this);
    }

    private void ShowExceptionDetails(string title, string summary, Exception exception)
    {
        var details = new List<string>();
        var friendlyMessage = CsvFileUtility.BuildUserFriendlySaveErrorMessage(exception);
        if (!string.Equals(friendlyMessage, exception.Message, StringComparison.Ordinal))
        {
            details.Add(friendlyMessage);
            details.Add(string.Empty);
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            details.Add($"{current.GetType().Name}: {current.Message}");
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                details.Add(current.StackTrace);
            }
        }

        using var dialog = new DetailsDialog(title, summary, details);
        dialog.ShowDialog(this);
    }


    private bool ValidateReady(bool requireOutputFolder = true)
    {
        if (csvRows.All(c => c.Status != CsvFileStatus.Ok)) { MessageBox.Show(LocalizationService.T("main.message.noValidCsv"), LocalizationService.T("common.appTitle")); return false; }
        if (requireOutputFolder && string.IsNullOrWhiteSpace(outputFolderBox.Text)) { MessageBox.Show(LocalizationService.T("main.message.outputFolderRequired"), LocalizationService.T("common.appTitle")); return false; }
        currentPreset.Fields = fieldRows.ToList();
        var result = preflightCheckService.Check(currentLayout, currentPreset, csvRows.ToList());
        if (result.HasIssues)
        {
            using var dialog = new DetailsDialog(
                LocalizationService.T("main.details.preflightTitle"),
                LocalizationService.T("main.details.preflightSummary", result.PrintableRowCount, result.Issues.Count),
                result.Issues);
            dialog.ShowDialog(this);
            return !result.HasBlockingIssues;
        }
        return true;
    }


}
