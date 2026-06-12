using System.Text;
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
                var parsed = ParseCsvContent(content, delimiter)
                    .Select(NormalizeBlankRecord)
                    .ToList();
                var item = new CsvFileItem
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Delimiter = delimiter,
                    EncodingName = detection.Kind switch
                    {
                        EncodingKind.Utf8Bom => "UTF-8 BOM",
                        EncodingKind.Utf16LeBom => "UTF-16 LE BOM",
                        EncodingKind.Utf16BeBom => "UTF-16 BE BOM",
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

                var mismatchedRow = item.Rows
                    .Select((row, index) => new { Row = row, RowNumber = hasHeaderRow ? index + 2 : index + 1 })
                    .FirstOrDefault(entry => !CsvFileUtility.IsBlankRow(entry.Row) && entry.Row.Length != item.HeaderRow.Length);

                if (expectedColumns is null) expectedColumns = item.ColumnCount;
                if (item.ColumnCount != expectedColumns)
                {
                    item.Status = CsvFileStatus.ColumnMismatch;
                    item.Note = $"列数が一致しません。期待値 {expectedColumns}, 実際 {item.ColumnCount}";
                }
                else if (mismatchedRow is not null)
                {
                    item.Status = CsvFileStatus.ColumnMismatch;
                    item.Note = $"列数が一致しません。行 {mismatchedRow.RowNumber}: 期待値 {item.HeaderRow.Length}, 実際 {mismatchedRow.Row.Length}";
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

        if (HasUtf16LeBom(bytes))
        {
            return new EncodingDetectionResult(Encoding.Unicode, EncodingKind.Utf16LeBom);
        }

        if (HasUtf16BeBom(bytes))
        {
            return new EncodingDetectionResult(Encoding.BigEndianUnicode, EncodingKind.Utf16BeBom);
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

    private static bool HasUtf16LeBom(byte[] bytes) =>
        bytes.Length >= 2 &&
        bytes[0] == 0xFF &&
        bytes[1] == 0xFE;

    private static bool HasUtf16BeBom(byte[] bytes) =>
        bytes.Length >= 2 &&
        bytes[0] == 0xFE &&
        bytes[1] == 0xFF;

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
        var records = FirstRecordTexts(content, maxRecords: 20)
            .Where(record => record.Any(c => !char.IsWhiteSpace(c)))
            .ToList();

        if (records.Count == 0)
        {
            return ",";
        }

        var candidates = new[] { ",", "\t", ";" };
        return candidates
            .Select(delimiter => ScoreDelimiter(records, delimiter))
            .OrderByDescending(score => score.DelimitedRecordCount)
            .ThenByDescending(score => score.ConsistentDelimitedRecordCount)
            .ThenByDescending(score => score.ModeDelimiterCount)
            .ThenByDescending(score => score.TotalDelimiterCount)
            .ThenBy(score => DelimiterPreference(score.Delimiter))
            .First().Delimiter;
    }

    private static IEnumerable<string> FirstRecordTexts(string content, int maxRecords)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        var yielded = 0;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '"')
            {
                current.Append(c);
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                {
                    current.Append(content[i + 1]);
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                yield return current.ToString();
                yielded++;
                if (yielded >= maxRecords)
                {
                    yield break;
                }

                current.Clear();
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0 || content.Length == 0 || content.EndsWith('\r') || content.EndsWith('\n'))
        {
            yield return current.ToString();
        }
    }

    private static DelimiterScore ScoreDelimiter(IReadOnlyList<string> records, string delimiter)
    {
        var delimiterChar = delimiter[0];
        var counts = records
            .Select(record => CountDelimiter(record, delimiterChar))
            .Where(count => count > 0)
            .ToList();

        if (counts.Count == 0)
        {
            return new DelimiterScore(delimiter, 0, 0, 0, 0);
        }

        var mode = counts
            .GroupBy(count => count)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .First();

        return new DelimiterScore(
            delimiter,
            DelimitedRecordCount: counts.Count,
            ConsistentDelimitedRecordCount: mode.Count(),
            TotalDelimiterCount: counts.Sum(),
            ModeDelimiterCount: mode.Key);
    }

    private static int DelimiterPreference(string delimiter) => delimiter switch
    {
        "," => 0,
        "\t" => 1,
        ";" => 2,
        _ => 3
    };

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


    private static string[] NormalizeBlankRecord(string[] row)
    {
        // Store physically blank CSV lines as empty rows so CsvFileUtility can write
        // them back as blank lines instead of treating them as one-column data rows.
        return row.Length == 1 && row[0].Length == 0 ? [] : row;
    }

    private static List<string[]> ParseCsvContent(string content, string delimiter)
    {
        var records = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var delimiterChar = delimiter.Length > 0 ? delimiter[0] : ',';
        var inQuotes = false;
        var atFieldStart = true;
        var recordHadCharacters = false;
        var lineNumber = 1;
        var recordStartLine = 1;

        void FinishRecord()
        {
            if (!recordHadCharacters && row.Count == 0 && field.Length == 0)
            {
                records.Add([]);
            }
            else
            {
                row.Add(field.ToString());
                records.Add(row.ToArray());
            }

            row = [];
            field.Clear();
            atFieldStart = true;
            recordHadCharacters = false;
            recordStartLine = lineNumber;
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (c == '"')
            {
                recordHadCharacters = true;
                if (inQuotes)
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (atFieldStart)
                {
                    inQuotes = true;
                    atFieldStart = false;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == delimiterChar && !inQuotes)
            {
                recordHadCharacters = true;
                row.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
                continue;
            }

            if ((c == '\r' || c == '\n') && !inQuotes)
            {
                FinishRecord();

                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }

                lineNumber++;
                recordStartLine = lineNumber;
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                lineNumber++;
            }

            recordHadCharacters = true;
            field.Append(c);
            atFieldStart = false;
        }

        if (inQuotes)
        {
            throw new FormatException($"CSV parse error at line {recordStartLine}: malformed quoted field.");
        }

        if (recordHadCharacters || row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            records.Add(row.ToArray());
        }

        return records;
    }

    private sealed record DelimiterScore(string Delimiter, int DelimitedRecordCount, int ConsistentDelimitedRecordCount, int TotalDelimiterCount, int ModeDelimiterCount);
    private sealed record EncodingDetectionResult(Encoding Encoding, EncodingKind Kind);
    private enum EncodingKind { Utf8Bom, Utf16LeBom, Utf16BeBom, Utf8, ShiftJis }
}
