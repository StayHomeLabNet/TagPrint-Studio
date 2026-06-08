using TagPrintStudio.Enums;
using TagPrintStudio.Services;

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
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.Controls.Add(top, 0, 0);

        const int fieldInputWidth = 220;
        searchFieldCombo = Combo(fieldInputWidth);
        searchTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 3, 12, 3) };
        searchTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Search();
        };
        flagColumnBox = FixedTextBox(fieldInputWidth);
        orderColumnBox = FixedTextBox(fieldInputWidth);
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
            Text = L("保存値", "Saved Value"),
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
            Text = L("検索後に自動的に対象にチェックを入れて保存して次へ", "Auto-check after search, save, and go next"),
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
            Text = L("プレビュー", "Preview"),
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

        top.Controls.Add(LabelText(L("検索項目", "Search Field")), 0, 0);
        top.Controls.Add(searchFieldCombo, 1, 0);
        top.Controls.Add(LabelText(L("検索値", "Search Value")), 2, 0);
        top.SetColumnSpan(searchTextBox, 3);
        top.Controls.Add(searchTextBox, 3, 0);
        top.Controls.Add(Button(L("検索", "Search"), (_, _) => Search(), primary: true), 6, 0);

        top.Controls.Add(LabelText(L("保存列", "Flag Column")), 0, 1);
        top.Controls.Add(flagColumnBox, 1, 1);
        top.Controls.Add(savedValuePanel, 2, 1);
        top.SetColumnSpan(savedValuePanel, 4);
        top.Controls.Add(Button(L("trueのみ表示", "Show True Only"), (_, _) => ShowCheckedOnly(), width: 120), 6, 1);

        top.Controls.Add(LabelText(L("印刷枚数", "Print Qty")), 0, 2);
        top.Controls.Add(printQuantityBox, 1, 2);
        top.Controls.Add(LabelText(L("順番列", "Order Column")), 2, 2);
        top.Controls.Add(orderColumnBox, 3, 2);
        top.Controls.Add(new Label
        {
            Text = L("2以上のときは印刷キューCSVを作成します。1の場合は通常保存です。", "2 or more creates a print queue CSV. 1 uses normal save."),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(4, 0, 4, 0)
        }, 4, 2);
        top.SetColumnSpan(top.GetControlFromPosition(4, 2)!, 2);
        if (top.GetControlFromPosition(0, 2) is Label printQtyLabel)
        {
            printQtyLabel.Text = L("印刷枚数", "Print Qty");
        }

        var orderLabel = top.GetControlFromPosition(2, 2);
        if (orderLabel is not null)
        {
            top.Controls.Remove(orderLabel);
        }

        var queueHint = top.GetControlFromPosition(4, 2);
        if (queueHint is Label queueHintLabel)
        {
            queueHintLabel.Text = L("2以上のときは印刷キューCSVを作成します", "2 or more creates a print queue CSV.");
            top.SetColumnSpan(queueHintLabel, 2);
            top.Controls.Remove(queueHintLabel);
            top.Controls.Add(queueHintLabel, 2, 2);
            queueHintLabel.Text = L("印刷枚数が2以上のときは印刷キューCSVを作成します", "When print quantity is 2 or more, a print queue CSV is created.");
            queueHintLabel.Margin = new Padding(-46, 0, 4, 0);
        }

        top.Controls.Remove(orderColumnBox);
        top.Controls.Add(LabelText(L("順番列", "Order Column")), 4, 2);
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
        resultsGrid.Columns.Add(TextColumn(nameof(TagFinderResultRow.DisplayRowNumber), L("行", "Row"), 60));
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
            Text = L("タグプレビュー", "Tag Preview"),
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
        var openDesignerButton = Button(L("レイアウトデザイナーで開く", "Open in Layout Designer"), (_, _) => OpenCurrentPreviewInLayoutDesigner(), primary: true, width: 236);
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

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = SurfaceColor,
            Padding = new Padding(0, 9, 12, 8)
        };
        bottom.Controls.Add(Button(L("閉じる", "Close"), (_, _) => CloseWithUnusedQueueCsvCleanup(), width: 100));
        bottom.Controls.Add(Button(L("キュー作成して次へ", "Queue && Next"), (_, _) => CreateQueueAndNext(), primary: true, width: 150));
        bottom.Controls.Add(Button(L("キュー作成", "Create Queue"), (_, _) => CreateQueue(), primary: true, width: 118));
        bottom.Controls.Add(Button(L("保存して次へ", "Save && Next"), (_, _) => SaveAndNext(), primary: true, width: 132));
        bottom.Controls.Add(Button(L("保存", "Save"), (_, _) => Save(), primary: true, width: 100));
        bottom.Controls.Add(Button(L("キューCSVをすべて削除", "Delete All Queue CSVs"), (_, _) => DeleteAllQueueCsvs(), width: 220));
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

    private static Button Button(string text, EventHandler click, bool primary = false, int width = 92)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 4, 0),
            BackColor = primary ? AccentColor : SurfaceColor,
            ForeColor = primary ? Color.White : Color.FromArgb(17, 24, 39)
        };
        button.FlatAppearance.BorderColor = primary ? AccentColor : Color.FromArgb(203, 213, 225);
        button.Click += click;
        return button;
    }

    private static string L(string japanese, string english) =>
        LocalizationService.CurrentLanguage == AppLanguage.Japanese ? japanese : english;
}
