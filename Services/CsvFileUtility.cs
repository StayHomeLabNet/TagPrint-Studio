using System.Text;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public sealed class CsvAtomicWriteException(string stage, string targetPath, string tempPath, Exception innerException)
    : IOException($"Atomic CSV save failed at stage '{stage}'. Target: '{targetPath}'. Temp: '{tempPath}'.", innerException)
{
    public string Stage { get; } = stage;
    public string TargetPath { get; } = targetPath;
    public string TempPath { get; } = tempPath;
}

public static class CsvFileUtility
{
    public const string DefaultDelimiter = ",";

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
        if (encodingName.Contains("UTF-16 LE", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.Unicode;
        }

        if (encodingName.Contains("UTF-16 BE", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.BigEndianUnicode;
        }

        if (encodingName.Contains("Shift_JIS", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.GetEncoding(932);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier:
            encodingName.Contains("UTF-8 BOM", StringComparison.OrdinalIgnoreCase));
    }

    public static Encoding EncodingForCsv(CsvPreviewRowContext context) => EncodingForCsv(context.EncodingName);

    public static void WriteAllTextAtomic(string path, string content, Encoding encoding)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("Could not resolve the destination folder for CSV save.");
        }

        Directory.CreateDirectory(directoryPath);
        var tempPath = Path.Combine(directoryPath, $"{Path.GetFileName(path)}.tmp-{Guid.NewGuid():N}");
        var stage = "prepare";

        try
        {
            stage = "write-temp";
            File.WriteAllText(tempPath, content, encoding);
            if (File.Exists(path))
            {
                stage = "replace-target";
                File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                stage = "move-target";
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup failures and preserve the original save error.
            }

            throw new CsvAtomicWriteException(stage, path, tempPath, ex);
        }
    }

    public static string BuildUserFriendlySaveErrorMessage(Exception exception)
    {
        if (exception is not CsvAtomicWriteException csvException)
        {
            return exception.Message;
        }

        var guidance = csvException.Stage switch
        {
            "write-temp" => LocalizationService.T("csvFile.saveError.writeTemp"),
            "replace-target" => LocalizationService.T("csvFile.saveError.replaceTarget"),
            "move-target" => LocalizationService.T("csvFile.saveError.moveTarget"),
            _ => LocalizationService.T("csvFile.saveError.generic")
        };

        var targetLine = LocalizationService.T("csvFile.saveError.targetFile", csvException.TargetPath);

        return $"{guidance}{Environment.NewLine}{Environment.NewLine}{targetLine}{Environment.NewLine}{csvException.InnerException?.Message ?? csvException.Message}";
    }


    public static string BuildCsvContent(CsvFileItem file, bool hasHeaderRow)
    {
        var lines = new List<string>();
        if (hasHeaderRow && file.HeaderRow is { Length: > 0 })
        {
            lines.Add(ToCsvLine(file.HeaderRow, file.Delimiter));
        }

        lines.AddRange(file.Rows.Select(row => ToCsvLine(row, file.Delimiter)));
        var newLine = string.IsNullOrEmpty(file.NewLine) ? Environment.NewLine : file.NewLine;
        return string.Join(newLine, lines) + newLine;
    }

    public static void WriteCsvFile(CsvFileItem file, bool hasHeaderRow, bool createBackup = true)
    {
        if (createBackup)
        {
            CsvBackupUtility.CreateTimestampedBackup(file.FilePath);
        }

        var content = BuildCsvContent(file, hasHeaderRow);
        WriteAllTextAtomic(file.FilePath, content, EncodingForCsv(file.EncodingName));
    }

    public static string NormalizeDelimiter(string? delimiter) => delimiter switch
    {
        "\t" => "\t",
        ";" => ";",
        "," => ",",
        _ => DefaultDelimiter
    };

    public static string DelimiterDisplayName(string? delimiter) => NormalizeDelimiter(delimiter) switch
    {
        "\t" => "Tab",
        ";" => "Semicolon",
        _ => "Comma"
    };

    public static bool IsBlankRow(string[] row) => row.Length == 0;

    public static string ToCsvLine(IEnumerable<string> cells, string? delimiter)
    {
        var resolvedDelimiter = NormalizeDelimiter(delimiter);
        return string.Join(resolvedDelimiter, cells.Select(cell => EscapeCsvCell(cell, resolvedDelimiter)));
    }

    public static string EscapeCsvCell(string? value, string? delimiter)
    {
        var text = value ?? "";
        var resolvedDelimiter = NormalizeDelimiter(delimiter);
        if (!text.Contains(resolvedDelimiter, StringComparison.Ordinal)
            && !text.Contains('"')
            && !text.Contains('\r')
            && !text.Contains('\n'))
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
