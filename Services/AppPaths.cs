namespace TagPrintStudio.Services;

public sealed class AppPaths
{
    public static string DefaultRoamingRootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TagPrintStudio");
    public static string DefaultLocalRootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TagPrintStudio");
    public static string DefaultOutputRootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TagPrint Studio Exports");
    public static string DefaultSettingsRootPath => Path.Combine(DefaultRoamingRootPath, "settings");

    public string RoamingRoot { get; }
    public string LocalRoot { get; }
    public string AppDataRoot { get; }
    public string Presets { get; }
    public string Layouts { get; }
    public string Settings { get; }
    public string Recent { get; }
    public string Cache { get; }
    public string Temp { get; }
    public string Logs { get; }
    public string DefaultOutput { get; }

    public AppPaths()
        : this(
            DefaultRoamingRootPath,
            DefaultLocalRootPath,
            DefaultOutputRootPath,
            DefaultRoamingRootPath)
    {
    }

    public AppPaths(string roamingRoot, string localRoot, string defaultOutput, string? appDataRoot = null)
    {
        RoamingRoot = roamingRoot;
        LocalRoot = localRoot;
        AppDataRoot = string.IsNullOrWhiteSpace(appDataRoot) ? roamingRoot : appDataRoot;
        Presets = Path.Combine(RoamingRoot, "presets");
        Layouts = Path.Combine(RoamingRoot, "layouts");
        Settings = Path.Combine(RoamingRoot, "settings");
        Recent = Path.Combine(RoamingRoot, "recent");
        Cache = Path.Combine(LocalRoot, "cache");
        Temp = Path.Combine(LocalRoot, "temp");
        Logs = Path.Combine(LocalRoot, "logs");
        DefaultOutput = defaultOutput;
    }

    public void EnsureDirectories()
    {
        foreach (var path in new[] { Presets, Layouts, Settings, Recent, Cache, Temp, Logs, DefaultOutput })
        {
            Directory.CreateDirectory(path);
        }
    }

    public static AppPaths FromAppDataRoot(string appDataRoot)
    {
        return new AppPaths(
            Path.Combine(appDataRoot, "roaming"),
            Path.Combine(appDataRoot, "local"),
            DefaultOutputRootPath,
            appDataRoot);
    }
}
