using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed class CrashLogServiceTests
{
    [Fact]
    public void WriteCrashLog_WritesLogToAppPathsLogsFolder()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(
            Path.Combine(temp.DirectoryPath, "roaming"),
            Path.Combine(temp.DirectoryPath, "local"),
            Path.Combine(temp.DirectoryPath, "exports"));

        var logPath = CrashLogService.WriteCrashLog(
            new InvalidOperationException("sample failure"),
            "test context",
            paths);

        Assert.True(File.Exists(logPath));
        Assert.StartsWith(paths.Logs, logPath);
        var content = File.ReadAllText(logPath);
        Assert.Contains("TagPrint Studio crash log", content);
        Assert.Contains("test context", content);
        Assert.Contains("sample failure", content);
        Assert.Contains(nameof(InvalidOperationException), content);
    }

    [Fact]
    public void BuildCrashLog_HandlesNonExceptionObjects()
    {
        var content = CrashLogService.BuildCrashLog("non exception failure", "non exception context");

        Assert.Contains("non exception context", content);
        Assert.Contains("non exception failure", content);
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string tempPath = Path.Combine(Path.GetTempPath(), "TagPrintStudioTests", Guid.NewGuid().ToString("N"));
        public string DirectoryPath => tempPath;

        public void Dispose()
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }
}
