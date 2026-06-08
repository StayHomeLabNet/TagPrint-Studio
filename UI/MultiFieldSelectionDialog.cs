using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class MultiFieldSelectionDialog : Form
{
    private readonly CheckedListBox fieldList = new();
    private readonly List<FieldDefinition> fields;

    public IReadOnlyList<string> SelectedFieldIds { get; private set; } = [];

    private sealed class FieldListItem
    {
        public string FieldId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
    }

    public MultiFieldSelectionDialog(IEnumerable<FieldDefinition> fields, IEnumerable<string> selectedFieldIds, string title)
    {
        this.fields = fields.ToList();
        var selected = selectedFieldIds.ToHashSet(StringComparer.Ordinal);

        Text = title;
        Width = 420;
        Height = 480;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        fieldList.Dock = DockStyle.Fill;
        fieldList.CheckOnClick = true;
        var items = this.fields.Select(field => new FieldListItem
        {
            FieldId = field.FieldId,
            DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName
        }).ToList();

        foreach (var item in items)
        {
            fieldList.Items.Add(item, selected.Contains(item.FieldId));
        }

        root.Controls.Add(fieldList, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var okButton = new Button { Text = LocalizationService.T("common.ok"), Width = 80, Height = 32, DialogResult = DialogResult.None };
        var cancelButton = new Button { Text = LocalizationService.T("common.cancel"), Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
        okButton.Click += (_, _) => AcceptSelection();
        buttons.Controls.AddRange([cancelButton, okButton]);
        root.Controls.Add(buttons, 0, 1);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AcceptSelection()
    {
        SelectedFieldIds = fieldList.CheckedItems.Cast<FieldListItem>().Select(item => item.FieldId).ToList();
        DialogResult = DialogResult.OK;
        Close();
    }
}
