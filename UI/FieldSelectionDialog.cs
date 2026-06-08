using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class FieldSelectionDialog : Form
{
    private readonly ListBox fieldList = new();
    private readonly List<FieldDefinition> fields;

    public FieldDefinition? SelectedField { get; private set; }

    public FieldSelectionDialog(IEnumerable<FieldDefinition> fields)
    {
        this.fields = fields.ToList();
        Text = LocalizationService.T("fieldSelection.title");
        Width = 360;
        Height = 420;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        fieldList.Dock = DockStyle.Fill;
        fieldList.DisplayMember = nameof(FieldListItem.DisplayName);
        fieldList.DataSource = this.fields.Select(FieldListItem.FromField).ToList();
        fieldList.DoubleClick += (_, _) => AcceptSelection();
        root.Controls.Add(fieldList, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };

        var okButton = new Button
        {
            Text = LocalizationService.T("common.ok"),
            Width = 82,
            Height = 30,
            MinimumSize = new Size(82, 30),
            DialogResult = DialogResult.None,
            Margin = new Padding(4, 0, 4, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var cancelButton = new Button
        {
            Text = LocalizationService.T("common.cancel"),
            Width = 82,
            Height = 30,
            MinimumSize = new Size(82, 30),
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(4, 0, 4, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        okButton.Click += (_, _) => AcceptSelection();
        buttons.Controls.AddRange([cancelButton, okButton]);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AcceptSelection()
    {
        if (fieldList.SelectedItem is not FieldListItem selected) return;
        SelectedField = fields.FirstOrDefault(f => string.Equals(f.FieldId, selected.FieldId, StringComparison.Ordinal));
        if (SelectedField is null) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class FieldListItem
    {
        public string FieldId { get; init; } = "";
        public string DisplayName { get; init; } = "";

        public static FieldListItem FromField(FieldDefinition field) => new()
        {
            FieldId = field.FieldId,
            DisplayName = string.IsNullOrWhiteSpace(field.DisplayName)
                ? field.FieldId
                : field.DisplayName
        };
    }
}
