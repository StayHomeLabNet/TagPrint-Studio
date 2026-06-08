using System.ComponentModel;
using Microsoft.VisualBasic.FileIO;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class TagFinderDialog : Form
{
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color HeaderBackColor = Color.FromArgb(248, 250, 252);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color TextColor = Color.FromArgb(31, 41, 55);

    private readonly List<FieldDefinition> fields;
    private readonly List<CsvFileItem> csvFiles;
    private readonly LayoutFile layout;
    private readonly PresetFile previewPreset;
    private readonly IPreviewRenderService previewRenderer = new PreviewRenderService(new UnitConversionService());
    private readonly ITagFinderService tagFinderService = new TagFinderService();
    private readonly ITagFinderCsvService tagFinderCsvService = new TagFinderCsvService();
    private readonly bool hasHeaderRow;
    private readonly AppSettings settings;
    private readonly Action saveSettings;
    private readonly Action<string> queueCreated;
    private readonly BindingList<TagFinderResultRow> resultRows = [];
    private bool targetHeaderChecked;
    private TagFinderResultRow? queueTargetFromDoubleClick;
    private bool showingCheckedOnlyRows;
    private bool closeCleanupChecked;

    private ComboBox searchFieldCombo = null!;
    private TextBox searchTextBox = null!;
    private TextBox flagColumnBox = null!;
    private TextBox orderColumnBox = null!;
    private NumericUpDown printQuantityBox = null!;
    private CheckBox autoCheckAfterSearchCheck = null!;
    private DataGridView resultsGrid = null!;
    private TableLayoutPanel contentPanel = null!;
    private TableLayoutPanel previewPanel = null!;
    private CheckBox tagPreviewVisibleCheck = null!;
    private PictureBox tagPreviewBox = null!;
    private Label tagPreviewLabel = null!;
    private Label statusLabel = null!;

    public bool CsvSaved { get; private set; }
    public string QueueCsvPath { get; private set; } = "";
    public List<CsvPreviewRowContext>? LayoutDesignerPreviewRows { get; private set; }

    public TagFinderDialog(
        IEnumerable<FieldDefinition> fields,
        IEnumerable<CsvFileItem> csvFiles,
        LayoutFile layout,
        bool hasHeaderRow,
        AppSettings settings,
        Action saveSettings,
        Action<string> queueCreated)
    {
        this.fields = fields
            .Where(field => field.Enabled && field.ColumnIndex is >= 0)
            .OrderBy(field => field.DisplayOrder)
            .ToList();
        this.csvFiles = csvFiles.Where(file => file.Status == CsvFileStatus.Ok).ToList();
        this.layout = layout;
        previewPreset = new PresetFile { Fields = this.fields.ToList() };
        this.hasHeaderRow = hasHeaderRow;
        this.settings = settings;
        this.saveSettings = saveSettings;
        this.queueCreated = queueCreated;

        Text = L("タグファインダー", "Tag Finder");
        Width = 1180;
        Height = 760;
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(244, 246, 248);
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        AppIcon.Apply(this);

        BuildUi();
        RestoreSettings();
        Search();
        Shown += (_, _) => BeginInvoke(new Action(FocusSearchValue));
        FormClosing += TagFinderDialog_FormClosing;
        FormClosed += (_, _) => SetTagPreviewImage(null);
    }

    private void FocusSearchValue()
    {
        ActiveControl = searchTextBox;
        searchTextBox.Focus();
        searchTextBox.SelectAll();
    }

    private void RestoreSettings()
    {
        searchFieldCombo.Items.Clear();
        foreach (var field in fields)
        {
            searchFieldCombo.Items.Add(new FieldComboItem(field));
        }

        var selected = searchFieldCombo.Items
            .Cast<FieldComboItem>()
            .FirstOrDefault(item => item.Field.FieldId == settings.LastTagFinderSearchFieldId)
            ?? searchFieldCombo.Items.Cast<FieldComboItem>().FirstOrDefault(item => item.Field.DisplayName.Contains("Item Code", StringComparison.OrdinalIgnoreCase))
            ?? searchFieldCombo.Items.Cast<FieldComboItem>().FirstOrDefault();

        if (selected is not null)
        {
            searchFieldCombo.SelectedItem = selected;
        }

        flagColumnBox.Text = string.IsNullOrWhiteSpace(settings.LastTagFinderFlagColumnName)
            ? "tagPrint"
            : settings.LastTagFinderFlagColumnName;
        orderColumnBox.Text = string.IsNullOrWhiteSpace(settings.LastTagFinderOrderColumnName)
            ? $"{FlagColumnName()}Order"
            : settings.LastTagFinderOrderColumnName;
        autoCheckAfterSearchCheck.Checked = settings.LastTagFinderAutoCheckAfterSearch;
        tagPreviewVisibleCheck.Checked = settings.LastTagFinderPreviewVisible;
        ApplyTagPreviewVisibility();
        printQuantityBox.Value = 1;
    }

    private void Search()
    {
        CommitGridEdit();
        SaveFinderSettings();
        queueTargetFromDoubleClick = null;
        showingCheckedOnlyRows = false;
        resultRows.Clear();

        if (searchFieldCombo.SelectedItem is not FieldComboItem selected || selected.Field.ColumnIndex is not int columnIndex)
        {
            statusLabel.Text = L("検索項目を選択してください。", "Select a search field.");
            UpdateTagPreview();
            return;
        }

        var query = searchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            statusLabel.Text = L("検索値を入力してください。", "Enter a search value.");
            UpdateTagPreview();
            return;
        }

        foreach (var match in tagFinderService.Search(csvFiles, columnIndex, query, FlagColumnName(), autoCheckAfterSearchCheck.Checked))
        {
            resultRows.Add(new TagFinderResultRow(match.File, match.RowIndex, match.Checked));
        }

        statusLabel.Text = resultRows.Count == 0
            ? L("一致する行はありません。", "No matching rows.")
            : L($"{resultRows.Count} 件見つかりました。", $"Found {resultRows.Count} row(s).");

        if (autoCheckAfterSearchCheck.Checked && resultRows.Count > 0)
        {
            if ((int)printQuantityBox.Value > 1)
            {
                statusLabel.Text = L(
                    $"{resultRows.Count} 件見つかりました。対象にチェックしました。キュー作成を押してください。",
                    $"Found {resultRows.Count} row(s). Checked targets. Press Create Queue.");
            }
            else
            {
                SaveAndNext();
            }
        }

        UpdateTargetHeaderCheckState();
        RefreshTargetCheckColumn();
        SelectFirstResult();
        UpdateTagPreview();
    }

    private void ShowCheckedOnly()
    {
        CommitGridEdit();
        SaveFinderSettings();
        queueTargetFromDoubleClick = null;
        showingCheckedOnlyRows = true;
        resultRows.Clear();

        foreach (var match in tagFinderService.CheckedRows(csvFiles, FlagColumnName()))
        {
            resultRows.Add(new TagFinderResultRow(match.File, match.RowIndex, match.Checked));
        }

        SortResultRowsByOrderColumn();

        statusLabel.Text = resultRows.Count == 0
            ? L("保存列が true の行はありません。", "No rows with true in the flag column.")
            : L($"保存列が true の行を {resultRows.Count} 件表示しています。", $"Showing {resultRows.Count} row(s) with true in the flag column.");

        UpdateTargetHeaderCheckState();
        RefreshTargetCheckColumn();
        SelectFirstResult();
        UpdateTagPreview();
    }

    private void SelectFirstResult()
    {
        if (resultsGrid.Rows.Count == 0) return;
        resultsGrid.ClearSelection();
        resultsGrid.Rows[0].Selected = true;
        resultsGrid.CurrentCell = resultsGrid.Rows[0].Cells[0];
    }

    private void UpdateTagPreview()
    {
        if (tagPreviewBox is null || tagPreviewLabel is null) return;
        if (!tagPreviewVisibleCheck.Checked)
        {
            SetTagPreviewImage(null);
            return;
        }

        if (CurrentResultRow() is not { } result)
        {
            SetTagPreviewImage(null);
            tagPreviewLabel.Text = L("タグプレビュー", "Tag Preview");
            return;
        }

        try
        {
            var data = OutputService.MapRow(previewPreset, result.File.Rows[result.RowIndex]);
            var image = previewRenderer.RenderSingleTag(layout, previewPreset, data, TagPreviewScale());
            SetTagPreviewImage(image);
            tagPreviewLabel.Text = $"{L("タグプレビュー", "Tag Preview")} - {result.FileName} {L("行", "Row")} {result.DisplayRowNumber}";
        }
        catch (Exception ex)
        {
            SetTagPreviewImage(null);
            tagPreviewLabel.Text = ex.Message;
        }
    }

    private TagFinderResultRow? CurrentResultRow()
    {
        if (resultsGrid.CurrentRow?.DataBoundItem is TagFinderResultRow current) return current;
        return resultsGrid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.DataBoundItem as TagFinderResultRow;
    }

    private void OpenCurrentPreviewInLayoutDesigner()
    {
        if (CurrentResultRow() is not { } result)
        {
            statusLabel.Text = L("レイアウトデザイナーで開く行を選択してください。", "Select a row to open in Layout Designer.");
            return;
        }
        if (CsvFileUtility.IsPrintQueueCsv(result.File.FilePath))
        {
            MessageBox.Show(
                L("キューCSVのタグはレイアウトデザイナーで編集できません。親CSVのタグを選択してください。", "Queue CSV tags cannot be edited in Layout Designer. Select the tag from the parent CSV."),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            LayoutDesignerPreviewRows =
            [
                new CsvPreviewRowContext
                {
                    FilePath = result.File.FilePath,
                    FileName = result.File.FileName,
                    EncodingName = result.File.EncodingName,
                    HasHeaderRow = hasHeaderRow,
                    HeaderRow = hasHeaderRow ? result.File.HeaderRow : null,
                    AllRows = result.File.Rows,
                    RowIndex = result.RowIndex,
                    Values = result.File.Rows[result.RowIndex],
                    Data = OutputService.MapRow(previewPreset, result.File.Rows[result.RowIndex])
                }
            ];
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private float TagPreviewScale()
    {
        if (layout.Tag.Width <= 0 || layout.Tag.Height <= 0) return 4f;
        var availableWidth = Math.Max(80, tagPreviewBox.ClientSize.Width - 16);
        var availableHeight = Math.Max(80, tagPreviewBox.ClientSize.Height - 16);
        return Math.Clamp(Math.Min(
            availableWidth / (float)layout.Tag.Width,
            availableHeight / (float)layout.Tag.Height), 4f, 12f);
    }

    private void SetTagPreviewImage(Image? image)
    {
        var old = tagPreviewBox.Image;
        tagPreviewBox.Image = image;
        old?.Dispose();
    }

    private void ApplyTagPreviewVisibility()
    {
        if (contentPanel is null || previewPanel is null || tagPreviewVisibleCheck is null) return;

        var visible = tagPreviewVisibleCheck.Checked;
        previewPanel.Visible = visible;
        contentPanel.ColumnStyles[1].Width = visible ? 260 : 0;
        if (!visible)
        {
            SetTagPreviewImage(null);
        }
        else
        {
            UpdateTagPreview();
        }
    }
    private void ResultsGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || resultsGrid.Columns[e.ColumnIndex].Name != "TargetCheckColumn")
        {
            return;
        }

        CommitGridEdit();
        SetAllResultChecks(!targetHeaderChecked);
    }

    private void ResultsGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (resultsGrid.IsCurrentCellDirty &&
            resultsGrid.CurrentCell is not null &&
            resultsGrid.Columns[resultsGrid.CurrentCell.ColumnIndex].Name == "TargetCheckColumn")
        {
            resultsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void ResultsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            resultsGrid.Columns[e.ColumnIndex].Name != "TargetCheckColumn")
        {
            return;
        }

        UpdateTargetHeaderCheckState();
        resultsGrid.InvalidateColumn(e.ColumnIndex);
    }

    private void ResultsGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= resultRows.Count) return;
        if (searchFieldCombo.SelectedItem is not FieldComboItem selected || selected.Field.ColumnIndex is not int columnIndex) return;

        var result = resultRows[e.RowIndex];
        searchTextBox.Text = ValueAt(result.File.Rows[result.RowIndex], columnIndex);
        queueTargetFromDoubleClick = result;
        searchTextBox.Focus();
        searchTextBox.SelectAll();
    }

    private void SetAllResultChecks(bool isChecked)
    {
        CommitGridEdit();
        targetHeaderChecked = isChecked;

        foreach (var row in resultRows)
        {
            row.Checked = isChecked;
        }

        UpdateTargetHeaderCheckState();
        RefreshTargetCheckColumn();
    }

    private void UpdateTargetHeaderCheckState()
    {
        targetHeaderChecked = resultRows.Count > 0 && resultRows.All(row => row.Checked);

        var targetColumn = resultsGrid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.Name == "TargetCheckColumn");

        if (targetColumn is not null)
        {
            targetColumn.HeaderText = TargetHeaderText(targetHeaderChecked);
        }
    }

    private void RefreshTargetCheckColumn()
    {
        resultRows.ResetBindings();

        var targetColumn = resultsGrid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => string.Equals(column.Name, "TargetCheckColumn", StringComparison.Ordinal));

        if (targetColumn is not null)
        {
            resultsGrid.InvalidateColumn(targetColumn.Index);
        }

        resultsGrid.EndEdit();
        resultsGrid.Invalidate();
        resultsGrid.Refresh();
    }

    private static string TargetHeaderText(bool isChecked) =>
        isChecked ? L("☑対象", "☑ Flag") : L("☐対象", "☐ Flag");

    private void ResultsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= resultRows.Count) return;
        var column = resultsGrid.Columns[e.ColumnIndex];
        if (!column.Name.StartsWith("Field_", StringComparison.Ordinal)) return;

        var fieldId = column.Name["Field_".Length..];
        var field = fields.FirstOrDefault(item => item.FieldId == fieldId);
        if (field?.ColumnIndex is not int index) return;

        e.Value = ValueAt(resultRows[e.RowIndex].File.Rows[resultRows[e.RowIndex].RowIndex], index);
        e.FormattingApplied = true;
    }

    private void CommitGridEdit()
    {
        if (resultsGrid.IsCurrentCellDirty)
        {
            resultsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        resultsGrid.EndEdit();
    }

    private static string ValueAt(string[] row, int index) => index >= 0 && index < row.Length ? row[index] : "";

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);

    private string OrderColumnName()
    {
        if (!string.IsNullOrWhiteSpace(orderColumnBox.Text)) return orderColumnBox.Text.Trim();
        return $"{FlagColumnName()}Order";
    }

    private void SortResultRowsByOrderColumn()
    {
        var orderColumnName = OrderColumnName();
        var sorted = resultRows
            .OrderBy(row => TryOrderValue(row, orderColumnName, out var value) ? 0 : 1)
            .ThenBy(row => TryOrderValue(row, orderColumnName, out var value) ? value : int.MaxValue)
            .ThenBy(row => row.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.DisplayRowNumber)
            .ToList();

        resultRows.Clear();
        foreach (var row in sorted)
        {
            resultRows.Add(row);
        }
    }

    private static bool TryOrderValue(TagFinderResultRow row, string orderColumnName, out int value)
    {
        value = 0;
        var index = FindHeaderIndex(row.File, orderColumnName);
        return index >= 0
            && index < row.File.Rows[row.RowIndex].Length
            && int.TryParse(row.File.Rows[row.RowIndex][index], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private sealed class FieldComboItem(FieldDefinition field)
    {
        public FieldDefinition Field { get; } = field;
        public override string ToString() => string.IsNullOrWhiteSpace(Field.DisplayName) ? Field.FieldId : Field.DisplayName;
    }

    private sealed class TagFinderResultRow(CsvFileItem file, int rowIndex, bool isChecked)
    {
        public CsvFileItem File { get; } = file;
        public int RowIndex { get; } = rowIndex;
        public bool Checked { get; set; } = isChecked;
        public string FileName => File.FileName;
        public int DisplayRowNumber => RowIndex + 1;
    }
}
