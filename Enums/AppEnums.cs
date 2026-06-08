namespace TagPrintStudio.Enums;

public enum AppLanguage { Japanese, English }
public enum PaperSizeType { A4, A5, Letter, Postcard, Custom }
public enum UnitType { Mm, Px, Inch }
public enum LayoutElementType { FieldText, StaticText, Image, PriceValue, Barcode }
public enum TextAlignType { Left, Center, Right }
public enum FontSizeSourceType { Fixed, CsvField }
public enum DecimalDisplayStyle
{
    Normal,
    SmallFractionBottom,
    SmallFractionTop,
    SmallFraction = SmallFractionBottom
}
public enum OutputFormatType { Pdf, Png, Print }
public enum OutputScopeType { SinglePdfAllCsv, SeparatePdfPerCsv, IndividualPngPerRow }
public enum PrintScalingMode { ActualSize, FitPrintableArea }
public enum PrintSortMode { LoadOrder, ItemCode, SelectedField, PrintTargetOrder }
public enum PrintSortValueType { Text, Number, Natural, Date }
public enum DuplicateValueType { Text, Number, Date }
public enum DuplicateResolutionRule { KeepLargest, KeepSmallest, KeepNonEmpty, KeepFirst }
public enum DuplicateCopyMode { FillIfEmpty, Overwrite }
public enum CsvFileStatus { Ok, ColumnMismatch, EncodingWarning, MissingRequiredField, EmptyRowsSkipped, LoadFailed }
public enum PreviewMode { Page, SingleTag }
public enum PrintConditionType { None, Bool, Date, Number, Text }
public enum PrintConditionOperator
{
    None,
    IsTrue,
    IsFalse,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    OnOrAfter,
    OnOrBefore
}
