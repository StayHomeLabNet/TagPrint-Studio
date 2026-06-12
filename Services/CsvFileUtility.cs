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
            "write-temp" => LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
                ? "一時ファイルの作成に失敗しました。保存先フォルダーの書き込み権限、空き容量、ウイルス対策ソフトの干渉を確認してください。"
                : "Failed to create a temporary file. Check write permissions, free disk space, and antivirus interference.",
            "replace-target" => LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
                ? "既存CSVの置き換えに失敗しました。CSVが他のアプリで開かれていないか確認してください。"
                : "Failed to replace the existing CSV. Make sure the CSV is not open in another application.",
            "move-target" => LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
                ? "新しいCSVの配置に失敗しました。保存先フォルダーの権限やファイル名競合を確認してください。"
                : "Failed to place the new CSV. Check folder permissions and file name conflicts.",
            _ => LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
                ? "CSV保存中にエラーが発生しました。"
                : "An error occurred while saving the CSV."
        };

        var targetLine = LocalizationService.CurrentLanguage == Enums.AppLanguage.Japanese
            ? $"対象ファイル: {csvException.TargetPath}"
            : $"Target file: {csvException.TargetPath}";

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
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
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
