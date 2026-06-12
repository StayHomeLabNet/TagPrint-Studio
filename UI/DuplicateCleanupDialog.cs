using System.ComponentModel;
using System.Globalization;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed partial class DuplicateCleanupDialog : Form
{
    private readonly PresetFile preset;
    private readonly List<FieldDefinition> fields;
    private readonly List<CsvFileItem> csvFiles;
    private readonly bool hasHeaderRow;
    private readonly AppSettings settings;
    private readonly IDuplicateCleanupService duplicateService = new DuplicateCleanupService();
    private readonly ICsvMergeService mergeService = new CsvMergeService();
    private readonly BindingList<DuplicateResolutionItem> duplicateRows = [];
    private readonly BindingList<CsvMergeCandidate> mergeRows = [];
    private readonly List<string> reloadCsvPaths = [];
    private readonly List<string> selectedCarryFieldIds = [];

    private ComboBox keyFieldCombo = null!;
    private ComboBox compareFieldCombo = null!;
    private ComboBox valueTypeCombo = null!;
    private ComboBox ruleCombo = null!;
    private CheckBox ignoreEmptyKeysCheck = null!;
    private CheckBox carryValuesCheck = null!;
    private Button selectCarryFieldsButton = null!;
    private ComboBox carryModeCombo = null!;
    private Label carryFieldsSummaryLabel = null!;
    private DataGridView duplicateGrid = null!;
    private Label duplicateSummaryLabel = null!;

    private TextBox mergeFolderBox = null!;
    private ComboBox parentCsvCombo = null!;
    private CheckBox deleteMergedSourcesCheck = null!;
    private CheckBox deleteMergeBakCheck = null!;
    private DataGridView mergeGrid = null!;
    private Label mergeSummaryLabel = null!;

    private DuplicateCleanupResult? lastDuplicateResult;
    private CsvMergePreview? lastMergePreview;
    private string? duplicateSortPropertyName;
    private bool duplicateSortAscending = true;
    private int busyOperationCount;

    public bool Applied { get; private set; }
    public IReadOnlyList<string> ReloadCsvPaths => reloadCsvPaths;

    private sealed class FieldItem
    {
        public string FieldId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
    }

    private sealed class OptionItem<T>
    {
        public required T Value { get; init; }
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
    }

    private sealed class CsvPathItem
    {
        public string Path { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
    }

    public DuplicateCleanupDialog(PresetFile preset, List<FieldDefinition> fields, List<CsvFileItem> csvFiles, bool hasHeaderRow, AppSettings settings)
    {
        this.preset = preset;
        this.settings = settings;
        this.fields = fields
            .Where(field => field.ColumnIndex is >= 0)
            .OrderBy(field => field.DisplayOrder)
            .ToList();
        this.csvFiles = csvFiles
            .Where(file => !CsvFileUtility.IsPrintQueueCsv(file.FilePath))
            .ToList();
        this.hasHeaderRow = hasHeaderRow;
        selectedCarryFieldIds.AddRange(settings.DuplicateCarryFieldIds.Where(id => this.fields.Any(field => string.Equals(field.FieldId, id, StringComparison.Ordinal))));

        BuildUi();
        InitializeMergeTab();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveDuplicateSelectionSettings();

        if (Applied && DialogResult == DialogResult.None)
        {
            DialogResult = DialogResult.OK;
        }

        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        Text = LocalizationService.T("duplicateCleanup.csvDuplicateCleanup");
        Width = 1320;
        Height = 720;
        MinimumSize = new Size(1240, 700);
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.FromArgb(244, 246, 248);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 6) };

        var duplicateTab = new TabPage(LocalizationService.T("duplicateCleanup.duplicateCleanup")) { BackColor = BackColor };
        duplicateTab.Controls.Add(BuildDuplicateTab());
        tabs.TabPages.Add(duplicateTab);

        var mergeTab = new TabPage(LocalizationService.T("duplicateCleanup.csvMerge")) { BackColor = BackColor };
        mergeTab.Controls.Add(BuildMergeTab());
        tabs.TabPages.Add(mergeTab);

        Controls.Add(tabs);
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
        foreach (Control control in Controls)
        {
            control.Enabled = false;
        }
    }

    private void EndBusyOperation()
    {
        busyOperationCount = Math.Max(0, busyOperationCount - 1);
        if (busyOperationCount > 0)
        {
            return;
        }

        foreach (Control control in Controls)
        {
            control.Enabled = true;
        }
        UseWaitCursor = false;
        Cursor.Current = Cursors.Default;
    }
}
