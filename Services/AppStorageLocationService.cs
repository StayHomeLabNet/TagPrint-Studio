namespace TagPrintStudio.Services;

public interface IAppStorageLocationService
{
    AppPaths ResolvePaths();
    string LocatorFilePath { get; }
    bool IsUsingDefaultLocation();
    void PrepareLocation(AppPaths currentPaths, string newAppDataRoot, bool copyCurrentData);
    void UseCustomLocation(string newAppDataRoot);
    void ResetToDefaultLocation();
}

public sealed class AppStorageLocationService : IAppStorageLocationService
{
    private sealed class AppStorageLocationConfig
    {
        public string CustomRoot { get; set; } = "";
    }

    private readonly string defaultRoamingRoot;
    private readonly string defaultLocalRoot;
    private readonly string defaultOutputRoot;

    public string LocatorFilePath { get; }

    public AppStorageLocationService(
        string? locatorFilePath = null,
        string? defaultRoamingRoot = null,
        string? defaultLocalRoot = null,
        string? defaultOutputRoot = null)
    {
        this.defaultRoamingRoot = string.IsNullOrWhiteSpace(defaultRoamingRoot) ? AppPaths.DefaultRoamingRootPath : defaultRoamingRoot;
        this.defaultLocalRoot = string.IsNullOrWhiteSpace(defaultLocalRoot) ? AppPaths.DefaultLocalRootPath : defaultLocalRoot;
        this.defaultOutputRoot = string.IsNullOrWhiteSpace(defaultOutputRoot) ? AppPaths.DefaultOutputRootPath : defaultOutputRoot;
        LocatorFilePath = string.IsNullOrWhiteSpace(locatorFilePath)
            ? Path.Combine(AppPaths.DefaultSettingsRootPath, "storage-location.json")
            : locatorFilePath;
    }

    public AppPaths ResolvePaths()
    {
        var config = LoadConfig();
        var customRoot = NormalizePath(config.CustomRoot);
        return string.IsNullOrWhiteSpace(customRoot)
            ? new AppPaths(defaultRoamingRoot, defaultLocalRoot, defaultOutputRoot, defaultRoamingRoot)
            : AppPaths.FromAppDataRoot(customRoot);
    }

    public bool IsUsingDefaultLocation() => string.IsNullOrWhiteSpace(NormalizePath(LoadConfig().CustomRoot));

    public void PrepareLocation(AppPaths currentPaths, string newAppDataRoot, bool copyCurrentData)
    {
        var normalizedRoot = NormalizePath(newAppDataRoot);
        if (string.IsNullOrWhiteSpace(normalizedRoot))
        {
            throw new InvalidOperationException("App data root is required.");
        }

        if (ConflictsWithManagedSource(normalizedRoot, currentPaths))
        {
            throw new InvalidOperationException(LocalizationService.T("appStorage.issue.conflictsWithManagedSource"));
        }

        var targetPaths = AppPaths.FromAppDataRoot(normalizedRoot);
        Directory.CreateDirectory(normalizedRoot);
        targetPaths.EnsureDirectories();

        if (!copyCurrentData)
        {
            return;
        }

        CopyDirectory(currentPaths.Presets, targetPaths.Presets);
        CopyDirectory(currentPaths.Layouts, targetPaths.Layouts);
        CopyDirectory(currentPaths.Settings, targetPaths.Settings);
        CopyDirectory(currentPaths.Recent, targetPaths.Recent);
    }

    public void UseCustomLocation(string newAppDataRoot)
    {
        var normalizedRoot = NormalizePath(newAppDataRoot);
        if (string.IsNullOrWhiteSpace(normalizedRoot))
        {
            throw new InvalidOperationException("App data root is required.");
        }

        JsonFile.Save(LocatorFilePath, new AppStorageLocationConfig { CustomRoot = normalizedRoot });
    }

    public void ResetToDefaultLocation()
    {
        if (File.Exists(LocatorFilePath))
        {
            File.Delete(LocatorFilePath);
        }
    }

    private AppStorageLocationConfig LoadConfig() => JsonFile.Load<AppStorageLocationConfig>(LocatorFilePath);

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool ConflictsWithManagedSource(string selectedRoot, AppPaths currentPaths)
    {
        var managedSources = new[]
        {
            currentPaths.RoamingRoot,
            currentPaths.LocalRoot,
            currentPaths.Presets,
            currentPaths.Layouts,
            currentPaths.Settings,
            currentPaths.Recent
        };

        return managedSources.Any(source =>
            IsSubPathOf(selectedRoot, source) ||
            IsSubPathOf(source, selectedRoot));
    }

    private static bool IsSubPathOf(string path, string parentPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedParent = NormalizePath(parentPath);
        if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedParent))
        {
            return false;
        }

        return string.Equals(normalizedPath, normalizedParent, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
