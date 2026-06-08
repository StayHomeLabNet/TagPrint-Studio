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
        var layoutChanged = SyncLayoutElementsToFields();
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

    private bool SyncLayoutElementsToFields()
    {
        var fieldsById = fieldDefinitions
            .Where(field => !string.IsNullOrWhiteSpace(field.FieldId))
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
        var changed = false;

        foreach (var element in currentLayout.Elements.Where(element => element.ElementType == LayoutElementType.FieldText))
        {
            if (string.IsNullOrWhiteSpace(element.FieldId)) continue;
            if (!fieldsById.TryGetValue(element.FieldId, out var field)) continue;

            var displayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
            if (element.Name == displayName) continue;
            element.Name = displayName;
            changed = true;
        }

        var existingFieldIds = currentLayout.Elements
            .Where(element => element.ElementType == LayoutElementType.FieldText)
            .Select(element => element.FieldId)
            .Where(fieldId => !string.IsNullOrWhiteSpace(fieldId))
            .ToHashSet(StringComparer.Ordinal);
        var excludedFieldIds = currentLayout.ExcludedAutoFieldIds
            .Where(fieldId => !string.IsNullOrWhiteSpace(fieldId))
            .ToHashSet(StringComparer.Ordinal);

        var nextZIndex = currentLayout.Elements.Count == 0 ? 10 : currentLayout.Elements.Max(element => element.ZIndex) + 10;
        for (var i = 0; i < fieldDefinitions.Count; i++)
        {
            var field = fieldDefinitions[i];
            if (string.IsNullOrWhiteSpace(field.FieldId) || existingFieldIds.Contains(field.FieldId)) continue;
            if (excludedFieldIds.Contains(field.FieldId)) continue;

            var element = CreateFieldElement(field, i, nextZIndex);
            currentLayout.Elements.Add(element);
            existingFieldIds.Add(field.FieldId);
            nextZIndex += 10;
            changed = true;
        }

        return changed;
    }

    private LayoutElement CreateFieldElement(FieldDefinition field, int fieldIndex, int zIndex)
    {
        var displayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName;
        var safeWidth = Math.Max(1, currentLayout.Tag.Width - 4);
        var availableHeight = Math.Max(4, currentLayout.Tag.Height - 4);
        var rowHeight = Clamp(availableHeight / Math.Max(1, fieldDefinitions.Count), 3, 6);
        var y = Clamp(2 + fieldIndex * rowHeight, 0, Math.Max(0, currentLayout.Tag.Height - rowHeight));

        return new LayoutElement
        {
            ElementId = $"el_{DateTime.Now:HHmmssfff}_{fieldIndex}",
            Name = displayName,
            ZIndex = zIndex,
            ElementType = LayoutElementType.FieldText,
            FieldId = field.FieldId,
            X = 2,
            Y = Math.Round(y, 1),
            Width = safeWidth,
            Height = Math.Round(rowHeight, 1),
            FontSize = 8,
            TextAlign = TextAlignType.Left,
            Visible = true
        };
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

        var filePath = Path.Combine(paths.Layouts, $"{ToSafeFileName(dialog.Value)}.tpslayout");
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

        var filePath = LayoutPathForName(dialog.Value);
        if (File.Exists(filePath))
        {
            MessageBox.Show(LocalizationService.T("layout.message.layoutExists"), LocalizationService.T("layout.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        currentLayout = CreateDefaultLayout(dialog.Value, filePath);
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

        var filePath = LayoutPathForName(dialog.Value);
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

        var newPath = LayoutPathForName(dialog.Value);
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
        UpdatePresetLayoutRefs(oldFileName, Path.GetFileName(newPath));
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

        var references = GetReferencingPresetNames(Path.GetFileName(currentLayoutItem.Path));
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

    private string LayoutPathForName(string name) => Path.Combine(paths.Layouts, $"{ToSafeFileName(name)}.tpslayout");

    private static LayoutFile CreateDefaultLayout(string name, string filePath)
    {
        var now = DateTime.Now;
        return new LayoutFile
        {
            LayoutId = Path.GetFileNameWithoutExtension(filePath),
            LayoutName = name,
            Description = "",
            CreatedAt = now,
            UpdatedAt = now,
            Paper = new PaperSettings { PaperSize = PaperSizeType.A4, Unit = UnitType.Mm },
            Tag = new TagSettings { Width = 50, Height = 30 },
            Margin = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
            Spacing = new SpacingSettings { Horizontal = 5, Vertical = 5 },
            Grid = new GridSettings { AutoCalculate = true, Columns = 3, Rows = 8 },
            FilePath = filePath,
            Elements =
            [
                new LayoutElement
                {
                    ElementId = $"el_{DateTime.Now:HHmmssfff}",
                    Name = LocalizationService.T("layout.default.text"),
                    ZIndex = 10,
                    ElementType = LayoutElementType.StaticText,
                    Text = LocalizationService.T("layout.default.textUpper"),
                    X = 4,
                    Y = 4,
                    Width = 42,
                    Height = 8,
                    FontSize = 10,
                    Bold = true,
                    TextAlign = TextAlignType.Center,
                    Visible = true
                }
            ]
        };
    }

    private List<string> GetReferencingPresetNames(string layoutFileName)
    {
        return Directory.GetFiles(paths.Presets, "*.tpspreset")
            .Select(path => new { Path = path, Preset = JsonFile.Load<PresetFile>(path) })
            .Where(item => string.Equals(item.Preset.LayoutRef, layoutFileName, StringComparison.OrdinalIgnoreCase))
            .Select(item => string.IsNullOrWhiteSpace(item.Preset.PresetName)
                ? Path.GetFileNameWithoutExtension(item.Path)
                : item.Preset.PresetName)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void UpdatePresetLayoutRefs(string oldLayoutFileName, string newLayoutFileName)
    {
        foreach (var presetPath in Directory.GetFiles(paths.Presets, "*.tpspreset"))
        {
            var preset = JsonFile.Load<PresetFile>(presetPath);
            if (!string.Equals(preset.LayoutRef, oldLayoutFileName, StringComparison.OrdinalIgnoreCase)) continue;
            preset.LayoutRef = newLayoutFileName;
            JsonFile.Save(presetPath, preset);
        }
    }

}
