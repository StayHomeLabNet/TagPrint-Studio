using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class MainForm : Form
{
    private static readonly Color PageBackColor = Color.FromArgb(244, 246, 248);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(216, 222, 230);
    private static readonly Color HeaderBackColor = Color.FromArgb(248, 250, 252);
    private static readonly Color TextColor = Color.FromArgb(31, 41, 55);
    private static readonly Color MutedTextColor = Color.FromArgb(75, 85, 99);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentHoverColor = Color.FromArgb(29, 78, 216);
    private const float MinSingleTagPreviewScale = 8f;
    private const float MaxSingleTagPreviewScale = 24f;
    private const float BasePagePreviewRenderScale = 3f;
    private const float MaxPagePreviewRenderScale = 9f;
    private const int PagePreviewZoomStepPercent = 10;
    private const int MinPagePreviewZoomPercent = -40;
    private const int MaxPagePreviewZoomPercent = 200;

    private readonly IAppStorageLocationService appStorageLocationService;
    private readonly AppPaths paths;
    private readonly BootstrapService bootstrap;
    private readonly IPresetService presetService;
    private readonly ILayoutService layoutService;
    private readonly ICsvLoaderService csvLoader = new CsvLoaderService();
    private readonly IUnitConversionService units = new UnitConversionService();
    private readonly IPreviewRenderService renderer;
    private readonly IOutputService outputService;
    private readonly IOutputOptionsFactory outputOptionsFactory;
    private readonly IPreflightCheckService preflightCheckService = new PreflightCheckService();
    private readonly IPrintTargetSummaryService printTargetSummaryService = new PrintTargetSummaryService();
    private readonly ISettingsArchiveService settingsArchiveService;
    private readonly IBackupCleanupService backupCleanupService = new BackupCleanupService();
    private readonly ICsvColumnService csvColumnService = new CsvColumnService();
    private readonly BindingList<FieldDefinition> fieldRows = [];
    private readonly BindingList<CsvFileItem> csvRows = [];

    private AppSettings settings = new();
    private PresetFile currentPreset = new();
    private LayoutFile currentLayout = new();
    private PreviewMode previewMode = PreviewMode.Page;
    private int previewPageIndex;
    private int pagePreviewZoomPercent;
    private bool previewPanning;
    private bool presetDirty;
    private bool loadingPreset;
    private bool loadingSettings;
    private bool suppressPresetSelectionChange;
    private FileComboItem? currentPresetItem;
    private PictureBox previewBox = null!;
    private Panel previewViewportHost = null!;
    private Panel previewScrollPanel = null!;
    private Panel previewCanvasHost = null!;
    private FlowLayoutPanel previewZoomTools = null!;
    private ComboBox presetCombo = null!;
    private ComboBox layoutCombo = null!;
    private DataGridView fieldsGrid = null!;
    private DataGridView csvGrid = null!;
    private ComboBox recentCsvCombo = null!;
    private CheckBox headerCheck = null!;
    private Label previewInfo = null!;
    private Button previousPageButton = null!;
    private Button nextPageButton = null!;
    private Button previewZoomInButton = null!;
    private Button previewZoomResetButton = null!;
    private Button previewZoomOutButton = null!;
    private Point previewPanStartCursor;
    private Point previewPanStartScroll;
    private Button duplicateCleanupButton = null!;
    private Button tagFinderButton = null!;
    private SplitContainer centerSplit = null!;
    private SplitContainer leftSplit = null!;
    private TextBox outputFolderBox = null!;
    private ComboBox recentOutputCombo = null!;
    private ComboBox outputScopeCombo = null!;
    private ComboBox outputDpiCombo = null!;
    private ComboBox printScalingCombo = null!;
    private ComboBox printSortCombo = null!;
    private ComboBox printSortFieldCombo = null!;
    private ComboBox printSortValueTypeCombo = null!;
    private NumericUpDown printOffsetXBox = null!;
    private NumericUpDown printOffsetYBox = null!;
    private MenuStrip menuStrip = null!;
    private ToolStripMenuItem japaneseLanguageMenuItem = null!;
    private ToolStripMenuItem englishLanguageMenuItem = null!;
    private ToolStripMenuItem settingsBackupMenuItem = null!;
    private ToolStripMenuItem settingsImportMenuItem = null!;
    private ToolStripMenuItem backupCleanupMenuItem = null!;
    private ToolStripMenuItem backupCleanupStartupMenuItem = null!;
    private ToolStripMenuItem backupCleanupStartupDisabledMenuItem = null!;
    private ToolStripMenuItem backupCleanupStartupAskMenuItem = null!;
    private ToolStripMenuItem backupCleanupStartupAutoMenuItem = null!;
    private ToolStripMenuItem confirmPrintTargetsMenuItem = null!;
    private ToolStripMenuItem largePrintWarningSettingsMenuItem = null!;
    private ToolStripMenuItem appDataLocationMenuItem = null!;
    private ToolStripMenuItem openAppDataLocationMenuItem = null!;
    private ToolStripMenuItem resetAppDataLocationMenuItem = null!;
    private ToolStripMenuItem openSettingsLocationMenuItem = null!;
    private ToolStripMenuItem openStartupCsvLocationMenuItem = null!;
    private ToolStripMenuItem openStartupFolderMenuItem = null!;
    private int busyOperationCount;
    private bool startupBackupCleanupChecked;

    private sealed class FileComboItem
    {
        public string Path { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
        public static FileComboItem FromPath(string path) => new()
        {
            Path = path,
            DisplayName = System.IO.Path.GetFileNameWithoutExtension(path)
        };
    }

    private sealed class RecentPathItem
    {
        public string Path { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public override string ToString() => DisplayName;
        public static RecentPathItem Placeholder(string displayName) => new()
        {
            Path = "",
            DisplayName = displayName
        };
        public static RecentPathItem FromPath(string path) => new()
        {
            Path = path,
            DisplayName = System.IO.Path.GetFileName(path)
        };
    }

    private sealed class EnumDisplayItem<TEnum> where TEnum : struct, Enum
    {
        public TEnum Value { get; init; }
        public string DisplayName { get; init; } = "";
    }

    public MainForm()
    {
        appStorageLocationService = new AppStorageLocationService();
        paths = appStorageLocationService.ResolvePaths();
        bootstrap = new BootstrapService(paths);
        presetService = new PresetService(paths);
        layoutService = new LayoutService(paths);
        renderer = new PreviewRenderService(units);
        outputService = new OutputService(renderer, units);
        outputOptionsFactory = new OutputOptionsFactory();
        settingsArchiveService = new SettingsArchiveService(paths);
        BuildUi();
        fieldRows.ListChanged += (_, _) => RefreshPrintSortFieldCombo();
        AppIcon.Apply(this);
        LocalizationService.LanguageChanged += HandleLanguageChanged;
        Load += (_, _) => InitializeData();
        Shown += (_, _) => BeginInvoke((Action)(() =>
        {
            ApplyInitialSplitDistances();
            RunStartupBackupCleanupIfNeeded();
        }));
        FormClosing += MainForm_FormClosing;
        FormClosed += (_, _) => LocalizationService.LanguageChanged -= HandleLanguageChanged;
    }

    private void BuildUi()
    {
        Text = "TagPrint Studio";
        Width = 1540;
        Height = 900;
        MinimumSize = new Size(1540, 720);
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = PageBackColor;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(14), BackColor = PageBackColor };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        Controls.Add(root);

        menuStrip = BuildMenuStrip();
        MainMenuStrip = menuStrip;
        root.SetColumnSpan(menuStrip, 2);
        root.Controls.Add(menuStrip, 0, 0);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(0, 0, 0, 10)
        };
        presetCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        layoutCombo = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
        var newPresetButton = Button("New", (_, _) => NewPreset());
        var savePresetButton = Button("プリセット保存", (_, _) => SavePreset(), primary: true);
        var savePresetAsButton = Button("Save As", (_, _) => SavePresetAs());
        var duplicatePresetButton = Button("Duplicate", (_, _) => DuplicatePreset());
        var renamePresetButton = Button("Rename", (_, _) => RenamePreset());
        var deletePresetButton = Button("Delete", (_, _) => DeletePreset());
        var designerButton = Button("Layout Designer", (_, _) => OpenLayoutDesigner(), primary: true);
        top.Controls.AddRange([
            LabelText("プリセット"),
            presetCombo,
            newPresetButton,
            savePresetButton,
            savePresetAsButton,
            duplicatePresetButton,
            renamePresetButton,
            deletePresetButton,
            LabelText("レイアウト", left: 14),
            layoutCombo,
            designerButton
        ]);
        root.SetColumnSpan(top, 2);
        root.Controls.Add(top, 0, 1);

        leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 270,
            BackColor = PageBackColor,
            SplitterWidth = 8,
            Margin = new Padding(0)
        };
        fieldsGrid = Grid(fieldRows);
        fieldsGrid.AutoGenerateColumns = false;
        fieldsGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        fieldsGrid.Columns.Add(CheckColumn("Enabled", "有効", 56));
        fieldsGrid.Columns.Add(TextColumn("DisplayName", "項目名", 120));
        fieldsGrid.Columns.Add(TextColumn("ColumnIndex", "列番号", 70));
        fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ヘッダー名", Name = "HeaderName", ReadOnly = true, Width = 120 });
        fieldsGrid.Columns.Add(CheckColumn("Required", "必須", 56));
        fieldsGrid.Columns.Add(CheckColumn("PrintConditionEnabled", "Filter", 52));
        fieldsGrid.Columns.Add(EnumColumn<PrintConditionType>("PrintConditionType", "Type", 82));
        fieldsGrid.Columns.Add(EnumColumn<PrintConditionOperator>("PrintConditionOperator", "Op", 92));
        fieldsGrid.Columns.Add(TextColumn("PrintConditionValue", "Value", 100));
        fieldsGrid.Columns.Add(ButtonColumn("Delete", "削除", 58));
        fieldsGrid.CellContentClick += FieldsGrid_CellContentClick;
        fieldsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (fieldsGrid.IsCurrentCellDirty) fieldsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        fieldsGrid.CellEndEdit += (_, _) => UpdateHeaderNames();
        fieldsGrid.CellValueChanged += (_, _) =>
        {
            UpdateHeaderNames();
            MarkPresetDirty();
            SaveCurrentCsvHeaderMapping();
            RedrawPreview();
        };
        fieldsGrid.DataError += (_, e) => e.ThrowException = false;

        var fieldTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = HeaderBackColor };
        headerCheck = new CheckBox { Text = "最初の行をヘッダーとして扱う", Checked = true, AutoSize = true };
        headerCheck.CheckedChanged += (_, _) =>
        {
            if (!loadingPreset) MarkPresetDirty();
            ReloadCsvWithCurrentFiles();
        };
        fieldTools.Controls.AddRange([Button("項目追加", (_, _) => AddField()), Button("main.button.addCsvColumn", (_, _) => AddCsvColumn()), headerCheck]);
        leftSplit.Panel1.Controls.Add(Section("CSVフィールド", fieldsGrid, fieldTools));

        leftSplit.Panel2.Controls.Add(BuildCsvImportSection());

        var previewTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = HeaderBackColor };
        previousPageButton = Button("前へ", (_, _) => MovePreviewPage(-1));
        nextPageButton = Button("次へ", (_, _) => MovePreviewPage(1));
        previewInfo = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0),
            ForeColor = MutedTextColor
        };
        previewTools.Controls.AddRange([
            Button("ページ全体", (_, _) => { previewMode = PreviewMode.Page; ResetPreviewPage(); RedrawPreview(); }),
            Button("1枚拡大", (_, _) => { previewMode = PreviewMode.SingleTag; ResetPreviewPage(); RedrawPreview(); }),
            previousPageButton,
            nextPageButton
        ]);
        previewBox = new PictureBox
        {
            BackColor = Color.FromArgb(233, 237, 242),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0),
            Location = Point.Empty
        };
        previewScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = previewBox.BackColor,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        previewViewportHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = previewBox.BackColor,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        previewCanvasHost = new Panel
        {
            BackColor = previewBox.BackColor,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Location = Point.Empty
        };
        previewCanvasHost.Controls.Add(previewBox);
        previewScrollPanel.Controls.Add(previewCanvasHost);
        previewZoomTools = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = HeaderBackColor,
            Margin = new Padding(0),
            Padding = new Padding(4),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = true
        };
        previewZoomInButton = Button("layout.button.zoomIn", (_, _) => AdjustPagePreviewZoom(PagePreviewZoomStepPercent));
        previewZoomResetButton = Button("layout.button.zoomReset", (_, _) => ResetPagePreviewZoom());
        previewZoomOutButton = Button("layout.button.zoomOut", (_, _) => AdjustPagePreviewZoom(-PagePreviewZoomStepPercent));
        ConfigurePreviewZoomButton(previewZoomInButton);
        ConfigurePreviewZoomButton(previewZoomResetButton);
        ConfigurePreviewZoomButton(previewZoomOutButton);
        previewZoomTools.Controls.Add(previewZoomInButton);
        previewZoomTools.Controls.Add(previewZoomResetButton);
        previewZoomTools.Controls.Add(previewZoomOutButton);
        previewViewportHost.Controls.Add(previewScrollPanel);
        previewViewportHost.Controls.Add(previewZoomTools);
        previewViewportHost.MouseDown += PreviewSurface_MouseDown;
        previewViewportHost.MouseMove += PreviewSurface_MouseMove;
        previewViewportHost.MouseUp += PreviewSurface_MouseUp;
        previewViewportHost.MouseLeave += PreviewSurface_MouseLeave;
        previewBox.MouseDown += PreviewSurface_MouseDown;
        previewBox.MouseMove += PreviewSurface_MouseMove;
        previewBox.MouseUp += PreviewSurface_MouseUp;
        previewBox.MouseLeave += PreviewSurface_MouseLeave;
        previewZoomTools.SizeChanged += (_, _) => PositionPreviewZoomTools();
        previewScrollPanel.Resize += (_, _) =>
        {
            ApplyPreviewImageLayout(previewMode == PreviewMode.Page, CurrentPreviewScrollPosition());
            PositionPreviewZoomTools();
        };
        previewViewportHost.Resize += (_, _) => PositionPreviewZoomTools();
        var previewContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        previewContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        previewContent.Controls.Add(previewViewportHost, 0, 0);
        previewContent.Controls.Add(previewInfo, 0, 1);
        centerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 820,
            SplitterWidth = 8,
            BackColor = PageBackColor,
            Margin = new Padding(0)
        };
        centerSplit.Panel1.Controls.Add(leftSplit);
        centerSplit.Panel2.Controls.Add(Section("プレビュー", previewContent, previewTools));
        root.SetColumnSpan(centerSplit, 2);
        root.Controls.Add(centerSplit, 0, 2);

        var outputPanel = BuildOutputSection();
        root.SetColumnSpan(outputPanel, 2);
        root.Controls.Add(outputPanel, 0, 3);

        presetCombo.SelectedIndexChanged += (_, _) => LoadSelectedPreset();
        layoutCombo.SelectedIndexChanged += (_, _) => LoadSelectedLayout();
    }

}
