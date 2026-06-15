using System.ComponentModel;
using System.Drawing.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private Control BuildPropertyEditor()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SurfaceColor };
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(8, 8, 8, 12), BackColor = SurfaceColor };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        paperSizeCombo = ComboBox(Enum.GetNames<PaperSizeType>());
        unitCombo = ComboBox(UnitItems());
        paperWidthBox = Number(1, 5000, 0.5m, 210);
        paperHeightBox = Number(1, 5000, 0.5m, 297);
        tagWidthBox = Number(1, 1000, 0.5m, 50);
        tagHeightBox = Number(1, 1000, 0.5m, 30);
        marginTopBox = Number(0, 1000, 0.5m, 10);
        marginBottomBox = Number(0, 1000, 0.5m, 10);
        marginLeftBox = Number(0, 1000, 0.5m, 10);
        marginRightBox = Number(0, 1000, 0.5m, 10);
        spacingHorizontalBox = Number(0, 1000, 0.5m, 5);
        spacingVerticalBox = Number(0, 1000, 0.5m, 5);
        autoGridCheck = new CheckBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 6, 3) };
        gridColumnsBox = Number(1, 100, 1, 3);
        gridRowsBox = Number(1, 100, 1, 8);

        nameBox = TextBox();
        typeCombo = ComboBox(Enum.GetNames<LayoutElementType>());
        xBox = Number();
        yBox = Number();
        widthBox = Number();
        heightBox = Number();
        zIndexBox = Number(decimal.MinValue, decimal.MaxValue, 1, 0);
        fieldIdCombo = ComboBox(FieldIdItems());
        fieldIdCombo.DropDownStyle = ComboBoxStyle.DropDown;
        barcodeTypeCombo = ComboBox(BarcodeTypeItems());
        textBox = TextBox();
        imagePathBox = TextBox();
        imageBrowseButton = Button("Browse", (_, _) => SelectImagePath());
        imageBrowseButton.Font = new Font(imageBrowseButton.Font.FontFamily, 8F, imageBrowseButton.Font.Style);
        imageBrowseButton.Padding = new Padding(6, 0, 6, 0);
        fontFamilyCombo = ComboBox(FontFamilyItems());
        fontFamilyCombo.DropDownStyle = ComboBoxStyle.DropDown;
        fontSizeBox = Number(1, 300, 0.5m, 10);
        fontSizeSourceCombo = ComboBox(FontSizeSourceItems());
        fontSizeFieldCombo = ComboBox(FieldIdItems());
        fontSizeFieldCombo.DropDownStyle = ComboBoxStyle.DropDown;
        minFontSizeBox = Number(1, 300, 0.5m, 2);
        maxFontSizeBox = Number(1, 300, 0.5m, 48);
        boldCheck = new CheckBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 6, 3) };
        lockedCheck = new CheckBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 6, 3) };
        visibleCheck = new CheckBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 6, 3) };
        barcodeShowTextCheck = new CheckBox { Text = ResolveUiText("Show Barcode Text"), Dock = DockStyle.Fill, Margin = new Padding(3, 5, 6, 3), Checked = true };
        BindIfLocalized(barcodeShowTextCheck, "Show Barcode Text");
        barcodeSizeGuideLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = MutedTextColor,
            Padding = new Padding(4, 0, 4, 0),
            Margin = new Padding(3, 3, 6, 3)
        };
        textColorButton = ColorButton();
        backgroundColorButton = ColorButton();
        borderColorButton = ColorButton();
        borderWidthBox = Number(0, 20, 0.25m, 0);
        alignCombo = ComboBox(Enum.GetNames<TextAlignType>());
        decimalStyleCombo = ComboBox(DecimalStyleItems());
        decimalPlacesBox = Number(-1, 6, 1, -1);
        fractionScaleBox = Number(0.2m, 1.5m, 0.05m, 0.65m);
        fractionYOffsetBox = Number(-10m, 10m, 0.1m, 0m);

        var row = 0;
        AddSection(panel, row++, "Paper");
        AddRow(panel, row++, "Size", paperSizeCombo, "Unit", unitCombo);
        AddRow(panel, row++, "Paper W", paperWidthBox, "Paper H", paperHeightBox);
        AddRow(panel, row++, "Tag W", tagWidthBox, "Tag H", tagHeightBox);
        AddRow(panel, row++, "Margin T", marginTopBox, "Margin B", marginBottomBox);
        AddRow(panel, row++, "Margin L", marginLeftBox, "Margin R", marginRightBox);
        AddRow(panel, row++, "Gap X", spacingHorizontalBox, "Gap Y", spacingVerticalBox);
        AddRow(panel, row++, "Auto Grid", autoGridCheck, "Cols", gridColumnsBox);
        AddRow(panel, row++, "Rows", gridRowsBox, "", new Label());
        AddSection(panel, row++, "Element");
        AddRow(panel, row++, "Name", nameBox, "Type", typeCombo);
        AddRow(panel, row++, "X", xBox, "Y", yBox);
        AddRow(panel, row++, "Width", widthBox, "Height", heightBox);
        AddRow(panel, row++, "ZIndex", zIndexBox, "Align", alignCombo);
        AddRow(panel, row++, "FieldId", fieldIdCombo, "Barcode Type", barcodeTypeCombo);
        AddRow(panel, row++, "Font", fontFamilyCombo, "Font Size", fontSizeBox);
        AddRow(panel, row++, "Font Size Source", fontSizeSourceCombo, "Font Size Field", fontSizeFieldCombo);
        AddRow(panel, row++, "Min Font Size", minFontSizeBox, "Max Font Size", maxFontSizeBox);
        AddRow(panel, row++, "Bold", boldCheck, "Locked", lockedCheck);
        AddRow(panel, row++, "Visible", visibleCheck, "", new Label());
        AddRow(panel, row++, "Barcode Text", barcodeShowTextCheck, "", new Label());
        AddWideRow(panel, row++, "Barcode Size Guide", barcodeSizeGuideLabel);
        AddRow(panel, row++, "Decimal", decimalStyleCombo, "Digits", decimalPlacesBox);
        AddRow(panel, row++, "Frac Scale", fractionScaleBox, "Frac Y", fractionYOffsetBox);
        AddRow(panel, row++, "Text", textBox, "", new Label());
        AddRow(panel, row++, "Text Color", textColorButton, "Background", backgroundColorButton);
        AddRow(panel, row++, "Border", borderColorButton, "Border W", borderWidthBox);
        AddRow(panel, row++, "Image", ImagePathEditor(), "", new Label());

        foreach (Control control in new Control[] { nameBox, textBox, imagePathBox })
        {
            control.Enter += (_, _) => CaptureUndo();
            control.TextChanged += (_, _) => ApplyEditorToElement();
        }
        foreach (var control in new[] { typeCombo, alignCombo, fieldIdCombo, barcodeTypeCombo, fontFamilyCombo, fontSizeSourceCombo, fontSizeFieldCombo, decimalStyleCombo })
        {
            control.DropDown += (_, _) => CaptureUndo();
            control.SelectedIndexChanged += (_, _) => ApplyEditorToElement();
        }
        fieldIdCombo.TextChanged += (_, _) => ApplyEditorToElement();
        fontSizeFieldCombo.TextChanged += (_, _) => ApplyEditorToElement();
        fontFamilyCombo.TextChanged += (_, _) => ApplyEditorToElement();
        foreach (var control in new[] { xBox, yBox, widthBox, heightBox, zIndexBox, fontSizeBox, minFontSizeBox, maxFontSizeBox, borderWidthBox, decimalPlacesBox, fractionScaleBox, fractionYOffsetBox })
        {
            control.Enter += (_, _) => CaptureUndo();
            control.ValueChanged += (_, _) => ApplyEditorToElement();
        }
        foreach (var control in new[] { paperSizeCombo, unitCombo })
        {
            control.DropDown += (_, _) => CaptureUndo();
            control.SelectedIndexChanged += (_, _) => ApplyLayoutSettings();
        }
        foreach (var control in new[] { paperWidthBox, paperHeightBox, tagWidthBox, tagHeightBox, marginTopBox, marginBottomBox, marginLeftBox, marginRightBox, spacingHorizontalBox, spacingVerticalBox, gridColumnsBox, gridRowsBox })
        {
            control.Enter += (_, _) => CaptureUndo();
            control.ValueChanged += (_, _) => ApplyLayoutSettings();
        }
        autoGridCheck.Click += (_, _) => CaptureUndo();
        autoGridCheck.CheckedChanged += (_, _) => ApplyLayoutSettings();
        textColorButton.Click += (_, _) => PickColor(textColorButton);
        backgroundColorButton.Click += (_, _) => PickColor(backgroundColorButton);
        borderColorButton.Click += (_, _) => PickColor(borderColorButton);
        boldCheck.Click += (_, _) => CaptureUndo();
        lockedCheck.Click += (_, _) => CaptureUndo();
        visibleCheck.Click += (_, _) => CaptureUndo();
        barcodeShowTextCheck.Click += (_, _) => CaptureUndo();
        boldCheck.CheckedChanged += (_, _) => ApplyEditorToElement();
        lockedCheck.CheckedChanged += (_, _) => ApplyEditorToElement();
        visibleCheck.CheckedChanged += (_, _) => ApplyEditorToElement();
        barcodeShowTextCheck.CheckedChanged += (_, _) => ApplyEditorToElement();

        scroll.Controls.Add(panel);
        return scroll;
    }


    private static object[] BarcodeTypeItems() => ["Code128", "EAN-13 / JAN-13", "QR Code"];

    private static object[] UnitItems() => ["mm", "px", "inch"];

    private static object[] FontSizeSourceItems() =>
    [
        FontSizeSourceDisplayText(FontSizeSourceType.Fixed),
        FontSizeSourceDisplayText(FontSizeSourceType.CsvField)
    ];

    private static string FontSizeSourceDisplayText(FontSizeSourceType source)
    {
        if (LocalizationService.CurrentLanguage != AppLanguage.Japanese)
        {
            return source == FontSizeSourceType.CsvField ? "CSV Field" : "Fixed";
        }

        return source == FontSizeSourceType.CsvField ? "CSV列" : "固定";
    }

    private FontSizeSourceType SelectedFontSizeSource()
    {
        var text = Convert.ToString(fontSizeSourceCombo.SelectedItem) ?? fontSizeSourceCombo.Text;
        if (string.Equals(text, "CSV Field", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "CSV列", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, FontSizeSourceType.CsvField.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return FontSizeSourceType.CsvField;
        }

        return FontSizeSourceType.Fixed;
    }

    private static bool IsQrCode(string? barcodeType)
    {
        return string.Equals(barcodeType, "QRCode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR Code", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "QR", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBarcodeType(string? barcodeType)
    {
        if (IsQrCode(barcodeType)) return "QR Code";
        return IsEan13Barcode(barcodeType) ? "EAN-13 / JAN-13" : "Code128";
    }

    private string SelectedBarcodeType()
    {
        var selected = Convert.ToString(barcodeTypeCombo.SelectedItem) ?? barcodeTypeCombo.Text;
        if (IsQrCode(selected)) return "QRCode";
        return IsEan13Barcode(selected) ? "EAN13" : "Code128";
    }

    private static bool IsEan13Barcode(string? barcodeType)
    {
        return string.Equals(barcodeType, "EAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "JAN-13", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(barcodeType, "EAN-13 / JAN-13", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadSelectedElementIntoEditor()    {
        var element = SelectedElement();
        loading = true;
        editorElement = element;
        LoadLayoutSettingsIntoEditor();
        SetEditorEnabled(element is not null);
        if (element is null)
        {
            nameBox.Text = "";
            typeCombo.SelectedIndex = -1;
            fieldIdCombo.Text = "";
            textBox.Text = "";
            imagePathBox.Text = "";
            fontSizeSourceCombo.SelectedItem = FontSizeSourceDisplayText(FontSizeSourceType.Fixed);
            fontSizeFieldCombo.Text = "";
            minFontSizeBox.Value = Clamp(2, minFontSizeBox);
            maxFontSizeBox.Value = Clamp(48, maxFontSizeBox);
            decimalStyleCombo.SelectedIndex = -1;
            decimalPlacesBox.Value = Clamp(-1, decimalPlacesBox);
            fractionScaleBox.Value = Clamp(0.65, fractionScaleBox);
            fractionYOffsetBox.Value = Clamp(0, fractionYOffsetBox);
            lockedCheck.Checked = false;
            visibleCheck.Checked = false;
            barcodeShowTextCheck.Checked = true;
            barcodeTypeCombo.SelectedItem = "Code128";
            UpdateElementSpecificEditorState(null);
            UpdateBarcodeSizeGuide(null);
            loading = false;
            RedrawPreview();
            return;
        }

        nameBox.Text = element.Name;
        typeCombo.SelectedItem = element.ElementType.ToString();
        xBox.Value = Clamp(element.X, xBox);
        yBox.Value = Clamp(element.Y, yBox);
        widthBox.Value = Clamp(element.Width, widthBox);
        heightBox.Value = Clamp(element.Height, heightBox);
        zIndexBox.Value = Clamp(element.ZIndex, zIndexBox);
        fieldIdCombo.Text = FieldDisplayText(element.FieldId);
        textBox.Text = element.Text;
        imagePathBox.Text = element.ImagePath;
        SelectFontFamily(element.FontFamily);
        fontSizeBox.Value = Clamp(element.FontSize, fontSizeBox);
        fontSizeSourceCombo.SelectedItem = FontSizeSourceDisplayText(element.FontSizeSource);
        fontSizeFieldCombo.Text = FieldDisplayText(element.FontSizeFieldId);
        minFontSizeBox.Value = Clamp(element.MinFontSize <= 0 ? 2 : element.MinFontSize, minFontSizeBox);
        maxFontSizeBox.Value = Clamp(element.MaxFontSize <= 0 ? 48 : element.MaxFontSize, maxFontSizeBox);
        SetColorButton(textColorButton, element.TextColor);
        SetColorButton(backgroundColorButton, element.BackgroundColor);
        SetColorButton(borderColorButton, element.BorderColor);
        borderWidthBox.Value = Clamp(element.BorderWidth, borderWidthBox);
        decimalStyleCombo.SelectedItem = element.DecimalDisplayStyle.ToString();
        decimalPlacesBox.Value = Clamp(element.DecimalPlaces, decimalPlacesBox);
        fractionScaleBox.Value = Clamp(element.FractionFontScale, fractionScaleBox);
        fractionYOffsetBox.Value = Clamp(element.FractionYOffset, fractionYOffsetBox);
        boldCheck.Checked = element.Bold;
        lockedCheck.Checked = element.Locked;
        visibleCheck.Checked = element.Visible;
        barcodeShowTextCheck.Checked = element.BarcodeShowText;
        barcodeTypeCombo.SelectedItem = NormalizeBarcodeType(element.BarcodeType);
        alignCombo.SelectedItem = element.TextAlign.ToString();
        UpdateElementSpecificEditorState(element);
        UpdateBarcodeSizeGuide(element);
        loading = false;
        RedrawPreview();
    }

    private void LoadLayoutSettingsIntoEditor()
    {
        paperSizeCombo.SelectedItem = currentLayout.Paper.PaperSize.ToString();
        unitCombo.SelectedItem = currentLayout.Paper.Unit switch
        {
            UnitType.Px => "px",
            UnitType.Inch => "inch",
            _ => "mm"
        };
        paperWidthBox.Value = Clamp(currentLayout.Paper.CustomWidth ?? 210, paperWidthBox);
        paperHeightBox.Value = Clamp(currentLayout.Paper.CustomHeight ?? 297, paperHeightBox);
        tagWidthBox.Value = Clamp(currentLayout.Tag.Width, tagWidthBox);
        tagHeightBox.Value = Clamp(currentLayout.Tag.Height, tagHeightBox);
        marginTopBox.Value = Clamp(currentLayout.Margin.Top, marginTopBox);
        marginBottomBox.Value = Clamp(currentLayout.Margin.Bottom, marginBottomBox);
        marginLeftBox.Value = Clamp(currentLayout.Margin.Left, marginLeftBox);
        marginRightBox.Value = Clamp(currentLayout.Margin.Right, marginRightBox);
        spacingHorizontalBox.Value = Clamp(currentLayout.Spacing.Horizontal, spacingHorizontalBox);
        spacingVerticalBox.Value = Clamp(currentLayout.Spacing.Vertical, spacingVerticalBox);
        autoGridCheck.Checked = currentLayout.Grid.AutoCalculate;
        gridColumnsBox.Value = Clamp(currentLayout.Grid.Columns <= 0 ? 1 : currentLayout.Grid.Columns, gridColumnsBox);
        gridRowsBox.Value = Clamp(currentLayout.Grid.Rows <= 0 ? 1 : currentLayout.Grid.Rows, gridRowsBox);
        UpdateLayoutSettingsEnabled();
    }

    private void ApplyLayoutSettings()
    {
        if (loading || applyingHistory) return;

        if (Enum.TryParse<PaperSizeType>(Convert.ToString(paperSizeCombo.SelectedItem), out var paperSize))
        {
            currentLayout.Paper.PaperSize = paperSize;
        }
        var unitText = Convert.ToString(unitCombo.SelectedItem) ?? unitCombo.Text;
        if (Enum.TryParse<UnitType>(unitText, ignoreCase: true, out var unit))
        {
            currentLayout.Paper.Unit = unit;
        }

        currentLayout.Paper.CustomWidth = (double)paperWidthBox.Value;
        currentLayout.Paper.CustomHeight = (double)paperHeightBox.Value;
        currentLayout.Tag.Width = (double)tagWidthBox.Value;
        currentLayout.Tag.Height = (double)tagHeightBox.Value;
        currentLayout.Margin.Top = (double)marginTopBox.Value;
        currentLayout.Margin.Bottom = (double)marginBottomBox.Value;
        currentLayout.Margin.Left = (double)marginLeftBox.Value;
        currentLayout.Margin.Right = (double)marginRightBox.Value;
        currentLayout.Spacing.Horizontal = (double)spacingHorizontalBox.Value;
        currentLayout.Spacing.Vertical = (double)spacingVerticalBox.Value;
        currentLayout.Grid.AutoCalculate = autoGridCheck.Checked;
        currentLayout.Grid.Columns = (int)gridColumnsBox.Value;
        currentLayout.Grid.Rows = (int)gridRowsBox.Value;

        UpdateLayoutSettingsEnabled();
        MarkDirty();
        RedrawPreview();
    }

    private void ApplyEditorToElement()
    {
        if (loading || applyingHistory) return;
        var element = editorElement;
        if (element is null || !elementRows.Contains(element)) return;

        element.Name = nameBox.Text;
        if (Enum.TryParse<LayoutElementType>(Convert.ToString(typeCombo.SelectedItem), out var type)) element.ElementType = type;
        element.X = (double)xBox.Value;
        element.Y = (double)yBox.Value;
        element.Width = (double)widthBox.Value;
        element.Height = (double)heightBox.Value;
        element.ZIndex = (int)zIndexBox.Value;
        element.FieldId = SelectedFieldId();
        element.Text = textBox.Text;
        element.ImagePath = imagePathBox.Text;
        element.FontFamily = SelectedFontFamily();
        element.FontSize = (double)fontSizeBox.Value;
        element.FontSizeSource = SelectedFontSizeSource();
        element.FontSizeFieldId = SelectedFieldId(fontSizeFieldCombo);
        element.MinFontSize = (double)minFontSizeBox.Value;
        element.MaxFontSize = Math.Max(element.MinFontSize, (double)maxFontSizeBox.Value);
        element.TextColor = Convert.ToString(textColorButton.Tag) ?? "#FF000000";
        element.BackgroundColor = Convert.ToString(backgroundColorButton.Tag) ?? "#00FFFFFF";
        element.BorderColor = Convert.ToString(borderColorButton.Tag) ?? "#FF000000";
        element.BorderWidth = (double)borderWidthBox.Value;
        if (Enum.TryParse<DecimalDisplayStyle>(Convert.ToString(decimalStyleCombo.SelectedItem), out var decimalStyle)) element.DecimalDisplayStyle = decimalStyle;
        element.DecimalPlaces = (int)decimalPlacesBox.Value;
        element.FractionFontScale = (double)fractionScaleBox.Value;
        element.FractionYOffset = (double)fractionYOffsetBox.Value;
        element.Bold = boldCheck.Checked;
        element.Locked = lockedCheck.Checked;
        element.Visible = visibleCheck.Checked;
        element.BarcodeShowText = barcodeShowTextCheck.Checked;
        element.BarcodeType = SelectedBarcodeType();
        if (Enum.TryParse<TextAlignType>(Convert.ToString(alignCombo.SelectedItem), out var align)) element.TextAlign = align;

        UpdateElementSpecificEditorState(element);
        UpdateBarcodeSizeGuide(element);
        elementsGrid.Refresh();
        currentLayout.Elements = elementRows.ToList();
        MarkDirty();
        RedrawPreview();
    }



    private static (double Width, double HeightNoText, double HeightWithText) RecommendedBarcodeSize(string? barcodeType)
    {
        if (IsQrCode(barcodeType))
        {
            return (20.0, 20.0, 24.0);
        }

        return IsEan13Barcode(barcodeType)
            ? (32.0, 18.0, 22.0)
            : (30.0, 12.0, 15.0);
    }

    private void UpdateBarcodeSizeGuide(LayoutElement? element)
    {
        if (barcodeSizeGuideLabel is null)
        {
            return;
        }

        if (element is null || element.ElementType != LayoutElementType.Barcode)
        {
            barcodeSizeGuideLabel.Text = "";
            barcodeSizeGuideLabel.Enabled = false;
            return;
        }

        var recommended = RecommendedBarcodeSize(element.BarcodeType);
        var recommendedHeight = element.BarcodeShowText ? recommended.HeightWithText : recommended.HeightNoText;
        var ok = element.Width >= recommended.Width && element.Height >= recommendedHeight;
        var status = LocalizationService.T(ok ? "layout.barcodeGuide.statusOk" : "layout.barcodeGuide.statusSmall");
        barcodeSizeGuideLabel.Enabled = true;
        barcodeSizeGuideLabel.Text = LocalizationService.T(
            "layout.barcodeGuide.text",
            NormalizeBarcodeType(element.BarcodeType),
            element.Width,
            element.Height,
            recommended.Width,
            recommendedHeight,
            status);
    }

    private void UpdateElementSpecificEditorState(LayoutElement? element)
    {
        var isBarcode = element?.ElementType == LayoutElementType.Barcode;
        var canUseFontSizeField = element is not null
            && element.ElementType is LayoutElementType.FieldText or LayoutElementType.StaticText or LayoutElementType.PriceValue;
        var usesCsvFontSize = canUseFontSizeField && SelectedFontSizeSource() == FontSizeSourceType.CsvField;
        if (fontSizeSourceCombo is not null)
        {
            fontSizeSourceCombo.Enabled = canUseFontSizeField;
        }
        if (fontSizeFieldCombo is not null)
        {
            fontSizeFieldCombo.Enabled = usesCsvFontSize;
        }
        if (minFontSizeBox is not null)
        {
            minFontSizeBox.Enabled = usesCsvFontSize;
        }
        if (maxFontSizeBox is not null)
        {
            maxFontSizeBox.Enabled = usesCsvFontSize;
        }
        if (barcodeShowTextCheck is not null)
        {
            barcodeShowTextCheck.Enabled = element is not null && isBarcode;
        }
        if (barcodeTypeCombo is not null)
        {
            barcodeTypeCombo.Enabled = element is not null && isBarcode;
        }
    }

    private void SelectFontFamily(string fontFamily)
    {
        for (var i = 0; i < fontFamilyCombo.Items.Count; i++)
        {
            if (fontFamilyCombo.Items[i] is FontFamilyItem item &&
                string.Equals(item.FontFamily, fontFamily, StringComparison.OrdinalIgnoreCase))
            {
                fontFamilyCombo.SelectedIndex = i;
                return;
            }
        }

        fontFamilyCombo.Text = fontFamily;
    }

    private string SelectedFontFamily()
    {
        if (fontFamilyCombo.SelectedItem is FontFamilyItem item)
        {
            return item.FontFamily;
        }

        var text = fontFamilyCombo.Text.Trim();
        var markerIndex = text.IndexOf("  [", StringComparison.Ordinal);
        if (markerIndex > 0)
        {
            text = text[..markerIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(text) ? "Yu Gothic UI" : text;
    }

    private void UpdateLayoutSettingsEnabled()
    {
        var customPaper = currentLayout.Paper.PaperSize == PaperSizeType.Custom;
        paperWidthBox.Enabled = customPaper;
        paperHeightBox.Enabled = customPaper;
        gridColumnsBox.Enabled = !autoGridCheck.Checked;
        gridRowsBox.Enabled = !autoGridCheck.Checked;
    }


    private static void AddWideRow(TableLayoutPanel panel, int row, string leftLabel, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panel.Controls.Add(PropertyLabel(leftLabel), 0, row);
        panel.Controls.Add(control, 1, row);
        panel.SetColumnSpan(control, 3);
    }

    private static void AddSection(TableLayoutPanel panel, int row, string label)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var section = new Label
        {
            Text = ResolveUiText(label),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Yu Gothic UI", 9.5F, FontStyle.Bold),
            ForeColor = TextColor,
            BackColor = HeaderBackColor,
            Padding = new Padding(8, 0, 0, 0),
            Margin = new Padding(0, 8, 6, 6),
            AccessibleDescription = LocalizationService.HasKey(NormalizeLocalizationKey(label)) ? $"loc:{NormalizeLocalizationKey(label)}" : null
        };
        panel.Controls.Add(section, 0, row);
        panel.SetColumnSpan(section, 4);
    }
}
