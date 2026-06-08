using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

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
        var completedOutputPaths = new List<string>();
        try
        {
            if (options.OutputScope == OutputScopeType.IndividualPngPerRow)
            {
                var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var completedRows = 0;
                var entries = PrintableRowService.PrintableRowEntries(preset, csvFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType);
                Report(progress, "PNG", 0, entries.Count);
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileKey = string.IsNullOrWhiteSpace(entry.File.FilePath) ? entry.File.FileName : entry.File.FilePath;
                    counters.TryGetValue(fileKey, out var index);
                    index++;
                    counters[fileKey] = index;

                    using var bitmap = renderer.RenderSingleTag(layout, preset, MapRow(preset, entry.Row), renderScale);
                    var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(entry.File.FileName));
                    var path = Path.Combine(options.OutputFolder, $"{baseName}_{index:0000}.png");
                    SaveBitmapAtomic(bitmap, path, ImageFormat.Png, cancellationToken);
                    completedOutputPaths.Add(path);
                    completedRows++;
                    Report(progress, "PNG", completedRows, entries.Count, entry.File.FileName);
                }
                return;
            }

            var validFiles = csvFiles.Where(f => f.Status == CsvFileStatus.Ok).ToList();
            var pageJobs = validFiles
                .Select(file => new
                {
                    File = file,
                    Pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType).ToList()
                })
                .ToList();
            var totalPages = pageJobs.Sum(job => job.Pages.Count);
            var completedPages = 0;
            Report(progress, "PNG", 0, totalPages);
            foreach (var job in pageJobs)
            {
                var pages = job.Pages;
                var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(job.File.FileName));
                for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var bitmap = RenderPageWithContext(layout, preset, pages[pageIndex], renderScale, "PNG", completedPages + 1, totalPages);
                    var suffix = pages.Count == 1 ? "page" : $"page_{pageIndex + 1:000}";
                    var path = Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.png");
                    SaveBitmapAtomic(bitmap, path, ImageFormat.Png, cancellationToken);
                    completedOutputPaths.Add(path);
                    completedPages++;
                    Report(progress, "PNG", completedPages, totalPages, job.File.FileName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            DeleteFiles(completedOutputPaths);
            throw;
        }
    }

    public void ExportPdf(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles, OutputOptions options, IProgress<OutputProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.OutputFolder);
        var renderScale = RenderScale(options);
        var paperSize = units.GetPaperSize(layout.Paper);
        var completedOutputPaths = new List<string>();

        try
        {
            if (options.OutputScope == OutputScopeType.SinglePdfAllCsv)
            {
                var pages = Pages(layout, preset, csvFiles.Where(f => f.Status == CsvFileStatus.Ok), options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType).ToList();
                if (pages.Count > 0)
                {
                    var path = Path.Combine(options.OutputFolder, "TagPrintStudio_Output.pdf");
                    Report(progress, "PDF", 0, pages.Count);
                    SavePdfAtomic(path, RenderPdfPages(layout, preset, pages, renderScale, "PDF", progress, cancellationToken), paperSize, cancellationToken);
                    completedOutputPaths.Add(path);
                }

                return;
            }

            foreach (var file in csvFiles.Where(f => f.Status == CsvFileStatus.Ok))
            {
                var pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType).ToList();
                if (pages.Count == 0)
                {
                    continue;
                }

                var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
                var path = Path.Combine(options.OutputFolder, $"{baseName}.pdf");
                Report(progress, "PDF", 0, pages.Count, file.FileName);
                SavePdfAtomic(path, RenderPdfPages(layout, preset, pages, renderScale, "PDF", progress, cancellationToken), paperSize, cancellationToken);
                completedOutputPaths.Add(path);
            }
        }
        catch (OperationCanceledException)
        {
            DeleteFiles(completedOutputPaths);
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
            options.PrintSortValueType).GetEnumerator();
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

        if (options.OutputFormat == OutputFormatType.Pdf)
        {
            if (options.OutputScope == OutputScopeType.SeparatePdfPerCsv)
            {
                outputs.AddRange(validFiles.Select(file => Path.Combine(options.OutputFolder, $"{SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(file.FileName))}.pdf")));
            }
            else
            {
                outputs.Add(Path.Combine(options.OutputFolder, "TagPrintStudio_Output.pdf"));
            }

            return outputs;
        }

        if (options.OutputScope == OutputScopeType.IndividualPngPerRow)
        {
            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in PrintableRowService.PrintableRowEntries(preset, validFiles, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType))
            {
                var fileKey = string.IsNullOrWhiteSpace(entry.File.FilePath) ? entry.File.FileName : entry.File.FilePath;
                counters.TryGetValue(fileKey, out var index);
                index++;
                counters[fileKey] = index;

                var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(entry.File.FileName));
                outputs.Add(Path.Combine(options.OutputFolder, $"{baseName}_{index:0000}.png"));
            }

            return outputs;
        }

        foreach (var file in validFiles)
        {
            var pages = Pages(layout, preset, file, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType).ToList();
            var baseName = SafeFileNameService.ToSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var suffix = pages.Count == 1 ? "page" : $"page_{pageIndex + 1:000}";
                outputs.Add(Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.png"));
            }
        }

        return outputs;
    }

    public static Dictionary<string, string> MapRow(PresetFile preset, string[] row)
    {
        var data = new Dictionary<string, string>();
        foreach (var field in preset.Fields.Where(f => f.Enabled))
        {
            data[field.FieldId] = field.ColumnIndex is int index && index >= 0 && index < row.Length ? row[index] : "";
        }
        return data;
    }

    private IEnumerable<List<Dictionary<string, string>>> Pages(LayoutFile layout, PresetFile preset, CsvFileItem file, PrintSortMode sortMode, string sortFieldId, PrintSortValueType sortValueType)
    {
        return Pages(layout, preset, [file], sortMode, sortFieldId, sortValueType);
    }

    private IEnumerable<List<Dictionary<string, string>>> Pages(LayoutFile layout, PresetFile preset, IEnumerable<CsvFileItem> files, PrintSortMode sortMode, string sortFieldId, PrintSortValueType sortValueType)
    {
        var rowsPerPage = renderer.GetRowsPerPage(layout);
        return PrintableRowService.PrintableRows(preset, files, sortMode, sortFieldId, sortValueType)
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
        var rowCount = PrintableRowService.PrintableRows(preset, files, options.PrintSortMode, options.PrintSortFieldId, options.PrintSortValueType).Count;
        return (int)Math.Ceiling(rowCount / (double)rowsPerPage);
    }

    private Bitmap RenderPageWithContext(
        LayoutFile layout,
        PresetFile preset,
        List<Dictionary<string, string>> page,
        float renderScale,
        string operation,
        int pageNumber,
        int totalPages)
    {
        try
        {
            return renderer.RenderPage(layout, preset, page, renderScale);
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

    private static void SaveBitmapAtomic(Bitmap bitmap, string path, ImageFormat format, CancellationToken cancellationToken)
    {
        var partialPath = PartialPath(path);
        DeleteIfExists(partialPath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bitmap.Save(partialPath, format);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partialPath, path, overwrite: true);
        }
        finally
        {
            DeleteIfExists(partialPath);
        }
    }

    private static void SavePdfAtomic(string path, IEnumerable<Bitmap> images, SizeF pageSizeMm, CancellationToken cancellationToken)
    {
        var partialPath = PartialPath(path);
        DeleteIfExists(partialPath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SimplePdfWriter.SaveImages(partialPath, images, pageSizeMm);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(partialPath))
            {
                File.Move(partialPath, path, overwrite: true);
            }
        }
        finally
        {
            DeleteIfExists(partialPath);
        }
    }

    private static string PartialPath(string path) => $"{path}.partial";

    private static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            DeleteIfExists(path);
        }
    }

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

            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Jpeg);
            var imageBytes = ms.ToArray();

            WriteObject(
                stream,
                offsets,
                pageObject,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfNumber(pageWidthPoints)} {PdfNumber(pageHeightPoints)}] /Resources << /XObject << /Im0 {imageObject} 0 R >> >> /Contents {contentObject} 0 R >>");
            WriteStreamObject(
                stream,
                offsets,
                imageObject,
                $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {imageBytes.Length} >>",
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
