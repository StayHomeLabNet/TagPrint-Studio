namespace TagPrintStudio.Services;

public static class SafeFileNameService
{
    public static string ToSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "untitled" : safe;
    }
}
