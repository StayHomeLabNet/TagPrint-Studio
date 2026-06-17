using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
{
    private void LoadSelectedPreset()
    {
        if (presetCombo.SelectedItem is not FileComboItem selected) return;
        if (suppressPresetSelectionChange) return;
        if (currentPresetItem is not null && !string.Equals(currentPresetItem.Path, selected.Path, StringComparison.OrdinalIgnoreCase))
        {
            if (!ConfirmSavePresetChanges())
            {
                suppressPresetSelectionChange = true;
                presetCombo.SelectedItem = currentPresetItem;
                suppressPresetSelectionChange = false;
                return;
            }
        }

        LoadPreset(selected);
    }

    private void LoadPreset(FileComboItem selected)
    {
        var path = selected.Path;
        currentPresetItem = selected;
        loadingPreset = true;
        currentPreset = presetService.Load(path);
        settings.LastPresetFile = Path.GetFileName(path);
        SaveCurrentSettings();
        headerCheck.Checked = currentPreset.HasHeaderRow;
        fieldRows.Clear();
        foreach (var field in currentPreset.Fields.OrderBy(f => f.DisplayOrder)) fieldRows.Add(field);
        loadingPreset = false;
        MarkPresetClean();
        UpdateHeaderNames();
        ResetPreviewPage();
        RedrawPreview();
    }

    private void LoadSelectedLayout()
    {
        if (layoutCombo.SelectedItem is not FileComboItem selected) return;
        var path = selected.Path;
        currentLayout = layoutService.Load(path);
        settings.LastLayoutFile = Path.GetFileName(path);
        SaveCurrentSettings();
        ResetPreviewPage();
        RedrawPreview();
    }

    private void OpenLayoutDesigner()
    {
        OpenLayoutDesigner(null, CurrentDesignerInitialPreviewRowIndex());
    }

    private void OpenLayoutDesigner(List<CsvPreviewRowContext>? previewRows)
    {
        OpenLayoutDesigner(previewRows, 0);
    }

    private void OpenLayoutDesigner(List<CsvPreviewRowContext>? previewRows, int initialPreviewRowIndex)
    {
        var initialLayoutPath = layoutCombo.SelectedItem is FileComboItem selectedLayout ? selectedLayout.Path : "";
        currentPreset.Fields = fieldRows.ToList();
        var designerPreviewRows = previewRows ?? DesignerPreviewRows(initialPreviewRowIndex);
        var designerInitialPreviewRowIndex = designerPreviewRows.Count == 0
            ? 0
            : Math.Clamp(initialPreviewRowIndex, 0, designerPreviewRows.Count - 1);
        using var designer = new LayoutDesignerForm(fieldRows.ToList(), initialLayoutPath, designerPreviewRows, paths, designerInitialPreviewRowIndex);
        designer.ShowDialog(this);
        if (designer.CsvSaved)
        {
            ReloadCsvWithCurrentFiles();
        }
        if (!designer.LayoutSaved) return;
        settings.LastLayoutFile = Path.GetFileName(designer.SavedLayoutPath);
        SaveCurrentSettings();
        ReloadLayoutCombo(designer.SavedLayoutPath);
    }

    private int CurrentDesignerInitialPreviewRowIndex()
    {
        return previewMode == PreviewMode.SingleTag ? Math.Max(0, previewPageIndex) : 0;
    }

    private List<CsvPreviewRowContext> DesignerPreviewRows(int requiredPreviewRowIndex = 0)
    {
        var scope = CurrentOutputScope();
        var files = PrintableRowService.SelectFiles(scope, csvRows, CurrentCsv());
        var maxRows = settings.PreviewRowLimit <= 0
            ? settings.PreviewRowLimit
            : Math.Max(settings.PreviewRowLimit, requiredPreviewRowIndex + 1);
        return PrintableRowService.PreviewRows(currentPreset, files, headerCheck.Checked, CurrentPrintSortMode(), SelectedPrintSortFieldId(), CurrentPrintSortValueType(), CurrentPrintTargetOrderColumnName(), maxRows);
    }

    private bool SavePreset()
    {
        if (presetCombo.SelectedItem is not FileComboItem selected) return false;
        var path = selected.Path;
        PrepareCurrentPresetForSave();
        presetService.Save(path, currentPreset);
        MarkPresetClean();
        MessageBox.Show(LocalizationService.T("main.message.presetSaved"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private bool SavePresetAs()
    {
        var initialName = string.IsNullOrWhiteSpace(currentPreset.PresetName)
            ? currentPresetItem?.DisplayName ?? LocalizationService.T("main.default.newPreset")
            : currentPreset.PresetName;
        using var dialog = new NamePromptDialog(LocalizationService.T("main.title.savePresetAs"), LocalizationService.T("main.label.presetName"), initialName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = Path.Combine(paths.Presets, $"{ToSafeFileName(dialog.Value)}.tpspreset");
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("main.message.presetExists"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var now = DateTime.Now;
        currentPreset.PresetId = Path.GetFileNameWithoutExtension(filePath);
        currentPreset.PresetName = dialog.Value;
        currentPreset.CreatedAt = now;
        currentPreset.UpdatedAt = now;
        PrepareCurrentPresetForSave();
        presetService.Save(filePath, currentPreset);

        SaveLastPreset(filePath);
        ReloadPresetCombo(filePath);
        MarkPresetClean();
        MessageBox.Show(LocalizationService.T("main.message.presetSaved"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private bool NewPreset()
    {
        if (!ConfirmSavePresetChanges()) return false;

        using var dialog = new NamePromptDialog(LocalizationService.T("main.title.newPreset"), LocalizationService.T("main.label.presetName"), LocalizationService.T("main.default.newPreset"));
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = PresetPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("main.message.presetExists"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var now = DateTime.Now;
        currentPreset = new PresetFile
        {
            PresetId = Path.GetFileNameWithoutExtension(filePath),
            PresetName = dialog.Value,
            Description = "",
            HasHeaderRow = headerCheck.Checked,
            LayoutRef = layoutCombo.SelectedItem is FileComboItem layout ? Path.GetFileName(layout.Path) : "",
            PrintTargetOrderColumnName = CurrentPrintTargetOrderColumnName(),
            CreatedAt = now,
            UpdatedAt = now,
            Fields = DefaultPresetFields()
        };
        presetService.Save(filePath, currentPreset);
        SaveLastPreset(filePath);
        ReloadPresetCombo(filePath);
        MessageBox.Show(LocalizationService.T("main.message.presetCreated"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private bool DuplicatePreset()
    {
        var baseName = string.IsNullOrWhiteSpace(currentPreset.PresetName)
            ? currentPresetItem?.DisplayName ?? "Preset"
            : currentPreset.PresetName;
        using var dialog = new NamePromptDialog(LocalizationService.T("main.title.duplicatePreset"), LocalizationService.T("main.label.presetName"), $"{baseName} {LocalizationService.T("main.default.copySuffix")}");
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = PresetPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("main.message.presetExists"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var now = DateTime.Now;
        currentPreset.PresetId = Path.GetFileNameWithoutExtension(filePath);
        currentPreset.PresetName = dialog.Value;
        currentPreset.CreatedAt = now;
        currentPreset.UpdatedAt = now;
        PrepareCurrentPresetForSave();
        presetService.Save(filePath, currentPreset);
        SaveLastPreset(filePath);
        ReloadPresetCombo(filePath);
        MessageBox.Show(LocalizationService.T("main.message.presetDuplicated"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private bool RenamePreset()
    {
        if (currentPresetItem is null) return false;

        var oldPath = currentPresetItem.Path;
        var oldName = string.IsNullOrWhiteSpace(currentPreset.PresetName) ? currentPresetItem.DisplayName : currentPreset.PresetName;
        using var dialog = new NamePromptDialog(LocalizationService.T("main.title.renamePreset"), LocalizationService.T("main.label.presetName"), oldName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var newPath = PresetPathForName(dialog.Value);
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return true;
        if (File.Exists(newPath))
        {
            MessageBox.Show(LocalizationService.T("main.message.presetExists"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentPreset.PresetId = Path.GetFileNameWithoutExtension(newPath);
        currentPreset.PresetName = dialog.Value;
        PrepareCurrentPresetForSave();
        presetService.Save(newPath, currentPreset);
        File.Delete(oldPath);
        SaveLastPreset(newPath);
        ReloadPresetCombo(newPath);
        MessageBox.Show(LocalizationService.T("main.message.presetRenamed"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private bool DeletePreset()
    {
        if (currentPresetItem is null) return false;

        var files = presetService.GetPresetFiles();
        if (files.Count <= 1)
        {
            MessageBox.Show(LocalizationService.T("main.message.lastPresetCannotDelete"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (MessageBox.Show(LocalizationService.T("main.confirm.deletePreset", currentPresetItem.DisplayName), LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return false;
        }

        var deletedPath = currentPresetItem.Path;
        File.Delete(deletedPath);
        var nextPath = presetService.GetPresetFiles().FirstOrDefault();
        if (nextPath is null) return false;

        SaveLastPreset(nextPath);
        ReloadPresetCombo(nextPath);
        MessageBox.Show(LocalizationService.T("main.message.presetDeleted"), LocalizationService.T("common.appTitle"));
        return true;
    }

    private void ReloadPresetCombo(string selectedPath)
    {
        suppressPresetSelectionChange = true;
        presetCombo.DataSource = presetService.GetPresetFiles().Select(FileComboItem.FromPath).ToList();
        SelectComboByPath(presetCombo, selectedPath);
        currentPresetItem = presetCombo.SelectedItem as FileComboItem;
        suppressPresetSelectionChange = false;
        if (currentPresetItem is not null)
        {
            LoadPreset(currentPresetItem);
        }
    }

    private void ReloadLayoutCombo(string selectedPath)
    {
        layoutCombo.DataSource = layoutService.GetLayoutFiles().Select(FileComboItem.FromPath).ToList();
        SelectComboByPath(layoutCombo, selectedPath);
        LoadSelectedLayout();
    }

    private void SaveLastPreset(string presetPath)
    {
        settings.LastPresetFile = Path.GetFileName(presetPath);
        SaveCurrentSettings();
    }

    private void PrepareCurrentPresetForSave()
    {
        fieldsGrid.EndEdit(DataGridViewDataErrorContexts.Commit);
        currentPreset.HasHeaderRow = headerCheck.Checked;
        currentPreset.LayoutRef = layoutCombo.SelectedItem is FileComboItem layout ? Path.GetFileName(layout.Path) : currentPreset.LayoutRef;
        currentPreset.PrintTargetOrderColumnName = CurrentPrintTargetOrderColumnName();
        currentPreset.Fields = fieldRows.ToList();
    }

    private string PresetPathForName(string name) => Path.Combine(paths.Presets, $"{ToSafeFileName(name)}.tpspreset");

    private static List<FieldDefinition> DefaultPresetFields() =>
    [
        new() { FieldId = "product_name_1", DisplayName = "商品名1", ColumnIndex = 0, Required = true, DisplayOrder = 1, Enabled = true },
        new() { FieldId = "product_name_2", DisplayName = "商品名2", ColumnIndex = 1, Required = false, DisplayOrder = 2, Enabled = true },
        new() { FieldId = "price", DisplayName = "価格", ColumnIndex = 2, Required = true, DisplayOrder = 3, Enabled = true },
        new() { FieldId = "code_1", DisplayName = "コード1", ColumnIndex = 3, Required = false, DisplayOrder = 4, Enabled = true },
        new() { FieldId = "code_2", DisplayName = "コード2", ColumnIndex = 4, Required = false, DisplayOrder = 5, Enabled = true }
    ];


    private void MarkPresetDirty()
    {
        if (loadingPreset || presetDirty) return;
        presetDirty = true;
        UpdateTitle();
    }

    private void MarkPresetClean()
    {
        presetDirty = false;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var title = LocalizationService.T("common.appTitle");
        Text = presetDirty ? $"{title} *" : title;
    }

    private bool ConfirmSavePresetChanges()
    {
        if (!presetDirty) return true;
        var result = MessageBox.Show(
            LocalizationService.T("main.message.unsavedPreset"),
            LocalizationService.T("main.title.unsavedPreset"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        return result switch
        {
            DialogResult.Yes => SavePreset(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmSavePresetChanges()) e.Cancel = true;
        if (!e.Cancel) SaveCurrentSettings();
    }


    private static string ToSafeFileName(string value)
    {
        return SafeFileNameService.ToSafeFileName(value);
    }


}
