namespace TagPrintStudio.UI;

internal static class AppIcon
{
    public static void Apply(Form form)
    {
        try
        {
            using var icon = Load();
            if (icon is not null) form.Icon = (Icon)icon.Clone();
        }
        catch
        {
            // Icon loading is cosmetic; keep the app usable if the asset is missing.
        }
    }

    private static Icon? Load()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(assetPath)) return new Icon(assetPath);

        var executablePath = Application.ExecutablePath;
        return File.Exists(executablePath) ? Icon.ExtractAssociatedIcon(executablePath) : null;
    }
}
