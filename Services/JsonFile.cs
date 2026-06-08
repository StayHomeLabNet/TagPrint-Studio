using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TagPrintStudio.Services;

public sealed record JsonRecoveryWarning(string Path, string BrokenPath, string Message);

public static class JsonFile
{
    private static readonly List<JsonRecoveryWarning> recoveryWarnings = [];

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<JsonRecoveryWarning> RecoveryWarnings => recoveryWarnings;

    public static void ClearRecoveryWarnings() => recoveryWarnings.Clear();

    public static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new T();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            var brokenPath = MoveBrokenJson(path);
            recoveryWarnings.Add(new JsonRecoveryWarning(path, brokenPath, ex.Message));
            return new T();
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }

    private static string MoveBrokenJson(string path)
    {
        var brokenPath = BrokenPath(path);
        File.Move(path, brokenPath);
        return brokenPath;
    }

    private static string BrokenPath(string path)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var candidate = $"{path}.broken-{timestamp}";
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; ; i++)
        {
            candidate = $"{path}.broken-{timestamp}-{i}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
