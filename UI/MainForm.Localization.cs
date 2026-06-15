using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
{
    private void InitializeData()
    {
        loadingSettings = true;
        settings = bootstrap.Initialize();
        LocalizationService.SetLanguage(settings.UiLanguage);
        outputFolderBox.Text = settings.LastOutputFolder;
        outputScopeCombo.SelectedIndex = settings.LastOutputScope switch
        {
            OutputScopeType.SeparatePdfPerCsv => 1,
            OutputScopeType.IndividualPngPerRow => 2,
            _ => 0
        };
        outputDpiCombo.SelectedItem = settings.LastOutputDpi.ToString();
        if (outputDpiCombo.SelectedIndex < 0)
        {
            outputDpiCombo.SelectedItem = "300";
        }
        pdfRenderModeCombo.SelectedIndex = settings.LastPdfRenderMode == PdfRenderMode.VectorText ? 1 : 0;
        printScalingCombo.SelectedIndex = settings.LastPrintScalingMode == PrintScalingMode.FitPrintableArea ? 1 : 0;
        printSortCombo.SelectedIndex = settings.LastPrintSortMode switch
        {
            PrintSortMode.ItemCode => 1,
            PrintSortMode.SelectedField => 2,
            PrintSortMode.PrintTargetOrder => 3,
            _ => 0
        };
        printSortValueTypeCombo.SelectedIndex = settings.LastPrintSortValueType switch
        {
            PrintSortValueType.Number => 1,
            PrintSortValueType.Natural => 2,
            PrintSortValueType.Date => 3,
            _ => 0
        };
        printOffsetXBox.Value = ClampToNumeric(settings.LastPrintOffsetXmm, printOffsetXBox);
        printOffsetYBox.Value = ClampToNumeric(settings.LastPrintOffsetYmm, printOffsetYBox);
        LoadCombos();
        RefreshPrintSortFieldCombo();
        RefreshRecentCsvCombo();
        RefreshRecentOutputCombo();
        loadingSettings = false;
        ApplyLocalization();
        LoadStartupSourceIfConfigured();
        ShowJsonRecoveryWarnings();
    }

    private void LoadCombos()
    {
        presetCombo.DataSource = presetService.GetPresetFiles().Select(FileComboItem.FromPath).ToList();
        layoutCombo.DataSource = layoutService.GetLayoutFiles().Select(FileComboItem.FromPath).ToList();
        SelectComboByFile(presetCombo, settings.LastPresetFile);
        SelectComboByFile(layoutCombo, settings.LastLayoutFile);
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void ChangeLanguage(AppLanguage language)
    {
        LocalizationService.SetLanguage(language);
        settings.UiLanguage = language;
        SaveCurrentSettings();
    }

    private void ApplyLocalization()
    {
        LocalizationService.Apply(this);
        LocalizationService.Apply(menuStrip);
        if (headerCheck is not null) headerCheck.Text = LocalizationService.T("main.checkbox.headerRow");
        if (duplicateCleanupButton is not null) duplicateCleanupButton.Text = LocalizationService.T("duplicateCleanup.duplicateCleanup");
        if (tagFinderButton is not null) tagFinderButton.Text = LocalizationService.T("main.button.tagFinder");
        if (settingsImportMenuItem is not null) settingsImportMenuItem.Text = SettingsImportMenuText();
        if (settingsBackupMenuItem is not null) settingsBackupMenuItem.Text = SettingsBackupMenuText();
        if (backupCleanupMenuItem is not null) backupCleanupMenuItem.Text = BackupCleanupMenuText();
        UpdateBackupCleanupStartupMenu();
        if (appDataLocationMenuItem is not null) appDataLocationMenuItem.Text = AppDataLocationMenuText();
        if (openAppDataLocationMenuItem is not null) openAppDataLocationMenuItem.Text = OpenAppDataLocationMenuText();
        if (resetAppDataLocationMenuItem is not null) resetAppDataLocationMenuItem.Text = ResetAppDataLocationMenuText();
        if (confirmPrintTargetsMenuItem is not null) confirmPrintTargetsMenuItem.Checked = settings.ConfirmPrintTargets;
        UpdateLargePrintWarningMenuText();
        if (openSettingsLocationMenuItem is not null) openSettingsLocationMenuItem.Text = OpenSettingsLocationMenuText();
        if (openStartupCsvLocationMenuItem is not null) openStartupCsvLocationMenuItem.Text = OpenStartupCsvLocationMenuText();
        if (openStartupFolderMenuItem is not null) openStartupFolderMenuItem.Text = OpenStartupFolderMenuText();
        RefreshRecentCsvCombo();
        RefreshRecentOutputCombo();
        UpdateLanguageMenuChecks();
        UpdateMainGridHeaders();
        UpdateFieldEnumColumnItems();
        RefreshOutputOptionTexts();
        UpdateTitle();
        RedrawPreview();
    }

    private void ShowJsonRecoveryWarnings()
    {
        if (JsonFile.RecoveryWarnings.Count == 0)
        {
            return;
        }

        var details = JsonFile.RecoveryWarnings
            .Select(warning =>
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"復旧対象: {warning.Path}{Environment.NewLine}退避先: {warning.BrokenPath}{Environment.NewLine}理由: {warning.Message}"
                    : $"Recovered: {warning.Path}{Environment.NewLine}Moved to: {warning.BrokenPath}{Environment.NewLine}Reason: {warning.Message}")
            .ToList();

        JsonFile.ClearRecoveryWarnings();
        using var dialog = new DetailsDialog(
            LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "JSON設定の復旧" : "JSON Settings Recovery",
            LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "壊れたJSONファイルを退避し、既定値で読み込みました。"
                : "Broken JSON files were moved aside and defaults were loaded.",
            details);
        dialog.ShowDialog(this);
    }

    private void UpdateLanguageMenuChecks()
    {
        if (japaneseLanguageMenuItem is null || englishLanguageMenuItem is null) return;
        japaneseLanguageMenuItem.Checked = LocalizationService.CurrentLanguage == AppLanguage.Japanese;
        englishLanguageMenuItem.Checked = LocalizationService.CurrentLanguage == AppLanguage.English;
    }

    private void UpdateMainGridHeaders()
    {
        if (fieldsGrid.Columns.Count >= 10)
        {
            fieldsGrid.Columns[0].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "有効" : "On";
            fieldsGrid.Columns[1].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "項目名" : "Name";
            fieldsGrid.Columns[2].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "列番号" : "Column";
            fieldsGrid.Columns[3].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ヘッダー名" : "Header";
            fieldsGrid.Columns[4].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "必須" : "Req";
            fieldsGrid.Columns[5].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "条件" : "Filter";
            fieldsGrid.Columns[6].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "種類" : "Type";
            fieldsGrid.Columns[7].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "演算" : "Op";
            fieldsGrid.Columns[8].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "値" : "Value";
            fieldsGrid.Columns[9].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "削除" : "Delete";
        }

        if (csvGrid.Columns.Count >= 7)
        {
            csvGrid.Columns[0].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ファイル名" : "File";
            csvGrid.Columns[1].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "パス" : "Path";
            csvGrid.Columns[2].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "行数" : "Rows";
            csvGrid.Columns[3].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "列数" : "Cols";
            csvGrid.Columns[4].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "状態" : "Status";
            csvGrid.Columns[5].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "備考" : "Note";
            csvGrid.Columns[6].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "文字コード" : "Encoding";
        }
    }

    private void UpdateFieldEnumColumnItems()
    {
        if (fieldsGrid.Columns["PrintConditionType"] is DataGridViewComboBoxColumn typeColumn)
        {
            typeColumn.DataSource = EnumItems<PrintConditionType>();
            typeColumn.DisplayMember = nameof(EnumDisplayItem<PrintConditionType>.DisplayName);
            typeColumn.ValueMember = nameof(EnumDisplayItem<PrintConditionType>.Value);
        }

        if (fieldsGrid.Columns["PrintConditionOperator"] is DataGridViewComboBoxColumn operatorColumn)
        {
            operatorColumn.DataSource = EnumItems<PrintConditionOperator>();
            operatorColumn.DisplayMember = nameof(EnumDisplayItem<PrintConditionOperator>.DisplayName);
            operatorColumn.ValueMember = nameof(EnumDisplayItem<PrintConditionOperator>.Value);
        }
    }

    private static void SelectComboByFile(ComboBox combo, string fileName)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is FileComboItem item && Path.GetFileName(item.Path) == fileName)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void ApplyInitialSplitDistances()
    {
        ApplyInitialLeftSplitDistance();
        ApplyInitialCenterSplitDistance();
    }

    private void ApplyInitialLeftSplitDistance()
    {
        if (leftSplit is null) return;

        const int desired = 338;
        var maxSplitterDistance = Math.Max(leftSplit.Panel1MinSize, leftSplit.Height - leftSplit.SplitterWidth - leftSplit.Panel2MinSize);
        leftSplit.SplitterDistance = Math.Max(leftSplit.Panel1MinSize, Math.Min(desired, maxSplitterDistance));
    }

    private void ApplyInitialCenterSplitDistance()
    {
        if (centerSplit is null || fieldsGrid is null) return;

        var desired = fieldsGrid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width) + 28;
        var minimumRightWidth = 320;
        var maxSplitterDistance = Math.Max(centerSplit.Panel1MinSize, centerSplit.Width - centerSplit.SplitterWidth - minimumRightWidth);
        centerSplit.SplitterDistance = Math.Max(centerSplit.Panel1MinSize, Math.Min(desired, maxSplitterDistance));
    }

    private static bool SelectComboByPath(ComboBox combo, string filePath)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is FileComboItem item && string.Equals(item.Path, filePath, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }


}
