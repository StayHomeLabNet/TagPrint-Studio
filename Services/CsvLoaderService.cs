using System.Text;
using Microsoft.VisualBasic.FileIO;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvLoaderService
{
    CsvImportResult LoadFiles(IEnumerable<string> filePaths, PresetFile preset, bool hasHeaderRow);
}

public sealed class CsvLoaderService : ICsvLoaderService
{
    static CsvLoaderService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvImportResult LoadFiles(IEnumerable<string> filePaths, PresetFile preset, bool hasHeaderRow)
    {
        var result = new CsvImportResult();
        int? expectedColumns = null;
        foreach (var filePath in filePaths.Where(p => string.Equals(Path.GetExtension(p), ".csv", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var detection = DetectEncoding(filePath);
                var content = File.ReadAllText(filePath, detection.Encoding);
                var delimiter = DetectDelimiter(content);
                var parsed = ParseCsvContent(content, delimiter).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
                var item = new CsvFileItem
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Delimiter = delimiter,
                    EncodingName = detection.Kind switch
                    {
                        EncodingKind.Utf8Bom => "UTF-8 BOM",
                        EncodingKind.Utf8 => "UTF-8",
                        EncodingKind.ShiftJis => "Shift_JIS",
                        _ => detection.Encoding.WebName
                    }
                };
                if (parsed.Count == 0)
                {
                    item.Status = CsvFileStatus.LoadFailed;
                    item.Note = "データ行がありません。";
                    result.Files.Add(item);
                    continue;
                }

                item.HeaderRow = hasHeaderRow ? parsed[0] : Enumerable.Range(1, parsed[0].Length).Select(i => $"Column {i}").ToArray();
                item.Rows = hasHeaderRow ? parsed.Skip(1).ToList() : parsed;
                item.RowCount = item.Rows.Count;
                item.ColumnCount = item.HeaderRow.Length;

                if (expectedColumns is null) expectedColumns = item.ColumnCount;
                if (item.ColumnCount != expectedColumns)
                {
                    item.Status = CsvFileStatus.ColumnMismatch;
                    item.Note = $"列数が一致しません。期待値 {expectedColumns}, 実際 {item.ColumnCount}";
                }
                else
                {
                    var missing = preset.Fields
                        .Where(f => f.Enabled && f.Required && (f.ColumnIndex is null || f.ColumnIndex.Value >= item.ColumnCount))
                        .Select(f => f.DisplayName)
                        .ToList();
                    if (missing.Count > 0)
                    {
                        item.Status = CsvFileStatus.MissingRequiredField;
                        item.Note = "必須項目不足: " + string.Join(", ", missing);
                    }
                }

                if (detection.Kind == EncodingKind.ShiftJis)
                {
                    item.Note = string.IsNullOrWhiteSpace(item.Note)
                        ? "Encoding: Shift_JIS"
                        : $"{item.Note} / Encoding: Shift_JIS";
                    result.Warnings.Add($"{item.FileName}: Shift_JIS として読み込みました。");
                }

                result.Files.Add(item);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                result.Files.Add(new CsvFileItem
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    EncodingName = "",
                    Status = CsvFileStatus.LoadFailed,
                    Note = ex.Message
                });
            }
        }

        if (result.Files.Count == 0) result.Warnings.Add("CSVファイルが見つかりませんでした。");
        return result;
    }

    private static EncodingDetectionResult DetectEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (HasUtf8Bom(bytes))
        {
            return new EncodingDetectionResult(new UTF8Encoding(true, true), EncodingKind.Utf8Bom);
        }

        if (IsValidUtf8(bytes))
        {
            return new EncodingDetectionResult(new UTF8Encoding(false, true), EncodingKind.Utf8);
        }

        return new EncodingDetectionResult(Encoding.GetEncoding(932), EncodingKind.ShiftJis);
    }

    private static bool HasUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 &&
        bytes[0] == 0xEF &&
        bytes[1] == 0xBB &&
        bytes[2] == 0xBF;

    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string DetectDelimiter(string content)
    {
        var firstRecord = FirstRecordText(content);
        var candidates = new[] { ",", "\t", ";" };
        return candidates
            .Select(delimiter => new { Delimiter = delimiter, Count = CountDelimiter(firstRecord, delimiter[0]) })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Delimiter == "," ? 0 : 1)
            .First().Delimiter;
    }

    private static string FirstRecordText(string content)
    {
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                break;
            }
            else
            {
                current.Append(c);
            }
        }

        return current.ToString();
    }

    private static int CountDelimiter(string text, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }

    private static List<string[]> ParseCsvContent(string content, string delimiter)
    {
        var records = new List<string[]>();
        using var reader = new StringReader(content);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(delimiter);

        try
        {
            while (!parser.EndOfData)
            {
                records.Add(parser.ReadFields() ?? []);
            }
        }
        catch (MalformedLineException ex)
        {
            throw new FormatException($"CSV parse error at line {ex.LineNumber}: {ex.Message}", ex);
        }

        return records;
    }
    private sealed record EncodingDetectionResult(Encoding Encoding, EncodingKind Kind);
    private enum EncodingKind { Utf8Bom, Utf8, ShiftJis }
}
