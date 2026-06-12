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
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = Path.Combine(
            string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory,
            $"{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Options));
            if (File.Exists(fullPath))
            {
                ReplaceFile(tempPath, fullPath);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void ReplaceFile(string sourcePath, string destinationPath)
    {
        try
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup. The destination file has either been saved or the original file is still present.
        }
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
