namespace TagPrintStudio.Services;

public sealed class AppPaths
{
    public string RoamingRoot { get; }
    public string LocalRoot { get; }
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TagPrintStudio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TagPrintStudio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TagPrint Studio Exports"))
    {
    }

    public AppPaths(string roamingRoot, string localRoot, string defaultOutput)
    {
        RoamingRoot = roamingRoot;
        LocalRoot = localRoot;
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
}
