using TagPrintStudio.Enums;
using TagPrintStudio.Services;

using TagPrintStudio.Models;

namespace TagPrintStudio.UI;

public sealed partial class TagFinderDialog
{
    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(244, 246, 248)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(root);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 3,
            BackColor = SurfaceColor,
            Padding = new Padding(14, 10, 14, 8)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.Controls.Add(top, 0, 0);

        const int leftInputWidth = 168;
        const int rightInputWidth = 208;
        searchFieldCombo = Combo(leftInputWidth);
        searchTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 3, 12, 3) };
        searchTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Search();
        };
        flagColumnBox = FixedTextBox(leftInputWidth);
        orderColumnBox = FixedTextBox(rightInputWidth);
        printQuantityBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 999,
            Value = 1,
            Width = 90,
            Height = 30,
            Margin = new Padding(4, 3, 12, 3)
        };
        var savedValuePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0, 1, 0, 0),
            AutoSize = false
        };
        savedValuePanel.Controls.Add(new Label
        {
            Text = T("tagFinder.label.savedValue"),
            AutoSize = false,
            Width = 64,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Margin = new Padding(4, 0, 8, 0)
        });
        savedValuePanel.Controls.Add(new Label
        {
            Text = "true / false",
            AutoSize = false,
            Width = 92,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 0)
        });
        autoCheckAfterSearchCheck = new CheckBox
        {
            Text = T("tagFinder.hint.autoCheckAfterSearch"),
            AutoSize = false,
            Width = 470,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            CheckAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Margin = new Padding(12, 2, 0, 0),
            Padding = new Padding(0, 1, 0, 0)
        };
        savedValuePanel.Controls.Add(autoCheckAfterSearchCheck);
        tagPreviewVisibleCheck = new CheckBox
        {
            Text = T("tagFinder.label.preview"),
            Checked = true,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            CheckAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Margin = new Padding(4, 0, 0, 0)
        };
        tagPreviewVisibleCheck.CheckedChanged += (_, _) =>
        {
            SaveFinderSettings();
            ApplyTagPreviewVisibility();
        };

        top.Controls.Add(LabelText(T("tagFinder.label.searchField")), 0, 0);
        top.Controls.Add(searchFieldCombo, 1, 0);
        top.Controls.Add(LabelText(T("tagFinder.label.searchValue")), 2, 0);
        top.SetColumnSpan(searchTextBox, 3);
        top.Controls.Add(searchTextBox, 3, 0);
        top.Controls.Add(Button(T("tagFinder.button.search"), (_, _) => Search(), primary: true), 6, 0);

        top.Controls.Add(LabelText(T("tagFinder.label.printTargetColumn")), 0, 1);
        top.Controls.Add(flagColumnBox, 1, 1);
        top.Controls.Add(savedValuePanel, 2, 1);
        top.SetColumnSpan(savedValuePanel, 4);
        var showTrueOnlyButton = Button(T("tagFinder.button.showTrueOnly"), (_, _) => ShowCheckedOnly(), width: 118, fontSize: 7.5f);
        showTrueOnlyButton.Margin = new Padding(2, 0, 2, 0);
        top.Controls.Add(showTrueOnlyButton, 6, 1);

        top.Controls.Add(LabelText(T("tagFinder.label.printQuantity")), 0, 2);
        var printQuantityPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        printQuantityBox.Margin = new Padding(4, 3, 6, 3);
        printQuantityPanel.Controls.Add(printQuantityBox);
        var queueHintLabel = new Label
        {
            Text = T("tagFinder.hint.queueQuantity"),
            AutoSize = true,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(0, 6, 4, 0)
        };
        printQuantityPanel.Controls.Add(queueHintLabel);
        top.Controls.Add(printQuantityPanel, 1, 2);
        top.SetColumnSpan(printQuantityPanel, 3);
        top.Controls.Add(LabelText(T("tagFinder.label.orderColumnName")), 4, 2);
        top.Controls.Add(orderColumnBox, 5, 2);
        top.Controls.Add(tagPreviewVisibleCheck, 6, 2);

        resultsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = SurfaceColor,
            BorderStyle = BorderStyle.None,
            DataSource = resultRows
        };
        resultsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderBackColor,
            ForeColor = TextColor
        };
        resultsGrid.EnableHeadersVisualStyles = false;
        resultsGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "TargetCheckColumn",
            DataPropertyName = nameof(TagFinderResultRow.Checked),
            HeaderText = TargetHeaderText(false),
            Width = 74,
            ThreeState = false,
            TrueValue = true,
            FalseValue = false
        });
        resultsGrid.Columns.Add(TextColumn(nameof(TagFinderResultRow.FileName), "CSV", 160));
        resultsGrid.Columns.Add(TextColumn(nameof(TagFinderResultRow.DisplayRowNumber), T("common.row"), 60));
        foreach (var field in fields.Take(10))
        {
            resultsGrid.Columns.Add(TextColumn($"Field_{field.FieldId}", field.DisplayName, 150));
        }
        resultsGrid.CellFormatting += ResultsGrid_CellFormatting;
        resultsGrid.ColumnHeaderMouseClick += ResultsGrid_ColumnHeaderMouseClick;
        resultsGrid.CurrentCellDirtyStateChanged += ResultsGrid_CurrentCellDirtyStateChanged;
        resultsGrid.CellValueChanged += ResultsGrid_CellValueChanged;
        resultsGrid.SelectionChanged += (_, _) => UpdateTagPreview();
        resultsGrid.CellDoubleClick += ResultsGrid_CellDoubleClick;

        contentPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(244, 246, 248),
            Margin = Padding.Empty
        };
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contentPanel.Controls.Add(resultsGrid, 0, 0);

        previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(12, 0, 0, 0),
            BackColor = Color.FromArgb(244, 246, 248)
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        tagPreviewLabel = new Label
        {
            Text = T("tagFinder.label.tagPreview"),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Font = new Font(Font, FontStyle.Bold)
        };
        tagPreviewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        var openDesignerButton = Button(T("tagFinder.button.openInLayoutDesigner"), (_, _) => OpenCurrentPreviewSetInLayoutDesigner(), primary: true, width: 236);
        openDesignerButton.Dock = DockStyle.Fill;
        openDesignerButton.Margin = new Padding(0, 8, 0, 0);
        previewPanel.Controls.Add(tagPreviewLabel, 0, 0);
        previewPanel.Controls.Add(tagPreviewBox, 0, 1);
        previewPanel.Controls.Add(openDesignerButton, 0, 2);
        contentPanel.Controls.Add(previewPanel, 1, 0);
        root.Controls.Add(contentPanel, 0, 1);

        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(55, 65, 81),
            BackColor = SurfaceColor,
            Padding = new Padding(12, 0, 0, 0)
        };
        root.Controls.Add(statusLabel, 0, 2);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SurfaceColor
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 9, 0, 8)
        };
        leftButtons.Controls.Add(Button(T("tagFinder.button.clearAllPrintTargets"), (_, _) => ClearAllPrintTargets(), width: 180, fontSize: 8.5f));

        var rightButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = SurfaceColor,
            Padding = new Padding(0, 9, 12, 8)
        };
        rightButtons.Controls.Add(Button(T("tagFinder.button.close"), (_, _) => CloseWithUnusedQueueCsvCleanup(), width: 100));
        rightButtons.Controls.Add(Button(T("tagFinder.button.queueAndNext"), (_, _) => CreateQueueAndNext(), primary: true, width: 150));
        rightButtons.Controls.Add(Button(T("tagFinder.button.createQueue"), (_, _) => CreateQueue(), primary: true, width: 118));
        rightButtons.Controls.Add(Button(T("tagFinder.button.saveAndNext"), (_, _) => SaveAndNext(), primary: true, width: 132));
        rightButtons.Controls.Add(Button(T("tagFinder.button.save"), (_, _) => Save(), primary: true, width: 100));
        rightButtons.Controls.Add(Button(T("tagFinder.button.deleteAllQueueCsvs"), (_, _) => DeleteAllQueueCsvs(), width: 220));
        bottom.Controls.Add(leftButtons, 0, 0);
        bottom.Controls.Add(rightButtons, 1, 0);
        root.Controls.Add(bottom, 0, 3);
    }

    private static DataGridViewTextBoxColumn TextColumn(string propertyName, string header, int width) => new()
    {
        Name = propertyName,
        DataPropertyName = propertyName,
        HeaderText = header,
        Width = width,
        ReadOnly = true
    };

    private static Label LabelText(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = TextColor,
        Margin = new Padding(4, 0, 4, 0)
    };

    private static ComboBox Combo(int width) => new()
    {
        Width = width,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Margin = new Padding(4, 3, 12, 3)
    };

    private static TextBox FixedTextBox(int width) => new()
    {
        Width = width,
        Dock = DockStyle.None,
        Anchor = AnchorStyles.Left | AnchorStyles.Top,
        Margin = new Padding(4, 3, 12, 3)
    };

    private static Button Button(string text, EventHandler click, bool primary = false, int width = 92, float? fontSize = null)
    {
        var baseFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 4, 0),
            BackColor = primary ? AccentColor : SurfaceColor,
            ForeColor = primary ? Color.White : Color.FromArgb(17, 24, 39),
            Font = fontSize.HasValue
                ? new Font(baseFont.FontFamily, fontSize.Value, FontStyle.Regular)
                : baseFont
        };
        button.FlatAppearance.BorderColor = primary ? AccentColor : Color.FromArgb(203, 213, 225);
        button.Click += click;
        return button;
    }

}
