using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public sealed class BootstrapService
{
    private readonly AppPaths paths;

    public BootstrapService(AppPaths paths) => this.paths = paths;

    public AppSettings Initialize()
    {
        paths.EnsureDirectories();
        var settingsPath = Path.Combine(paths.Settings, "appsettings.json");
        var settings = JsonFile.Load<AppSettings>(settingsPath);
        if (string.IsNullOrWhiteSpace(settings.LastOutputFolder))
        {
            settings.LastOutputFolder = paths.DefaultOutput;
        }

        EnsureDefaultPreset();
        EnsureDefaultLayout();
        settings.FirstRunCompleted = true;
        JsonFile.Save(settingsPath, settings);
        return settings;
    }

    private void EnsureDefaultPreset()
    {
        var path = Path.Combine(paths.Presets, "default-price-tag.tpspreset");
        if (File.Exists(path) && !ContainsMojibake(path)) return;

        var now = DateTime.Now;
        var preset = new PresetFile
        {
            PresetId = "default-price-tag",
            PresetName = "Default Price Tag",
            Description = "標準の値札用プリセット",
            HasHeaderRow = true,
            LayoutRef = "default-price-tag.tpslayout",
            CreatedAt = now,
            UpdatedAt = now,
            Fields =
            [
                Field("product_name_1", "商品名1", 0, true, 1),
                Field("product_name_2", "商品名2", 1, false, 2),
                Field("price", "価格", 2, true, 3),
                Field("code_1", "コード1", 3, false, 4),
                Field("code_2", "コード2", 4, false, 5)
            ]
        };
        JsonFile.Save(path, preset);
    }

    private static FieldDefinition Field(string id, string name, int column, bool required, int order) => new()
    {
        FieldId = id,
        DisplayName = name,
        ColumnIndex = column,
        Required = required,
        DisplayOrder = order,
        Enabled = true
    };

    private void EnsureDefaultLayout()
    {
        var path = Path.Combine(paths.Layouts, "default-price-tag.tpslayout");
        if (File.Exists(path) && !ContainsMojibake(path)) return;

        var now = DateTime.Now;
        var layout = new LayoutFile
        {
            LayoutId = "default-price-tag-layout",
            LayoutName = "Default Price Tag Layout",
            Description = "標準の値札レイアウト",
            CreatedAt = now,
            UpdatedAt = now,
            Paper = new PaperSettings { PaperSize = PaperSizeType.A4, Unit = UnitType.Mm },
            Tag = new TagSettings { Width = 50, Height = 30 },
            Margin = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
            Spacing = new SpacingSettings { Horizontal = 5, Vertical = 5 },
            Grid = new GridSettings { AutoCalculate = true, Columns = 3, Rows = 8 },
            Elements =
            [
                Text("el_004", "固定ラベル", 0, LayoutElementType.StaticText, "", "SALE", 4, 1, 15, 5, 8, true, TextAlignType.Left),
                Text("el_001", "商品名1", 10, LayoutElementType.FieldText, "product_name_1", "", 4, 4, 42, 8, 10, true, TextAlignType.Center),
                Text("el_002", "商品名2", 20, LayoutElementType.FieldText, "product_name_2", "", 4, 11, 42, 5, 8, false, TextAlignType.Center),
                Text("el_003", "価格", 30, LayoutElementType.FieldText, "price", "", 4, 17, 42, 9, 15, true, TextAlignType.Right)
            ]
        };
        JsonFile.Save(path, layout);
    }

    private static LayoutElement Text(string id, string name, int zIndex, LayoutElementType type, string fieldId, string text, double x, double y, double width, double height, double fontSize, bool bold, TextAlignType align) => new()
    {
        ElementId = id,
        Name = name,
        ZIndex = zIndex,
        ElementType = type,
        FieldId = fieldId,
        Text = text,
        X = x,
        Y = y,
        Width = width,
        Height = height,
        FontSize = fontSize,
        Bold = bold,
        TextAlign = align,
        Visible = true
    };

    private static bool ContainsMojibake(string path)
    {
        var text = File.ReadAllText(path);
        return text.Contains('繝')
            || text.Contains('縺')
            || text.Contains('譛')
            || text.Contains('郢')
            || text.Contains('邵')
            || text.Contains('陷')
            || text.Contains('隴')
            || text.Contains('陜')
            || text.Contains('關');
    }
}
