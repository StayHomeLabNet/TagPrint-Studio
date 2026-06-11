using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
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
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
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
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
            ForeColor = TextColor,
            Padding = new Padding(0, 5, 12, 0)
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

    private static Label BottomLabelText(string text, int left = 0, bool alignRight = false, int right = 0)
    {
        var label = new Label
        {
            Text = ResolveUiText(text),
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 30,
            AutoEllipsis = true,
            ForeColor = MutedTextColor,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            TextAlign = alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 2, 0, 2),
            Padding = new Padding(left, 0, right, 0)
        };
        BindIfLocalized(label, text);
        return label;
    }

    private static ComboBox BottomComboBox() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Margin = new Padding(2, 2, 4, 2),
        IntegralHeight = false
    };

    private static NumericUpDown BottomNumericUpDown() => new()
    {
        Dock = DockStyle.Fill,
        AutoSize = false,
        Height = 30,
        DecimalPlaces = 1,
        Minimum = -20,
        Maximum = 20,
        Increment = 0.1m,
        Margin = new Padding(2, 2, 4, 2)
    };

    private static Button Button(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = ResolveUiText(text),
            AutoSize = true,
            Height = 30,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentColor : SurfaceColor,
            ForeColor = primary ? Color.White : TextColor,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? AccentColor : BorderColor;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHoverColor : HeaderBackColor;
        button.Click += click;
        BindIfLocalized(button, text);
        return button;
    }

    private static void MatchToolbarHeight(Button button, int height)
    {
        button.AutoSize = false;
        button.Size = new Size(Math.Max(button.PreferredSize.Width + 8, button.Width), height);
    }

    private static void ConfigurePreviewZoomButton(Button button)
    {
        button.AutoSize = false;
        button.Size = new Size(104, 30);
        button.Margin = new Padding(0, 0, 0, 4);
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        BeginBusyOperation();
        try
        {
            await operation();
        }
        finally
        {
            EndBusyOperation();
        }
    }

    private void BeginBusyOperation()
    {
        busyOperationCount++;
        if (busyOperationCount > 1)
        {
            return;
        }

        UseWaitCursor = true;
        Cursor.Current = Cursors.WaitCursor;
        SetInteractiveControlsEnabled(false);
    }

    private void EndBusyOperation()
    {
        busyOperationCount = Math.Max(0, busyOperationCount - 1);
        if (busyOperationCount > 0)
        {
            return;
        }

        SetInteractiveControlsEnabled(true);
        UseWaitCursor = false;
        Cursor.Current = Cursors.Default;
    }

    private void SetInteractiveControlsEnabled(bool enabled)
    {
        foreach (var control in Controls.OfType<Control>())
        {
            control.Enabled = enabled;
        }
    }


    private sealed class CommitEnterDataGridView : DataGridView
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsCtrlSemicolon(keyData) && TryInsertTodayDate())
            {
                return true;
            }

            if ((keyData & Keys.KeyCode) == Keys.Enter && IsEditingText())
            {
                return CommitEditAndLeaveCell();
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (IsCtrlSemicolon(keyData) && TryInsertTodayDate())
            {
                return true;
            }

            if ((keyData & Keys.KeyCode) == Keys.Enter && IsEditingText())
            {
                return CommitEditAndLeaveCell();
            }

            return base.ProcessDialogKey(keyData);
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (IsCtrlSemicolon(e.KeyData) && TryInsertTodayDate())
            {
                return true;
            }

            if (e.KeyCode == Keys.Enter && IsEditingText())
            {
                return CommitEditAndLeaveCell();
            }

            return base.ProcessDataGridViewKey(e);
        }

        private bool IsEditingText() => IsCurrentCellInEditMode || EditingControl is not null;

        private static bool IsCtrlSemicolon(Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;
            return (keyData & Keys.Control) == Keys.Control
                && (keyCode == Keys.OemSemicolon || keyCode == Keys.Oem1);
        }

        private bool TryInsertTodayDate()
        {
            if (!IsDateValueCellEditing()) return false;
            if (EditingControl is not TextBox editingTextBox) return false;

            editingTextBox.SelectedText = DateTime.Today.ToString("dd/MM/yy");
            if (EditingControl is IDataGridViewEditingControl editingControl)
            {
                editingControl.EditingControlValueChanged = true;
                NotifyCurrentCellDirty(true);
            }

            return true;
        }

        private bool IsDateValueCellEditing()
        {
            if (!IsCurrentCellInEditMode || CurrentCell?.OwningColumn?.Name != "PrintConditionValue") return false;
            if (CurrentRow?.DataBoundItem is not FieldDefinition field) return false;
            return field.PrintConditionType == PrintConditionType.Date;
        }

        private bool CommitEditAndLeaveCell()
        {
            if (EditingControl is IDataGridViewEditingControl editingControl)
            {
                editingControl.EditingControlValueChanged = true;
                NotifyCurrentCellDirty(true);
            }

            CommitEdit(DataGridViewDataErrorContexts.Commit);
            EndEdit(DataGridViewDataErrorContexts.Commit);
            ClearSelection();
            if (CurrentCell is not null)
            {
                CurrentCell.Selected = true;
                if (CurrentRow is not null) CurrentRow.Selected = true;
            }
            Focus();
            return true;
        }
    }

    private static DataGridView Grid(object dataSource) => new CommitEnterDataGridView()
    {
        Dock = DockStyle.Fill,
        DataSource = dataSource,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BorderStyle = BorderStyle.None,
        BackgroundColor = SurfaceColor,
        GridColor = BorderColor,
        RowHeadersVisible = false,
        EnableHeadersVisualStyles = false,
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = HeaderBackColor,
            ForeColor = TextColor,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            SelectionBackColor = HeaderBackColor,
            SelectionForeColor = TextColor,
            Padding = new Padding(4, 2, 4, 2)
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SurfaceColor,
            ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = TextColor,
            Padding = new Padding(4, 2, 4, 2)
        },
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(250, 252, 254)
        },
        RowTemplate = { Height = 28 },
        ColumnHeadersHeight = 32
    };

    private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width) => new() { DataPropertyName = property, HeaderText = ResolveUiText(header), Name = property, Width = width };
    private static DataGridViewCheckBoxColumn CheckColumn(string property, string header, int width) => new() { DataPropertyName = property, HeaderText = ResolveUiText(header), Name = property, Width = width };
    private static DataGridViewButtonColumn ButtonColumn(string name, string header, int width) => new() { Name = name, HeaderText = ResolveUiText(header), Text = ResolveUiText(header), UseColumnTextForButtonValue = true, Width = width };
    private static DataGridViewComboBoxColumn EnumColumn<TEnum>(string property, string header, int width) where TEnum : struct, Enum => new()
    {
        DataPropertyName = property,
        HeaderText = header,
        Name = property,
        Width = width,
        DataSource = EnumItems<TEnum>(),
        DisplayMember = nameof(EnumDisplayItem<TEnum>.DisplayName),
        ValueMember = nameof(EnumDisplayItem<TEnum>.Value)
    };

    private static List<EnumDisplayItem<TEnum>> EnumItems<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => new EnumDisplayItem<TEnum>
            {
                Value = value,
                DisplayName = EnumDisplayText(value)
            })
            .ToList();

    private static string EnumDisplayText<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (typeof(TEnum) == typeof(PrintConditionType))
        {
            return PrintConditionTypeText((PrintConditionType)(object)value);
        }

        if (typeof(TEnum) == typeof(PrintConditionOperator))
        {
            return PrintConditionOperatorText((PrintConditionOperator)(object)value);
        }

        return value.ToString();
    }


    private static void BindIfLocalized(Control control, string textOrKey)
    {
        var key = NormalizeLocalizationKey(textOrKey);
        if (LocalizationService.HasKey(key))
        {
            LocalizationService.Bind(control, key);
        }
    }

    private static void BindIfLocalized(ToolStripItem item, string textOrKey)
    {
        var key = NormalizeLocalizationKey(textOrKey);
        if (LocalizationService.HasKey(key))
        {
            LocalizationService.Bind(item, key);
        }
    }

    private static string ResolveUiText(string textOrKey)
    {
        var key = NormalizeLocalizationKey(textOrKey);
        return LocalizationService.HasKey(key) ? LocalizationService.T(key) : textOrKey;
    }

    private static string NormalizeLocalizationKey(string textOrKey)
    {
        return textOrKey switch
        {
            "プリセット" => "main.label.preset",
            "レイアウト" => "main.label.layout",
            "プリセット保存" => "main.button.savePreset",
            "最初の行をヘッダーとして扱う" => "main.checkbox.headerRow",
            "項目追加" => "main.button.addField",
            "CSVフィールド" => "main.section.csvFields",
            "CSVファイル選択" => "main.button.selectCsv",
            "CSV選択/追加" => "main.button.selectCsv",
            "フォルダー選択" => "main.button.selectFolder",
            "CSV読み込み" => "main.section.csvImport",
            "前へ" => "main.button.previous",
            "次へ" => "main.button.next",
            "ページ全体" => "main.button.pagePreview",
            "1枚拡大" => "main.button.singleTagPreview",
            "プレビュー" => "main.section.preview",
            "出力単位" => "main.label.outputScope",
            "印刷" => "main.label.print",
            "main.label.printOffsetX" => "main.label.printOffsetX",
            "main.label.printOffsetY" => "main.label.printOffsetY",
            "出力先" => "main.label.outputFolder",
            "選択" => "main.button.select",
            "開く" => "main.button.openOutputFolder",
            "Open" => "main.button.openOutputFolder",
            "PNG保存" => "main.button.savePng",
            "PDF保存" => "main.button.savePdf",
            "直接印刷" => "main.button.printDirect",
            "New" => "main.button.new",
            "Save As" => "main.button.saveAs",
            "Duplicate" => "main.button.duplicate",
            "Rename" => "main.button.rename",
            "Delete" => "main.button.delete",
            "Layout Designer" => "main.button.layoutDesigner",
            "File" => "main.menu.file",
            "Layout" => "main.menu.layout",
            "Export" => "main.menu.export",
            "View" => "main.menu.view",
            "Settings" => "main.menu.settings",
            "Help" => "main.menu.help",
            "About" => "main.menu.about",
            "Open CSV..." => "main.menu.openCsv",
            "Open Folder..." => "main.menu.openFolder",
            "Output Folder..." => "main.menu.outputFolder",
            "Exit" => "main.menu.exit",
            "Save Preset" => "main.menu.savePreset",
            "Save Preset As" => "main.menu.savePresetAs",
            "New Preset" => "main.menu.newPreset",
            "Save PNG" => "main.menu.savePng",
            "Save PDF" => "main.menu.savePdf",
            "Print" => "main.menu.print",
            "Page Preview" => "main.menu.pagePreview",
            "Single Tag Preview" => "main.menu.singleTagPreview",
            "Previous Page" => "main.menu.previousPage",
            "Next Page" => "main.menu.nextPage",
            "Startup CSV..." => "main.menu.startupCsv",
            "Open Startup CSV Location" => "main.menu.openStartupCsvLocation",
            "起動時CSVの場所を開く" => "main.menu.openStartupCsvLocation",
            "Clear Startup CSV" => "main.menu.clearStartupCsv",
            "Startup Folder..." => "main.menu.startupFolder",
            "Open Startup Folder" => "main.menu.openStartupFolder",
            "起動時フォルダーを開く" => "main.menu.openStartupFolder",
            "Clear Startup Folder" => "main.menu.clearStartupFolder",
            "Japanese" => "common.language.japanese",
            "English" => "common.language.english",
            _ => textOrKey
        };
    }

}
