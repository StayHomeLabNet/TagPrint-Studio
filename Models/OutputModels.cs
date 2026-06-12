using System.Drawing.Printing;
using System.Text.Json.Serialization;
using TagPrintStudio.Enums;

namespace TagPrintStudio.Models;

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
    public string PrintTargetOrderColumnName { get; set; } = "tagPrintOrder";
    public PrintSortValueType PrintSortValueType { get; set; } = PrintSortValueType.Text;
    public double PrintOffsetXmm { get; set; }
    public double PrintOffsetYmm { get; set; }
    [JsonIgnore]
    public PrinterSettings? PrinterSettings { get; set; }
    [JsonIgnore]
    public int? ExpectedPageCount { get; set; }
}

public class OutputSelectionState
{
    public OutputScopeType OutputScope { get; set; } = OutputScopeType.SinglePdfAllCsv;
    public string OutputFolder { get; set; } = "";
    public bool ConfirmOverwrite { get; set; } = true;
    public int OutputDpi { get; set; } = 300;
    public PrintScalingMode PrintScalingMode { get; set; } = PrintScalingMode.ActualSize;
    public PrintSortMode PrintSortMode { get; set; } = PrintSortMode.LoadOrder;
    public string PrintSortFieldId { get; set; } = "";
    public string PrintTargetOrderColumnName { get; set; } = "tagPrintOrder";
    public PrintSortValueType PrintSortValueType { get; set; } = PrintSortValueType.Text;
    public double PrintOffsetXmm { get; set; }
    public double PrintOffsetYmm { get; set; }
}
