using System.Text;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public static class CsvHeaderResolver
{
    public static int FindIndex(CsvFileItem file, string? columnName)
    {
        return file.HeaderRow is { Length: > 0 } header
            ? FindIndex(header, columnName)
            : -1;
    }

    public static int FindIndex(IReadOnlyList<string>? header, string? columnName)
    {
        if (header is null || header.Count == 0 || string.IsNullOrWhiteSpace(columnName))
        {
            return -1;
        }

        for (var i = 0; i < header.Count; i++)
        {
            if (Matches(header[i], columnName))
            {
                return i;
            }
        }

        return -1;
    }

    public static int FindFirstIndex(IReadOnlyList<string>? header, params string?[] columnNames)
    {
        if (header is null || header.Count == 0 || columnNames.Length == 0)
        {
            return -1;
        }

        foreach (var columnName in columnNames)
        {
            var index = FindIndex(header, columnName);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    public static bool Matches(string? value, string? expected)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return string.Equals(Normalize(value), Normalize(expected), StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        return new string(normalized.Where(character =>
            !char.IsWhiteSpace(character) &&
            character is not '_' and not '-' and not '‐' and not '‑' and not '‒' and not '–' and not '—' and not '―' and not '－').ToArray());
    }
}
