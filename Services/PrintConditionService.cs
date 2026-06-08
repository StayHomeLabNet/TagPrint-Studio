using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public static class PrintConditionService
{
    public static bool MatchesAll(PresetFile preset, string[] row)
    {
        var data = OutputService.MapRow(preset, row);
        return MatchesAll(preset, data);
    }

    public static bool MatchesAll(PresetFile preset, Dictionary<string, string> rowData)
    {
        foreach (var field in preset.Fields.Where(f => f.Enabled && f.PrintConditionEnabled))
        {
            var value = rowData.GetValueOrDefault(field.FieldId, "");
            if (!Matches(field, value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Matches(FieldDefinition field, string actualValue)
    {
        return field.PrintConditionType switch
        {
            PrintConditionType.Bool => MatchBool(field.PrintConditionOperator, actualValue),
            PrintConditionType.Date => MatchDate(field.PrintConditionOperator, actualValue, field.PrintConditionValue),
            PrintConditionType.Number => MatchNumber(field.PrintConditionOperator, actualValue, field.PrintConditionValue),
            PrintConditionType.Text => MatchText(field.PrintConditionOperator, actualValue, field.PrintConditionValue),
            _ => true
        };
    }

    private static bool MatchBool(PrintConditionOperator op, string actualValue)
    {
        if (!TryParseBool(actualValue, out var actual)) return false;

        return op switch
        {
            PrintConditionOperator.IsTrue => actual,
            PrintConditionOperator.IsFalse => !actual,
            _ => true
        };
    }

    private static bool MatchDate(PrintConditionOperator op, string actualValue, string compareValue)
    {
        if (!TryParseDate(actualValue, out var actual)) return false;
        if (!TryParseDate(compareValue, out var target)) return false;

        return op switch
        {
            PrintConditionOperator.Equal => actual.Date == target.Date,
            PrintConditionOperator.NotEqual => actual.Date != target.Date,
            PrintConditionOperator.GreaterThan => actual.Date > target.Date,
            PrintConditionOperator.GreaterThanOrEqual => actual.Date >= target.Date,
            PrintConditionOperator.LessThan => actual.Date < target.Date,
            PrintConditionOperator.LessThanOrEqual => actual.Date <= target.Date,
            PrintConditionOperator.OnOrAfter => actual.Date >= target.Date,
            PrintConditionOperator.OnOrBefore => actual.Date <= target.Date,
            _ => true
        };
    }

    private static bool MatchNumber(PrintConditionOperator op, string actualValue, string compareValue)
    {
        if (!double.TryParse(actualValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var actual) &&
            !double.TryParse(actualValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out actual))
        {
            return false;
        }

        if (!double.TryParse(compareValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var target) &&
            !double.TryParse(compareValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out target))
        {
            return false;
        }

        return op switch
        {
            PrintConditionOperator.Equal => actual == target,
            PrintConditionOperator.NotEqual => actual != target,
            PrintConditionOperator.GreaterThan => actual > target,
            PrintConditionOperator.GreaterThanOrEqual => actual >= target,
            PrintConditionOperator.LessThan => actual < target,
            PrintConditionOperator.LessThanOrEqual => actual <= target,
            _ => true
        };
    }

    private static bool MatchText(PrintConditionOperator op, string actualValue, string compareValue)
    {
        var comparison = StringComparer.CurrentCultureIgnoreCase.Compare(actualValue ?? "", compareValue ?? "");

        return op switch
        {
            PrintConditionOperator.Equal => comparison == 0,
            PrintConditionOperator.NotEqual => comparison != 0,
            PrintConditionOperator.GreaterThan => comparison > 0,
            PrintConditionOperator.GreaterThanOrEqual => comparison >= 0,
            PrintConditionOperator.LessThan => comparison < 0,
            PrintConditionOperator.LessThanOrEqual => comparison <= 0,
            _ => true
        };
    }

    private static bool TryParseBool(string value, out bool result)
    {
        var text = value.Trim();
        if (bool.TryParse(text, out result)) return true;

        switch (text.ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                result = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool TryParseDate(string value, out DateTime result)
    {
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "yyyy/M/d",
            "yyyy-MM",
            "yyyy/MM",
            "yyyy/M",
            "yyyyMMdd"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value.Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                if (format is "yyyy-MM" or "yyyy/MM" or "yyyy/M")
                {
                    result = new DateTime(result.Year, result.Month, 1);
                }
                return true;
            }
        }

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
