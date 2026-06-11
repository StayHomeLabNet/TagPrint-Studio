using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ILayoutFieldSyncService
{
    bool SyncFieldElements(LayoutFile layout, IReadOnlyList<FieldDefinition> fieldDefinitions);
}

public sealed class LayoutFieldSyncService : ILayoutFieldSyncService
{
    public bool SyncFieldElements(LayoutFile layout, IReadOnlyList<FieldDefinition> fieldDefinitions)
    {
        var fieldsById = fieldDefinitions
            .Where(field => !string.IsNullOrWhiteSpace(field.FieldId))
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var changed = false;

        foreach (var element in layout.Elements.Where(element => element.ElementType == LayoutElementType.FieldText))
        {
            if (string.IsNullOrWhiteSpace(element.FieldId)) continue;
            if (!fieldsById.TryGetValue(element.FieldId, out var field)) continue;

            var displayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
            if (element.Name == displayName) continue;
            element.Name = displayName;
            changed = true;
        }

        var existingFieldIds = layout.Elements
            .Where(element => element.ElementType == LayoutElementType.FieldText)
            .Select(element => element.FieldId)
            .Where(fieldId => !string.IsNullOrWhiteSpace(fieldId))
            .ToHashSet(StringComparer.Ordinal);
        var excludedFieldIds = layout.ExcludedAutoFieldIds
            .Where(fieldId => !string.IsNullOrWhiteSpace(fieldId))
            .ToHashSet(StringComparer.Ordinal);

        var nextZIndex = layout.Elements.Count == 0 ? 10 : layout.Elements.Max(element => element.ZIndex) + 10;
        for (var i = 0; i < fieldDefinitions.Count; i++)
        {
            var field = fieldDefinitions[i];
            if (string.IsNullOrWhiteSpace(field.FieldId) || existingFieldIds.Contains(field.FieldId)) continue;
            if (excludedFieldIds.Contains(field.FieldId)) continue;

            var element = CreateFieldElement(layout, fieldDefinitions.Count, field, i, nextZIndex);
            layout.Elements.Add(element);
            existingFieldIds.Add(field.FieldId);
            nextZIndex += 10;
            changed = true;
        }

        return changed;
    }

    private static LayoutElement CreateFieldElement(LayoutFile layout, int fieldCount, FieldDefinition field, int fieldIndex, int zIndex)
    {
        var displayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
        var safeWidth = Math.Max(1, layout.Tag.Width - 4);
        var availableHeight = Math.Max(4, layout.Tag.Height - 4);
        var rowHeight = Clamp(availableHeight / Math.Max(1, fieldCount), 3, 6);
        var y = Clamp(2 + fieldIndex * rowHeight, 0, Math.Max(0, layout.Tag.Height - rowHeight));

        return new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}_{fieldIndex}",
            Name = displayName,
            ZIndex = zIndex,
            ElementType = LayoutElementType.FieldText,
            FieldId = field.FieldId,
            X = 2,
            Y = Math.Round(y, 1),
            Width = safeWidth,
            Height = Math.Round(rowHeight, 1),
            FontSize = 8,
            TextAlign = TextAlignType.Left,
            Visible = true
        };
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}

public interface ILayoutCatalogService
{
    string LayoutPathForName(string name);
    LayoutFile CreateDefaultLayout(string name, string filePath);
    List<string> GetReferencingPresetNames(string layoutFileName);
    void UpdatePresetLayoutRefs(string oldLayoutFileName, string newLayoutFileName);
}

public sealed class LayoutCatalogService(AppPaths paths) : ILayoutCatalogService
{
    public string LayoutPathForName(string name)
    {
        return Path.Combine(paths.Layouts, $"{SafeFileNameService.ToSafeFileName(name)}.tpslayout");
    }

    public LayoutFile CreateDefaultLayout(string name, string filePath)
    {
        var now = DateTime.Now;
        return new LayoutFile
        {
            LayoutId = Path.GetFileNameWithoutExtension(filePath),
            LayoutName = name,
            Description = "",
            CreatedAt = now,
            UpdatedAt = now,
            Paper = new PaperSettings { PaperSize = PaperSizeType.A4, Unit = UnitType.Mm },
            Tag = new TagSettings { Width = 50, Height = 30 },
            Margin = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
            Spacing = new SpacingSettings { Horizontal = 5, Vertical = 5 },
            Grid = new GridSettings { AutoCalculate = true, Columns = 3, Rows = 8 },
            FilePath = filePath,
            Elements =
            [
                new LayoutElement
                {
                    ElementId = $"el_{DateTime.Now:HHmmssfff}",
                    Name = LocalizationService.T("layout.default.text"),
                    ZIndex = 10,
                    ElementType = LayoutElementType.StaticText,
                    Text = LocalizationService.T("layout.default.textUpper"),
                    X = 4,
                    Y = 4,
                    Width = 42,
                    Height = 8,
                    FontSize = 10,
                    Bold = true,
                    TextAlign = TextAlignType.Center,
                    Visible = true
                }
            ]
        };
    }

    public List<string> GetReferencingPresetNames(string layoutFileName)
    {
        return Directory.GetFiles(paths.Presets, "*.tpspreset")
            .Select(path => new { Path = path, Preset = JsonFile.Load<PresetFile>(path) })
            .Where(item => string.Equals(item.Preset.LayoutRef, layoutFileName, StringComparison.OrdinalIgnoreCase))
            .Select(item => string.IsNullOrWhiteSpace(item.Preset.PresetName)
                ? Path.GetFileNameWithoutExtension(item.Path)
                : item.Preset.PresetName)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void UpdatePresetLayoutRefs(string oldLayoutFileName, string newLayoutFileName)
    {
        foreach (var presetPath in Directory.GetFiles(paths.Presets, "*.tpspreset"))
        {
            var preset = JsonFile.Load<PresetFile>(presetPath);
            if (!string.Equals(preset.LayoutRef, oldLayoutFileName, StringComparison.OrdinalIgnoreCase)) continue;
            preset.LayoutRef = newLayoutFileName;
            JsonFile.Save(presetPath, preset);
        }
    }
}
