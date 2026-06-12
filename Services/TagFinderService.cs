using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ITagFinderService
{
    List<TagFinderMatch> Search(
        IEnumerable<CsvFileItem> csvFiles,
        int columnIndex,
        string query,
        string flagColumnName,
        bool autoCheckMatches);

    List<TagFinderMatch> CheckedRows(
        IEnumerable<CsvFileItem> csvFiles,
        string flagColumnName);
}

public sealed class TagFinderService : ITagFinderService
{
    public List<TagFinderMatch> Search(
        IEnumerable<CsvFileItem> csvFiles,
        int columnIndex,
        string query,
        string flagColumnName,
        bool autoCheckMatches)
    {
        var matches = new List<TagFinderMatch>();
        foreach (var file in csvFiles)
        {
            var flagIndex = CsvHeaderResolver.FindIndex(file, flagColumnName);
            for (var i = 0; i < file.Rows.Count; i++)
            {
                var row = file.Rows[i];
                if (columnIndex < 0 || columnIndex >= row.Length) continue;
                if (!string.Equals(row[columnIndex].Trim(), query, StringComparison.OrdinalIgnoreCase)) continue;

                matches.Add(new TagFinderMatch
                {
                    File = file,
                    RowIndex = i,
                    Checked = autoCheckMatches || IsTrue(ValueAt(row, flagIndex))
                });
            }
        }

        return matches;
    }

    public List<TagFinderMatch> CheckedRows(IEnumerable<CsvFileItem> csvFiles, string flagColumnName)
    {
        var matches = new List<TagFinderMatch>();
        foreach (var file in csvFiles)
        {
            var flagIndex = CsvHeaderResolver.FindIndex(file, flagColumnName);
            if (flagIndex < 0) continue;

            for (var i = 0; i < file.Rows.Count; i++)
            {
                var row = file.Rows[i];
                if (!IsTrue(ValueAt(row, flagIndex))) continue;

                matches.Add(new TagFinderMatch
                {
                    File = file,
                    RowIndex = i,
                    Checked = true
                });
            }
        }

        return matches;
    }


    private static string ValueAt(string[] row, int index) => index >= 0 && index < row.Length ? row[index] : "";

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
}
