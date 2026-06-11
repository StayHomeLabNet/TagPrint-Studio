namespace TagPrintStudio.Models;

public class TagFinderMatch
{
    public required CsvFileItem File { get; init; }
    public required int RowIndex { get; init; }
    public bool Checked { get; init; }
}

public class TagFinderSaveSelection
{
    public required CsvFileItem File { get; init; }
    public required int RowIndex { get; init; }
    public bool Checked { get; init; }
    public bool WasChecked { get; init; }
}

public class TagFinderQueueSource
{
    public required CsvFileItem File { get; init; }
    public required int RowIndex { get; init; }
}

public class TagFinderQueueCsvResult
{
    public string FilePath { get; set; } = "";
    public string EncodingName { get; set; } = "";
    public string[] Header { get; set; } = [];
    public List<string[]> Rows { get; set; } = [];
}
