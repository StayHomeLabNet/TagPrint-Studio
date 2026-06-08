using System.Text;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public static class CsvFileUtility
{
    static CsvFileUtility()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static bool IsPrintQueueCsv(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("print_queue_", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase);
    }

    public static Encoding EncodingForCsv(string encodingName)
    {
        if (encodingName.Contains("Shift_JIS", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.GetEncoding(932);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier:
            encodingName.Contains("UTF-8 BOM", StringComparison.OrdinalIgnoreCase)
            || encodingName.Contains("BOM", StringComparison.OrdinalIgnoreCase));
    }

    public static Encoding EncodingForCsv(CsvPreviewRowContext context) => EncodingForCsv(context.EncodingName);
}
