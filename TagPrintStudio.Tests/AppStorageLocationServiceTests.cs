using System.Text;
using TagPrintStudio.Services;

namespace TagPrintStudio.Tests;

public sealed partial class ServiceTests
{
    [Fact]
    public void AppStorageLocation_UsesDefaultPathsWhenLocatorIsMissing()
    {
        using var temp = new TempDirectory();
        var locatorPath = Path.Combine(temp.DirectoryPath, "storage-location.json");
        var roamingRoot = Path.Combine(temp.DirectoryPath, "default-roaming");
        var localRoot = Path.Combine(temp.DirectoryPath, "default-local");
        var defaultOutput = Path.Combine(temp.DirectoryPath, "default-output");
        var service = new AppStorageLocationService(locatorPath, roamingRoot, localRoot, defaultOutput);

        var paths = service.ResolvePaths();

        Assert.Equal(roamingRoot, paths.RoamingRoot);
        Assert.Equal(localRoot, paths.LocalRoot);
        Assert.Equal(roamingRoot, paths.AppDataRoot);
        Assert.True(service.IsUsingDefaultLocation());
    }

    [Fact]
    public void AppStorageLocation_UsesCustomRootWhenLocatorExists()
    {
        using var temp = new TempDirectory();
        var locatorPath = Path.Combine(temp.DirectoryPath, "storage-location.json");
        var customRoot = Path.Combine(temp.DirectoryPath, "custom-root");
        var service = new AppStorageLocationService(
            locatorPath,
            Path.Combine(temp.DirectoryPath, "default-roaming"),
            Path.Combine(temp.DirectoryPath, "default-local"),
            Path.Combine(temp.DirectoryPath, "default-output"));

        service.UseCustomLocation(customRoot);
        var paths = service.ResolvePaths();

        Assert.Equal(customRoot, paths.AppDataRoot);
        Assert.Equal(Path.Combine(customRoot, "roaming"), paths.RoamingRoot);
        Assert.Equal(Path.Combine(customRoot, "local"), paths.LocalRoot);
        Assert.False(service.IsUsingDefaultLocation());
    }

    [Fact]
    public void AppStorageLocation_PrepareLocation_CopiesManagedDirectories()
    {
        using var temp = new TempDirectory();
        var currentRoot = Path.Combine(temp.DirectoryPath, "current-root");
        var currentPaths = AppPaths.FromAppDataRoot(currentRoot);
        currentPaths.EnsureDirectories();
        File.WriteAllText(Path.Combine(currentPaths.Presets, "a.json"), "preset", Encoding.UTF8);
        File.WriteAllText(Path.Combine(currentPaths.Layouts, "b.json"), "layout", Encoding.UTF8);
        File.WriteAllText(Path.Combine(currentPaths.Settings, "c.json"), "settings", Encoding.UTF8);
        File.WriteAllText(Path.Combine(currentPaths.Recent, "d.json"), "recent", Encoding.UTF8);

        var locatorPath = Path.Combine(temp.DirectoryPath, "storage-location.json");
        var service = new AppStorageLocationService(
            locatorPath,
            Path.Combine(temp.DirectoryPath, "default-roaming"),
            Path.Combine(temp.DirectoryPath, "default-local"),
            Path.Combine(temp.DirectoryPath, "default-output"));
        var targetRoot = Path.Combine(temp.DirectoryPath, "new-root");

        service.PrepareLocation(currentPaths, targetRoot, copyCurrentData: true);

        var targetPaths = AppPaths.FromAppDataRoot(targetRoot);
        Assert.True(File.Exists(Path.Combine(targetPaths.Presets, "a.json")));
        Assert.True(File.Exists(Path.Combine(targetPaths.Layouts, "b.json")));
        Assert.True(File.Exists(Path.Combine(targetPaths.Settings, "c.json")));
        Assert.True(File.Exists(Path.Combine(targetPaths.Recent, "d.json")));
    }
}
