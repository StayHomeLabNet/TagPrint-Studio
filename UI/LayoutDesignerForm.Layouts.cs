using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private void InitializeData()
    {
        paths.EnsureDirectories();
        layoutCombo.DataSource = layoutService.GetLayoutFiles().Select(FileComboItem.FromPath).ToList();
        SelectInitialLayout();
        UpdateHistoryButtons();
        ApplyLocalization();
        ResetCanvasAreaZoom();
    }

    private void SelectInitialLayout()
    {
        if (!string.IsNullOrWhiteSpace(initialLayoutPath))
        {
            for (var i = 0; i < layoutCombo.Items.Count; i++)
            {
                if (layoutCombo.Items[i] is FileComboItem item &&
                    string.Equals(item.Path, initialLayoutPath, StringComparison.OrdinalIgnoreCase))
                {
                    layoutCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        if (layoutCombo.Items.Count > 0) layoutCombo.SelectedIndex = 0;
    }

    private void LoadSelectedLayout()
    {
        if (layoutCombo.SelectedItem is not FileComboItem selected) return;
        if (suppressLayoutSelectionChange) return;
        if (currentLayoutItem is not null && !string.Equals(currentLayoutItem.Path, selected.Path, StringComparison.OrdinalIgnoreCase))
        {
            if (!ConfirmSaveChanges())
            {
                suppressLayoutSelectionChange = true;
                layoutCombo.SelectedItem = currentLayoutItem;
                suppressLayoutSelectionChange = false;
                return;
            }
        }

        LoadLayout(selected);
    }

    private void LoadLayout(FileComboItem selected)
    {
        var path = selected.Path;
        loading = true;
        currentLayoutItem = selected;
        currentLayoutPath = path;
        currentLayout = layoutService.Load(path);
        NormalizeElementOrder(currentLayout);
        var layoutChanged = layoutFieldSyncService.SyncFieldElements(currentLayout, fieldDefinitions);
        undoStack.Clear();
        redoStack.Clear();
        elementRows.Clear();
        foreach (var element in currentLayout.Elements.OrderBy(e => e.ZIndex)) elementRows.Add(element);
        loading = false;
        if (elementsGrid.Rows.Count > 0) elementsGrid.Rows[0].Selected = true;
        LoadSelectedElementIntoEditor();
        UpdateHistoryButtons();
        MarkClean();
        if (layoutChanged) MarkDirty();
        RedrawPreview();
    }

    private void AddElement()
    {
        CaptureUndo();
        var next = elementRows.Count == 0 ? 1 : elementRows.Max(e => e.ZIndex) + 10;
        var element = new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}",
            Name = LocalizationService.T("layout.default.newElement"),
            ZIndex = next,
            ElementType = LayoutElementType.StaticText,
            Text = LocalizationService.T("layout.default.textUpper"),
            X = Snap(2),
            Y = Snap(2),
            Width = Math.Max(10, currentLayout.Tag.Width / 2),
            Height = 6,
            FontSize = 10,
            Visible = true
        };
        elementRows.Add(element);
        currentLayout.Elements = elementRows.ToList();
        SelectElement(element);
        MarkDirty();
        RedrawPreview();
    }

    private void AddFieldElement()
    {
        if (fieldDefinitions.Count == 0)
        {
            MessageBox.Show(LocalizationService.T("layout.message.noPresetFields"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new FieldSelectionDialog(fieldDefinitions);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedField is not { } field) return;

        CaptureUndo();
        var next = elementRows.Count == 0 ? 1 : elementRows.Max(e => e.ZIndex) + 10;
        var element = new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}",
            Name = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName,
            ZIndex = next,
            ElementType = LayoutElementType.FieldText,
            FieldId = field.FieldId,
            X = Snap(2),
            Y = Snap(2),
            Width = Math.Max(10, currentLayout.Tag.Width / 2),
            Height = 6,
            FontSize = 10,
            TextAlign = TextAlignType.Left,
            Visible = true
        };
        elementRows.Add(element);
        currentLayout.ExcludedAutoFieldIds.RemoveAll(fieldId => string.Equals(fieldId, field.FieldId, StringComparison.Ordinal));
        currentLayout.Elements = elementRows.ToList();
        SelectElement(element);
        MarkDirty();
        RedrawPreview();
    }

    private void AddImageElement()
    {
        var imagePath = BrowseImagePath("");
        if (string.IsNullOrWhiteSpace(imagePath)) return;

        CaptureUndo();
        var next = elementRows.Count == 0 ? 1 : elementRows.Max(e => e.ZIndex) + 10;
        var element = new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}",
            Name = Path.GetFileNameWithoutExtension(imagePath),
            ZIndex = next,
            ElementType = LayoutElementType.Image,
            ImagePath = ToStoredImagePath(imagePath),
            X = Snap(2),
            Y = Snap(2),
            Width = Math.Max(10, currentLayout.Tag.Width / 2),
            Height = Math.Max(10, currentLayout.Tag.Height / 2),
            Visible = true,
            KeepAspectRatio = true
        };
        elementRows.Add(element);
        currentLayout.Elements = elementRows.ToList();
        SelectElement(element);
        MarkDirty();
        RedrawPreview();
    }

    private void DeleteElement()
    {
        var element = SelectedElement();
        if (element is null) return;
        if (MessageBox.Show(LocalizationService.T("layout.message.deleteElement"), LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        CaptureUndo();
        elementRows.Remove(element);
        ExcludeDeletedAutoField(element);
        currentLayout.Elements = elementRows.ToList();
        LoadSelectedElementIntoEditor();
        MarkDirty();
        RedrawPreview();
    }

    private void ExcludeDeletedAutoField(LayoutElement element)
    {
        if (element.ElementType != LayoutElementType.FieldText || string.IsNullOrWhiteSpace(element.FieldId)) return;
        if (currentLayout.ExcludedAutoFieldIds.Contains(element.FieldId, StringComparer.Ordinal)) return;
        currentLayout.ExcludedAutoFieldIds.Add(element.FieldId);
    }

    private void DuplicateElement()
    {
        var source = SelectedElement();
        if (source is null) return;

        CaptureUndo();
        var copy = CloneElement(source);
        copy.ElementId = $"el_{DateTime.Now:HHmmssfff}";
        copy.Name = $"{source.Name} Copy";
        copy.ZIndex = elementRows.Count == 0 ? 1 : elementRows.Max(e => e.ZIndex) + 10;

        var offset = Math.Max(1, (double)gridSizeBox.Value);
        copy.X = Math.Round(Snap(Clamp(source.X + offset, 0, currentLayout.Tag.Width - source.Width)), 1);
        copy.Y = Math.Round(Snap(Clamp(source.Y + offset, 0, currentLayout.Tag.Height - source.Height)), 1);

        elementRows.Add(copy);
        currentLayout.Elements = elementRows.ToList();
        SelectElement(copy);
        MarkDirty();
        RedrawPreview();
    }

    private bool SaveLayout()
    {
        if (string.IsNullOrWhiteSpace(currentLayoutPath)) return false;
        currentLayout.Elements = elementRows.OrderBy(e => e.ZIndex).ToList();
        var errors = layoutService.Validate(currentLayout);
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), LocalizationService.T("layout.title.cannotSave"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        layoutService.Save(currentLayoutPath, currentLayout);
        LayoutSaved = true;
        SavedLayoutPath = currentLayoutPath;
        MarkClean();
        statusLabel.Text = LocalizationService.T("layout.message.statusSaved", Path.GetFileName(currentLayoutPath));
        return true;
    }

    private bool SaveLayoutAs()
    {
        var initialName = string.IsNullOrWhiteSpace(currentLayout.LayoutName)
            ? currentLayoutItem?.DisplayName ?? "New Layout"
            : currentLayout.LayoutName;
        using var dialog = new NamePromptDialog(LocalizationService.T("layout.title.saveLayoutAs"), LocalizationService.T("layout.label.layoutName"), initialName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = layoutCatalogService.LayoutPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("layout.message.layoutExists"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentLayout.LayoutId = Path.GetFileNameWithoutExtension(filePath);
        currentLayout.LayoutName = dialog.Value;
        currentLayout.CreatedAt = DateTime.Now;
        currentLayout.Elements = elementRows.OrderBy(e => e.ZIndex).ToList();
        currentLayoutPath = filePath;

        var errors = layoutService.Validate(currentLayout);
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), LocalizationService.T("layout.title.cannotSave"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        layoutService.Save(filePath, currentLayout);
        LayoutSaved = true;
        SavedLayoutPath = filePath;
        ReloadLayoutCombo(filePath);
        MarkClean();
        statusLabel.Text = LocalizationService.T("layout.message.statusSaved", Path.GetFileName(filePath));
        return true;
    }

    private bool NewLayout()
    {
        if (!ConfirmSaveChanges()) return false;

        using var dialog = new NamePromptDialog(LocalizationService.T("layout.title.newLayout"), LocalizationService.T("layout.label.layoutName"), LocalizationService.T("layout.default.newLayout"));
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = layoutCatalogService.LayoutPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("layout.message.layoutExists"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentLayout = layoutCatalogService.CreateDefaultLayout(dialog.Value, filePath);
        currentLayoutPath = filePath;
        elementRows.Clear();
        foreach (var element in currentLayout.Elements.OrderBy(e => e.ZIndex)) elementRows.Add(element);

        layoutService.Save(filePath, currentLayout);
        LayoutSaved = true;
        SavedLayoutPath = filePath;
        ReloadLayoutCombo(filePath);
        MarkClean();
        return true;
    }

    private bool DuplicateLayout()
    {
        var baseName = string.IsNullOrWhiteSpace(currentLayout.LayoutName)
            ? currentLayoutItem?.DisplayName ?? LocalizationService.T("layout.default.newLayout")
            : currentLayout.LayoutName;
        using var dialog = new NamePromptDialog(LocalizationService.T("layout.title.duplicateLayout"), LocalizationService.T("layout.label.layoutName"), $"{baseName} {LocalizationService.T("main.default.copySuffix")}");
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var filePath = layoutCatalogService.LayoutPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("layout.message.layoutExists"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentLayout.LayoutId = Path.GetFileNameWithoutExtension(filePath);
        currentLayout.LayoutName = dialog.Value;
        currentLayout.CreatedAt = DateTime.Now;
        currentLayout.Elements = elementRows.OrderBy(e => e.ZIndex).Select(CloneElement).ToList();
        currentLayoutPath = filePath;

        layoutService.Save(filePath, currentLayout);
        LayoutSaved = true;
        SavedLayoutPath = filePath;
        ReloadLayoutCombo(filePath);
        MarkClean();
        return true;
    }

    private bool RenameLayout()
    {
        if (currentLayoutItem is null) return false;

        var oldPath = currentLayoutItem.Path;
        var oldFileName = Path.GetFileName(oldPath);
        var oldName = string.IsNullOrWhiteSpace(currentLayout.LayoutName) ? currentLayoutItem.DisplayName : currentLayout.LayoutName;
        using var dialog = new NamePromptDialog(LocalizationService.T("layout.title.renameLayout"), LocalizationService.T("layout.label.layoutName"), oldName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;

        var newPath = layoutCatalogService.LayoutPathForName(dialog.Value);
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return true;
        if (File.Exists(newPath))
        {
            MessageBox.Show(LocalizationService.T("layout.message.layoutExists"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentLayout.LayoutId = Path.GetFileNameWithoutExtension(newPath);
        currentLayout.LayoutName = dialog.Value;
        currentLayout.Elements = elementRows.OrderBy(e => e.ZIndex).ToList();
        currentLayoutPath = newPath;

        var errors = layoutService.Validate(currentLayout);
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), LocalizationService.T("layout.title.cannotRename"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        layoutService.Save(newPath, currentLayout);
        layoutCatalogService.UpdatePresetLayoutRefs(oldFileName, Path.GetFileName(newPath));
        File.Delete(oldPath);

        LayoutSaved = true;
        SavedLayoutPath = newPath;
        ReloadLayoutCombo(newPath);
        MarkClean();
        statusLabel.Text = LocalizationService.T("layout.message.statusRenamed", Path.GetFileName(newPath));
        return true;
    }

    private bool DeleteLayout()
    {
        if (currentLayoutItem is null) return false;

        var files = layoutService.GetLayoutFiles();
        if (files.Count <= 1)
        {
            MessageBox.Show(LocalizationService.T("layout.message.lastLayoutCannotDelete"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var references = layoutCatalogService.GetReferencingPresetNames(Path.GetFileName(currentLayoutItem.Path));
        if (references.Count > 0)
        {
            MessageBox.Show(
                LocalizationService.T("layout.message.layoutInUse", string.Join(Environment.NewLine, references)),
                LocalizationService.T("layout.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (MessageBox.Show(LocalizationService.T("layout.message.deleteLayout", currentLayoutItem.DisplayName), LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return false;
        }

        var deletedPath = currentLayoutItem.Path;
        File.Delete(deletedPath);
        var nextPath = layoutService.GetLayoutFiles().FirstOrDefault();
        if (nextPath is null) return false;

        LayoutSaved = true;
        SavedLayoutPath = nextPath;
        ReloadLayoutCombo(nextPath);
        MarkClean();
        statusLabel.Text = LocalizationService.T("layout.message.statusDeleted", Path.GetFileName(deletedPath));
        return true;
    }

    private void ReloadLayoutCombo(string selectedPath)
    {
        suppressLayoutSelectionChange = true;
        layoutCombo.DataSource = layoutService.GetLayoutFiles().Select(FileComboItem.FromPath).ToList();
        SelectComboByPath(layoutCombo, selectedPath);
        currentLayoutItem = layoutCombo.SelectedItem as FileComboItem;
        suppressLayoutSelectionChange = false;
        if (currentLayoutItem is not null)
        {
            LoadLayout(currentLayoutItem);
        }
    }

}
