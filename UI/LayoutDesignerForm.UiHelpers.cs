namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm
{
    private static TableLayoutPanel Section(string title, Control content, Control? tools = null)
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = SurfaceColor,
            Padding = new Padding(1),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = HeaderBackColor,
            Padding = new Padding(12, 6, 10, 6),
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var titleLabel = new Label
        {
            Text = ResolveUiText(title),
            AutoSize = true,
            BackColor = HeaderBackColor,
            ForeColor = TextColor,
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
            Padding = new Padding(0, 5, 16, 0)
        };
        BindIfLocalized(titleLabel, title);
        header.Controls.Add(titleLabel, 0, 0);
        if (tools is not null)
        {
            tools.Margin = new Padding(0);
            tools.Padding = new Padding(0);
            header.Controls.Add(tools, 1, 0);
        }
        content.Margin = new Padding(0);
        section.Controls.Add(header, 0, 0);
        section.Controls.Add(content, 0, 1);
        return section;
    }

    private static Label LabelText(string text, int left = 0)
    {
        var label = new Label
        {
            Text = ResolveUiText(text),
            AutoSize = true,
            ForeColor = MutedTextColor,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            Padding = new Padding(left, 8, 0, 0)
        };
        BindIfLocalized(label, text);
        return label;
    }

    private static Button Button(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = ResolveUiText(text),
            AutoSize = true,
            Height = ToolbarControlHeight,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentColor : SurfaceColor,
            ForeColor = primary ? Color.White : TextColor,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? AccentColor : BorderColor;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHoverColor : HeaderBackColor;
        button.Click += click;
        BindIfLocalized(button, text);
        return button;
    }

    private static void ConfigureCanvasZoomButton(Button button)
    {
        button.AutoSize = false;
        button.Width = 104;
        button.Height = ToolbarControlHeight;
        button.Margin = new Padding(0, 0, 0, 6);
        button.Padding = new Padding(4, 0, 4, 0);
    }

    private static void SetToolbarButtonState(Button button, bool active)
    {
        button.BackColor = active ? AccentColor : SurfaceColor;
        button.ForeColor = active ? Color.White : TextColor;
        button.FlatAppearance.BorderColor = active ? AccentColor : BorderColor;
        button.FlatAppearance.MouseOverBackColor = active ? AccentHoverColor : HeaderBackColor;
    }

    private static ComboBox ToolbarComboBox(int width)
    {
        var combo = new ComboBox
        {
            Width = width,
            Height = ToolbarControlHeight,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = ToolbarControlHeight - 6,
            Margin = new Padding(4, 0, 4, 0)
        };
        combo.DrawItem += DrawToolbarComboItem;
        return combo;
    }

    private static TextBox ToolbarTextBox(int width) => new()
    {
        Width = width,
        Height = ToolbarControlHeight,
        AutoSize = false,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(4, 0, 4, 0)
    };

    private static void DrawToolbarComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0) return;

        e.DrawBackground();
        var text = combo.GetItemText(combo.Items[e.Index]);
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var color = !combo.Enabled
            ? MutedTextColor
            : selected
                ? SystemColors.HighlightText
                : TextColor;
        var bounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            combo.Font,
            bounds,
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private static TextBox TextBox() => new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3, 3, 6, 3) };
}
