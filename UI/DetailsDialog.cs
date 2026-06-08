using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class DetailsDialog : Form
{
    private readonly TextBox detailsBox = new();

    public DetailsDialog(string title, string summary, IEnumerable<string> details)
    {
        Text = title;
        Width = 720;
        Height = 460;
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = Color.FromArgb(244, 246, 248) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = summary,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(31, 41, 55),
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold)
        }, 0, 0);

        detailsBox.Dock = DockStyle.Fill;
        detailsBox.Multiline = true;
        detailsBox.ReadOnly = true;
        detailsBox.ScrollBars = ScrollBars.Both;
        detailsBox.WordWrap = false;
        detailsBox.BorderStyle = BorderStyle.FixedSingle;
        detailsBox.BackColor = Color.White;
        detailsBox.ForeColor = Color.FromArgb(31, 41, 55);
        detailsBox.Text = string.Join(Environment.NewLine, details);
        root.Controls.Add(detailsBox, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var closeButton = DialogButton(LocalizationService.T("details.button.close"), primary: true);
        closeButton.DialogResult = DialogResult.OK;
        var copyButton = DialogButton(LocalizationService.T("details.button.copy"));
        copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(detailsBox.Text))
            {
                Clipboard.SetText(detailsBox.Text);
            }
        };
        buttons.Controls.AddRange([closeButton, copyButton]);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    private static Button DialogButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = 88,
            Height = 30,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(216, 222, 230);
        return button;
    }
}
