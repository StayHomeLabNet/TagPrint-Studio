using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface IOutputOptionsFactory
{
    OutputOptions Create(OutputSelectionState state, OutputFormatType format);
}

public sealed class OutputOptionsFactory : IOutputOptionsFactory
{
    public OutputOptions Create(OutputSelectionState state, OutputFormatType format)
    {
        return new OutputOptions
        {
            OutputFormat = format,
            OutputScope = state.OutputScope,
            OutputFolder = state.OutputFolder,
            ConfirmOverwrite = state.ConfirmOverwrite,
            OutputDpi = state.OutputDpi,
            PdfRenderMode = state.PdfRenderMode,
            PrintScalingMode = state.PrintScalingMode,
            PrintSortMode = state.PrintSortMode,
            PrintSortFieldId = state.PrintSortFieldId,
            PrintTargetOrderColumnName = state.PrintTargetOrderColumnName,
            PrintSortValueType = state.PrintSortValueType,
            PrintOffsetXmm = state.PrintOffsetXmm,
            PrintOffsetYmm = state.PrintOffsetYmm
        };
    }
}
