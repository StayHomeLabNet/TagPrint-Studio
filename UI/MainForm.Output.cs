using System.Diagnostics;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class MainForm
{
    private TableLayoutPanel BuildOutputSection()
    {
        var outputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 17,
            RowCount = 3,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(0, 10, 0, 0)
        };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        outputScopeCombo = BottomComboBox();
        outputScopeCombo.Items.AddRange(OutputScopeItems());
        outputScopeCombo.SelectedIndex = 0;
        outputScopeCombo.SelectedIndexChanged += (_, _) =>
        {
            ResetPreviewPage();
            SaveCurrentSettings();
            RedrawPreview();
        };

        outputDpiCombo = BottomComboBox();
        outputDpiCombo.Items.AddRange(["150", "300", "600"]);
        outputDpiCombo.SelectedItem = "300";
        outputDpiCombo.SelectedIndexChanged += (_, _) => SaveCurrentSettings();

        printScalingCombo = BottomComboBox();
        printScalingCombo.Items.AddRange(["実寸優先", "印刷可能領域に合わせる"]);
        printScalingCombo.SelectedIndex = 0;
        printScalingCombo.SelectedIndexChanged += (_, _) => SaveCurrentSettings();

        printSortCombo = BottomComboBox();
        printSortCombo.Items.AddRange(PrintSortItems());
        printSortCombo.SelectedIndex = 0;
        printSortCombo.SelectedIndexChanged += (_, _) =>
        {
            UpdatePrintSortFieldState();
            ResetPreviewPage();
            SaveCurrentSettings();
            RedrawPreview();
        };

        printSortFieldCombo = BottomComboBox();
        printSortFieldCombo.SelectedIndexChanged += (_, _) =>
        {
            ResetPreviewPage();
            SaveCurrentSettings();
            RedrawPreview();
        };

        printSortValueTypeCombo = BottomComboBox();
        printSortValueTypeCombo.Items.AddRange(PrintSortValueTypeItems());
        printSortValueTypeCombo.SelectedIndex = 0;
        printSortValueTypeCombo.SelectedIndexChanged += (_, _) =>
        {
            ResetPreviewPage();
            SaveCurrentSettings();
            RedrawPreview();
        };

        printOffsetXBox = BottomNumericUpDown();
        printOffsetYBox = BottomNumericUpDown();
        printOffsetXBox.ValueChanged += (_, _) => SaveCurrentSettings();
        printOffsetYBox.ValueChanged += (_, _) => SaveCurrentSettings();

        outputFolderBox = new TextBox { MinimumSize = new Size(220, 0), Margin = new Padding(2, 6, 10, 6) };
        outputFolderBox.Leave += (_, _) => SaveCurrentSettings();

        recentOutputCombo = BottomComboBox();
        recentOutputCombo.Margin = new Padding(2, 4, 4, 4);
        recentOutputCombo.SelectedIndexChanged += (_, _) => ApplyRecentOutputSelection();

        {
            var newSelectOutputButton = Button("選択", (_, _) => SelectOutputFolder());
            var newOpenOutputButton = Button("開く", (_, _) => OpenOutputFolder());
            var newSavePngButton = Button("PNG保存", async (_, _) => await ExportPngAsync(), primary: true);
            var newSavePdfButton = Button("PDF保存", async (_, _) => await ExportPdfAsync(), primary: true);
            var newPrintDirectButton = Button("直接印刷", async (_, _) => await PrintDirectAsync(), primary: true);

            outputPanel.Controls.Clear();
            outputPanel.ColumnStyles.Clear();
            outputPanel.RowStyles.Clear();
            outputPanel.ColumnCount = 1;
            outputPanel.RowCount = 2;
            outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var row1 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540));
            row1.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var leftSettings = OutputFlowRow();
            leftSettings.Controls.Add(OutputRowLabel("main.label.outputScope", 82));
            leftSettings.Controls.Add(OutputRowControl(outputScopeCombo, 150));
            leftSettings.Controls.Add(OutputRowLabel("main.label.dpi", 44));
            leftSettings.Controls.Add(OutputRowControl(outputDpiCombo, 56));
            leftSettings.Controls.Add(OutputRowLabel("main.label.print", 58));
            leftSettings.Controls.Add(OutputRowControl(printScalingCombo, 172));
            leftSettings.Controls.Add(OutputRowLabel("main.label.printOffsetX", 72));
            leftSettings.Controls.Add(OutputRowControl(printOffsetXBox, 62));
            leftSettings.Controls.Add(OutputRowLabel("main.label.printOffsetY", 72));
            leftSettings.Controls.Add(OutputRowControl(printOffsetYBox, 62));
            row1.Controls.Add(leftSettings, 0, 0);

            var rightSort = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 10, 0)
            };
            rightSort.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rightSort.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            rightSort.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            rightSort.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            rightSort.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            rightSort.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            printSortFieldCombo.Margin = new Padding(4, 3, 8, 3);
            printSortCombo.Margin = new Padding(4, 3, 0, 3);
            printSortValueTypeCombo.Margin = new Padding(8, 3, 4, 3);
            rightSort.Controls.Add(printSortFieldCombo, 1, 0);
            var printSortLabel = BottomLabelText("main.label.printSort", alignRight: true, right: 8);
            printSortLabel.Margin = new Padding(0);
            printSortLabel.AutoEllipsis = false;
            rightSort.Controls.Add(printSortLabel, 2, 0);
            rightSort.Controls.Add(printSortCombo, 3, 0);
            rightSort.Controls.Add(printSortValueTypeCombo, 4, 0);
            row1.Controls.Add(rightSort, 1, 0);
            outputPanel.Controls.Add(row1, 0, 0);

            var row2 = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 740));
            row2.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var folderLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            folderLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            folderLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outputFolderBox.Dock = DockStyle.Fill;
            outputFolderBox.Margin = new Padding(2, 15, 8, 0);
            folderLeft.Controls.Add(BottomLabelText("main.label.outputFolder", alignRight: true, right: 8), 0, 0);
            folderLeft.Controls.Add(outputFolderBox, 1, 0);
            row2.Controls.Add(folderLeft, 0, 0);

            var actionsRight = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
            actionsRight.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            actionsRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            PrepareOutputButton(newSavePngButton, 106);
            PrepareOutputButton(newSavePdfButton, 106);
            PrepareOutputButton(newPrintDirectButton, 132);
            PrepareOutputButton(newSelectOutputButton, 94);
            PrepareOutputButton(newOpenOutputButton, 82);
            recentOutputCombo.Margin = new Padding(0, 15, 0, 0);
            var recentOutputControl = OutputRowControl(recentOutputCombo, 160);
            recentOutputControl.Margin = new Padding(8, 15, 8, 0);
            actionsRight.Controls.Add(recentOutputControl, 0, 0);
            actionsRight.Controls.Add(newSelectOutputButton, 1, 0);
            actionsRight.Controls.Add(newOpenOutputButton, 2, 0);
            actionsRight.Controls.Add(newSavePngButton, 3, 0);
            actionsRight.Controls.Add(newSavePdfButton, 4, 0);
            actionsRight.Controls.Add(newPrintDirectButton, 5, 0);
            row2.Controls.Add(actionsRight, 1, 0);
            outputPanel.Controls.Add(row2, 0, 1);

            return outputPanel;
        }


        static FlowLayoutPanel OutputFlowRow(FlowDirection flowDirection = FlowDirection.LeftToRight) => new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = flowDirection,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        static Label OutputRowLabel(string key, int width)
        {
            var label = BottomLabelText(key, alignRight: true, right: 8);
            label.Dock = DockStyle.None;
            label.Width = width;
            label.Height = 30;
            label.Margin = new Padding(0, 3, 2, 3);
            return label;
        }

        static Control OutputRowControl(Control control, int width)
        {
            control.Dock = DockStyle.None;
            control.Width = width;
            control.Height = 30;
            control.Margin = new Padding(2, 3, 12, 3);
            return control;
        }

        static void PrepareOutputButton(Button button, int width)
        {
            button.AutoSize = false;
            button.Width = width;
            button.Height = 30;
            button.Margin = new Padding(4, 15, 4, 0);
        }
    }

    private void RefreshOutputOptionTexts()
    {
        var outputScopeIndex = outputScopeCombo.SelectedIndex < 0 ? 0 : outputScopeCombo.SelectedIndex;
        var printScalingIndex = printScalingCombo.SelectedIndex < 0 ? 0 : printScalingCombo.SelectedIndex;
        var printSortIndex = printSortCombo.SelectedIndex < 0 ? 0 : printSortCombo.SelectedIndex;

        outputScopeCombo.Items.Clear();
        outputScopeCombo.Items.AddRange(OutputScopeItems());
        outputScopeCombo.SelectedIndex = Math.Min(outputScopeIndex, outputScopeCombo.Items.Count - 1);

        printScalingCombo.Items.Clear();
        printScalingCombo.Items.AddRange(PrintScalingItems());
        printScalingCombo.SelectedIndex = Math.Min(printScalingIndex, printScalingCombo.Items.Count - 1);

        printSortCombo.Items.Clear();
        printSortCombo.Items.AddRange(PrintSortItems());
        printSortCombo.SelectedIndex = Math.Min(printSortIndex, printSortCombo.Items.Count - 1);
        var printSortValueTypeIndex = printSortValueTypeCombo.SelectedIndex < 0 ? 0 : printSortValueTypeCombo.SelectedIndex;
        printSortValueTypeCombo.Items.Clear();
        printSortValueTypeCombo.Items.AddRange(PrintSortValueTypeItems());
        printSortValueTypeCombo.SelectedIndex = Math.Min(printSortValueTypeIndex, printSortValueTypeCombo.Items.Count - 1);
        RefreshPrintSortFieldCombo();
        UpdatePrintSortFieldState();
    }

    private static object[] OutputScopeItems() =>
    [
        LocalizationService.T("main.outputScope.singlePdfAllCsv"),
        LocalizationService.T("main.outputScope.separatePdfPerCsv"),
        LocalizationService.T("main.outputScope.individualPngPerRow")
    ];

    private static object[] PrintScalingItems() =>
    [
        LocalizationService.T("main.printScaling.actualSize"),
        LocalizationService.T("main.printScaling.fitPrintableArea")
    ];

    private static object[] PrintSortItems() =>
    [
        LocalizationService.T("main.printSort.loadOrder"),
        LocalizationService.T("main.printSort.itemCode"),
        LocalizationService.T("main.printSort.selectedField"),
        LocalizationService.T("main.printSort.printTargetOrder")
    ];

    private static object[] PrintSortValueTypeItems() =>
    [
        LocalizationService.T("main.printSortValueType.text"),
        LocalizationService.T("main.printSortValueType.number"),
        LocalizationService.T("main.printSortValueType.natural"),
        LocalizationService.T("main.printSortValueType.date")
    ];

    private void RefreshPrintSortFieldCombo()
    {
        if (printSortFieldCombo is null) return;

        var selectedFieldId = SelectedPrintSortFieldId();
        if (string.IsNullOrWhiteSpace(selectedFieldId))
        {
            selectedFieldId = settings.LastPrintSortFieldId;
        }

        var wasLoading = loadingSettings;
        loadingSettings = true;
        printSortFieldCombo.BeginUpdate();
        printSortFieldCombo.Items.Clear();
        printSortFieldCombo.Items.Add(PrintSortFieldItem.Placeholder(LocalizationService.T("main.placeholder.printSortField")));
        foreach (var field in fieldRows.Where(field => field.Enabled && field.ColumnIndex is not null).OrderBy(field => field.DisplayOrder))
        {
            printSortFieldCombo.Items.Add(PrintSortFieldItem.FromField(field));
        }
        printSortFieldCombo.EndUpdate();
        SelectPrintSortField(selectedFieldId);
        loadingSettings = wasLoading;
        UpdatePrintSortFieldState();
    }

    private void SelectPrintSortField(string fieldId)
    {
        if (printSortFieldCombo.Items.Count == 0) return;

        for (var i = 0; i < printSortFieldCombo.Items.Count; i++)
        {
            if (printSortFieldCombo.Items[i] is PrintSortFieldItem item &&
                string.Equals(item.FieldId, fieldId, StringComparison.Ordinal))
            {
                printSortFieldCombo.SelectedIndex = i;
                return;
            }
        }

        printSortFieldCombo.SelectedIndex = 0;
    }

    private void UpdatePrintSortFieldState()
    {
        if (printSortFieldCombo is null) return;
        var enabled = CurrentPrintSortMode() == PrintSortMode.SelectedField;
        printSortFieldCombo.Enabled = enabled;
        if (printSortValueTypeCombo is not null) printSortValueTypeCombo.Enabled = enabled;
        if (enabled && printSortFieldCombo.SelectedIndex <= 0 && printSortFieldCombo.Items.Count > 1)
        {
            printSortFieldCombo.SelectedIndex = 1;
        }
    }

    private void RememberRecentOutputFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!RememberRecentPath(settings.RecentOutputFolders, path)) return;
        RefreshRecentOutputCombo();
        if (!loadingSettings) SaveCurrentSettings();
    }

    private void RefreshRecentOutputCombo()
    {
        if (recentOutputCombo is null) return;
        loadingSettings = true;
        recentOutputCombo.BeginUpdate();
        recentOutputCombo.Items.Clear();
        recentOutputCombo.Items.Add(RecentPathItem.Placeholder(LocalizationService.T("main.placeholder.recentOutputFolder")));
        foreach (var entry in settings.RecentOutputFolders.OrderByDescending(entry => entry.LastOpenedAt))
        {
            recentOutputCombo.Items.Add(RecentPathItem.FromPath(entry.Path));
        }
        recentOutputCombo.EndUpdate();
        recentOutputCombo.SelectedIndex = 0;
        loadingSettings = false;
    }

    private void ApplyRecentOutputSelection()
    {
        if (loadingSettings || recentOutputCombo.SelectedItem is not RecentPathItem item) return;
        if (string.IsNullOrWhiteSpace(item.Path)) return;
        outputFolderBox.Text = item.Path;
        RememberRecentOutputFolder(item.Path);
        SaveCurrentSettings();
    }

    private OutputScopeType CurrentOutputScope() => outputScopeCombo.SelectedIndex switch
    {
        1 => OutputScopeType.SeparatePdfPerCsv,
        2 => OutputScopeType.IndividualPngPerRow,
        _ => OutputScopeType.SinglePdfAllCsv
    };

    private void SelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = outputFolderBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            outputFolderBox.Text = dialog.SelectedPath;
            RememberRecentOutputFolder(dialog.SelectedPath);
            SaveCurrentSettings();
        }
    }

    private void OpenOutputFolder()
    {
        SaveCurrentSettings();
        var folder = ResolveOutputFolder();
        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(LocalizationService.T("main.message.outputFolderRequired"), LocalizationService.T("common.appTitle"));
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(LocalizationService.T("main.error.outputTitle"), LocalizationService.T("main.error.outputSummary"), ex);
        }
    }

    private OutputOptions Options(OutputFormatType format)
    {
        return outputOptionsFactory.Create(CurrentOutputSelectionState(), format);
    }

    private OutputSelectionState CurrentOutputSelectionState() => new()
    {
        OutputScope = CurrentOutputScope(),
        OutputFolder = ResolveOutputFolder(),
        ConfirmOverwrite = settings.ConfirmOverwrite,
        OutputDpi = SelectedOutputDpi(),
        PrintScalingMode = SelectedPrintScalingMode(),
        PrintSortMode = CurrentPrintSortMode(),
        PrintSortFieldId = SelectedPrintSortFieldId(),
        PrintTargetOrderColumnName = CurrentPrintTargetOrderColumnName(),
        PrintSortValueType = CurrentPrintSortValueType(),
        PrintOffsetXmm = (double)printOffsetXBox.Value,
        PrintOffsetYmm = (double)printOffsetYBox.Value
    };

    private string ResolveOutputFolder()
    {
        var raw = outputFolderBox.Text.Trim();
        return string.IsNullOrWhiteSpace(raw) ? raw : Path.GetFullPath(raw);
    }

    private string CurrentPrintTargetOrderColumnName()
    {
        return string.IsNullOrWhiteSpace(currentPreset.PrintTargetOrderColumnName)
            ? "tagPrintOrder"
            : currentPreset.PrintTargetOrderColumnName.Trim();
    }

    private void UpdatePresetPrintTargetOrderColumnName(string orderColumnName)
    {
        var normalized = string.IsNullOrWhiteSpace(orderColumnName)
            ? "tagPrintOrder"
            : orderColumnName.Trim();

        if (string.Equals(currentPreset.PrintTargetOrderColumnName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        currentPreset.PrintTargetOrderColumnName = normalized;
        MarkPresetDirty();
        ResetPreviewPage();
        RedrawPreview();
    }

    private int SelectedOutputDpi()
    {
        return int.TryParse(Convert.ToString(outputDpiCombo.SelectedItem), out var dpi) ? dpi : 300;
    }

    private PrintScalingMode SelectedPrintScalingMode()
    {
        return printScalingCombo.SelectedIndex == 1
            ? PrintScalingMode.FitPrintableArea
            : PrintScalingMode.ActualSize;
    }

    private PrintSortMode CurrentPrintSortMode()
    {
        return printSortCombo.SelectedIndex switch
        {
            1 => PrintSortMode.ItemCode,
            2 => PrintSortMode.SelectedField,
            3 => PrintSortMode.PrintTargetOrder,
            _ => PrintSortMode.LoadOrder
        };
    }

    private string SelectedPrintSortFieldId()
    {
        return printSortFieldCombo?.SelectedItem is PrintSortFieldItem item ? item.FieldId : "";
    }

    private PrintSortValueType CurrentPrintSortValueType()
    {
        return printSortValueTypeCombo?.SelectedIndex switch
        {
            1 => PrintSortValueType.Number,
            2 => PrintSortValueType.Natural,
            3 => PrintSortValueType.Date,
            _ => PrintSortValueType.Text
        };
    }

    private sealed class PrintSortFieldItem
    {
        public string FieldId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;

        public static PrintSortFieldItem Placeholder(string text) => new() { DisplayName = text };

        public static PrintSortFieldItem FromField(FieldDefinition field) => new()
        {
            FieldId = field.FieldId,
            DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName
        };
    }

    private static decimal ClampToNumeric(double value, NumericUpDown box)
    {
        var decimalValue = (decimal)value;
        if (decimalValue < box.Minimum) return box.Minimum;
        if (decimalValue > box.Maximum) return box.Maximum;
        return decimalValue;
    }

}
