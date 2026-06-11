using System.ComponentModel;
using System.Globalization;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed partial class DuplicateCleanupDialog : Form
{
    private Control BuildDuplicateTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        keyFieldCombo = Combo();
        compareFieldCombo = Combo();
        valueTypeCombo = Combo();
        ruleCombo = Combo();
        carryModeCombo = Combo();
        ignoreEmptyKeysCheck = TopCheckBox(L("空キーを無視", "Ignore empty keys"), isChecked: true, width: 130);
        carryValuesCheck = TopCheckBox(L("元の値を引き継ぐ", "Carry values"), width: 140);
        carryValuesCheck.CheckedChanged += (_, _) => UpdateCarryControls();
        carryValuesCheck.Checked = settings.DuplicateCarryValuesEnabled;
        carryValuesCheck.CheckedChanged += (_, _) => settings.DuplicateCarryValuesEnabled = carryValuesCheck.Checked;
        selectCarryFieldsButton = Button(L("列を選択", "Select Fields"), (_, _) => SelectCarryFields());
        carryFieldsSummaryLabel = new Label
        {
            Width = 360,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(4, 2, 0, 2)
        };
        var detectButton = Button(L("検出", "Detect"), async (_, _) => await DetectDuplicatesAsync(), primary: true);

        keyFieldCombo.Items.AddRange(fields.Select(FieldItemFromField).Cast<object>().ToArray());
        compareFieldCombo.Items.AddRange(fields.Select(FieldItemFromField).Cast<object>().ToArray());
        valueTypeCombo.Items.AddRange(ValueTypeItems().Cast<object>().ToArray());
        carryModeCombo.Items.AddRange(CarryModeItems().Cast<object>().ToArray());
        valueTypeCombo.SelectedIndexChanged += (_, _) => RefreshRuleItems();

        SelectFieldById(keyFieldCombo, settings.LastDuplicateKeyFieldId, fallbackIndex: 0);
        SelectFieldById(compareFieldCombo, settings.LastDuplicateCompareFieldId, fallbackIndex: compareFieldCombo.Items.Count - 1);

        SelectOption(valueTypeCombo, settings.LastDuplicateValueType);
        RefreshRuleItems();

        SelectOption(ruleCombo, settings.LastDuplicateRule);
        SelectOption(carryModeCombo, settings.LastDuplicateCarryCopyMode);

        ignoreEmptyKeysCheck.Checked = settings.LastDuplicateIgnoreEmptyKeys;
        UpdateCarryControls();

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.White, Padding = new Padding(10) };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var duplicateConditionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        duplicateConditionRow.Controls.Add(FieldGroup(L("キー列", "Key Column"), keyFieldCombo, 270));
        duplicateConditionRow.Controls.Add(FieldGroup(L("判定列", "Compare Column"), compareFieldCombo, 280));
        duplicateConditionRow.Controls.Add(FieldGroup(L("判定型", "Value Type"), valueTypeCombo, 180));
        duplicateConditionRow.Controls.Add(FieldGroup(L("ルール", "Rule"), ruleCombo, 300));
        duplicateConditionRow.Controls.Add(ignoreEmptyKeysCheck);
        duplicateConditionRow.Controls.Add(detectButton);
        duplicateConditionRow.Controls[2].Width -= 50;
        duplicateConditionRow.Controls[3].Width -= 50;
        top.Controls.Add(duplicateConditionRow, 0, 0);

        var carryConditionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        carryConditionRow.Controls.Add(FieldGroup(L("引き継ぎ", "Carry"), carryValuesCheck, 250));
        carryConditionRow.Controls.Add(FieldGroup(L("対象列", "Fields"), selectCarryFieldsButton, 200));
        carryConditionRow.Controls.Add(FieldGroup(L("コピー条件", "Copy Mode"), carryModeCombo, 310));
        carryConditionRow.Controls.Add(carryFieldsSummaryLabel);
        SetFieldGroupLabelWidth(carryConditionRow.Controls[0], 76);
        SetFieldGroupLabelWidth(carryConditionRow.Controls[2], 76);
        top.Controls.Add(carryConditionRow, 0, 1);
        root.Controls.Add(top, 0, 0);

        duplicateGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = duplicateRows,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToResizeRows = false
        };
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.DisplayKeyValue), L("キー", "Key"), 180));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.FileName), "CSV", 180));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.DisplayRowNumber), L("行", "Row"), 60));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.CompareValue), L("判定値", "Compare"), 170));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.CarrySummary), L("引き継ぎ", "Carry"), 150));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.ActionText), L("操作", "Action"), 90));
        duplicateGrid.Columns.Add(TextColumn(nameof(DuplicateResolutionItem.Reason), L("理由", "Reason"), 250));
        duplicateGrid.ColumnHeaderMouseClick += DuplicateGrid_ColumnHeaderMouseClick;
        duplicateGrid.DataBindingComplete += (_, _) => ApplyDuplicateRowStyles();
        root.Controls.Add(duplicateGrid, 0, 1);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.White, Padding = new Padding(10) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        duplicateSummaryLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 85, 99) };
        UpdateDuplicateSummary(null);
        var applyButton = Button(L("適用", "Apply"), async (_, _) => await ApplyDuplicateCleanupAsync(), primary: true);
        var closeButton = Button(LocalizationService.T("common.close"), (_, _) => CloseDialog());
        bottom.Controls.Add(duplicateSummaryLabel, 0, 0);
        bottom.Controls.Add(applyButton, 1, 0);
        bottom.Controls.Add(closeButton, 2, 0);
        root.Controls.Add(bottom, 0, 2);

        AcceptButton = detectButton;
        CancelButton = closeButton;
        return root;
    }

    private async Task DetectDuplicatesAsync()
    {
        if (!TryBuildDuplicateOptions(out var options))
        {
            return;
        }

        try
        {
            DuplicateCleanupResult? result = null;
            await RunBusyAsync(async () => result = await Task.Run(() => duplicateService.Analyze(preset, csvFiles, options)));
            if (result is null)
            {
                return;
            }

            lastDuplicateResult = result;
            duplicateSortPropertyName = null;
            duplicateSortAscending = true;
            RebindDuplicateRows(lastDuplicateResult.Items);
            UpdateDuplicateSummary(lastDuplicateResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ApplyDuplicateCleanupAsync()
    {
        if (lastDuplicateResult is null || lastDuplicateResult.DeleteCount == 0)
        {
            MessageBox.Show(
                L("削除対象がありません。先に検出を実行してください。", "No delete candidates. Run detection first."),
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var message = string.Format(
            L("{0}件のCSVを更新し、{1}行を削除します。", "Update {0} CSV file(s) and delete {1} row(s)."),
            lastDuplicateResult.TargetFileCount,
            lastDuplicateResult.DeleteCount);
        if (lastDuplicateResult.CarryCellCount > 0)
        {
            message += Environment.NewLine + string.Format(
                L("あわせて {0} セルを残す側へコピーします。", "Also copy {0} cell(s) into kept rows."),
                lastDuplicateResult.CarryCellCount);
        }

        message += Environment.NewLine + Environment.NewLine + L("続行しますか？", "Continue?");
        if (MessageBox.Show(message, LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await RunBusyAsync(() => Task.Run(() => duplicateService.Apply(csvFiles, lastDuplicateResult, hasHeaderRow)));
            reloadCsvPaths.Clear();
            reloadCsvPaths.AddRange(csvFiles.Select(file => file.FilePath));
            Applied = true;

            if (TryBuildDuplicateOptions(out var options))
            {
                DuplicateCleanupResult? result = null;
                await RunBusyAsync(async () => result = await Task.Run(() => duplicateService.Analyze(preset, csvFiles, options)));
                if (result is not null)
                {
                    lastDuplicateResult = result;
                }
                RebindDuplicateRows(lastDuplicateResult.Items);
                UpdateDuplicateSummary(lastDuplicateResult);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(CsvFileUtility.BuildUserFriendlySaveErrorMessage(ex), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectCarryFields()
    {
        var keyFieldId = (keyFieldCombo.SelectedItem as FieldItem)?.FieldId ?? "";
        var selectableFields = fields.Where(field => !string.Equals(field.FieldId, keyFieldId, StringComparison.Ordinal)).ToList();
        using var dialog = new MultiFieldSelectionDialog(selectableFields, selectedCarryFieldIds, L("引き継ぎ列", "Carry Fields"));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        selectedCarryFieldIds.Clear();
        selectedCarryFieldIds.AddRange(dialog.SelectedFieldIds);
        settings.DuplicateCarryFieldIds = selectedCarryFieldIds.ToList();
        UpdateCarryControls();
    }

    private void UpdateCarryControls()
    {
        var enabled = carryValuesCheck?.Checked == true;
        if (selectCarryFieldsButton is not null) selectCarryFieldsButton.Enabled = enabled;
        if (carryModeCombo is not null) carryModeCombo.Enabled = enabled;
        if (carryFieldsSummaryLabel is null)
        {
            return;
        }

        carryFieldsSummaryLabel.Text = enabled ? BuildCarryFieldSummary() : L("無効", "Disabled");
    }

    private string BuildCarryFieldSummary()
    {
        if (selectedCarryFieldIds.Count == 0)
        {
            return L("未選択", "None selected");
        }

        var selectedNames = fields
            .Where(field => selectedCarryFieldIds.Contains(field.FieldId))
            .Select(field => string.IsNullOrWhiteSpace(field.DisplayName) ? field.FieldId : field.DisplayName)
            .ToList();
        return string.Join(", ", selectedNames);
    }

    private void UpdateDuplicateSummary(DuplicateCleanupResult? result)
    {
        duplicateSummaryLabel.Text = result is null
            ? L("重複検出を実行してください。", "Run duplicate detection.")
            : string.Format(
                L("重複グループ: {0}  削除候補: {1}  コピー予定セル: {2}  更新CSV: {3}",
                    "Groups: {0}  Delete: {1}  Carry Cells: {2}  Files: {3}"),
                result.GroupCount,
                result.DeleteCount,
                result.CarryCellCount,
                result.TargetFileCount);
    }

    private void ApplyDuplicateRowStyles()
    {
        foreach (DataGridViewRow gridRow in duplicateGrid.Rows)
        {
            if (gridRow.DataBoundItem is not DuplicateResolutionItem item)
            {
                continue;
            }

            var baseBackColor = item.GroupIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);
            var actionColor = item.Keep ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28);
            gridRow.DefaultCellStyle.BackColor = baseBackColor;
            gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55);
            gridRow.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            gridRow.DefaultCellStyle.SelectionForeColor = Color.FromArgb(17, 24, 39);
            gridRow.DefaultCellStyle.Font = new Font(Font, item.Keep ? FontStyle.Bold : FontStyle.Regular);
            gridRow.Cells[nameof(DuplicateResolutionItem.ActionText)].Style.ForeColor = actionColor;
            gridRow.Cells[nameof(DuplicateResolutionItem.Reason)].Style.ForeColor = Color.FromArgb(75, 85, 99);
            gridRow.Cells[nameof(DuplicateResolutionItem.CarrySummary)].Style.ForeColor = Color.FromArgb(37, 99, 235);
            gridRow.DefaultCellStyle.Padding = item.IsFirstInGroup ? new Padding(0, 8, 0, 0) : Padding.Empty;
        }
    }

    private void DuplicateGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || lastDuplicateResult is null)
        {
            return;
        }

        var column = duplicateGrid.Columns[e.ColumnIndex];
        var propertyName = column.DataPropertyName;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        if (string.Equals(duplicateSortPropertyName, propertyName, StringComparison.Ordinal))
        {
            duplicateSortAscending = !duplicateSortAscending;
        }
        else
        {
            duplicateSortPropertyName = propertyName;
            duplicateSortAscending = true;
        }

        RebindDuplicateRows(SortDuplicateItems(lastDuplicateResult.Items, propertyName, duplicateSortAscending));
        column.HeaderCell.SortGlyphDirection = duplicateSortAscending ? SortOrder.Ascending : SortOrder.Descending;
    }

    private void RebindDuplicateRows(IEnumerable<DuplicateResolutionItem> items)
    {
        duplicateRows.Clear();
        foreach (var item in items)
        {
            duplicateRows.Add(item);
        }
    }

    private static List<DuplicateResolutionItem> SortDuplicateItems(IEnumerable<DuplicateResolutionItem> items, string propertyName, bool ascending)
    {
        var list = items.ToList();
        var comparer = StringComparer.CurrentCultureIgnoreCase;

        IOrderedEnumerable<DuplicateResolutionItem> ordered = propertyName switch
        {
            nameof(DuplicateResolutionItem.DisplayKeyValue) or nameof(DuplicateResolutionItem.KeyValue) =>
                OrderBy(list, item => item.KeyValue, ascending, comparer),
            nameof(DuplicateResolutionItem.FileName) =>
                OrderBy(list, item => item.FileName, ascending, comparer),
            nameof(DuplicateResolutionItem.DisplayRowNumber) =>
                ascending ? list.OrderBy(item => item.DisplayRowNumber) : list.OrderByDescending(item => item.DisplayRowNumber),
            nameof(DuplicateResolutionItem.CompareValue) =>
                OrderByCompareValue(list, ascending),
            nameof(DuplicateResolutionItem.CarrySummary) =>
                OrderBy(list, item => item.CarrySummary, ascending, comparer),
            nameof(DuplicateResolutionItem.ActionText) =>
                ascending
                    ? list.OrderBy(item => item.Keep ? 0 : 1).ThenBy(item => item.KeyValue, comparer)
                    : list.OrderByDescending(item => item.Keep ? 0 : 1).ThenBy(item => item.KeyValue, comparer),
            nameof(DuplicateResolutionItem.Reason) =>
                OrderBy(list, item => item.Reason, ascending, comparer),
            _ =>
                ascending ? list.OrderBy(item => item.GroupIndex).ThenBy(item => item.RowIndex)
                    : list.OrderByDescending(item => item.GroupIndex).ThenByDescending(item => item.RowIndex)
        };

        return ordered.ToList();
    }

    private static IOrderedEnumerable<DuplicateResolutionItem> OrderBy(
        IEnumerable<DuplicateResolutionItem> items,
        Func<DuplicateResolutionItem, string> selector,
        bool ascending,
        StringComparer comparer) =>
        ascending
            ? items.OrderBy(selector, comparer).ThenBy(item => item.RowIndex)
            : items.OrderByDescending(selector, comparer).ThenBy(item => item.RowIndex);

    private static IOrderedEnumerable<DuplicateResolutionItem> OrderByCompareValue(List<DuplicateResolutionItem> items, bool ascending)
    {
        if (TryGetDateSort(items, out var dateValues))
        {
            return ascending
                ? items.OrderBy(item => dateValues[item]).ThenBy(item => item.RowIndex)
                : items.OrderByDescending(item => dateValues[item]).ThenBy(item => item.RowIndex);
        }

        if (TryGetDecimalSort(items, out var decimalValues))
        {
            return ascending
                ? items.OrderBy(item => decimalValues[item]).ThenBy(item => item.RowIndex)
                : items.OrderByDescending(item => decimalValues[item]).ThenBy(item => item.RowIndex);
        }

        return ascending
            ? items.OrderBy(item => item.CompareValue, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.RowIndex)
            : items.OrderByDescending(item => item.CompareValue, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.RowIndex);
    }

    private static bool TryGetDateSort(IEnumerable<DuplicateResolutionItem> items, out Dictionary<DuplicateResolutionItem, DateTime> values)
    {
        values = [];
        foreach (var item in items)
        {
            if (!TryParseDate(item.CompareValue, out var parsed))
            {
                values.Clear();
                return false;
            }

            values[item] = parsed;
        }

        return values.Count > 0;
    }

    private static bool TryGetDecimalSort(IEnumerable<DuplicateResolutionItem> items, out Dictionary<DuplicateResolutionItem, decimal> values)
    {
        values = [];
        foreach (var item in items)
        {
            if (!decimal.TryParse(item.CompareValue, NumberStyles.Number | NumberStyles.Currency, CultureInfo.CurrentCulture, out var current) &&
                !decimal.TryParse(item.CompareValue, NumberStyles.Number | NumberStyles.Currency, CultureInfo.InvariantCulture, out current))
            {
                values.Clear();
                return false;
            }

            values[item] = current;
        }

        return values.Count > 0;
    }

    private static bool TryParseDate(string value, out DateTime parsed) =>
        DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed) ||
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);

    private static FieldItem FieldItemFromField(FieldDefinition field) => new()
    {
        FieldId = field.FieldId,
        DisplayName = string.IsNullOrWhiteSpace(field.DisplayName)
            ? field.FieldId
            : field.DisplayName
    };

    private List<OptionItem<DuplicateValueType>> ValueTypeItems() =>
    [
        new() { Value = DuplicateValueType.Date, DisplayName = L("日付", "Date") },
        new() { Value = DuplicateValueType.Number, DisplayName = L("数値", "Number") },
        new() { Value = DuplicateValueType.Text, DisplayName = L("文字列", "Text") }
    ];

    private List<OptionItem<DuplicateCopyMode>> CarryModeItems() =>
    [
        new() { Value = DuplicateCopyMode.FillIfEmpty, DisplayName = L("残す側が空ならコピー", "Copy if keep row is empty") },
        new() { Value = DuplicateCopyMode.Overwrite, DisplayName = L("上書きする", "Overwrite keep row") }
    ];

    private void RefreshRuleItems()
    {
        var previousRule = ruleCombo.SelectedItem is OptionItem<DuplicateResolutionRule> selectedRule
            ? selectedRule.Value
            : DuplicateResolutionRule.KeepFirst;

        var allowedRules = GetAllowedRuleItems(GetSelectedValueType());
        ruleCombo.BeginUpdate();
        ruleCombo.Items.Clear();
        ruleCombo.Items.AddRange(allowedRules.Cast<object>().ToArray());

        var selectedIndex = allowedRules.FindIndex(item => item.Value.Equals(previousRule));
        if (selectedIndex < 0)
        {
            selectedIndex = allowedRules.FindIndex(item => item.Value.Equals(DuplicateResolutionRule.KeepFirst));
        }

        ruleCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        ruleCombo.EndUpdate();
    }

    private DuplicateValueType GetSelectedValueType() =>
        valueTypeCombo.SelectedItem is OptionItem<DuplicateValueType> valueType ? valueType.Value : DuplicateValueType.Text;

    private bool TryBuildDuplicateOptions(out DuplicateCleanupOptions options)
    {
        options = new DuplicateCleanupOptions();
        if (keyFieldCombo.SelectedItem is not FieldItem keyField || compareFieldCombo.SelectedItem is not FieldItem compareField)
        {
            return false;
        }

        options = new DuplicateCleanupOptions
        {
            KeyFieldId = keyField.FieldId,
            CompareFieldId = compareField.FieldId,
            ValueType = valueTypeCombo.SelectedItem is OptionItem<DuplicateValueType> valueType ? valueType.Value : DuplicateValueType.Text,
            Rule = ruleCombo.SelectedItem is OptionItem<DuplicateResolutionRule> rule ? rule.Value : DuplicateResolutionRule.KeepFirst,
            IgnoreEmptyKeys = ignoreEmptyKeysCheck.Checked,
            Carry = new DuplicateCarryOptions
            {
                Enabled = carryValuesCheck.Checked,
                FieldIds = selectedCarryFieldIds.ToList(),
                CopyMode = carryModeCombo.SelectedItem is OptionItem<DuplicateCopyMode> carryMode ? carryMode.Value : DuplicateCopyMode.FillIfEmpty
            }
        };
        return true;
    }

    private List<OptionItem<DuplicateResolutionRule>> GetAllowedRuleItems(DuplicateValueType valueType) => valueType switch
    {
        DuplicateValueType.Date =>
        [
            new() { Value = DuplicateResolutionRule.KeepLargest, DisplayName = L("新しい日付を残す", "Keep Newer Date") },
            new() { Value = DuplicateResolutionRule.KeepSmallest, DisplayName = L("古い日付を残す", "Keep Older Date") },
            new() { Value = DuplicateResolutionRule.KeepFirst, DisplayName = L("先に見つかった行を残す", "Keep First Found Row") }
        ],
        DuplicateValueType.Number =>
        [
            new() { Value = DuplicateResolutionRule.KeepLargest, DisplayName = L("大きい値を残す", "Keep Larger Value") },
            new() { Value = DuplicateResolutionRule.KeepSmallest, DisplayName = L("小さい値を残す", "Keep Smaller Value") },
            new() { Value = DuplicateResolutionRule.KeepFirst, DisplayName = L("先に見つかった行を残す", "Keep First Found Row") }
        ],
        _ =>
        [
            new() { Value = DuplicateResolutionRule.KeepNonEmpty, DisplayName = L("空でない値を残す", "Keep Non-Empty") },
            new() { Value = DuplicateResolutionRule.KeepFirst, DisplayName = L("先に見つかった行を残す", "Keep First Found Row") }
        ]
    };

    private void SaveDuplicateSelectionSettings()
    {
        if (keyFieldCombo.SelectedItem is FieldItem keyField)
        {
            settings.LastDuplicateKeyFieldId = keyField.FieldId;
        }

        if (compareFieldCombo.SelectedItem is FieldItem compareField)
        {
            settings.LastDuplicateCompareFieldId = compareField.FieldId;
        }

        if (valueTypeCombo.SelectedItem is OptionItem<DuplicateValueType> valueType)
        {
            settings.LastDuplicateValueType = valueType.Value;
        }

        if (ruleCombo.SelectedItem is OptionItem<DuplicateResolutionRule> rule)
        {
            settings.LastDuplicateRule = rule.Value;
        }

        settings.LastDuplicateIgnoreEmptyKeys = ignoreEmptyKeysCheck.Checked;
        settings.DuplicateCarryValuesEnabled = carryValuesCheck.Checked;

        if (carryModeCombo.SelectedItem is OptionItem<DuplicateCopyMode> carryMode)
        {
            settings.LastDuplicateCarryCopyMode = carryMode.Value;
        }

        settings.DuplicateCarryFieldIds = selectedCarryFieldIds.ToList();
    }
}
