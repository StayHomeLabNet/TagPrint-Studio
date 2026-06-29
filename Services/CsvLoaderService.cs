using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvLoaderService
{
    CsvImportResult LoadFiles(IEnumerable<string> filePaths, PresetFile preset, bool hasHeaderRow, CsvLoadOptions? options = null);
}

public sealed class CsvLoaderService : ICsvLoaderService
{
    internal const long MaxCsvFileBytes = 100L * 1024 * 1024;

    static CsvLoaderService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvImportResult LoadFiles(IEnumerable<string> filePaths, PresetFile preset, bool hasHeaderRow, CsvLoadOptions? options = null)
    {
        options ??= new CsvLoadOptions();
        var result = new CsvImportResult();
        int? expectedColumns = null;
        foreach (var filePath in filePaths.Where(p => string.Equals(Path.GetExtension(p), ".csv", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                ValidateFileSize(filePath);
                var detection = DetectEncoding(filePath);
                var delimiter = DetectDelimiter(filePath, detection.Encoding);
                var newLine = DetectNewLine(filePath, detection.Encoding);
                var parsed = ReadCsvRecords(filePath, detection.Encoding, delimiter, options.MaxDataRowsPerFile, hasHeaderRow);
                var item = new CsvFileItem
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Delimiter = delimiter,
                    NewLine = newLine,
                    Snapshot = CsvFileUtility.CreateSnapshot(filePath),
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
                    item.Note = LocalizationService.T("csvLoader.issue.noDataRows");
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
                    item.Note = LocalizationService.T("csvLoader.issue.columnCountMismatch", expectedColumns, item.ColumnCount);
                }
                else if (mismatchedRow is not null)
                {
                    item.Status = CsvFileStatus.ColumnMismatch;
                    item.Note = LocalizationService.T("csvLoader.issue.rowColumnCountMismatch", mismatchedRow.RowNumber, item.HeaderRow.Length, mismatchedRow.Row.Length);
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
                        item.Note = LocalizationService.T("csvLoader.issue.missingRequiredFields", string.Join(", ", missing));
                    }
                }

                if (detection.Kind == EncodingKind.ShiftJis)
                {
                    item.Note = string.IsNullOrWhiteSpace(item.Note)
                        ? "Encoding: Shift_JIS"
                        : $"{item.Note} / Encoding: Shift_JIS";
                    result.Warnings.Add(LocalizationService.T("csvLoader.warning.shiftJis", item.FileName));
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
                    Snapshot = CsvFileUtility.CreateSnapshot(filePath),
                    Note = ex.Message
                });
            }
        }

        if (result.Files.Count == 0) result.Warnings.Add(LocalizationService.T("csvLoader.warning.noCsvFiles"));
        return result;
    }

    private static void ValidateFileSize(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxCsvFileBytes)
        {
            throw new InvalidOperationException(LocalizationService.T("csvLoader.issue.fileTooLarge", FormatBytes(fileInfo.Length), FormatBytes(MaxCsvFileBytes)));
        }
    }

    private static string FormatBytes(long bytes)
    {
        var mib = bytes / 1024d / 1024d;
        return $"{mib:0.#} MB";
    }

    private static string DetectNewLine(string path, Encoding encoding)
    {
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var previousWasCr = false;
        while (reader.Read() is var value && value >= 0)
        {
            var c = (char)value;
            if (previousWasCr)
            {
                return c == '\n' ? "\r\n" : "\r";
            }

            if (c == '\r')
            {
                previousWasCr = true;
            }
            else if (c == '\n')
            {
                return "\n";
            }
        }

        return previousWasCr ? "\r" : Environment.NewLine;
    }

    private static EncodingDetectionResult DetectEncoding(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var prefix = new byte[Math.Min(3, stream.Length)];
        _ = stream.Read(prefix, 0, prefix.Length);
        stream.Position = 0;

        if (HasUtf8Bom(prefix))
        {
            return new EncodingDetectionResult(new UTF8Encoding(true, true), EncodingKind.Utf8Bom);
        }

        if (HasUtf16LeBom(prefix))
        {
            return new EncodingDetectionResult(Encoding.Unicode, EncodingKind.Utf16LeBom);
        }

        if (HasUtf16BeBom(prefix))
        {
            return new EncodingDetectionResult(Encoding.BigEndianUnicode, EncodingKind.Utf16BeBom);
        }

        if (IsValidUtf8(stream))
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

    private static bool IsValidUtf8(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
            var buffer = new char[8192];
            while (reader.Read(buffer, 0, buffer.Length) > 0)
            {
            }
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string DetectDelimiter(string path, Encoding encoding)
    {
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var records = FirstRecordTexts(reader, maxRecords: 20)
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

    private static IEnumerable<string> FirstRecordTexts(TextReader reader, int maxRecords)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        var yielded = 0;

        while (reader.Read() is var value && value >= 0)
        {
            var c = (char)value;
            if (c == '"')
            {
                current.Append(c);
                if (inQuotes && reader.Peek() == '"')
                {
                    current.Append((char)reader.Read());
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
                if (c == '\r' && reader.Peek() == '\n')
                {
                    _ = reader.Read();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0 || yielded == 0)
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

    private static List<string[]> ReadCsvRecords(string path, Encoding encoding, string delimiter, int maxDataRows, bool hasHeaderRow)
    {
        var records = new List<string[]>();
        var recordIndex = 0;
        var dataRowCount = 0;
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        foreach (var record in ParseCsvContent(reader, delimiter).Select(NormalizeBlankRecord))
        {
            records.Add(record);
            var isHeaderRow = hasHeaderRow && recordIndex == 0;
            if (!isHeaderRow && !CsvFileUtility.IsBlankRow(record))
            {
                dataRowCount++;
                if (maxDataRows > 0 && dataRowCount > maxDataRows)
                {
                    throw new InvalidOperationException(LocalizationService.T("csvLoader.issue.tooManyRows", maxDataRows));
                }
            }

            recordIndex++;
        }

        return records;
    }

    private static IEnumerable<string[]> ParseCsvContent(TextReader reader, string delimiter)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var delimiterChar = delimiter.Length > 0 ? delimiter[0] : ',';
        var inQuotes = false;
        var atFieldStart = true;
        var recordHadCharacters = false;
        var lineNumber = 1;
        var recordStartLine = 1;

        string[] FinishRecord()
        {
            string[] record;
            if (!recordHadCharacters && row.Count == 0 && field.Length == 0)
            {
                record = [];
            }
            else
            {
                row.Add(field.ToString());
                record = row.ToArray();
            }

            row = [];
            field.Clear();
            atFieldStart = true;
            recordHadCharacters = false;
            recordStartLine = lineNumber;
            return record;
        }

        while (reader.Read() is var value && value >= 0)
        {
            var c = (char)value;

            if (c == '"')
            {
                recordHadCharacters = true;
                if (inQuotes)
                {
                    if (reader.Peek() == '"')
                    {
                        field.Append('"');
                        _ = reader.Read();
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
                yield return FinishRecord();

                if (c == '\r' && reader.Peek() == '\n')
                {
                    _ = reader.Read();
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
            throw new FormatException(LocalizationService.T("csvLoader.issue.parseError", recordStartLine));
        }

        if (recordHadCharacters || row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            yield return row.ToArray();
        }
    }

    private sealed record DelimiterScore(string Delimiter, int DelimitedRecordCount, int ConsistentDelimitedRecordCount, int TotalDelimiterCount, int ModeDelimiterCount);
    private sealed record EncodingDetectionResult(Encoding Encoding, EncodingKind Kind);
    private enum EncodingKind { Utf8Bom, Utf16LeBom, Utf16BeBom, Utf8, ShiftJis }
}
