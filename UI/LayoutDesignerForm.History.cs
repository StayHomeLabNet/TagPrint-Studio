using TagPrintStudio.Models;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private void CaptureUndo()
    {
        if (loading || applyingHistory) return;
        undoStack.Push(CloneElements(elementRows));
        redoStack.Clear();
        UpdateHistoryButtons();
    }

    private void Undo()
    {
        if (undoStack.Count == 0) return;
        redoStack.Push(CloneElements(elementRows));
        RestoreElements(undoStack.Pop());
        MarkDirty();
    }

    private void Redo()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push(CloneElements(elementRows));
        RestoreElements(redoStack.Pop());
        MarkDirty();
    }

    private void RestoreElements(List<LayoutElement> elements)
    {
        var selectedId = SelectedElement()?.ElementId;
        applyingHistory = true;
        loading = true;
        elementRows.Clear();
        foreach (var element in elements.OrderBy(e => e.ZIndex)) elementRows.Add(element);
        currentLayout.Elements = elementRows.ToList();
        loading = false;
        applyingHistory = false;

        var selected = elementRows.FirstOrDefault(e => e.ElementId == selectedId) ?? elementRows.FirstOrDefault();
        if (selected is not null) SelectElement(selected);
        LoadSelectedElementIntoEditor();
        UpdateHistoryButtons();
        RedrawPreview();
    }

    private void UpdateHistoryButtons()
    {
        if (undoButton is null || redoButton is null) return;
        undoButton.Enabled = undoStack.Count > 0;
        redoButton.Enabled = redoStack.Count > 0;
    }

}
