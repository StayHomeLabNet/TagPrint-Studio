using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private List<LayoutElement> SelectedElements()
    {
        var selected = elementsGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as LayoutElement)
            .Where(element => element is not null)
            .Cast<LayoutElement>()
            .Distinct()
            .ToList();

        if (selected.Count > 0) return selected;

        var current = SelectedElement();
        return current is null ? [] : [current];
    }

    private void ArrangeSelectedElements()
    {
        if (arrangeCombo.SelectedItem is not ArrangeComboItem item) return;

        var selected = SelectedElements();
        if (selected.Count < 2)
        {
            statusLabel.Text = LocalizationService.T("layout.message.arrangeNeedTwo");
            return;
        }

        if (selected.Any(element => element.Locked))
        {
            statusLabel.Text = LocalizationService.T("layout.message.arrangeLocked");
            return;
        }

        if ((item.Action == ArrangeAction.DistributeHorizontal || item.Action == ArrangeAction.DistributeVertical) && selected.Count < 3)
        {
            statusLabel.Text = LocalizationService.T("layout.message.arrangeNeedThree");
            return;
        }

        CaptureUndo();
        ArrangeElements(selected, item.Action);
        currentLayout.Elements = elementRows.OrderBy(element => element.ZIndex).ToList();
        elementsGrid.Refresh();
        LoadSelectedElementIntoEditor();
        MarkDirty();
        RedrawPreview();
        statusLabel.Text = LocalizationService.T("layout.message.arranged", item.Text);
    }

    private void ArrangeElements(List<LayoutElement> elements, ArrangeAction action)
    {
        switch (action)
        {
            case ArrangeAction.AlignLeft:
                AlignLeft(elements);
                break;
            case ArrangeAction.AlignCenter:
                AlignCenter(elements);
                break;
            case ArrangeAction.AlignRight:
                AlignRight(elements);
                break;
            case ArrangeAction.AlignMiddle:
                AlignMiddle(elements);
                break;
            case ArrangeAction.DistributeHorizontal:
                DistributeHorizontal(elements);
                break;
            case ArrangeAction.DistributeVertical:
                DistributeVertical(elements);
                break;
        }
    }

    private void AlignLeft(List<LayoutElement> elements)
    {
        var targetX = elements.Min(element => element.X);
        foreach (var element in elements)
        {
            element.X = RoundLayoutValue(Clamp(targetX, 0, Math.Max(0, currentLayout.Tag.Width - element.Width)));
        }
    }

    private void AlignCenter(List<LayoutElement> elements)
    {
        var left = elements.Min(element => element.X);
        var right = elements.Max(element => element.X + element.Width);
        var center = (left + right) / 2d;
        foreach (var element in elements)
        {
            var x = center - (element.Width / 2d);
            element.X = RoundLayoutValue(Clamp(x, 0, Math.Max(0, currentLayout.Tag.Width - element.Width)));
        }
    }

    private void AlignRight(List<LayoutElement> elements)
    {
        var targetRight = elements.Max(element => element.X + element.Width);
        foreach (var element in elements)
        {
            var x = targetRight - element.Width;
            element.X = RoundLayoutValue(Clamp(x, 0, Math.Max(0, currentLayout.Tag.Width - element.Width)));
        }
    }

    private void AlignMiddle(List<LayoutElement> elements)
    {
        var top = elements.Min(element => element.Y);
        var bottom = elements.Max(element => element.Y + element.Height);
        var middle = (top + bottom) / 2d;
        foreach (var element in elements)
        {
            var y = middle - (element.Height / 2d);
            element.Y = RoundLayoutValue(Clamp(y, 0, Math.Max(0, currentLayout.Tag.Height - element.Height)));
        }
    }

    private void DistributeHorizontal(List<LayoutElement> elements)
    {
        var ordered = elements.OrderBy(element => element.X).ThenBy(element => element.ZIndex).ToList();
        var left = ordered.First().X;
        var right = ordered.Last().X + ordered.Last().Width;
        var totalWidth = ordered.Sum(element => element.Width);
        var availableGap = right - left - totalWidth;
        var gap = ordered.Count > 1 ? availableGap / (ordered.Count - 1) : 0d;
        var x = left;

        foreach (var element in ordered)
        {
            element.X = RoundLayoutValue(Clamp(x, 0, Math.Max(0, currentLayout.Tag.Width - element.Width)));
            x += element.Width + gap;
        }
    }

    private void DistributeVertical(List<LayoutElement> elements)
    {
        var ordered = elements.OrderBy(element => element.Y).ThenBy(element => element.ZIndex).ToList();
        var top = ordered.First().Y;
        var bottom = ordered.Last().Y + ordered.Last().Height;
        var totalHeight = ordered.Sum(element => element.Height);
        var availableGap = bottom - top - totalHeight;
        var gap = ordered.Count > 1 ? availableGap / (ordered.Count - 1) : 0d;
        var y = top;

        foreach (var element in ordered)
        {
            element.Y = RoundLayoutValue(Clamp(y, 0, Math.Max(0, currentLayout.Tag.Height - element.Height)));
            y += element.Height + gap;
        }
    }

    private static double RoundLayoutValue(double value) => Math.Round(value, 1);
}
