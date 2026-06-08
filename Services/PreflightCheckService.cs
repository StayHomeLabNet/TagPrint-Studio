using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IPreflightCheckService
{
    PreflightCheckResult Check(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles);
}

public sealed class PreflightCheckService : IPreflightCheckService
{
    private const double RecommendedCode128BarcodeWidthMm = 30.0;
    private const double RecommendedCode128BarcodeHeightMm = 12.0;
    private const double RecommendedCode128BarcodeHeightWithTextMm = 15.0;
    private const double RecommendedEan13BarcodeWidthMm = 32.0;
    private const double RecommendedEan13BarcodeHeightMm = 18.0;
    private const double RecommendedEan13BarcodeHeightWithTextMm = 22.0;
    private const double RecommendedQrCodeSizeMm = 20.0;
    private const double RecommendedQrCodeSizeWithTextMm = 24.0;

    public PreflightCheckResult Check(LayoutFile layout, PresetFile preset, List<CsvFileItem> csvFiles)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var printableRowCount = 0;

        errors.AddRange(CheckLayoutBounds(layout));
        errors.AddRange(CheckImages(layout));
        warnings.AddRange(CheckBarcodeWarnings(layout));
        errors.AddRange(CheckBarcodeErrors(layout, preset));

        foreach (var file in csvFiles.Where(file => file.Status == CsvFileStatus.Ok))
        {
            for (var rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
            {
                var row = file.Rows[rowIndex];
                if (!PrintConditionService.MatchesAll(preset, row)) continue;
                printableRowCount++;

                errors.AddRange(CheckRequiredFields(preset, file, rowIndex, row));
                errors.AddRange(CheckPriceFields(preset, file, rowIndex, row));
                errors.AddRange(CheckBarcodeValues(layout, preset, file, rowIndex, row));
            }
        }

        if (printableRowCount == 0)
        {
            errors.Add(LocalizationService.T("preflight.issue.noPrintableRows"));
        }

        return new PreflightCheckResult
        {
            PrintableRowCount = printableRowCount,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static IEnumerable<string> CheckLayoutBounds(LayoutFile layout)
    {
        foreach (var element in layout.Elements)
        {
            var label = string.IsNullOrWhiteSpace(element.Name) ? element.ElementId : element.Name;
            if (element.X + element.Width > layout.Tag.Width || element.Y + element.Height > layout.Tag.Height)
            {
                yield return LocalizationService.T("preflight.issue.elementOverflow", label);
            }
        }
    }

    private static IEnumerable<string> CheckImages(LayoutFile layout)
    {
        foreach (var element in layout.Elements.Where(element => element.ElementType == Enums.LayoutElementType.Image))
        {
            if (string.IsNullOrWhiteSpace(element.ImagePath)) continue;
            var resolved = ResolveImagePath(layout.FilePath, element.ImagePath);
            if (!File.Exists(resolved))
            {
                var label = string.IsNullOrWhiteSpace(element.Name) ? element.ElementId : element.Name;
                yield return LocalizationService.T("preflight.issue.imageMissing", label, element.ImagePath);
            }
        }
    }

    private static IEnumerable<string> CheckBarcodeWarnings(LayoutFile layout)
    {
        foreach (var element in layout.Elements.Where(element => element.Visible && element.ElementType == LayoutElementType.Barcode))
        {
            var label = ElementLabel(element);
            var recommended = RecommendedBarcodeSize(element);
            if (element.Width < recommended.Width || element.Height < recommended.Height)
            {
                yield return LocalizationService.T(
                    "preflight.issue.barcodeTooSmall",
                    label,
                    element.Width,
                    element.Height,
                    recommended.Width,
                    recommended.Height);
            }
        }
    }

    private static IEnumerable<string> CheckBarcodeErrors(LayoutFile layout, PresetFile preset)
    {
        foreach (var element in layout.Elements.Where(element => element.Visible && element.ElementType == LayoutElementType.Barcode))
        {
            var label = ElementLabel(element);
            if (!string.IsNullOrWhiteSpace(element.FieldId) && !preset.Fields.Any(field => string.Equals(field.FieldId, element.FieldId, StringComparison.OrdinalIgnoreCase)))
            {
                yield return LocalizationService.T("preflight.issue.barcodeFieldMissing", label, element.FieldId);
            }
        }
    }


    private static (double Width, double Height) RecommendedBarcodeSize(LayoutElement element)
    {
        if (IsQrCode(element.BarcodeType))
        {
            var size = element.BarcodeShowText ? RecommendedQrCodeSizeWithTextMm : RecommendedQrCodeSizeMm;
            return (size, size);
        }

        if (IsEan13Barcode(element.BarcodeType))
        {
            return (
                RecommendedEan13BarcodeWidthMm,
                element.BarcodeShowText ? RecommendedEan13BarcodeHeightWithTextMm : RecommendedEan13BarcodeHeightMm);
        }

        return (
            RecommendedCode128BarcodeWidthMm,
            element.BarcodeShowText ? RecommendedCode128BarcodeHeightWithTextMm : RecommendedCode128BarcodeHeightMm);
    }

    private static IEnumerable<string> CheckBarcodeValues(LayoutFile layout, PresetFile preset, CsvFileItem file, int rowIndex, string[] row)
    {
        foreach (var element in layout.Elements.Where(element => element.Visible && element.ElementType == LayoutElementType.Barcode))
        {
            var label = BarcodeLabel(element, preset);
            var value = BarcodeValue(element, preset, row);
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return LocalizationService.T("preflight.issue.barcodeEmpty", file.FileName, rowIndex + 1, label);
                continue;
            }

            if (IsQrCode(element.BarcodeType))
            {
                // QR Code accepts arbitrary text. Empty values are handled above.
            }
            else if (IsEan13Barcode(element.BarcodeType))
            {
                if (!TryNormalizeEan13(value, out _, out _))
                {
                    yield return LocalizationService.T("preflight.issue.barcodeEanInvalid", file.FileName, rowIndex + 1, label);
                }
            }
            else if (string.Equals(element.BarcodeType, "Code128", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsCode128BCompatible(value))
                {
                    yield return LocalizationService.T("preflight.issue.barcodeInvalid", file.FileName, rowIndex + 1, label);
                }
            }
            else
            {
                yield return LocalizationService.T("preflight.issue.barcodeUnsupportedType", file.FileName, rowIndex + 1, label, element.BarcodeType);
            }
        }
    }

    private static IEnumerable<string> CheckRequiredFields(PresetFile preset, CsvFileItem file, int rowIndex, string[] row)
    {
        foreach (var field in preset.Fields.Where(field => field.Enabled && field.Required))
        {
            var value = FieldValue(field, row);
            if (!string.IsNullOrWhiteSpace(value)) continue;

            var label = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
            yield return LocalizationService.T("preflight.issue.requiredEmpty", file.FileName, rowIndex + 1, label);
        }
    }

    private static IEnumerable<string> CheckPriceFields(PresetFile preset, CsvFileItem file, int rowIndex, string[] row)
    {
        foreach (var field in preset.Fields.Where(field => field.Enabled && IsPriceField(field)))
        {
            var value = FieldValue(field, row);
            if (!string.IsNullOrWhiteSpace(value)) continue;

            var label = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
            yield return LocalizationService.T("preflight.issue.priceEmpty", file.FileName, rowIndex + 1, label);
        }
    }

    private static bool IsPriceField(FieldDefinition field)
    {
        var text = Normalize(field.FieldId + " " + field.DisplayName);
        return text.Contains("price", StringComparison.Ordinal) ||
               text.Contains("amount", StringComparison.Ordinal) ||
               text.Contains("cost", StringComparison.Ordinal) ||
               text.Contains("価格", StringComparison.Ordinal) ||
               text.Contains("値段", StringComparison.Ordinal) ||
               text.Contains("金額", StringComparison.Ordinal) ||
               text.Contains("単価", StringComparison.Ordinal);
    }

    private static string FieldValue(FieldDefinition field, string[] row)
    {
        return field.ColumnIndex is int index && index >= 0 && index < row.Length
            ? row[index]?.Trim() ?? ""
            : "";
    }

    private static string BarcodeValue(LayoutElement element, PresetFile preset, string[] row)
    {
        if (string.IsNullOrWhiteSpace(element.FieldId))
        {
            return element.Text.Trim();
        }

        var field = preset.Fields.FirstOrDefault(field => string.Equals(field.FieldId, element.FieldId, StringComparison.OrdinalIgnoreCase));
        return field is null ? "" : FieldValue(field, row);
    }

    private static string BarcodeLabel(LayoutElement element, PresetFile preset)
    {
        if (!string.IsNullOrWhiteSpace(element.FieldId))
        {
            var field = preset.Fields.FirstOrDefault(field => string.Equals(field.FieldId, element.FieldId, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                return string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
            }
        }

        return ElementLabel(element);
    }

    private static string ElementLabel(LayoutElement element)
    {
        return string.IsNullOrWhiteSpace(element.Name) ? element.ElementId : element.Name;
    }

    private static bool IsCode128BCompatible(string value)
    {
        foreach (var ch in value)
        {
            if (ch < 32 || ch > 126)
            {
                return false;
            }
        }

        return true;
    }


    private static bool IsQrCode(string? barcodeType)
    {
        return string.Equals(barcodeType, "QRCode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR Code", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEan13Barcode(string? barcodeType)
    {
        return string.Equals(barcodeType, "EAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13 / JAN-13", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeEan13(string value, out string ean13, out string reason)
    {
        ean13 = "";
        reason = "";
        var digits = value.Trim();
        if (digits.Length == 0)
        {
            reason = "empty";
            return false;
        }

        if (digits.Any(ch => !char.IsDigit(ch)))
        {
            reason = "digits";
            return false;
        }

        if (digits.Length == 12)
        {
            ean13 = digits + CalculateEan13CheckDigit(digits);
            return true;
        }

        if (digits.Length == 13)
        {
            var expected = CalculateEan13CheckDigit(digits[..12]);
            if (digits[12] != expected)
            {
                reason = "check";
                return false;
            }

            ean13 = digits;
            return true;
        }

        reason = "length";
        return false;
    }

    private static char CalculateEan13CheckDigit(string first12Digits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = first12Digits[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    private static string ResolveImagePath(string layoutPath, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        if (Path.IsPathRooted(imagePath)) return imagePath;

        var layoutDirectory = Path.GetDirectoryName(layoutPath);
        return string.IsNullOrWhiteSpace(layoutDirectory)
            ? imagePath
            : Path.GetFullPath(Path.Combine(layoutDirectory, imagePath));
    }

    private static string Normalize(string text)
    {
        return text.Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();
    }
}

public sealed class PreflightCheckResult
{
    public int PrintableRowCount { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> Issues => Errors.Concat(Warnings).ToList();
    public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;
    public bool HasBlockingIssues => Errors.Count > 0;
}
