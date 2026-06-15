using System.Drawing.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private Control ImagePathEditor()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.Controls.Add(imagePathBox, 0, 0);
        panel.Controls.Add(imageBrowseButton, 1, 0);
        return panel;
    }

    private void SelectImagePath()
    {
        var element = editorElement ?? SelectedElement();
        if (element is null) return;

        var imagePath = BrowseImagePath(imagePathBox.Text);
        if (string.IsNullOrWhiteSpace(imagePath)) return;

        CaptureUndo();
        element.ImagePath = ToStoredImagePath(imagePath);
        element.ElementType = LayoutElementType.Image;
        if (string.IsNullOrWhiteSpace(element.Name) || element.Name == "New Element")
        {
            element.Name = Path.GetFileNameWithoutExtension(imagePath);
        }

        loading = true;
        imagePathBox.Text = element.ImagePath;
        typeCombo.SelectedItem = LayoutElementType.Image.ToString();
        nameBox.Text = element.Name;
        loading = false;

        elementsGrid.Refresh();
        currentLayout.Elements = elementRows.ToList();
        MarkDirty();
        RedrawPreview();
    }

    private void AddBarcodeElement()
    {
        if (fieldDefinitions.Count == 0)
        {
            MessageBox.Show(LocalizationService.T("layout.message.noPresetFields"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new FieldSelectionDialog(fieldDefinitions);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedField is not { } field) return;

        CaptureUndo();
        var next = elementRows.Count == 0 ? 1 : elementRows.Max(e => e.ZIndex) + 10;
        var displayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
        var element = new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}",
            Name = LocalizationService.T("layout.default.barcode"),
            ZIndex = next,
            ElementType = LayoutElementType.Barcode,
            FieldId = field.FieldId,
            Text = displayName,
            X = Snap(2),
            Y = Snap(2),
            Width = Math.Max(28, currentLayout.Tag.Width / 2),
            Height = Math.Max(12, currentLayout.Tag.Height / 4),
            FontSize = 7,
            TextAlign = TextAlignType.Center,
            BarcodeType = "Code128",
            BarcodeShowText = true,
            Visible = true
        };
        elementRows.Add(element);
        currentLayout.Elements = elementRows.ToList();
        SelectElement(element);
        MarkDirty();
        RedrawPreview();
    }

    private string BrowseImagePath(string initialPath)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            FileName = ResolveImagePathForEditor(initialPath)
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : "";
    }

    private string ResolveImagePathForEditor(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        if (Path.IsPathRooted(imagePath)) return imagePath;

        var layoutDirectory = Path.GetDirectoryName(currentLayoutPath);
        return string.IsNullOrWhiteSpace(layoutDirectory)
            ? imagePath
            : Path.GetFullPath(Path.Combine(layoutDirectory, imagePath));
    }

    private string ToStoredImagePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return "";
        if (!Path.IsPathRooted(imagePath)) return imagePath.Replace('\\', '/');

        var layoutDirectory = Path.GetDirectoryName(currentLayoutPath);
        if (string.IsNullOrWhiteSpace(layoutDirectory)) return imagePath;

        try
        {
            return Path.GetRelativePath(layoutDirectory, imagePath).Replace('\\', '/');
        }
        catch
        {
            return imagePath;
        }
    }

    private static Button ColorButton() => new()
    {
        Dock = DockStyle.Fill,
        Text = "",
        Tag = "#FF000000",
        BackColor = Color.Black,
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(3, 3, 6, 3)
    };

    private void PickColor(Button button)
    {
        CaptureUndo();
        using var dialog = new ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = ParseColor(Convert.ToString(button.Tag), Color.Black)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SetColorButton(button, ToColorString(dialog.Color));
        ApplyEditorToElement();
    }

    private static void SetColorButton(Button button, string colorText)
    {
        var color = ParseColor(colorText, Color.Black);
        button.Tag = ToColorString(color);
        button.BackColor = color;
        button.Text = color.A == 0 ? "None" : "";
        button.ForeColor = color.GetBrightness() < 0.5 ? Color.White : Color.Black;
    }

    private static string ToColorString(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim().TrimStart('#');
        if (text.Length == 6 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
        if (text.Length == 8 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var argb))
        {
            return Color.FromArgb((argb >> 24) & 0xFF, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
        }
        return fallback;
    }

    private static ComboBox ComboBox(object[] items)
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 3, 6, 3) };
        combo.Items.AddRange(items);
        return combo;
    }

    private object[] FieldIdItems()
    {
        return fieldDefinitions
            .Select(f => string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldId : $"{f.FieldId} - {f.DisplayName}")
            .Cast<object>()
            .ToArray();
    }

    private static object[] FontFamilyItems()
    {
        using var fonts = new InstalledFontCollection();
        var installed = fonts.Families
            .Select(f => f.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var items = new List<object>();
        foreach (var recommended in RecommendedVectorPdfFonts())
        {
            if (installed.Any(font => string.Equals(font, recommended, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(FontFamilyItem.FromFamily(recommended, recommended: true));
            }
        }

        foreach (var name in installed.Where(name => !items.OfType<FontFamilyItem>().Any(item => string.Equals(item.FontFamily, name, StringComparison.OrdinalIgnoreCase))))
        {
            items.Add(FontFamilyItem.FromFamily(name));
        }

        return items.ToArray();
    }

    private static string[] RecommendedVectorPdfFonts() =>
    [
        "Noto Sans JP",
        "Noto Sans CJK JP",
        "Arial Unicode MS",
        "Yu Mincho",
        "Arial"
    ];

    private sealed class FontFamilyItem
    {
        public string FontFamily { get; init; } = "";
        public bool Recommended { get; init; }
        public bool PdfEmbeddable { get; init; }
        public string EmbeddedFamily { get; init; } = "";

        public override string ToString()
        {
            var status = PdfEmbeddable ? "PDF OK" : $"PDF -> {EmbeddedFamily}";
            return Recommended
                ? $"{FontFamily}  [Recommended, {status}]"
                : $"{FontFamily}  [{status}]";
        }

        public static FontFamilyItem FromFamily(string fontFamily, bool recommended = false)
        {
            var resolution = WindowsPdfFontResolver.DescribeFamilyResolution(fontFamily);
            return new FontFamilyItem
            {
                FontFamily = fontFamily,
                Recommended = recommended,
                PdfEmbeddable = resolution.IsDirectlyEmbeddable,
                EmbeddedFamily = resolution.EmbeddedFamily
            };
        }
    }

    private string FieldDisplayText(string fieldId)
    {
        var field = fieldDefinitions.FirstOrDefault(f => string.Equals(f.FieldId, fieldId, StringComparison.Ordinal));
        return field is null || string.IsNullOrWhiteSpace(field.DisplayName) ? fieldId : $"{field.FieldId} - {field.DisplayName}";
    }

    private string SelectedFieldId()
    {
        return SelectedFieldId(fieldIdCombo);
    }

    private static string SelectedFieldId(ComboBox combo)
    {
        var text = combo.Text.Trim();
        var separator = text.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 ? text[..separator] : text;
    }

    private static string[] DecimalStyleItems() =>
    [
        DecimalDisplayStyle.Normal.ToString(),
        DecimalDisplayStyle.SmallFractionBottom.ToString(),
        DecimalDisplayStyle.SmallFractionTop.ToString()
    ];

    private static NumericUpDown Number(decimal min = 0, decimal max = 1000, decimal increment = 0.5m, decimal value = 0) => new()
    {
        Dock = DockStyle.Fill,
        Minimum = min,
        Maximum = max,
        DecimalPlaces = increment < 1 ? 1 : 0,
        Increment = increment,
        Value = value,
        Margin = new Padding(3, 3, 6, 3)
    };

    private static void AddRow(TableLayoutPanel panel, int row, string leftLabel, Control left, string rightLabel, Control right)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.Controls.Add(PropertyLabel(leftLabel), 0, row);
        panel.Controls.Add(left, 1, row);
        panel.Controls.Add(PropertyLabel(rightLabel), 2, row);
        panel.Controls.Add(right, 3, row);
    }

    private static Label PropertyLabel(string text) => new()
    {
        Text = ResolveUiText(text),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = MutedTextColor,
        Padding = new Padding(8, 0, 0, 0),
        AccessibleDescription = LocalizationService.HasKey(NormalizeLocalizationKey(text)) ? $"loc:{NormalizeLocalizationKey(text)}" : null
    };

    private static void ApplyGridStyle(DataGridView grid)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = SurfaceColor;
        grid.GridColor = BorderColor;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderBackColor,
            ForeColor = TextColor,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            SelectionBackColor = HeaderBackColor,
            SelectionForeColor = TextColor,
            Padding = new Padding(4, 2, 4, 2)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SurfaceColor,
            ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = TextColor,
            Padding = new Padding(4, 2, 4, 2)
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 252, 254) };
        grid.RowTemplate.Height = 28;
        grid.ColumnHeadersHeight = 32;
    }

    private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width) => new() { DataPropertyName = property, HeaderText = ResolveUiText(header), Name = property, Width = width };
    private static DataGridViewCheckBoxColumn CheckColumn(string property, string header, int width) => new() { DataPropertyName = property, HeaderText = ResolveUiText(header), Name = property, Width = width };
}
