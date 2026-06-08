using TagPrintStudio.Enums;
using System.Drawing.Printing;
using System.Text.Json.Serialization;
using TagPrintStudio.Services;

namespace TagPrintStudio.Models;

public class PresetFile
{
    public int FormatVersion { get; set; } = 1;
    public string PresetId { get; set; } = "";
    public string PresetName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool HasHeaderRow { get; set; } = true;
    public string LayoutRef { get; set; } = "";
    public List<FieldDefinition> Fields { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FieldDefinition
{
    public string FieldId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? ColumnIndex { get; set; }
    public bool Required { get; set; }
    public int DisplayOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public bool PrintConditionEnabled { get; set; }
    public PrintConditionType PrintConditionType { get; set; }
    public PrintConditionOperator PrintConditionOperator { get; set; }
    public string PrintConditionValue { get; set; } = "";
}

public class LayoutFile
{
    public int FormatVersion { get; set; } = 1;
    public string LayoutId { get; set; } = "";
    public string LayoutName { get; set; } = "";
    public string Description { get; set; } = "";
    public PaperSettings Paper { get; set; } = new();
    public TagSettings Tag { get; set; } = new();
    public MarginSettings Margin { get; set; } = new();
    public SpacingSettings Spacing { get; set; } = new();
    public GridSettings Grid { get; set; } = new();
    public List<LayoutElement> Elements { get; set; } = [];
    public List<string> ExcludedAutoFieldIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [JsonIgnore]
    public string FilePath { get; set; } = "";
}

public class PaperSettings
{
    public PaperSizeType PaperSize { get; set; } = PaperSizeType.A4;
    public UnitType Unit { get; set; } = UnitType.Mm;
    public double? CustomWidth { get; set; }
    public double? CustomHeight { get; set; }
}

public class TagSettings { public double Width { get; set; } public double Height { get; set; } }
public class MarginSettings { public double Top { get; set; } public double Bottom { get; set; } public double Left { get; set; } public double Right { get; set; } }
public class SpacingSettings { public double Horizontal { get; set; } public double Vertical { get; set; } }
public class GridSettings { public bool AutoCalculate { get; set; } = true; public int Columns { get; set; } public int Rows { get; set; } }

public class LayoutElement
{
    public string ElementId { get; set; } = "";
    public string Name { get; set; } = "";
    public int ZIndex { get; set; }
    public LayoutElementType ElementType { get; set; }
    public string FieldId { get; set; } = "";
    public string Text { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FontFamily { get; set; } = "Yu Gothic UI";
    public double FontSize { get; set; } = 10;
    public FontSizeSourceType FontSizeSource { get; set; } = FontSizeSourceType.Fixed;
    public string FontSizeFieldId { get; set; } = "";
    public double MinFontSize { get; set; } = 6;
    public double MaxFontSize { get; set; } = 48;
    public bool Bold { get; set; }
    public string TextColor { get; set; } = "#FF000000";
    public string BackgroundColor { get; set; } = "#00FFFFFF";
    public string BorderColor { get; set; } = "#FF000000";
    public double BorderWidth { get; set; }
    public TextAlignType TextAlign { get; set; } = TextAlignType.Left;
    public DecimalDisplayStyle DecimalDisplayStyle { get; set; } = DecimalDisplayStyle.Normal;
    public int DecimalPlaces { get; set; } = -1;
    public double FractionFontScale { get; set; } = 0.65;
    public double FractionYOffset { get; set; }
    public string BarcodeType { get; set; } = "Code128";
    public bool BarcodeShowText { get; set; } = true;
    public bool KeepAspectRatio { get; set; } = true;
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
}

public class FieldTextElement : LayoutElement { public FieldTextElement() => ElementType = LayoutElementType.FieldText; }
public class StaticTextElement : LayoutElement { public StaticTextElement() => ElementType = LayoutElementType.StaticText; }
public class ImageElement : LayoutElement { public ImageElement() => ElementType = LayoutElementType.Image; }
public class BarcodeElement : LayoutElement { public BarcodeElement() => ElementType = LayoutElementType.Barcode; }

public class CsvFileItem
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string EncodingName { get; set; } = "";
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public CsvFileStatus Status { get; set; } = CsvFileStatus.Ok;
    public string Note { get; set; } = "";
    public List<string[]> Rows { get; set; } = [];
    public string[]? HeaderRow { get; set; }
}

public class CsvImportResult
{
    public List<CsvFileItem> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public class CsvHeaderMappingProfile
{
    public string PresetKey { get; set; } = "";
    public string HeaderSignature { get; set; } = "";
    public List<string> Headers { get; set; } = [];
    public List<FieldDefinition> Fields { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

public class DuplicateCleanupOptions
{
    public string KeyFieldId { get; set; } = "";
    public string CompareFieldId { get; set; } = "";
    public DuplicateValueType ValueType { get; set; } = DuplicateValueType.Text;
    public DuplicateResolutionRule Rule { get; set; } = DuplicateResolutionRule.KeepFirst;
    public bool IgnoreEmptyKeys { get; set; } = true;
    public DuplicateCarryOptions Carry { get; set; } = new();
}

public class DuplicateCarryOptions
{
    public bool Enabled { get; set; }
    public List<string> FieldIds { get; set; } = [];
    public DuplicateCopyMode CopyMode { get; set; } = DuplicateCopyMode.FillIfEmpty;
}

public class DuplicateCarryAction
{
    public string KeepFilePath { get; set; } = "";
    public int KeepRowIndex { get; set; }
    public string SourceFilePath { get; set; } = "";
    public int SourceRowIndex { get; set; }
    public string FieldId { get; set; } = "";
    public int ColumnIndex { get; set; }
    public string Value { get; set; } = "";
}

public class DuplicateResolutionItem
{
    public int GroupIndex { get; set; }
    public bool IsFirstInGroup { get; set; }
    public string KeyValue { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public int RowIndex { get; set; }
    public int DisplayRowNumber => RowIndex + 1;
    public string DisplayKeyValue => IsFirstInGroup ? KeyValue : "";
    public string CompareValue { get; set; } = "";
    public bool Keep { get; set; }
    public string CarrySummary { get; set; } = "";
    public string ActionText => Keep
        ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "残す" : "Keep")
        : (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "削除" : "Delete");
    public string Reason { get; set; } = "";
}

public class DuplicateCleanupResult
{
    public List<DuplicateResolutionItem> Items { get; set; } = [];
    public List<DuplicateCarryAction> CarryActions { get; set; } = [];
    public int GroupCount { get; set; }
    public int DeleteCount { get; set; }
    public int CarryCellCount { get; set; }
    public int TargetFileCount { get; set; }
}

public class DuplicateApplyResult
{
    public int UpdatedFileCount { get; set; }
    public int DeletedRowCount { get; set; }
    public List<string> UpdatedFiles { get; set; } = [];
}

public class CsvMergeOptions
{
    public string FolderPath { get; set; } = "";
    public string ParentCsvPath { get; set; } = "";
    public bool HasHeaderRow { get; set; } = true;
    public bool DeleteMergedSourceFiles { get; set; }
    public bool DeleteBakFileAfterMerge { get; set; }
}

public class CsvMergeCandidate
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool IsParent { get; set; }
    public int RowCount { get; set; }
    public bool HeaderMatches { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public string RoleText => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? (IsParent ? "親" : "子")
        : (IsParent ? "Parent" : "Child");
    public string HeaderMatchText => IsParent
        ? "-"
        : LocalizationService.CurrentLanguage == AppLanguage.Japanese
            ? (HeaderMatches ? "一致" : "不一致")
            : (HeaderMatches ? "Match" : "Mismatch");
}

public class CsvMergePreview
{
    public string FolderPath { get; set; } = "";
    public string ParentCsvPath { get; set; } = "";
    public string ParentFileName { get; set; } = "";
    public bool HasHeaderRow { get; set; } = true;
    public bool DeleteMergedSourceFiles { get; set; }
    public bool DeleteBakFileAfterMerge { get; set; }
    public List<CsvMergeCandidate> Items { get; set; } = [];
    public List<string> AppendFilePaths { get; set; } = [];
    public int AppendFileCount { get; set; }
    public int AppendRowCount { get; set; }
    public int SkipFileCount { get; set; }
    public int ResultRowCount { get; set; }
}

public class CsvMergeApplyResult
{
    public string ParentCsvPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public int AppendedFileCount { get; set; }
    public int AppendedRowCount { get; set; }
    public int DeletedSourceFileCount { get; set; }
    public int DeletedBakFileCount { get; set; }
}

public class CsvPreviewRowContext
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string EncodingName { get; set; } = "";
    public bool HasHeaderRow { get; set; }
    public string[]? HeaderRow { get; set; }
    public List<string[]> AllRows { get; set; } = [];
    public int RowIndex { get; set; }
    public string[] Values { get; set; } = [];
    public Dictionary<string, string> Data { get; set; } = [];
    public bool IsDirty { get; set; }
}

public class OutputOptions
{
    public OutputFormatType OutputFormat { get; set; }
    public OutputScopeType OutputScope { get; set; }
    public string OutputFolder { get; set; } = "";
    public bool ConfirmOverwrite { get; set; } = true;
    public int OutputDpi { get; set; } = 300;
    public PrintScalingMode PrintScalingMode { get; set; } = PrintScalingMode.ActualSize;
    public PrintSortMode PrintSortMode { get; set; } = PrintSortMode.LoadOrder;
    public string PrintSortFieldId { get; set; } = "";
    public PrintSortValueType PrintSortValueType { get; set; } = PrintSortValueType.Text;
    public double PrintOffsetXmm { get; set; }
    public double PrintOffsetYmm { get; set; }
    [JsonIgnore]
    public PrinterSettings? PrinterSettings { get; set; }
    [JsonIgnore]
    public int? ExpectedPageCount { get; set; }
}

public class RecentFileEntry { public string Path { get; set; } = ""; public DateTime LastOpenedAt { get; set; } }
public class ValidationResult { public bool IsValid => Errors.Count == 0; public List<string> Errors { get; set; } = []; public List<string> Warnings { get; set; } = []; }

public class AppSettings
{
    public int FormatVersion { get; set; } = 1;
    public AppLanguage UiLanguage { get; set; } = AppLanguage.Japanese;
    public string LastPresetFile { get; set; } = "default-price-tag.tpspreset";
    public string LastLayoutFile { get; set; } = "default-price-tag.tpslayout";
    public string LastOutputFolder { get; set; } = "";
    public string StartupCsvPath { get; set; } = "";
    public string StartupCsvFolderPath { get; set; } = "";
    public OutputScopeType LastOutputScope { get; set; } = OutputScopeType.SinglePdfAllCsv;
    public int LastOutputDpi { get; set; } = 300;
    public OutputFormatType LastOutputFormat { get; set; } = OutputFormatType.Pdf;
    public PrintScalingMode LastPrintScalingMode { get; set; } = PrintScalingMode.ActualSize;
    public PrintSortMode LastPrintSortMode { get; set; } = PrintSortMode.LoadOrder;
    public string LastPrintSortFieldId { get; set; } = "";
    public PrintSortValueType LastPrintSortValueType { get; set; } = PrintSortValueType.Text;
    public double LastPrintOffsetXmm { get; set; }
    public double LastPrintOffsetYmm { get; set; }
    public List<CsvHeaderMappingProfile> CsvHeaderMappings { get; set; } = [];
    public List<RecentFileEntry> RecentCsvEntries { get; set; } = [];
    public List<RecentFileEntry> RecentOutputFolders { get; set; } = [];
    public UnitType DefaultUnit { get; set; } = UnitType.Mm;
    public bool DefaultHasHeaderRow { get; set; } = true;
    public bool ConfirmOverwrite { get; set; } = true;
    public bool ConfirmPrintTargets { get; set; } = true;
    public int LargePrintPageWarningThreshold { get; set; } = 100;
    public int BackupCleanupRetentionDays { get; set; } = 30;
    public int BackupCleanupMaxCountPerCsv { get; set; } = 10;
    public string LastBackupCleanupFolderPath { get; set; } = "";
    public bool OpenSaveFolderAfterPdfSave { get; set; }
    public bool DuplicateCarryValuesEnabled { get; set; }
    public List<string> DuplicateCarryFieldIds { get; set; } = [];
    public string LastDuplicateKeyFieldId { get; set; } = "";
    public string LastDuplicateCompareFieldId { get; set; } = "";
    public DuplicateValueType LastDuplicateValueType { get; set; } = DuplicateValueType.Date;
    public DuplicateResolutionRule LastDuplicateRule { get; set; } = DuplicateResolutionRule.KeepLargest;
    public bool LastDuplicateIgnoreEmptyKeys { get; set; } = true;
    public DuplicateCopyMode LastDuplicateCarryCopyMode { get; set; } = DuplicateCopyMode.Overwrite;
    public string LastTagFinderSearchFieldId { get; set; } = "";
    public string LastTagFinderFlagColumnName { get; set; } = "tagPrint";
    public string LastTagFinderOrderColumnName { get; set; } = "tagPrintOrder";
    public bool LastTagFinderAutoCheckAfterSearch { get; set; }
    public bool LastTagFinderPreviewVisible { get; set; } = true;
    public string LastMergeFolderPath { get; set; } = "";
    public string LastMergeParentCsvPath { get; set; } = "";
    public bool LastMergeDeleteSourceFiles { get; set; }
    public bool LastMergeDeleteBakFiles { get; set; }
    public bool FirstRunCompleted { get; set; }
}

public class DirtyStateTracker
{
    public bool IsDirty { get; private set; }
    public event EventHandler? Changed;
    public void MarkDirty() { IsDirty = true; Changed?.Invoke(this, EventArgs.Empty); }
    public void MarkClean() { IsDirty = false; Changed?.Invoke(this, EventArgs.Empty); }
}
