using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IPresetService
{
    List<string> GetPresetFiles();
    PresetFile Load(string filePath);
    void Save(string filePath, PresetFile preset);
    List<string> Validate(PresetFile preset);
}

public interface ILayoutService
{
    List<string> GetLayoutFiles();
    LayoutFile Load(string filePath);
    void Save(string filePath, LayoutFile layout);
    List<string> Validate(LayoutFile layout);
}

public sealed class PresetService(AppPaths paths) : IPresetService
{
    public List<string> GetPresetFiles() => Directory.GetFiles(paths.Presets, "*.tpspreset").OrderBy(Path.GetFileName).ToList();
    public PresetFile Load(string filePath) => JsonFile.Load<PresetFile>(filePath);
    public void Save(string filePath, PresetFile preset) { preset.UpdatedAt = DateTime.Now; JsonFile.Save(filePath, preset); }
    public List<string> Validate(PresetFile preset)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(preset.PresetName)) errors.Add(LocalizationService.T("presetValidation.presetNameEmpty"));
        foreach (var field in preset.Fields.Where(f => f.Enabled && string.IsNullOrWhiteSpace(f.DisplayName))) errors.Add(LocalizationService.T("presetValidation.fieldNameEmpty"));
        return errors;
    }
}

public sealed class LayoutService(AppPaths paths) : ILayoutService
{
    public List<string> GetLayoutFiles() => Directory.GetFiles(paths.Layouts, "*.tpslayout").OrderBy(Path.GetFileName).ToList();
    public LayoutFile Load(string filePath)
    {
        var layout = JsonFile.Load<LayoutFile>(filePath);
        layout.FilePath = filePath;
        return layout;
    }

    public void Save(string filePath, LayoutFile layout)
    {
        layout.UpdatedAt = DateTime.Now;
        layout.FilePath = filePath;
        JsonFile.Save(filePath, ToPersistedLayout(layout, filePath));
    }
    public List<string> Validate(LayoutFile layout)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(layout.LayoutName)) errors.Add(LocalizationService.T("layoutValidation.layoutNameEmpty"));
        if (layout.Tag.Width <= 0 || layout.Tag.Height <= 0) errors.Add(LocalizationService.T("layoutValidation.invalidTagSize"));
        if (layout.Margin.Top < 0 || layout.Margin.Bottom < 0 || layout.Margin.Left < 0 || layout.Margin.Right < 0) errors.Add(LocalizationService.T("layoutValidation.negativeMargin"));
        if (layout.Spacing.Horizontal < 0 || layout.Spacing.Vertical < 0) errors.Add(LocalizationService.T("layoutValidation.negativeSpacing"));
        if (!layout.Grid.AutoCalculate && (layout.Grid.Columns <= 0 || layout.Grid.Rows <= 0)) errors.Add(LocalizationService.T("layoutValidation.invalidManualGrid"));

        foreach (var element in layout.Elements)
        {
            var label = string.IsNullOrWhiteSpace(element.Name) ? element.ElementId : element.Name;
            if (string.IsNullOrWhiteSpace(element.ElementId)) errors.Add(LocalizationService.T("layoutValidation.elementIdEmpty"));
            if (element.Width <= 0 || element.Height <= 0) errors.Add(LocalizationService.T("layoutValidation.invalidElementSize", label));
            if (element.X < 0 || element.Y < 0) errors.Add(LocalizationService.T("layoutValidation.negativeElementPosition", label));
            if (element.X + element.Width > layout.Tag.Width || element.Y + element.Height > layout.Tag.Height) errors.Add(LocalizationService.T("layoutValidation.elementOverflow", label));
        }

        return errors;
    }

    private static LayoutFile ToPersistedLayout(LayoutFile layout, string filePath)
    {
        return new LayoutFile
        {
            FormatVersion = layout.FormatVersion,
            LayoutId = layout.LayoutId,
            LayoutName = layout.LayoutName,
            Description = layout.Description,
            Paper = new PaperSettings
            {
                PaperSize = layout.Paper.PaperSize,
                Unit = layout.Paper.Unit,
                CustomWidth = layout.Paper.CustomWidth,
                CustomHeight = layout.Paper.CustomHeight
            },
            Tag = new TagSettings { Width = layout.Tag.Width, Height = layout.Tag.Height },
            Margin = new MarginSettings
            {
                Top = layout.Margin.Top,
                Bottom = layout.Margin.Bottom,
                Left = layout.Margin.Left,
                Right = layout.Margin.Right
            },
            Spacing = new SpacingSettings
            {
                Horizontal = layout.Spacing.Horizontal,
                Vertical = layout.Spacing.Vertical
            },
            Grid = new GridSettings
            {
                AutoCalculate = layout.Grid.AutoCalculate,
                Columns = layout.Grid.Columns,
                Rows = layout.Grid.Rows
            },
            ExcludedAutoFieldIds = layout.ExcludedAutoFieldIds
                .Where(fieldId => !string.IsNullOrWhiteSpace(fieldId))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Elements = layout.Elements.Select(element => new LayoutElement
            {
                ElementId = element.ElementId,
                Name = element.Name,
                ZIndex = element.ZIndex,
                ElementType = element.ElementType,
                FieldId = element.FieldId,
                Text = element.Text,
                ImagePath = ToStoredImagePath(element.ImagePath, filePath),
                X = element.X,
                Y = element.Y,
                Width = element.Width,
                Height = element.Height,
                FontFamily = element.FontFamily,
                FontSize = element.FontSize,
                FontSizeSource = element.FontSizeSource,
                FontSizeFieldId = element.FontSizeFieldId,
                MinFontSize = element.MinFontSize,
                MaxFontSize = element.MaxFontSize,
                Bold = element.Bold,
                TextColor = element.TextColor,
                BackgroundColor = element.BackgroundColor,
                BorderColor = element.BorderColor,
                BorderWidth = element.BorderWidth,
                TextAlign = element.TextAlign,
                DecimalDisplayStyle = element.DecimalDisplayStyle,
                DecimalPlaces = element.DecimalPlaces,
                FractionFontScale = element.FractionFontScale,
                FractionYOffset = element.FractionYOffset,

                BarcodeType = element.BarcodeType,
                BarcodeShowText = element.BarcodeShowText,

                KeepAspectRatio = element.KeepAspectRatio,
                Locked = element.Locked,
                Visible = element.Visible
            }).ToList(),
            CreatedAt = layout.CreatedAt,
            UpdatedAt = layout.UpdatedAt
        };
    }

    private static string ToStoredImagePath(string imagePath, string layoutPath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        if (!Path.IsPathRooted(imagePath)) return imagePath.Replace('\\', '/');

        var layoutDirectory = Path.GetDirectoryName(layoutPath);
        if (string.IsNullOrWhiteSpace(layoutDirectory)) return imagePath;

        try
        {
            var relative = Path.GetRelativePath(layoutDirectory, imagePath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return imagePath;
        }
    }
}
