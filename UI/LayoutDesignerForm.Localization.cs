using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private static void BindIfLocalized(Control control, string textOrKey)
    {
        var key = NormalizeLocalizationKey(textOrKey);
        if (LocalizationService.HasKey(key))
        {
            LocalizationService.Bind(control, key);
        }
    }

    private static string ResolveUiText(string textOrKey)
    {
        var key = NormalizeLocalizationKey(textOrKey);
        return LocalizationService.HasKey(key) ? LocalizationService.T(key) : textOrKey;
    }

    private static string NormalizeLocalizationKey(string textOrKey)
    {
        return textOrKey switch
        {
            "Layout Designer" => "layout.title",
            "Layout" => "layout.label.layout",
            "Grid mm" => "layout.label.gridMm",
            "Preview Row" => "layout.label.previewRow",
            "Column" => "layout.label.column",
            "Elements" => "layout.section.elements",
            "Properties" => "layout.section.properties",
            "Canvas" => "layout.section.canvas",
            "Paper" => "layout.section.paper",
            "Element" => "layout.section.element",
            "New" => "layout.button.new",
            "Save" => "layout.button.save",
            "Save As" => "layout.button.saveAs",
            "Duplicate Layout" => "layout.button.duplicateLayout",
            "Rename" => "layout.button.rename",
            "Delete Layout" => "layout.button.deleteLayout",
            "Add Element" => "layout.button.addElement",
            "Add Field" => "layout.button.addField",
            "Add Image" => "layout.button.addImage",
            "Add Barcode" => "layout.button.addBarcode",
            "Duplicate" => "layout.button.duplicate",
            "Delete Element" => "layout.button.deleteElement",
            "Undo" => "layout.button.undo",
            "Redo" => "layout.button.redo",
            "Arrange" => "layout.button.arrange",
            "Save CSV" => "layout.button.saveCsv",
            "Browse" => "layout.button.browse",
            "Page Preview" => "layout.preview.page",
            "Single Tag" => "layout.preview.single",
            "Previous" => "layout.button.previous",
            "Next" => "layout.button.next",
            "Grid" => "layout.checkbox.grid",
            "Snap" => "layout.checkbox.snap",
            "Size" => "layout.property.size",
            "Unit" => "layout.property.unit",
            "Paper W" => "layout.property.paperW",
            "Paper H" => "layout.property.paperH",
            "Tag W" => "layout.property.tagW",
            "Tag H" => "layout.property.tagH",
            "Margin T" => "layout.property.marginT",
            "Margin B" => "layout.property.marginB",
            "Margin L" => "layout.property.marginL",
            "Margin R" => "layout.property.marginR",
            "Gap X" => "layout.property.gapX",
            "Gap Y" => "layout.property.gapY",
            "Auto Grid" => "layout.property.autoGrid",
            "Cols" => "layout.property.cols",
            "Rows" => "layout.property.rows",
            "Name" => "layout.property.name",
            "Type" => "layout.property.type",
            "Width" => "layout.property.width",
            "Height" => "layout.property.height",
            "Align" => "layout.property.align",
            "FieldId" => "layout.property.fieldId",
            "Font Size" => "layout.property.fontSize",
            "Font Size Source" => "layout.property.fontSizeSource",
            "Font Size Field" => "layout.property.fontSizeField",
            "Min Font Size" => "layout.property.minFontSize",
            "Max Font Size" => "layout.property.maxFontSize",
            "Font" => "layout.property.font",
            "Bold" => "layout.property.bold",
            "Locked" => "layout.property.locked",
            "Decimal" => "layout.property.decimal",
            "Digits" => "layout.property.digits",
            "Frac Scale" => "layout.property.fracScale",
            "Frac Y" => "layout.property.fracY",
            "Text" => "layout.property.text",
            "Visible" => "layout.property.visible",
            "Barcode Text" => "layout.property.barcodeText",
            "Show Barcode Text" => "layout.checkbox.barcodeShowText",
            "Barcode Type" => "layout.property.barcodeType",
            "Barcode Size Guide" => "layout.property.barcodeSizeGuide",
            "Text Color" => "layout.property.textColor",
            "Background" => "layout.property.background",
            "Border" => "layout.property.border",
            "Border W" => "layout.property.borderW",
            "Image" => "layout.property.image",
            _ => textOrKey
        };
    }
}
