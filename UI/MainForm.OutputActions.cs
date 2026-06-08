using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class MainForm
{
    private async Task ExportPngAsync()
    {
        if (!ValidateReady()) return;
        currentPreset.Fields = fieldRows.ToList();
        var options = Options(OutputFormatType.Png);
        settings.LastOutputFormat = OutputFormatType.Png;
        SaveCurrentSettings();
        if (!ConfirmPrintTargets(options)) return;
        if (!ConfirmOverwrite(options)) return;
        try
        {
            var layout = currentLayout;
            var preset = currentPreset;
            var rows = csvRows.ToList();
            await RunOutputWithProgressAsync(
                LocalizationService.T("output.progress.png"),
                LocalizationService.T("output.progress.png"),
                (progress, token) => Task.Run(() => outputService.ExportPng(layout, preset, rows, options, progress, token), token));
            MessageBox.Show(LocalizationService.T("main.message.pngSaved"), LocalizationService.T("common.appTitle"));
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(LocalizationService.T("output.progress.canceled"), LocalizationService.T("common.appTitle"));
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.exportPngTitle"), LocalizationService.T("main.error.exportPngSummary"), ex);
        }
    }

    private async Task ExportPdfAsync()
    {
        if (!ValidateReady()) return;
        currentPreset.Fields = fieldRows.ToList();
        var options = Options(OutputFormatType.Pdf);
        settings.LastOutputFormat = OutputFormatType.Pdf;
        SaveCurrentSettings();
        if (!ConfirmPrintTargets(options)) return;
        if (!ConfirmOverwrite(options)) return;
        try
        {
            var layout = currentLayout;
            var preset = currentPreset;
            var rows = csvRows.ToList();
            await RunOutputWithProgressAsync(
                LocalizationService.T("output.progress.pdf"),
                LocalizationService.T("output.progress.pdf"),
                (progress, token) => Task.Run(() => outputService.ExportPdf(layout, preset, rows, options, progress, token), token));
            using var dialog = new SaveCompleteDialog(
                LocalizationService.T("common.appTitle"),
                LocalizationService.T("main.message.pdfSaved"),
                options.OutputFolder,
                settings.OpenSaveFolderAfterPdfSave);
            dialog.ShowDialog(this);
            settings.OpenSaveFolderAfterPdfSave = dialog.OpenFolderChecked;
            SaveCurrentSettings();
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(LocalizationService.T("output.progress.canceled"), LocalizationService.T("common.appTitle"));
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.exportPdfTitle"), LocalizationService.T("main.error.exportPdfSummary"), ex);
        }
    }

    private async Task PrintDirectAsync()
    {
        if (!ValidateReady(requireOutputFolder: false)) return;
        currentPreset.Fields = fieldRows.ToList();

        var options = Options(OutputFormatType.Print);
        settings.LastOutputFormat = OutputFormatType.Print;
        SaveCurrentSettings();
        var printTargetData = BuildPrintTargetData(options);
        if (!ConfirmPrintTargets(printTargetData)) return;
        if (!ConfirmLargePrintJob(printTargetData)) return;

        using var dialog = new PrintDialog
        {
            AllowCurrentPage = false,
            AllowSomePages = false,
            UseEXDialog = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        options.PrinterSettings = dialog.PrinterSettings;
        options.ExpectedPageCount = printTargetData.PageCount;
        try
        {
            var layout = currentLayout;
            var preset = currentPreset;
            var rows = csvRows.ToList();
            await RunOutputWithProgressAsync(
                LocalizationService.T("output.progress.print"),
                LocalizationService.T("output.progress.print"),
                (progress, token) => Task.Run(() => outputService.Print(layout, preset, rows, options, progress, token), token));
            MessageBox.Show(LocalizationService.T("main.message.printSent"), LocalizationService.T("common.appTitle"));
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(LocalizationService.T("output.progress.canceled"), LocalizationService.T("common.appTitle"));
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.printTitle"), LocalizationService.T("main.error.printSummary"), ex);
        }
    }

    private bool ConfirmOverwrite(OutputOptions options)
    {
        if (!options.ConfirmOverwrite) return true;

        var existingFiles = outputService
            .GetPlannedOutputPaths(currentLayout, currentPreset, csvRows.ToList(), options)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existingFiles.Count == 0) return true;

        var preview = string.Join(Environment.NewLine, existingFiles.Take(5).Select(Path.GetFileName));
        var more = existingFiles.Count > 5 ? LocalizationService.T("main.confirm.overwriteMore", existingFiles.Count - 5) : "";
        var message = LocalizationService.T("main.confirm.overwriteBody", preview, more);

        return MessageBox.Show(message, LocalizationService.T("main.confirm.overwriteTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private bool ConfirmPrintTargets(OutputOptions options)
    {
        return ConfirmPrintTargets(BuildPrintTargetData(options));
    }

    private bool ConfirmPrintTargets(PrintTargetConfirmationData data)
    {
        if (!settings.ConfirmPrintTargets) return true;
        if (data.TotalTags == 0) return true;

        var summary = LocalizationService.T(
            "printTarget.summary",
            data.TotalTags,
            data.CsvCount,
            data.PageCount,
            PrintSortDisplayName(data.SortMode));
        using var dialog = new PrintTargetConfirmationDialog(
            LocalizationService.T("printTarget.title"),
            summary,
            data.CsvSummaries,
            data.OrderedRows);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    private PrintTargetConfirmationData BuildPrintTargetData(OutputOptions options)
    {
        return printTargetSummaryService.Build(
            currentLayout,
            currentPreset,
            csvRows.ToList(),
            options,
            headerCheck.Checked,
            renderer.GetRowsPerPage(currentLayout));
    }

    private bool ConfirmLargePrintJob(PrintTargetConfirmationData data)
    {
        var threshold = settings.LargePrintPageWarningThreshold;
        if (threshold <= 0 || data.PageCount < threshold) return true;

        var message = LocalizationService.T(
            "main.confirm.largePrintBody",
            data.PageCount,
            data.TotalTags,
            threshold);
        return MessageBox.Show(
            message,
            LocalizationService.T("main.confirm.largePrintTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private async Task RunOutputWithProgressAsync(
        string title,
        string initialMessage,
        Func<IProgress<OutputProgress>, CancellationToken, Task> operation)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var dialog = new OutputProgressDialog(title, initialMessage, cancellationTokenSource);
        var progress = new Progress<OutputProgress>(dialog.Report);
        dialog.Show(this);
        var failed = false;

        try
        {
            await RunBusyAsync(() => operation(progress, cancellationTokenSource.Token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failed = true;
            dialog.ShowFailure(ex);
            await dialog.WaitForCloseAsync();
            throw;
        }
        finally
        {
            if (!failed && !dialog.IsDisposed)
            {
                dialog.Close();
            }
        }
    }

    private static string PrintSortDisplayName(PrintSortMode mode) => mode switch
    {
        PrintSortMode.ItemCode => LocalizationService.T("main.printSort.itemCode"),
        PrintSortMode.SelectedField => LocalizationService.T("main.printSort.selectedField"),
        PrintSortMode.PrintTargetOrder => LocalizationService.T("main.printSort.printTargetOrder"),
        _ => LocalizationService.T("main.printSort.loadOrder")
    };
}
