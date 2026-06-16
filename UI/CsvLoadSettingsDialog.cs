using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class CsvLoadSettingsDialog : Form
{
    private readonly NumericUpDown maxDataRowsBox;
    private readonly NumericUpDown previewRowsBox;

    public int MaxDataRowsPerFile => (int)maxDataRowsBox.Value;
    public int PreviewRowLimit => (int)previewRowsBox.Value;

    public CsvLoadSettingsDialog(int maxDataRowsPerFile, int previewRowLimit)
    {
        Text = LocalizationService.T("main.csvLoadSettings.title");
        Width = 440;
        Height = 215;
        MinimumSize = new Size(440, 215);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        maxDataRowsBox = NumberBox(maxDataRowsPerFile, 1, 1_000_000);
        previewRowsBox = NumberBox(previewRowLimit, 1, 10_000);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        layout.Controls.Add(Label(LocalizationService.T("main.csvLoadSettings.maxRows")), 0, 0);
        layout.Controls.Add(maxDataRowsBox, 1, 0);
        layout.Controls.Add(Label(LocalizationService.T("main.csvLoadSettings.previewRows")), 0, 1);
        layout.Controls.Add(previewRowsBox, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
            WrapContents = false
        };
        var ok = new Button { Text = LocalizationService.T("common.ok"), DialogResult = DialogResult.OK, Width = 92, Height = 32, Margin = new Padding(6, 0, 0, 0) };
        var cancel = new Button { Text = LocalizationService.T("common.cancel"), DialogResult = DialogResult.Cancel, Width = 92, Height = 32, Margin = new Padding(6, 0, 0, 0) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(layout);
    }

    private static Label Label(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static NumericUpDown NumberBox(int value, int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        ThousandsSeparator = true,
        Dock = DockStyle.Fill
    };
}
