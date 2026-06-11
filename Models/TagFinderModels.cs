namespace TagPrintStudio.Models;

public class TagFinderResultRow
{
    public required CsvFileItem File { get; init; }
    public required int RowIndex { get; init; }
    public bool Checked { get; set; }
    public string FileName => File.FileName;
    public int DisplayRowNumber => RowIndex + 1;
}
