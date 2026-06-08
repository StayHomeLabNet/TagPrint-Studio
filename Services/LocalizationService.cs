using TagPrintStudio.Enums;

namespace TagPrintStudio.Services;

public static partial class LocalizationService
{
    private const string BindingPrefix = "loc:";

    private static readonly Dictionary<AppLanguage, Dictionary<string, string>> Resources = BuildResources();

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Japanese;

    public static event EventHandler? LanguageChanged;

    public static bool HasKey(string key)
    {
        return Resources[AppLanguage.Japanese].ContainsKey(key) || Resources[AppLanguage.English].ContainsKey(key);
    }

    public static void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language) return;
        CurrentLanguage = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key)
    {
        if (Resources.TryGetValue(CurrentLanguage, out var current) && current.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Resources[AppLanguage.English].TryGetValue(key, out value))
        {
            return value;
        }

        return key;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    public static void Bind(Control control, string key)
    {
        control.AccessibleDescription = BindingPrefix + key;
        control.Text = T(key);
    }

    public static void Bind(ToolStripItem item, string key)
    {
        item.Tag = BindingPrefix + key;
        item.Text = T(key);
    }

    public static void Apply(Control root)
    {
        ApplyControl(root);

        foreach (Control child in root.Controls)
        {
            Apply(child);
        }

        if (root is MenuStrip strip)
        {
            Apply(strip.Items);
        }
    }

    public static void Apply(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            if (item.Tag is string binding && binding.StartsWith(BindingPrefix, StringComparison.Ordinal))
            {
                item.Text = T(binding[BindingPrefix.Length..]);
            }

            if (item is ToolStripDropDownItem dropDown)
            {
                Apply(dropDown.DropDownItems);
            }
        }
    }

    private static void ApplyControl(Control control)
    {
        if (control.AccessibleDescription is string binding && binding.StartsWith(BindingPrefix, StringComparison.Ordinal))
        {
            control.Text = T(binding[BindingPrefix.Length..]);
        }
    }
}
