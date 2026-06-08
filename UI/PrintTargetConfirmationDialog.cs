using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class PrintTargetConfirmationDialog : Form
{
    private readonly List<PrintTargetOrderRow> orderedRowsList;
    private readonly ComboBox searchColumnCombo = new();
    private readonly TextBox searchTextBox = new();
    private readonly Label searchStatusLabel = new();
    private DataGridView orderGrid = null!;

    public PrintTargetConfirmationDialog(
        string title,
        string summary,
        IEnumerable<PrintTargetCsvSummaryRow> csvSummaries,
        IEnumerable<PrintTargetOrderRow> orderedRows)
    {
        orderedRowsList = orderedRows.ToList();

        Text = title;
        Width = 860;
        Height = 600;
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            Padding = new Padding(14),
            BackColor = Color.FromArgb(244, 246, 248)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
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

        var summaryGrid = Grid();
        summaryGrid.Columns.Add(TextColumn(nameof(PrintTargetCsvSummaryRow.CsvName), LocalizationService.T("printTarget.column.csv"), 320));
        summaryGrid.Columns.Add(TextColumn(nameof(PrintTargetCsvSummaryRow.RowCount), LocalizationService.T("printTarget.column.rows"), 90));
        summaryGrid.Columns.Add(TextColumn(nameof(PrintTargetCsvSummaryRow.TagCount), LocalizationService.T("printTarget.column.tags"), 90));
        summaryGrid.Columns.Add(TextColumn(nameof(PrintTargetCsvSummaryRow.FirstOrder), LocalizationService.T("printTarget.column.firstOrder"), 110));
        summaryGrid.Columns.Add(TextColumn(nameof(PrintTargetCsvSummaryRow.LastOrder), LocalizationService.T("printTarget.column.lastOrder"), 110));
        summaryGrid.DataSource = csvSummaries.ToList();
        root.Controls.Add(summaryGrid, 0, 1);

        root.Controls.Add(BuildSearchRow(), 0, 2);

        orderGrid = Grid();
        orderGrid.Columns.Add(TextColumn(nameof(PrintTargetOrderRow.Order), LocalizationService.T("printTarget.column.order"), 72));
        orderGrid.Columns.Add(TextColumn(nameof(PrintTargetOrderRow.CsvName), LocalizationService.T("printTarget.column.csv"), 260));
        orderGrid.Columns.Add(TextColumn(nameof(PrintTargetOrderRow.RowNumber), LocalizationService.T("printTarget.column.row"), 80));
        orderGrid.Columns.Add(TextColumn(nameof(PrintTargetOrderRow.SortValue), LocalizationService.T("printTarget.column.sortValue"), 180));
        orderGrid.Columns.Add(TextColumn(nameof(PrintTargetOrderRow.KeyValue), LocalizationService.T("printTarget.column.keyValue"), 220));
        orderGrid.DataSource = orderedRowsList;
        root.Controls.Add(orderGrid, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var okButton = DialogButton(LocalizationService.T("printTarget.button.continue"), primary: true);
        okButton.DialogResult = DialogResult.OK;
        var cancelButton = DialogButton(LocalizationService.T("common.cancel"));
        cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange([okButton, cancelButton]);
        root.Controls.Add(buttons, 0, 4);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter && ActiveControl == searchTextBox)
        {
            FindAndSelectOrderRow();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Control BuildSearchRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.FromArgb(244, 246, 248),
            Padding = new Padding(0, 6, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        searchColumnCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        searchColumnCombo.Dock = DockStyle.Fill;
        searchColumnCombo.Margin = new Padding(4, 1, 10, 1);
        searchColumnCombo.Items.AddRange([
            new SearchColumnItem(LocalizationService.T("printTarget.column.order"), row => row.Order.ToString()),
            new SearchColumnItem(LocalizationService.T("printTarget.column.csv"), row => row.CsvName),
            new SearchColumnItem(LocalizationService.T("printTarget.column.row"), row => row.RowNumber.ToString()),
            new SearchColumnItem(LocalizationService.T("printTarget.column.sortValue"), row => row.SortValue),
            new SearchColumnItem(LocalizationService.T("printTarget.column.keyValue"), row => row.KeyValue)
        ]);
        searchColumnCombo.SelectedIndex = Math.Min(3, searchColumnCombo.Items.Count - 1);

        searchTextBox.Dock = DockStyle.Fill;
        searchTextBox.Margin = new Padding(4, 1, 10, 1);
        searchTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            FindAndSelectOrderRow();
        };

        var searchButton = DialogButton(LocalizationService.T("printTarget.button.search"));
        searchButton.Width = 84;
        searchButton.Height = 30;
        searchButton.Margin = new Padding(4, -2, 4, 4);
        searchButton.Click += (_, _) => FindAndSelectOrderRow();

        searchStatusLabel.Dock = DockStyle.Fill;
        searchStatusLabel.AutoEllipsis = true;
        searchStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        searchStatusLabel.ForeColor = Color.FromArgb(75, 85, 99);

        panel.Controls.Add(SearchLabel(LocalizationService.T("printTarget.label.searchColumn")), 0, 0);
        panel.Controls.Add(searchColumnCombo, 1, 0);
        panel.Controls.Add(SearchLabel(LocalizationService.T("printTarget.label.searchValue")), 2, 0);
        panel.Controls.Add(searchTextBox, 3, 0);
        panel.Controls.Add(searchButton, 4, 0);
        panel.Controls.Add(searchStatusLabel, 5, 0);
        return panel;
    }

    private void FindAndSelectOrderRow()
    {
        if (searchColumnCombo.SelectedItem is not SearchColumnItem column) return;

        var query = searchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            searchStatusLabel.Text = LocalizationService.T("printTarget.search.enterValue");
            return;
        }

        var index = orderedRowsList.FindIndex(row => column.Value(row).Contains(query, StringComparison.CurrentCultureIgnoreCase));
        if (index < 0)
        {
            searchStatusLabel.Text = LocalizationService.T("printTarget.search.notFound");
            System.Media.SystemSounds.Beep.Play();
            return;
        }

        orderGrid.ClearSelection();
        var gridRow = orderGrid.Rows[index];
        gridRow.Selected = true;
        orderGrid.CurrentCell = gridRow.Cells[0];
        orderGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, index);
        searchStatusLabel.Text = LocalizationService.T("printTarget.search.found", orderedRowsList[index].Order);
        orderGrid.Focus();
    }

    private static Label SearchLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(31, 41, 55),
        Margin = new Padding(0, 0, 4, 0)
    };

    private static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        ReadOnly = true,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(31, 41, 55),
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = Color.FromArgb(15, 23, 42)
        }
    };

    private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width) => new()
    {
        DataPropertyName = property,
        HeaderText = header,
        Name = property,
        Width = width
    };

    private static Button DialogButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = primary ? 120 : 96,
            Height = 36,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(31, 41, 55)
        };
        button.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(216, 222, 230);
        return button;
    }
}

internal sealed class SearchColumnItem(string displayName, Func<PrintTargetOrderRow, string> value)
{
    public string DisplayName { get; } = displayName;
    public Func<PrintTargetOrderRow, string> Value { get; } = value;
    public override string ToString() => DisplayName;
}
