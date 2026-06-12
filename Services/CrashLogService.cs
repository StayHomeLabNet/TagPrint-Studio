using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TagPrintStudio.Services;

public static class CrashLogService
{
    public static string WriteCrashLog(Exception exception, string? context = null, AppPaths? appPaths = null)
    {
        return WriteCrashLog((object)exception, context, appPaths);
    }

    public static string WriteCrashLog(object exceptionObject, string? context = null, AppPaths? appPaths = null)
    {
        ArgumentNullException.ThrowIfNull(exceptionObject);

        appPaths ??= new AppPaths();
        Directory.CreateDirectory(appPaths.Logs);

        var fileName = $"crash-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log";
        var path = Path.Combine(appPaths.Logs, fileName);
        File.WriteAllText(path, BuildCrashLog(exceptionObject, context), Encoding.UTF8);
        return path;
    }

    public static string BuildCrashLog(object exceptionObject, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exceptionObject);

        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? version;

        var builder = new StringBuilder();
        builder.AppendLine("TagPrint Studio crash log");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Context: {NullIfWhiteSpace(context) ?? "Unhandled exception"}");
        builder.AppendLine($"App version: {informationalVersion}");
        builder.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine();
        builder.AppendLine("Exception:");

        if (exceptionObject is Exception exception)
        {
            builder.AppendLine(exception.ToString());
        }
        else
        {
            builder.AppendLine(exceptionObject.ToString() ?? exceptionObject.GetType().FullName ?? "Non-Exception object");
        }

        return builder.ToString();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
