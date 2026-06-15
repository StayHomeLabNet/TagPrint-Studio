using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace TagPrintStudio.Services;

public interface IOutputService
{
    void ExportPdf(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default);
    void ExportPng(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default);
    void Print(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default);
    List<string> GetPlannedOutputPaths(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options);
}

public sealed class OutputService(IPreviewRenderService renderer, IUnitConversionService units) : IOutputService
{
    public void ExportPng(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.OutputFolder);
        var renderScale = RenderScale(options);
        using var fileTransaction = new OutputFileTransaction();
        var validFiles = csvFiles.Where(f => f.Status == CsvFileStatus.Ok).ToList();
        var outputBaseNames = UniqueOutputBaseNames(validFiles);
        try
        {
            if (options.OutputScope == OutputScopeType.IndividualPngPerRow)
            {
                var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var completedRows = 0;
                var entries = PrintableRowService.PrintableRowEntries(preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName);
                Report(progress, "PNG", 0, entries.Count);
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileKey = string.IsNullOrWhiteSpace(entry.File.FilePath) ? entry.File.FileName : entry.File.FilePath;
                    counters.TryGetValue(fileKey, out var index);
                    index++;
                    counters[fileKey] = index;

                    using var bitmap = renderer.RenderSingleTag(layout, preset, MapRow(preset, entry.Row), renderScale);
                    var baseName = OutputBaseName(outputBaseNames, entry.File);
                    var path = Path.Combine(options.OutputFolder, $"{baseName}_{index:0000}.png");
                    SaveBitmapAtomic(bitmap, path, ImageFormat.Png, fileTransaction, cancellationToken);
                    completedRows++;
                    Report(progress, "PNG", completedRows, entries.Count, entry.File.FileName);
                }
                fileTransaction.Commit();
                return;
            }

            var pageJobs = validFiles
                .Select(file => new
                {
                    File = file,
                    Pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).ToList()
                })
                .ToList();
            var totalPages = pageJobs.Sum(job => job.Pages.Count);
            var completedPages = 0;
            Report(progress, "PNG", 0, totalPages);
            foreach (var job in pageJobs)
            {
                var pages = job.Pages;
                var baseName = OutputBaseName(outputBaseNames, job.File);
                for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var bitmap = RenderPageWithContext(layout, preset, pages[pageIndex], renderScale, "PNG", completedPages + 1, totalPages);
                    var suffix = pages.Count == 1 ? "page" : $"page_{pageIndex + 1:000}";
                    var path = Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.png");
                    SaveBitmapAtomic(bitmap, path, ImageFormat.Png, fileTransaction, cancellationToken);
                    completedPages++;
                    Report(progress, "PNG", completedPages, totalPages, job.File.FileName);
                }
            }

            fileTransaction.Commit();
        }
        catch
        {
            fileTransaction.Rollback();
            throw;
        }
    }

    public void ExportPdf(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.OutputFolder);
        var renderScale = RenderScale(options);
        var paperSize = units.GetPaperSize(layout.Paper);
        using var fileTransaction = new OutputFileTransaction();
        var validFiles = csvFiles.Where(f => f.Status == CsvFileStatus.Ok).ToList();
        var outputBaseNames = UniqueOutputBaseNames(validFiles);

        try
        {
            if (options.OutputScope == OutputScopeType.SinglePdfAllCsv)
            {
                var pages = Pages(layout, preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).ToList();
                if (pages.Count > 0)
                {
                    var path = Path.Combine(options.OutputFolder, "TagPrintStudio_Output.pdf");
                    Report(progress, "PDF", 0, pages.Count);
                    SavePdfAtomic(path, layout, preset, pages, options, renderScale, paperSize, fileTransaction, progress, cancellationToken);
                }

                fileTransaction.Commit();
                return;
            }

            foreach (var file in validFiles)
            {
                var pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).ToList();
                if (pages.Count == 0)
                {
                    continue;
                }

                var baseName = OutputBaseName(outputBaseNames, file);
                var path = Path.Combine(options.OutputFolder, $"{baseName}.pdf");
                Report(progress, "PDF", 0, pages.Count, file.FileName);
                SavePdfAtomic(path, layout, preset, pages, options, renderScale, paperSize, fileTransaction, progress, cancellationToken);
            }

            fileTransaction.Commit();
        }
        catch
        {
            fileTransaction.Rollback();
            throw;
        }
    }

    public void Print(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var renderScale = RenderScale(options);
        var totalPages = Math.Max(0, options.ExpectedPageCount ?? CountPages(layout, preset, csvFiles.Where(f => f.Status == CsvFileStatus.Ok), options));
        using var pageEnumerator = Pages(
            layout,
            preset,
            csvFiles.Where(f => f.Status == CsvFileStatus.Ok),
            options.PrintSortMode,
            options.PrintSortFieldId,
            options.PrintSortValueType,
            options.PrintTargetOrderColumnName).GetEnumerator();
        if (!pageEnumerator.MoveNext()) return;
        Report(progress, "Print", 0, totalPages);

        using var document = new PrintDocument
        {
            DocumentName = "TagPrint Studio"
        };

        if (options.PrinterSettings is not null)
        {
            document.PrinterSettings = options.PrinterSettings;
        }

        var paper = units.GetPaperSize(layout.Paper);
        document.DefaultPageSettings.PaperSize = new PaperSize(
            layout.Paper.PaperSize.ToString(),
            MmToHundredthsOfInchForSize(paper.Width),
            MmToHundredthsOfInchForSize(paper.Height));
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.OriginAtMargins = false;

        var pageNumber = 1;
        document.PrintPage += (_, e) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                e.Cancel = true;
                e.HasMorePages = false;
                return;
            }

            if (e.Graphics is null)
            {
                e.HasMorePages = false;
                return;
            }

            using var pageImage = RenderPageWithContext(layout, preset, pageEnumerator.Current, renderScale, "Print", pageNumber, totalPages);
            var target = PrintTargetRectangle(e, e.Graphics, options.PrintScalingMode, options.PrintOffsetXmm, options.PrintOffsetYmm);
            e.Graphics.DrawImage(pageImage, target);
            Report(progress, "Print", pageNumber, totalPages);
            pageNumber++;
            e.HasMorePages = !cancellationToken.IsCancellationRequested && pageEnumerator.MoveNext();
            e.Cancel = cancellationToken.IsCancellationRequested;
        };

        document.Print();
        cancellationToken.ThrowIfCancellationRequested();
    }

    public List<string> GetPlannedOutputPaths(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options)
    {
        var outputs = new List<string>();
        var validFiles = csvFiles.Where(f => f.Status == CsvFileStatus.Ok).ToList();
        var outputBaseNames = UniqueOutputBaseNames(validFiles);

        if (options.OutputFormat == OutputFormatType.Pdf)
        {
            if (options.OutputScope == OutputScopeType.SeparatePdfPerCsv)
            {
                foreach (var file in validFiles)
                {
                    var hasPages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).Any();
                    if (!hasPages)
                    {
                        continue;
                    }

                    outputs.Add(Path.Combine(options.OutputFolder, $"{OutputBaseName(outputBaseNames, file)}.pdf"));
                }
            }
            else
            {
                var hasPages = Pages(layout, preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).Any();
                if (hasPages)
                {
                    outputs.Add(Path.Combine(options.OutputFolder, "TagPrintStudio_Output.pdf"));
                }
            }

            return outputs;
        }

        if (options.OutputScope == OutputScopeType.IndividualPngPerRow)
        {
            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in PrintableRowService.PrintableRowEntries(preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName))
            {
                var fileKey = string.IsNullOrWhiteSpace(entry.File.FilePath) ? entry.File.FileName : entry.File.FilePath;
                counters.TryGetValue(fileKey, out var index);
                index++;
                counters[fileKey] = index;

                var baseName = OutputBaseName(outputBaseNames, entry.File);
                outputs.Add(Path.Combine(options.OutputFolder, $"{baseName}_{index:0000}.png"));
            }

            return outputs;
        }

        foreach (var file in validFiles)
        {
            var pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).ToList();
            var baseName = OutputBaseName(outputBaseNames, file);
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var suffix = pages.Count == 1 ? "page" : $"page_{pageIndex + 1:000}";
                outputs.Add(Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.png"));
            }
        }

        return outputs;
    }

    private static Dictionary<CsvFileItem, string> UniqueOutputBaseNames(IEnumerable<CsvFileItem> files)
    {
        var outputBaseNames = new Dictionary<CsvFileItem, string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
            var candidate = baseName;
            var suffix = 2;
            while (!usedNames.Add(candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }

            outputBaseNames[file] = candidate;
        }

        return outputBaseNames;
    }

    private static string OutputBaseName(IReadOnlyDictionary<CsvFileItem, string> outputBaseNames, CsvFileItem file)
    {
        return outputBaseNames.TryGetValue(file, out var baseName)
            ? baseName
            : SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
    }

    public static Dictionary<string, string> MapRow(PresetFile preset, string[] row)
    {
        var data = new Dictionary<string, string>();
        foreach (var field in preset.Fields.Where(f => f.Enabled))
        {
            data[field.FieldId] = field.ColumnIndex is int index && index >= 0 && index < row.Length ? row[index] ?? "" : "";
        }
        return data;
    }

    private IEnumerable<List<Dictionary<string, string>>> Pages(LayoutFile layout, PresetFile preset, CsvFileItem file, PrintSortMode sortMode, string sortFieldId, PrintSortValueType sortValueType, string printTargetOrderColumnName)
    {
        return Pages(layout, preset, [file], sortMode, sortFieldId, sortValueType, printTargetOrderColumnName);
    }

    private IEnumerable<List<Dictionary<string, string>>> Pages(LayoutFile layout, PresetFile preset, IEnumerable<CsvFileItem> files, PrintSortMode sortMode, string sortFieldId, PrintSortValueType sortValueType, string printTargetOrderColumnName)
    {
        var rowsPerPage = renderer.GetRowsPerPage(layout);
        return PrintableRowService.PrintableRows(preset, files, sortMode, sortFieldId, sortValueType, printTargetOrderColumnName)
            .Select(row => MapRow(preset, row))
            .Chunk(rowsPerPage)
            .Select(chunk => chunk.ToList());
    }

    private IEnumerable<Bitmap> RenderPdfPages(
        LayoutFile layout,
        PresetFile preset,
        IReadOnlyCollection<List<Dictionary<string, string>>> pages,
        float renderScale,
        string operation,
        IProgress<OutputProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pageNumber = 1;
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = RenderPageWithContext(layout, preset, page, renderScale, operation, pageNumber, pages.Count);
            Report(progress, operation, pageNumber, pages.Count);
            pageNumber++;
            yield return bitmap;
        }
    }

    private int CountPages(LayoutFile layout, PresetFile preset, IEnumerable<CsvFileItem> files, OutputOptions options)
    {
        var rowsPerPage = Math.Max(1, renderer.GetRowsPerPage(layout));
        var rowCount = PrintableRowService.PrintableRows(preset, files, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType, options.PrintTargetOrderColumnName).Count;
        return (int)Math.Ceiling(rowCount / (double)rowsPerPage);
    }

    private Bitmap RenderPageWithContext(
        LayoutFile layout,
        PresetFile preset,
        List<Dictionary<string, string>> page,
        float renderScale,
        string operation,
        int pageNumber,
        int totalPages,
        RenderLayerOptions? layerOptions = null)
    {
        try
        {
            return renderer.RenderPage(layout, preset, page, renderScale, layerOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OutputPageException(operation, pageNumber, totalPages, ex);
        }
    }

    private static void Report(IProgress<OutputProgress>? progress, string operation, int current, int total, string detail = "")
    {
        progress?.Report(new OutputProgress
        {
            Operation = operation,
            Current = current,
            Total = total,
            Detail = detail
        });
    }

    private static void SaveBitmapAtomic(Bitmap bitmap, string path, ImageFormat format, OutputFileTransaction fileTransaction, CancellationToken cancellationToken)
    {
        var partialPath = PartialPath(path);
        DeleteIfExists(partialPath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bitmap.Save(partialPath, format);
            cancellationToken.ThrowIfCancellationRequested();
            fileTransaction.ReplaceWithPartial(partialPath, path);
        }
        finally
        {
            DeleteIfExists(partialPath);
        }
    }

    private void SavePdfAtomic(
        string path,
        LayoutFile layout,
        PresetFile preset,
        IReadOnlyCollection<List<Dictionary<string, string>>> pages,
        OutputOptions options,
        float renderScale,
        SizeF pageSizeMm,
        OutputFileTransaction fileTransaction,
        IProgress<OutputProgress>? progress,
        CancellationToken cancellationToken)
    {
        var partialPath = PartialPath(path);
        DeleteIfExists(partialPath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (options.PdfRenderMode == PdfRenderMode.VectorText)
            {
                VectorTextPdfWriter.Save(
                    partialPath,
                    layout,
                    preset,
                    pages,
                    pageSizeMm,
                    renderScale,
                    (page, pageNumber, totalPages) => RenderPageWithContext(
                        layout,
                        preset,
                        page,
                        renderScale,
                        "PDF",
                        pageNumber,
                        totalPages,
                        VectorTextPdfWriter.RasterBackgroundLayerOptions),
                    progress,
                    cancellationToken);
            }
            else
            {
                SimplePdfWriter.SaveImages(partialPath, RenderPdfPages(layout, preset, pages, renderScale, "PDF", progress, cancellationToken), pageSizeMm);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(partialPath))
            {
                fileTransaction.ReplaceWithPartial(partialPath, path);
            }
        }
        finally
        {
            DeleteIfExists(partialPath);
        }
    }

    private static string PartialPath(string path) => $"{path}.partial-{Guid.NewGuid():N}";

    private sealed class OutputFileTransaction : IDisposable
    {
        private readonly List<OutputFileTransactionEntry> entries = [];
        private bool completed;

        public void ReplaceWithPartial(string partialPath, string path)
        {
            var backupPath = BackupPath(path);
            var hadExistingFile = File.Exists(path);
            var entry = new OutputFileTransactionEntry(path, backupPath, hadExistingFile, BackupCreated: false);

            try
            {
                if (hadExistingFile)
                {
                    DeleteIfExists(backupPath);
                    File.Move(path, backupPath);
                    entry = entry with { BackupCreated = true };
                }

                entries.Add(entry);
                File.Move(partialPath, path, overwrite: false);
            }
            catch
            {
                entries.Remove(entry);
                RestoreEntry(entry);
                throw;
            }
        }

        public void Commit()
        {
            foreach (var entry in entries)
            {
                if (entry.BackupCreated)
                {
                    DeleteIfExists(entry.BackupPath);
                }
            }

            entries.Clear();
            completed = true;
        }

        public void Rollback()
        {
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                RestoreEntry(entries[index]);
            }

            entries.Clear();
            completed = true;
        }

        public void Dispose()
        {
            if (!completed)
            {
                Rollback();
            }
        }

        private static void RestoreEntry(OutputFileTransactionEntry entry)
        {
            if (entry.HadExistingFile)
            {
                if (entry.BackupCreated && File.Exists(entry.BackupPath))
                {
                    DeleteIfExists(entry.Path);

                    try
                    {
                        File.Move(entry.BackupPath, entry.Path, overwrite: true);
                    }
                    catch
                    {
                        // Rollback is best effort; avoid masking the original export error.
                    }
                }

                return;
            }

            DeleteIfExists(entry.Path);
            DeleteIfExists(entry.BackupPath);
        }

        private static string BackupPath(string path) => $"{path}.original-backup-{Guid.NewGuid():N}";
    }

    private sealed record OutputFileTransactionEntry(string Path, string BackupPath, bool HadExistingFile, bool BackupCreated);

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort; the original operation result should remain visible.
        }
    }

    private static float RenderScale(OutputOptions options)
    {
        var dpi = Math.Max(72, options.OutputDpi);
        return (float)(dpi / 25.4);
    }

    private static int MmToHundredthsOfInchForSize(float millimeters)
    {
        return Math.Max(1, (int)Math.Round(millimeters / 25.4f * 100f));
    }

    private static int MmToHundredthsOfInchForOffset(float millimeters)
    {
        return (int)Math.Round(millimeters / 25.4f * 100f);
    }

    private static RectangleF PrintTargetRectangle(PrintPageEventArgs e, Graphics graphics, PrintScalingMode scalingMode, double offsetXmm, double offsetYmm)
    {
        var offsetX = MmToHundredthsOfInchForOffset((float)offsetXmm);
        var offsetY = MmToHundredthsOfInchForOffset((float)offsetYmm);

        if (scalingMode == PrintScalingMode.FitPrintableArea)
        {
            var printable = e.PageSettings.PrintableArea;
            if (printable.Width > 0 && printable.Height > 0)
            {
                return new RectangleF(printable.X + offsetX, printable.Y + offsetY, printable.Width, printable.Height);
            }

            return new RectangleF(e.MarginBounds.X + offsetX, e.MarginBounds.Y + offsetY, e.MarginBounds.Width, e.MarginBounds.Height);
        }

        graphics.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        var bounds = e.PageBounds;
        var baseRect = bounds.Width > 0 && bounds.Height > 0
            ? new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height)
            : new RectangleF(e.MarginBounds.X, e.MarginBounds.Y, e.MarginBounds.Width, e.MarginBounds.Height);
        return new RectangleF(baseRect.X + offsetX, baseRect.Y + offsetY, baseRect.Width, baseRect.Height);
    }
}

public static class VectorTextPdfWriter
{
    private const double FontSizeToMillimeters = 0.72;
    private const string DefaultVectorFontFamily = "Yu Gothic UI";
    private static readonly string[] VectorFontFallbackFamilies =
    [
        "Noto Sans JP",
        "Noto Sans CJK JP",
        "Arial Unicode MS",
        "Yu Mincho",
        "Yu Gothic UI",
        "Yu Gothic",
        "Meiryo",
        "MS Gothic",
        "Arial"
    ];

    public static readonly RenderLayerOptions RasterBackgroundLayerOptions = new(
        DrawRasterText: false,
        DrawRasterPrice: false,
        DrawRasterBarcodeText: true);

    private static int fontResolverConfigured;

    public static void Save(
        string path,
        LayoutFile layout,
        PresetFile preset,
        IReadOnlyCollection<List<Dictionary<string, string>>> pages,
        SizeF pageSizeMm,
        float renderScale,
        Func<List<Dictionary<string, string>>, int, int, Bitmap> renderPage,
        IProgress<OutputProgress>? progress,
        CancellationToken cancellationToken)
    {
        EnsureFontResolver();
        using var document = new PdfDocument();
        var pageNumber = 1;
        foreach (var rows in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = renderPage(rows, pageNumber, pages.Count);
            using var pageImageStream = new MemoryStream();
            bitmap.Save(pageImageStream, ImageFormat.Png);
            pageImageStream.Position = 0;

            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(pageSizeMm.Width);
            page.Height = XUnit.FromMillimeter(pageSizeMm.Height);
            using var graphics = XGraphics.FromPdfPage(page);
            using var image = XImage.FromStream(pageImageStream);
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            DrawVectorTextLayer(graphics, layout, rows);
            progress?.Report(new OutputProgress { Operation = "PDF", Current = pageNumber, Total = pages.Count });
            pageNumber++;
        }

        if (document.PageCount == 0)
        {
            return;
        }

        document.Save(path);
    }

    private static void DrawVectorTextLayer(XGraphics graphics, LayoutFile layout, IReadOnlyList<Dictionary<string, string>> rows)
    {
        var (columns, pageRows) = new PreviewRenderService(new UnitConversionService()).GetGrid(layout);
        var index = 0;
        for (var y = 0; y < pageRows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (index >= rows.Count) return;
                var left = layout.Margin.Left + x * (layout.Tag.Width + layout.Spacing.Horizontal);
                var top = layout.Margin.Top + y * (layout.Tag.Height + layout.Spacing.Vertical);
                DrawTagVectorText(graphics, layout, rows[index], left, top);
                index++;
            }
        }
    }

    private static void DrawTagVectorText(XGraphics graphics, LayoutFile layout, IReadOnlyDictionary<string, string> rowData, double tagLeftMm, double tagTopMm)
    {
        foreach (var element in layout.Elements.Where(e => e.Visible).OrderBy(e => e.ZIndex))
        {
            if (element.ElementType != LayoutElementType.FieldText &&
                element.ElementType != LayoutElementType.StaticText &&
                element.ElementType != LayoutElementType.PriceValue)
            {
                continue;
            }

            var rect = new XRect(
                XUnit.FromMillimeter(tagLeftMm + element.X).Point,
                XUnit.FromMillimeter(tagTopMm + element.Y).Point,
                XUnit.FromMillimeter(element.Width).Point,
                XUnit.FromMillimeter(element.Height).Point);

            var previousClip = graphics.Save();
            graphics.IntersectClip(rect);
            DrawVectorTextElement(graphics, element, rowData, rect);
            graphics.Restore(previousClip);
        }
    }

    private static void DrawVectorTextElement(XGraphics graphics, LayoutElement element, IReadOnlyDictionary<string, string> rowData, XRect rect)
    {
        var text = TextForElement(element, rowData);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = ResolvePdfFontSize(element, rowData);
        if (element.ElementType == LayoutElementType.PriceValue ||
            element.DecimalDisplayStyle != DecimalDisplayStyle.Normal ||
            element.DecimalPlaces >= 0)
        {
            DrawVectorPriceValue(graphics, element, text, rect, fontSize);
            return;
        }

        DrawVectorText(graphics, element, text, rect, fontSize);
    }

    private static string TextForElement(LayoutElement element, IReadOnlyDictionary<string, string> rowData)
    {
        if (element.ElementType == LayoutElementType.StaticText)
        {
            return element.Text ?? "";
        }

        return rowData.TryGetValue(element.FieldId, out var value) ? value ?? "" : "";
    }

    private static void DrawVectorText(XGraphics graphics, LayoutElement element, string text, XRect rect, double fontSize)
    {
        var font = CreateFont(element, fontSize, text);
        var brush = new XSolidBrush(ToXColor(ParseColorForPdf(element.TextColor, Color.Black)));
        var lines = WrapText(graphics, text, font, rect.Width);
        if (lines.Count == 0)
        {
            return;
        }

        var lineHeight = graphics.MeasureString("Ag", font).Height;
        var totalHeight = lineHeight * lines.Count;
        var y = rect.Top + Math.Max(0, (rect.Height - totalHeight) / 2);
        foreach (var line in lines)
        {
            var lineWidth = graphics.MeasureString(line, font).Width;
            var x = element.TextAlign switch
            {
                TextAlignType.Center => rect.Left + Math.Max(0, (rect.Width - lineWidth) / 2),
                TextAlignType.Right => rect.Right - lineWidth,
                _ => rect.Left
            };
            graphics.DrawString(line, font, brush, new XPoint(x, y + lineHeight));
            y += lineHeight;
            if (y > rect.Bottom)
            {
                break;
            }
        }
    }

    private static void DrawVectorPriceValue(XGraphics graphics, LayoutElement element, string rawText, XRect rect, double fontSize)
    {
        var normalized = NormalizePriceText(rawText, element.DecimalPlaces);
        if ((element.DecimalDisplayStyle != DecimalDisplayStyle.SmallFractionBottom &&
             element.DecimalDisplayStyle != DecimalDisplayStyle.SmallFractionTop) ||
            !TrySplitPrice(normalized, out var integerPart, out var fractionPart))
        {
            DrawVectorText(graphics, element, normalized, rect, fontSize);
            return;
        }

        var wholeFont = CreateFont(element, fontSize, integerPart);
        var fractionElement = new LayoutElement
        {
            FontFamily = element.FontFamily,
            Bold = element.Bold
        };
        var fractionFont = CreateFont(fractionElement, Math.Max(1, fontSize * element.FractionFontScale), fractionPart);
        var brush = new XSolidBrush(ToXColor(ParseColorForPdf(element.TextColor, Color.Black)));
        var wholeSize = graphics.MeasureString(integerPart, wholeFont);
        var fractionSize = graphics.MeasureString(fractionPart, fractionFont);
        var totalWidth = wholeSize.Width + fractionSize.Width;
        var x = rect.Left;
        if (element.TextAlign == TextAlignType.Center)
        {
            x = rect.Left + (rect.Width - totalWidth) / 2;
        }
        else if (element.TextAlign == TextAlignType.Right)
        {
            x = rect.Right - totalWidth;
        }

        var wholeTop = rect.Top + (rect.Height - wholeSize.Height) / 2;
        var baselineY = wholeTop + FontAscentPoints(wholeFont);
        var fractionTop = element.DecimalDisplayStyle == DecimalDisplayStyle.SmallFractionTop
            ? wholeTop
            : baselineY - FontAscentPoints(fractionFont);
        fractionTop += XUnit.FromMillimeter(element.FractionYOffset).Point;

        graphics.DrawString(integerPart, wholeFont, brush, new XPoint(x, baselineY));
        graphics.DrawString(fractionPart, fractionFont, brush, new XPoint(x + wholeSize.Width, fractionTop + FontAscentPoints(fractionFont)));
    }

    private static void EnsureFontResolver()
    {
        if (Interlocked.Exchange(ref fontResolverConfigured, 1) == 1)
        {
            return;
        }

        try
        {
            GlobalFontSettings.FontResolver ??= WindowsPdfFontResolver.Instance;
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        catch (InvalidOperationException)
        {
            // PDFsharp locks global font settings after first font access.
        }
    }

    private static XFont CreateFont(LayoutElement element, double fontSize, string text = "")
    {
        var size = PdfFontPointSize(fontSize);
        var style = element.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
        var family = string.IsNullOrWhiteSpace(element.FontFamily) ? DefaultVectorFontFamily : element.FontFamily;

        foreach (var candidate in FontCandidates(family, text))
        {
            try
            {
                return new XFont(candidate, size, style, XPdfFontOptions.UnicodeDefault);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException)
            {
                // Try the next known Windows font. The last candidate should exist on supported systems.
            }
        }

        return new XFont("Arial", size, style, XPdfFontOptions.UnicodeDefault);
    }

    private static double PdfFontPointSize(double fontSize) =>
        XUnit.FromMillimeter(Math.Max(1, fontSize) * FontSizeToMillimeters).Point;

    private static IEnumerable<string> FontCandidates(string family, string text)
    {
        if (ContainsCjk(text))
        {
            foreach (var fallback in VectorFontFallbackFamilies)
            {
                if (!string.Equals(fallback, "Arial", StringComparison.OrdinalIgnoreCase))
                {
                    yield return fallback;
                }
            }

            if (!VectorFontFallbackFamilies.Any(fallback => string.Equals(family, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                yield return family;
            }

            yield return "Arial";
            yield break;
        }

        yield return family;
        foreach (var fallback in VectorFontFallbackFamilies)
        {
            if (!string.Equals(family, fallback, StringComparison.OrdinalIgnoreCase))
            {
                yield return fallback;
            }
        }
    }

    private static bool ContainsCjk(string text) => text.Any(IsCjk);

    private static bool IsCjk(char value) =>
        value is >= '\u3040' and <= '\u30ff' ||
        value is >= '\u3400' and <= '\u9fff' ||
        value is >= '\uf900' and <= '\ufaff' ||
        value is >= '\uff00' and <= '\uffef';

    private static List<string> WrapText(XGraphics graphics, string text, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        if (maxWidth <= 0)
        {
            return lines;
        }

        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        foreach (var paragraph in paragraphs)
        {
            WrapParagraph(graphics, paragraph, font, maxWidth, lines);
        }

        return lines;
    }

    private static void WrapParagraph(XGraphics graphics, string paragraph, XFont font, double maxWidth, List<string> lines)
    {
        if (string.IsNullOrEmpty(paragraph))
        {
            lines.Add("");
            return;
        }

        var current = "";
        foreach (var token in TextTokens(paragraph))
        {
            var candidate = string.IsNullOrEmpty(current)
                ? token.TrimStart()
                : current + token;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (graphics.MeasureString(candidate, font).Width <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                lines.Add(current.TrimEnd());
                current = token.TrimStart();
            }

            while (!string.IsNullOrEmpty(current) && graphics.MeasureString(current, font).Width > maxWidth)
            {
                var split = FittingPrefixLength(graphics, current, font, maxWidth);
                lines.Add(current[..split].TrimEnd());
                current = current[split..].TrimStart();
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            lines.Add(current.TrimEnd());
        }
    }

    private static IEnumerable<string> TextTokens(string text)
    {
        var token = new StringBuilder();
        foreach (var value in text)
        {
            if (char.IsWhiteSpace(value))
            {
                token.Append(value);
                continue;
            }

            if (IsCjk(value))
            {
                if (token.Length > 0)
                {
                    yield return token.ToString();
                    token.Clear();
                }

                yield return value.ToString();
                continue;
            }

            if (token.Length > 0 && char.IsWhiteSpace(token[^1]))
            {
                yield return token.ToString();
                token.Clear();
            }

            token.Append(value);
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }

    private static int FittingPrefixLength(XGraphics graphics, string text, XFont font, double maxWidth)
    {
        for (var length = Math.Max(1, text.Length - 1); length > 1; length--)
        {
            if (graphics.MeasureString(text[..length], font).Width <= maxWidth)
            {
                return length;
            }
        }

        return 1;
    }

    private static double FontAscentPoints(XFont font) =>
        font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);

    private static double ResolvePdfFontSize(LayoutElement element, IReadOnlyDictionary<string, string> rowData)
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

    private static Color ParseColorForPdf(string value, Color fallback)
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

    private static XColor ToXColor(Color color) => XColor.FromArgb(color.A, color.R, color.G, color.B);
}

internal sealed class WindowsPdfFontResolver : IFontResolver
{
    public static readonly WindowsPdfFontResolver Instance = new();

    private sealed record FontFace(string FaceName, string FileName);

    internal sealed record FontEmbeddingResolution(
        string RequestedFamily,
        string EmbeddedFamily,
        bool UsesRequestedFont,
        bool IsDirectlyEmbeddable,
        string Reason);

    private static readonly Dictionary<string, FontFace> FontFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yugothic-regular"] = new("yugothic-regular", "YuGothR.ttc"),
        ["yugothic-bold"] = new("yugothic-bold", "YuGothB.ttc"),
        ["meiryo-regular"] = new("meiryo-regular", "meiryo.ttc"),
        ["meiryo-bold"] = new("meiryo-bold", "meiryob.ttc"),
        ["msgothic-regular"] = new("msgothic-regular", "msgothic.ttc"),
        ["msgothic-bold"] = new("msgothic-bold", "msgothic.ttc"),
        ["yumincho-regular"] = new("yumincho-regular", "yumin.ttf"),
        ["yumincho-bold"] = new("yumincho-bold", "yumindb.ttf"),
        ["notosansjp-regular"] = new("notosansjp-regular", "NotoSansJP-Regular.otf"),
        ["notosansjp-bold"] = new("notosansjp-bold", "NotoSansJP-Bold.otf"),
        ["notosanscjk-regular"] = new("notosanscjk-regular", "NotoSansCJKjp-Regular.ttf"),
        ["notosanscjk-bold"] = new("notosanscjk-bold", "NotoSansCJKjp-Regular.ttf"),
        ["arial-regular"] = new("arial-regular", "arial.ttf"),
        ["arial-bold"] = new("arial-bold", "arialbd.ttf"),
        ["arialunicode-regular"] = new("arialunicode-regular", "ARIALUNI.TTF"),
        ["arialunicode-bold"] = new("arialunicode-bold", "ARIALUNI.TTF")
    };

    private static readonly Lazy<Dictionary<string, FontFace>> InstalledEmbeddableFontFaces = new(LoadInstalledEmbeddableFontFaces);

    private readonly Dictionary<string, byte[]> fontCache = new(StringComparer.OrdinalIgnoreCase);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var family = NormalizeFamilyName(familyName);
        var faceName = FindInstalledEmbeddableFace(familyName, bold)?.FaceName ?? (family switch
        {
            "notosansjp" => FontExists(bold ? "notosansjp-bold" : "notosansjp-regular") ? bold ? "notosansjp-bold" : "notosansjp-regular" : JapaneseSafeFaceName(bold),
            "notosanscjkjp" => FontExists(bold ? "notosanscjk-bold" : "notosanscjk-regular") ? bold ? "notosanscjk-bold" : "notosanscjk-regular" : JapaneseSafeFaceName(bold),
            "yugothicui" or "yugothic" or "meiryo" or "msgothic" => JapaneseSafeFaceName(bold),
            "yumincho" => FontExists(bold ? "yumincho-bold" : "yumincho-regular") ? bold ? "yumincho-bold" : "yumincho-regular" : JapaneseSafeFaceName(bold),
            "arial" => bold ? "arial-bold" : "arial-regular",
            "arialunicode" or "arialunicodems" => FontExists(bold ? "arialunicode-bold" : "arialunicode-regular") ? bold ? "arialunicode-bold" : "arialunicode-regular" : JapaneseSafeFaceName(bold),
            _ => JapaneseSafeFaceName(bold)
        });

        return FontExists(faceName)
            ? new FontResolverInfo(faceName, false, italic)
            : new FontResolverInfo(bold ? "arial-bold" : "arial-regular", false, italic);
    }

    public byte[]? GetFont(string faceName)
    {
        if (fontCache.TryGetValue(faceName, out var cached))
        {
            return cached;
        }

        var face = FindFontFace(faceName);
        if (face is null)
        {
            return null;
        }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), face.FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        fontCache[faceName] = bytes;
        return bytes;
    }

    private static bool FontExists(string faceName)
    {
        var face = FindFontFace(faceName);
        if (face is null)
        {
            return false;
        }

        return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), face.FileName));
    }

    private static string JapaneseSafeFaceName(bool bold)
    {
        var notoSansJp = bold ? "notosansjp-bold" : "notosansjp-regular";
        if (FontExists(notoSansJp))
        {
            return notoSansJp;
        }

        var notoSansCjk = bold ? "notosanscjk-bold" : "notosanscjk-regular";
        if (FontExists(notoSansCjk))
        {
            return notoSansCjk;
        }

        var arialUnicode = bold ? "arialunicode-bold" : "arialunicode-regular";
        if (FontExists(arialUnicode))
        {
            return arialUnicode;
        }

        var yuMincho = bold ? "yumincho-bold" : "yumincho-regular";
        if (FontExists(yuMincho))
        {
            return yuMincho;
        }

        return bold ? "arial-bold" : "arial-regular";
    }

    internal static FontEmbeddingResolution DescribeFamilyResolution(string familyName, bool bold = false)
    {
        var direct = FindInstalledEmbeddableFace(familyName, bold);
        var fallbackFaceName = JapaneseSafeFaceName(bold);
        var embeddedFamily = DisplayFamilyName(direct?.FaceName ?? fallbackFaceName);

        if (direct is not null)
        {
            return new FontEmbeddingResolution(
                familyName,
                embeddedFamily,
                UsesRequestedFont: true,
                IsDirectlyEmbeddable: true,
                Reason: "");
        }

        var requested = FindRegisteredFontFile(familyName);
        var reason = requested switch
        {
            null => "font file was not found in the Windows font registry",
            _ when IsEmbeddableFontFile(requested) => "font file could not be resolved as a direct PDF font face",
            _ => $"font file '{requested}' is not a TTF/OTF file supported for direct PDF embedding"
        };

        return new FontEmbeddingResolution(
            familyName,
            embeddedFamily,
            UsesRequestedFont: false,
            IsDirectlyEmbeddable: false,
            Reason: reason);
    }

    private static FontFace? FindFontFace(string faceName)
    {
        if (FontFiles.TryGetValue(faceName, out var knownFace))
        {
            return knownFace;
        }

        return InstalledEmbeddableFontFaces.Value.GetValueOrDefault(faceName);
    }

    private static FontFace? FindInstalledEmbeddableFace(string familyName, bool bold)
    {
        var fonts = InstalledEmbeddableFontFaces.Value;
        var normalized = NormalizeFamilyName(familyName);
        var styledKey = $"{normalized}-{(bold ? "bold" : "regular")}";
        if (fonts.TryGetValue(styledKey, out var styledFace))
        {
            return styledFace;
        }

        return fonts.GetValueOrDefault($"{normalized}-regular") ?? fonts.GetValueOrDefault(normalized);
    }

    private static Dictionary<string, FontFace> LoadInstalledEmbeddableFontFaces()
    {
        var fonts = new Dictionary<string, FontFace>(StringComparer.OrdinalIgnoreCase);
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key is null)
        {
            return fonts;
        }

        foreach (var valueName in key.GetValueNames())
        {
            if (key.GetValue(valueName) is not string fileName || !IsEmbeddableFontFile(fileName))
            {
                continue;
            }

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            var displayName = valueName.Split('(')[0].Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            foreach (var name in displayName.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                AddFontFace(fonts, name, fileName);
            }
        }

        return fonts;
    }

    private static void AddFontFace(Dictionary<string, FontFace> fonts, string displayName, string fileName)
    {
        var style = FontStyleSuffix(displayName);
        var familyName = StripStyleSuffix(displayName);
        var normalizedFamily = NormalizeFamilyName(familyName);
        if (string.IsNullOrWhiteSpace(normalizedFamily))
        {
            return;
        }

        var faceName = $"installed-{NormalizeFamilyName(displayName)}";
        var face = new FontFace(faceName, fileName);
        fonts.TryAdd(faceName, face);
        fonts.TryAdd($"{normalizedFamily}-{style}", face);
        if (style == "regular")
        {
            fonts.TryAdd(normalizedFamily, face);
        }
    }

    private static string? FindRegisteredFontFile(string familyName)
    {
        var normalized = NormalizeFamilyName(familyName);
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key is null)
        {
            return null;
        }

        foreach (var valueName in key.GetValueNames())
        {
            var displayName = valueName.Split('(')[0].Trim();
            foreach (var name in displayName.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(NormalizeFamilyName(StripStyleSuffix(name)), normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeFamilyName(name), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return key.GetValue(valueName) as string;
                }
            }
        }

        return null;
    }

    private static bool IsEmbeddableFontFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase);
    }

    private static string FontStyleSuffix(string displayName)
    {
        var normalized = NormalizeFamilyName(displayName);
        if (normalized.EndsWith("bold", StringComparison.Ordinal) ||
            normalized.Contains("bolditalic", StringComparison.Ordinal) ||
            normalized.Contains("semibold", StringComparison.Ordinal) ||
            normalized.Contains("extrabold", StringComparison.Ordinal) ||
            normalized.Contains("black", StringComparison.Ordinal))
        {
            return "bold";
        }

        return "regular";
    }

    private static string StripStyleSuffix(string displayName)
    {
        var suffixes = new[]
        {
            " Bold Italic",
            " Bold",
            " Italic",
            " Regular",
            " Medium",
            " SemiBold",
            " Semibold",
            " ExtraBold",
            " Black",
            " Light",
            " Thin"
        };

        foreach (var suffix in suffixes)
        {
            if (displayName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return displayName[..^suffix.Length].Trim();
            }
        }

        return displayName.Trim();
    }

    private static string DisplayFamilyName(string faceName)
    {
        if (faceName.StartsWith("installed-", StringComparison.OrdinalIgnoreCase))
        {
            return faceName["installed-".Length..];
        }

        return faceName switch
        {
            "notosansjp-regular" or "notosansjp-bold" => "Noto Sans JP",
            "notosanscjk-regular" or "notosanscjk-bold" => "Noto Sans CJK JP",
            "arialunicode-regular" or "arialunicode-bold" => "Arial Unicode MS",
            "yumincho-regular" or "yumincho-bold" => "Yu Mincho",
            "arial-regular" or "arial-bold" => "Arial",
            _ => faceName
        };
    }

    private static string NormalizeFamilyName(string familyName)
    {
        var builder = new StringBuilder();
        foreach (var value in familyName)
        {
            if (char.IsLetterOrDigit(value))
            {
                builder.Append(char.ToLowerInvariant(value));
            }
        }

        return builder.ToString();
    }
}

public static class SimplePdfWriter
{
    public static void SaveImages(string path, IEnumerable<Bitmap> images, SizeF pageSizeMm)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new Dictionary<int, long>();
        var pageObjects = new List<int>();
        var pageWidthPoints = MmToPoints(pageSizeMm.Width);
        var pageHeightPoints = MmToPoints(pageSizeMm.Height);

        stream.Write(Ascii("%PDF-1.4\n"));
        WriteObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");

        var pageIndex = 0;
        foreach (var image in images)
        {
            var pageObject = 3 + pageIndex * 3;
            var imageObject = pageObject + 1;
            var contentObject = pageObject + 2;
            pageObjects.Add(pageObject);

            var imageBytes = EncodeLosslessRgb(image);

            WriteObject(
                stream,
                offsets,
                pageObject,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfNumber(pageWidthPoints)} {PdfNumber(pageHeightPoints)}] /Resources << /XObject << /Im0 {imageObject} 0 R >> >> /Contents {contentObject} 0 R >>");
            WriteStreamObject(
                stream,
                offsets,
                imageObject,
                $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {imageBytes.Length} >>",
                imageBytes);

            var content = Ascii($"q\n{PdfNumber(pageWidthPoints)} 0 0 {PdfNumber(pageHeightPoints)} 0 0 cm\n/Im0 Do\nQ");
            WriteStreamObject(stream, offsets, contentObject, $"<< /Length {content.Length} >>", content);
            pageIndex++;
        }

        if (pageObjects.Count == 0)
        {
            stream.Close();
            File.Delete(path);
            return;
        }

        var kids = string.Join(" ", pageObjects.Select(pageObject => $"{pageObject} 0 R"));
        WriteObject(stream, offsets, 2, $"<< /Type /Pages /Kids [{kids}] /Count {pageObjects.Count} >>");

        var xref = stream.Position;
        var maxObjectNumber = offsets.Keys.Max();
        stream.Write(Ascii($"xref\n0 {maxObjectNumber + 1}\n0000000000 65535 f \n"));
        for (var objectNumber = 1; objectNumber <= maxObjectNumber; objectNumber++)
        {
            stream.Write(Ascii($"{offsets[objectNumber]:0000000000} 00000 n \n"));
        }
        stream.Write(Ascii($"trailer\n<< /Size {maxObjectNumber + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
    }

    private static byte[] EncodeLosslessRgb(Bitmap image)
    {
        using var rgbImage = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(rgbImage))
        {
            graphics.DrawImageUnscaled(image, 0, 0);
        }

        var rectangle = new Rectangle(0, 0, rgbImage.Width, rgbImage.Height);
        var bitmapData = rgbImage.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var sourceStride = Math.Abs(bitmapData.Stride);
            var sourceBytes = new byte[sourceStride * rgbImage.Height];
            Marshal.Copy(bitmapData.Scan0, sourceBytes, 0, sourceBytes.Length);

            using var raw = new MemoryStream(rgbImage.Width * rgbImage.Height * 3);
            var rowBytes = rgbImage.Width * 3;
            for (var y = 0; y < rgbImage.Height; y++)
            {
                var rowOffset = y * sourceStride;
                for (var x = 0; x < rowBytes; x += 3)
                {
                    raw.WriteByte(sourceBytes[rowOffset + x + 2]);
                    raw.WriteByte(sourceBytes[rowOffset + x + 1]);
                    raw.WriteByte(sourceBytes[rowOffset + x]);
                }
            }

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(zlib);
            }

            return compressed.ToArray();
        }
        finally
        {
            rgbImage.UnlockBits(bitmapData);
        }
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
    private static string PdfNumber(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static float MmToPoints(float millimeters) => millimeters * 72f / 25.4f;

    private static void WriteObject(Stream stream, Dictionary<int, long> offsets, int objectNumber, string body)
    {
        offsets[objectNumber] = stream.Position;
        stream.Write(Ascii($"{objectNumber} 0 obj\n"));
        stream.Write(Ascii(body));
        stream.Write(Ascii("\nendobj\n"));
    }

    private static void WriteStreamObject(Stream stream, Dictionary<int, long> offsets, int objectNumber, string dictionary, byte[] body)
    {
        offsets[objectNumber] = stream.Position;
        stream.Write(Ascii($"{objectNumber} 0 obj\n"));
        stream.Write(Ascii(dictionary));
        stream.Write(Ascii("\nstream\n"));
        stream.Write(body);
        stream.Write(Ascii("\nendstream\nendobj\n"));
    }
}
