using TagPrintStudio.Models;
using TagPrintStudio.Services;

using TagPrintStudio.Enums;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private void ActivateElementFromCanvas(LayoutElement element)
    {
        SelectElement(element);
        LoadSelectedElementIntoEditor();
        elementsGrid.Refresh();
        RedrawPreview();
        previewBox.Focus();
    }

    private void PreviewBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (previewMode != DesignerPreviewMode.SingleTag) return;
        if (e.Button != MouseButtons.Left) return;
        if (canvasTextEditor is not null)
        {
            EndCanvasTextEdit(commit: true);
        }

        var point = PointToTag(e.Location);
        if (point is null) return;

        var selected = SelectedElement();
        var handle = selected is null ? DragMode.None : HitHandle(point.Value, selected);
        var hit = handle == DragMode.None ? HitTest(point.Value) : selected;
        if (hit is null) return;

        ActivateElementFromCanvas(hit);

        if (hit.Locked)
        {
            statusLabel.Text = LocalizationService.T("layout.message.lockedCanvas");
            previewBox.Cursor = Cursors.No;
            return;
        }

        CaptureUndo();
        dragMode = handle == DragMode.None ? DragMode.Move : handle;
        dragStartBounds = ElementBounds(hit);
        dragOffsetMm = new PointF(point.Value.X - (float)hit.X, point.Value.Y - (float)hit.Y);
        previewBox.Cursor = CursorForDragMode(dragMode);
    }

    private void PreviewBox_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (previewMode != DesignerPreviewMode.SingleTag) return;
        if (e.Button != MouseButtons.Left) return;
        var point = PointToTag(e.Location);
        if (point is null) return;

        var hit = HitTest(point.Value);
        if (hit is null) return;

        ActivateElementFromCanvas(hit);
        if (hit.Locked)
        {
            statusLabel.Text = LocalizationService.T("layout.message.lockedCanvas");
            typeCombo.Focus();
            return;
        }
        if (CanEditTextOnCanvas(hit))
        {
            BeginCanvasTextEdit(hit);
            return;
        }

        if (hit.ElementType == LayoutElementType.Image)
        {
            SelectImagePath();
            return;
        }

        if (hit.ElementType == LayoutElementType.FieldText && !string.IsNullOrWhiteSpace(hit.FieldId))
        {
            BeginCanvasTextEdit(hit);
            return;
        }

        statusLabel.Text = LocalizationService.T("layout.message.canvasEditUnavailable");
        typeCombo.Focus();
    }

    private void PreviewBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (previewMode != DesignerPreviewMode.SingleTag)
        {
            previewBox.Cursor = Cursors.Default;
            return;
        }
        var tagPoint = PointToTag(e.Location);
        if (dragMode == DragMode.None)
        {
            previewBox.Cursor = tagPoint is { } point ? CursorForHover(point) : Cursors.Cross;
            return;
        }

        var element = SelectedElement();
        if (element is null || tagPoint is null) return;

        if (dragMode == DragMode.Move)
        {
            MoveElement(element, tagPoint.Value);
        }
        else
        {
            ResizeElement(element, tagPoint.Value);
        }

        SyncEditorPositionAndSize(element);
        elementsGrid.Refresh();
        RedrawPreview();
    }

    private void EndDrag()
    {
        if (dragMode == DragMode.None) return;
        dragMode = DragMode.None;
        previewBox.Cursor = Cursors.Cross;
    }

    private void MoveElement(LayoutElement element, PointF tagPoint)
    {
        var x = tagPoint.X - dragOffsetMm.X;
        var y = tagPoint.Y - dragOffsetMm.Y;
        element.X = Math.Round(Snap(Clamp(x, 0, currentLayout.Tag.Width - element.Width)), 1);
        element.Y = Math.Round(Snap(Clamp(y, 0, currentLayout.Tag.Height - element.Height)), 1);
        MarkDirty();
    }

    private void ResizeElement(LayoutElement element, PointF tagPoint)
    {
        double left = dragStartBounds.Left;
        double top = dragStartBounds.Top;
        double right = dragStartBounds.Right;
        double bottom = dragStartBounds.Bottom;
        var x = Snap(tagPoint.X);
        var y = Snap(tagPoint.Y);

        switch (dragMode)
        {
            case DragMode.ResizeTopLeft:
                left = Clamp(x, 0, right - MinElementSize);
                top = Clamp(y, 0, bottom - MinElementSize);
                break;
            case DragMode.ResizeTopRight:
                right = Clamp(x, left + MinElementSize, currentLayout.Tag.Width);
                top = Clamp(y, 0, bottom - MinElementSize);
                break;
            case DragMode.ResizeBottomLeft:
                left = Clamp(x, 0, right - MinElementSize);
                bottom = Clamp(y, top + MinElementSize, currentLayout.Tag.Height);
                break;
            case DragMode.ResizeBottomRight:
                right = Clamp(x, left + MinElementSize, currentLayout.Tag.Width);
                bottom = Clamp(y, top + MinElementSize, currentLayout.Tag.Height);
                break;
        }

        element.X = Math.Round(left, 1);
        element.Y = Math.Round(top, 1);
        element.Width = Math.Round(right - left, 1);
        element.Height = Math.Round(bottom - top, 1);
        MarkDirty();
    }

    private LayoutElement? HitTest(PointF point)
    {
        return elementRows
            .Where(e => e.Visible)
            .OrderByDescending(e => e.ZIndex)
            .FirstOrDefault(e =>
                point.X >= e.X &&
                point.Y >= e.Y &&
                point.X <= e.X + e.Width &&
                point.Y <= e.Y + e.Height);
    }

    private DragMode HitHandle(PointF point, LayoutElement element)
    {
        if (element.Locked) return DragMode.None;
        var rect = ElementBounds(element);
        foreach (var (mode, handle) in SelectionHandlesMm(rect))
        {
            if (handle.Contains(point)) return mode;
        }
        return DragMode.None;
    }

    private Cursor CursorForHover(PointF point)
    {
        var selected = SelectedElement();
        if (selected is not null)
        {
            var handle = HitHandle(point, selected);
            if (handle != DragMode.None) return CursorForDragMode(handle);
        }
        var hit = HitTest(point);
        if (hit is null) return Cursors.Cross;
        return hit.Locked ? Cursors.No : Cursors.SizeAll;
    }

    private static Cursor CursorForDragMode(DragMode mode) => mode switch
    {
        DragMode.ResizeTopLeft or DragMode.ResizeBottomRight => Cursors.SizeNWSE,
        DragMode.ResizeTopRight or DragMode.ResizeBottomLeft => Cursors.SizeNESW,
        DragMode.Move => Cursors.SizeAll,
        _ => Cursors.Cross
    };

    private PointF? PointToTag(Point mousePoint)
    {
        if (previewBox.Image is null) return null;
        var imageRect = GetImageDisplayRectangle();
        if (imageRect.Width <= 0 || imageRect.Height <= 0 || !imageRect.Contains(mousePoint)) return null;

        var imageX = (mousePoint.X - imageRect.Left) / imageRect.Width * previewBox.Image.Width;
        var imageY = (mousePoint.Y - imageRect.Top) / imageRect.Height * previewBox.Image.Height;
        return new PointF(imageX / PreviewScale, imageY / PreviewScale);
    }

    private RectangleF GetImageDisplayRectangle()
    {
        if (previewBox.Image is null) return RectangleF.Empty;
        var image = previewBox.Image;
        var client = previewBox.ClientRectangle;
        if (client.Width <= 0 || client.Height <= 0 || image.Width <= 0 || image.Height <= 0) return RectangleF.Empty;

        var ratio = Math.Min((float)client.Width / image.Width, (float)client.Height / image.Height);
        var width = image.Width * ratio;
        var height = image.Height * ratio;
        return new RectangleF(
            client.Left + (client.Width - width) / 2,
            client.Top + (client.Height - height) / 2,
            width,
            height);
    }

    private bool CanEditTextOnCanvas(LayoutElement element) =>
        !element.Locked &&
        (
            element.ElementType == LayoutElementType.StaticText ||
            (element.ElementType == LayoutElementType.FieldText && !string.IsNullOrWhiteSpace(element.FieldId)) ||
            (element.ElementType == LayoutElementType.PriceValue && string.IsNullOrWhiteSpace(element.FieldId))
        );

    private void BeginCanvasTextEdit(LayoutElement element)
    {
        if (previewBox.Image is null) return;

        EndCanvasTextEdit(commit: true);
        CaptureUndo();
        canvasEditingElement = element;
        canvasOriginalText = CanvasTextValue(element);

        var bounds = ElementClientBounds(element);
        if (bounds.Width < 24 || bounds.Height < 20)
        {
            bounds = new Rectangle(bounds.Left, bounds.Top, Math.Max(120, bounds.Width), Math.Max(24, bounds.Height));
        }

        canvasTextEditor = new TextBox
        {
            Bounds = bounds,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            ImeMode = ImeMode.On,
            Text = canvasOriginalText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = CreateCanvasEditorFont(element),
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        canvasTextEditor.ContextMenuStrip = CreateCanvasTextEditorContextMenu(canvasTextEditor);
        canvasTextEditor.KeyDown += CanvasTextEditor_KeyDown;
        canvasTextEditor.Leave += (_, _) => EndCanvasTextEdit(commit: true);
        previewBox.Controls.Add(canvasTextEditor);
        canvasTextEditor.BringToFront();
        canvasTextEditor.Focus();
        canvasTextEditor.SelectAll();

        statusLabel.Text = LocalizationService.T("layout.message.canvasTextEditing");
    }

    private static ContextMenuStrip CreateCanvasTextEditorContextMenu(TextBox editor)
    {
        var menu = new ContextMenuStrip();
        var undo = MenuItem("layout.context.undo", (_, _) => editor.Undo());
        var cut = MenuItem("layout.context.cut", (_, _) => editor.Cut());
        var copy = MenuItem("layout.context.copy", (_, _) => editor.Copy());
        var paste = MenuItem("layout.context.paste", (_, _) => editor.Paste());
        var delete = MenuItem("layout.context.delete", (_, _) => editor.SelectedText = string.Empty);
        var selectAll = MenuItem("layout.context.selectAll", (_, _) => editor.SelectAll());
        var lower = MenuItem("layout.context.toLower", (_, _) => TransformEditorText(editor, text => text.ToLowerInvariant()));
        var upper = MenuItem("layout.context.toUpper", (_, _) => TransformEditorText(editor, text => text.ToUpperInvariant()));
        var title = MenuItem("layout.context.toTitle", (_, _) => TransformEditorText(editor, ToProductTitleCase));

        menu.Items.AddRange([
            undo,
            new ToolStripSeparator(),
            cut,
            copy,
            paste,
            delete,
            new ToolStripSeparator(),
            lower,
            upper,
            title,
            new ToolStripSeparator(),
            selectAll
        ]);

        menu.Opening += (_, e) =>
        {
            if (editor.IsDisposed)
            {
                e.Cancel = true;
                return;
            }

            var hasText = editor.TextLength > 0;
            var hasSelection = editor.SelectionLength > 0;
            undo.Enabled = editor.CanUndo;
            cut.Enabled = hasSelection;
            copy.Enabled = hasSelection;
            delete.Enabled = hasSelection;
            lower.Enabled = hasText;
            upper.Enabled = hasText;
            title.Enabled = hasText;
            selectAll.Enabled = hasText;
        };

        return menu;
    }

    private static ToolStripMenuItem MenuItem(string key, EventHandler onClick) =>
        new(LocalizationService.T(key), null, onClick);

    private static void TransformEditorText(TextBox editor, Func<string, string> transform)
    {
        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        if (selectionLength > 0)
        {
            var selected = editor.SelectedText;
            editor.SelectedText = transform(selected);
            editor.SelectionStart = selectionStart;
            editor.SelectionLength = selectionLength;
        }
        else
        {
            editor.Text = transform(editor.Text);
            editor.SelectionStart = Math.Min(selectionStart, editor.TextLength);
            editor.SelectionLength = 0;
        }

        editor.Focus();
    }

    private static string ToProductTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lowerText = text.ToLowerInvariant();
        var result = new System.Text.StringBuilder(lowerText.Length);
        var word = new System.Text.StringBuilder();
        var wordIndex = 0;

        void FlushWord()
        {
            if (word.Length == 0)
            {
                return;
            }

            var value = word.ToString();
            result.Append(ShouldCapitalizeProductTitleWord(value, wordIndex) ? CapitalizeWord(value) : value);
            word.Clear();
            wordIndex++;
        }

        foreach (var c in lowerText)
        {
            if (char.IsLetterOrDigit(c) || c == '\'')
            {
                word.Append(c);
            }
            else
            {
                FlushWord();
                result.Append(c);
            }
        }

        FlushWord();
        return result.ToString();
    }

    private static bool ShouldCapitalizeProductTitleWord(string word, int wordIndex)
    {
        if (wordIndex == 0)
        {
            return true;
        }

        return !ProductTitleLowercaseWords.Contains(word);
    }

    private static string CapitalizeWord(string word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            if (!char.IsLetter(word[i]))
            {
                continue;
            }

            return word[..i] + char.ToUpperInvariant(word[i]) + word[(i + 1)..];
        }

        return word;
    }

    private static readonly HashSet<string> ProductTitleLowercaseWords = new(StringComparer.Ordinal)
    {
        "a",
        "an",
        "and",
        "as",
        "at",
        "but",
        "by",
        "for",
        "from",
        "in",
        "into",
        "nor",
        "of",
        "on",
        "or",
        "per",
        "set",
        "the",
        "to",
        "via",
        "vs",
        "with"
    };

    private Rectangle ElementClientBounds(LayoutElement element)
    {
        var imageRect = GetImageDisplayRectangle();
        if (previewBox.Image is null || imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return Rectangle.Empty;
        }

        var scaleX = imageRect.Width / previewBox.Image.Width;
        var scaleY = imageRect.Height / previewBox.Image.Height;
        return Rectangle.Round(new RectangleF(
            imageRect.Left + (float)(element.X * PreviewScale * scaleX),
            imageRect.Top + (float)(element.Y * PreviewScale * scaleY),
            (float)(element.Width * PreviewScale * scaleX),
            (float)(element.Height * PreviewScale * scaleY)));
    }

    private Font CreateCanvasEditorFont(LayoutElement element)
    {
        var imageRect = GetImageDisplayRectangle();
        var displayScale = previewBox.Image is null || previewBox.Image.Width <= 0
            ? 1f
            : imageRect.Width / previewBox.Image.Width;
        var renderedPixelSize = (float)(element.FontSize * PreviewScale * 0.72);
        using var graphics = previewBox.CreateGraphics();
        var pointSize = renderedPixelSize * displayScale * 72f / graphics.DpiY;
        return new Font(
            element.FontFamily,
            Math.Max(1f, pointSize),
            element.Bold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Point);
    }

    private void CanvasTextEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            EndCanvasTextEdit(commit: false);
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            EndCanvasTextEdit(commit: true);
            e.SuppressKeyPress = true;
        }
    }

    private void EndCanvasTextEdit(bool commit)
    {
        if (canvasTextEditor is null) return;

        var editor = canvasTextEditor;
        canvasTextEditor = null;
        previewBox.Controls.Remove(editor);

        if (canvasEditingElement is not null)
        {
            if (commit)
            {
                SetCanvasTextValue(canvasEditingElement, editor.Text);
            }
            else if (canvasEditingElement.ElementType != LayoutElementType.FieldText)
            {
                SetCanvasTextValue(canvasEditingElement, canvasOriginalText);
            }
            if (ReferenceEquals(editorElement, canvasEditingElement))
            {
                loading = true;
                textBox.Text = canvasEditingElement.Text;
                loading = false;
            }
            if (commit && canvasEditingElement.ElementType != LayoutElementType.FieldText)
            {
                MarkDirty();
            }
        }

        canvasEditingElement = null;
        editor.Dispose();
        RedrawPreview();
    }

    private string CanvasTextValue(LayoutElement element)
    {
        if (element.ElementType == LayoutElementType.FieldText && !string.IsNullOrWhiteSpace(element.FieldId))
        {
            return previewData.GetValueOrDefault(element.FieldId, "");
        }

        return element.Text;
    }

    private void SetCanvasTextValue(LayoutElement element, string value)
    {
        if (element.ElementType == LayoutElementType.FieldText && !string.IsNullOrWhiteSpace(element.FieldId))
        {
            SetCsvFieldValue(element.FieldId, value);
            return;
        }

        element.Text = value;
    }

}
