namespace TagPrintStudio.Models;

public sealed class TagFinderMatch
{
    public CsvFileItem File { get; init; } = null!;
    public int RowIndex { get; init; }
    public bool Checked { get; init; }
}

public sealed class TagFinderSaveSelection
{
    public CsvFileItem File { get; init; } = null!;
    public int RowIndex { get; init; }
    public bool Checked { get; init; }
    public bool WasChecked { get; init; }
}

public sealed class TagFinderQueueSource
{
    public CsvFileItem File { get; init; } = null!;
    public int RowIndex { get; init; }
}

public sealed class TagFinderQueueCsvResult
{
    public string FilePath { get; init; } = "";
    public string EncodingName { get; init; } = "";
    public string[] Header { get; init; } = [];
    public List<string[]> Rows { get; init; } = [];
}
