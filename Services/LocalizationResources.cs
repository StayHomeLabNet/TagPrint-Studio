using TagPrintStudio.Enums;

namespace TagPrintStudio.Services;

public static partial class LocalizationService
{
    private static Dictionary<AppLanguage, Dictionary<string, string>> BuildResources() => new()
    {
        [AppLanguage.Japanese] = JapaneseResources(),
        [AppLanguage.English] = EnglishResources()
    };
}
