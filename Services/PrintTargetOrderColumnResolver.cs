using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public static class PrintTargetOrderColumnResolver
{
    public const string DefaultColumnName = "tagPrintOrder";

    public static int FindColumnIndex(PresetFile preset, CsvFileItem file, string? preferredColumnName = null)
    {
        if (file.HeaderRow is { Length: > 0 } header)
        {
            var headerIndex = CsvHeaderResolver.FindFirstIndex(header, preferredColumnName, DefaultColumnName);
            if (headerIndex >= 0) return headerIndex;
        }

        var field = FindPresetField(preset, preferredColumnName);
        return field?.ColumnIndex ?? -1;
    }

    public static bool IsOrderColumnName(string value, string? preferredColumnName = null)
    {
        return CsvHeaderResolver.Matches(value, preferredColumnName)
            || CsvHeaderResolver.Matches(value, DefaultColumnName);
    }

    private static FieldDefinition? FindPresetField(PresetFile preset, string? preferredColumnName)
    {
        var fields = preset.Fields.Where(field => field.Enabled && field.ColumnIndex is not null).ToList();
        return fields.FirstOrDefault(field => CsvHeaderResolver.Matches(field.DisplayName, preferredColumnName))
            ?? fields.FirstOrDefault(field => CsvHeaderResolver.Matches(field.FieldId, preferredColumnName))
            ?? fields.FirstOrDefault(field => CsvHeaderResolver.Matches(field.DisplayName, DefaultColumnName))
            ?? fields.FirstOrDefault(field => CsvHeaderResolver.Matches(field.FieldId, DefaultColumnName));
    }
}
