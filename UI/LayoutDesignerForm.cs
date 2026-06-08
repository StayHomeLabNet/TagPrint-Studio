using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class LayoutDesignerForm : Form
{
    private const float PreviewScale = 8f;
    private const double MinElementSize = 1.0;
    private const int ToolbarControlHeight = 30;
    private const int DefaultEditorCanvasSplitterDistance = 330;
    private const int CanvasAreaZoomStepPercent = 10;
    private const int MinCanvasAreaZoomPercent = -40;
    private const int MaxCanvasAreaZoomPercent = 50;
    private static readonly Color PageBackColor = Color.FromArgb(244, 246, 248);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color HeaderBackColor = Color.FromArgb(248, 250, 252);
    private static readonly Color BorderColor = Color.FromArgb(216, 222, 230);
    private static readonly Color TextColor = Color.FromArgb(31, 41, 55);
    private static readonly Color MutedTextColor = Color.FromArgb(75, 85, 99);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);
    private static readonly Color AccentHoverColor = Color.FromArgb(29, 78, 216);

    private enum DragMode { None, Move, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight }
    private enum ArrangeAction { AlignLeft, AlignCenter, AlignRight, AlignMiddle, DistributeHorizontal, DistributeVertical }
    private enum DesignerPreviewMode { SingleTag, Page }

    private readonly AppPaths paths = new();
    private readonly ILayoutService layoutService;
    private readonly IPreviewRenderService renderer;
    private readonly BindingList<LayoutElement> elementRows = [];
    private readonly List<FieldDefinition> fieldDefinitions;
    private readonly string initialLayoutPath;
    private readonly List<CsvPreviewRowContext> previewRows;
    private readonly Dictionary<string, string> previewData;
    private readonly Stack<List<LayoutElement>> undoStack = new();
    private readonly Stack<List<LayoutElement>> redoStack = new();

    private LayoutFile currentLayout = new();
    private string currentLayoutPath = "";
    private bool loading;
    private bool applyingHistory;
    private bool isDirty;
    private bool csvPreviewDirty;
    private bool suppressLayoutSelectionChange;
    private int canvasAreaZoomPercent;
    private DesignerPreviewMode previewMode = DesignerPreviewMode.SingleTag;
    private DragMode dragMode = DragMode.None;
    private PointF dragOffsetMm;
    private RectangleF dragStartBounds;
    private FileComboItem? currentLayoutItem;
    private LayoutElement? editorElement;
    private TextBox? canvasTextEditor;
    private LayoutElement? canvasEditingElement;
    private string canvasOriginalText = "";
    private bool updatingCsvValueEditor;

    private ComboBox layoutCombo = null!;
    private ComboBox arrangeCombo = null!;
    private DataGridView elementsGrid = null!;
    private PictureBox previewBox = null!;
    private SplitContainer editorCanvasSplit = null!;
    private Label statusLabel = null!;
    private Button undoButton = null!;
    private Button redoButton = null!;
    private Button saveCsvButton = null!;
    private Button pagePreviewButton = null!;
    private Button singleTagPreviewButton = null!;
    private Button previousPreviewRowButton = null!;
    private Button nextPreviewRowButton = null!;
    private CheckBox showGridCheck = null!;
    private CheckBox snapCheck = null!;
    private ComboBox previewRowCombo = null!;
    private ComboBox csvColumnCombo = null!;
    private TextBox csvValueBox = null!;
    private NumericUpDown gridSizeBox = null!;
    private TextBox nameBox = null!;
    private ComboBox typeCombo = null!;
    private NumericUpDown xBox = null!;
    private NumericUpDown yBox = null!;
    private NumericUpDown widthBox = null!;
    private NumericUpDown heightBox = null!;
    private NumericUpDown zIndexBox = null!;
    private ComboBox fieldIdCombo = null!;
    private TextBox textBox = null!;
    private TextBox imagePathBox = null!;
    private Button imageBrowseButton = null!;
    private ComboBox fontFamilyCombo = null!;
    private NumericUpDown fontSizeBox = null!;
    private ComboBox fontSizeSourceCombo = null!;
    private ComboBox fontSizeFieldCombo = null!;
    private NumericUpDown minFontSizeBox = null!;
    private NumericUpDown maxFontSizeBox = null!;
    private CheckBox boldCheck = null!;
    private CheckBox lockedCheck = null!;
    private CheckBox visibleCheck = null!;
    private CheckBox barcodeShowTextCheck = null!;
    private ComboBox barcodeTypeCombo = null!;
    private Label barcodeSizeGuideLabel = null!;
    private Button textColorButton = null!;
    private Button backgroundColorButton = null!;
    private Button borderColorButton = null!;
    private NumericUpDown borderWidthBox = null!;
    private ComboBox alignCombo = null!;
    private ComboBox decimalStyleCombo = null!;
    private NumericUpDown decimalPlacesBox = null!;
    private NumericUpDown fractionScaleBox = null!;
    private NumericUpDown fractionYOffsetBox = null!;
    private ComboBox paperSizeCombo = null!;
    private ComboBox unitCombo = null!;
    private NumericUpDown paperWidthBox = null!;
    private NumericUpDown paperHeightBox = null!;
    private NumericUpDown tagWidthBox = null!;
    private NumericUpDown tagHeightBox = null!;
    private NumericUpDown marginTopBox = null!;
    private NumericUpDown marginBottomBox = null!;
    private NumericUpDown marginLeftBox = null!;
    private NumericUpDown marginRightBox = null!;
    private NumericUpDown spacingHorizontalBox = null!;
    private NumericUpDown spacingVerticalBox = null!;
    private CheckBox autoGridCheck = null!;
    private NumericUpDown gridColumnsBox = null!;
    private NumericUpDown gridRowsBox = null!;

    public bool LayoutSaved { get; private set; }
    public bool CsvSaved { get; private set; }
    public string SavedLayoutPath { get; private set; } = "";

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

    public LayoutDesignerForm() : this([], "", (List<CsvPreviewRowContext>?)null)
    {
    }

    public LayoutDesignerForm(IEnumerable<FieldDefinition> fields) : this(fields, "", (List<CsvPreviewRowContext>?)null)
    {
    }

    public LayoutDesignerForm(IEnumerable<FieldDefinition> fields, string initialLayoutPath) : this(fields, initialLayoutPath, (List<CsvPreviewRowContext>?)null)
    {
    }

    public LayoutDesignerForm(IEnumerable<FieldDefinition> fields, string initialLayoutPath, Dictionary<string, string>? previewData)
        : this(fields, initialLayoutPath, previewData is { Count: > 0 } ? [new CsvPreviewRowContext { Data = previewData.ToDictionary() }] : null)
    {
    }

    public LayoutDesignerForm(IEnumerable<FieldDefinition> fields, string initialLayoutPath, List<CsvPreviewRowContext>? previewRows)
    {
        fieldDefinitions = fields.Where(f => f.Enabled).OrderBy(f => f.DisplayOrder).ToList();
        this.initialLayoutPath = initialLayoutPath;
        this.previewRows = previewRows?.Where(row => row.Data.Count > 0).ToList() ?? [];
        previewData = this.previewRows.Count > 0
            ? this.previewRows[0].Data.ToDictionary()
            : DefaultPreviewData(fieldDefinitions);
        layoutService = new LayoutService(paths);
        renderer = new PreviewRenderService(new UnitConversionService());
        BuildUi();
        AppIcon.Apply(this);
        LocalizationService.LanguageChanged += HandleLanguageChanged;
        Load += (_, _) => InitializeData();
        FormClosed += (_, _) => previewBox.Image?.Dispose();
        FormClosed += (_, _) => LocalizationService.LanguageChanged -= HandleLanguageChanged;
        FormClosing += LayoutDesignerForm_FormClosing;
        KeyPreview = true;
        KeyDown += LayoutDesignerForm_KeyDown;
    }

    private void BuildUi()
    {
        Text = LocalizationService.T("layout.title");
        Width = 1460;
        Height = 860;
        MinimumSize = new Size(1180, 700);
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = PageBackColor;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14), BackColor = PageBackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = SurfaceColor,
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 0, 0, 10)
        };
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        var layoutTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = SurfaceColor, Margin = new Padding(0) };
        var elementTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = SurfaceColor, Margin = new Padding(0) };

        layoutCombo = ToolbarComboBox(240);
        layoutCombo.SelectedIndexChanged += (_, _) => LoadSelectedLayout();
        arrangeCombo = ToolbarComboBox(176);
        undoButton = Button("Undo", (_, _) => Undo());
        redoButton = Button("Redo", (_, _) => Redo());
        showGridCheck = new CheckBox { Text = "Grid", Checked = true, AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
        snapCheck = new CheckBox { Text = "Snap", Checked = true, AutoSize = true, Padding = new Padding(2, 7, 0, 0) };
        previewRowCombo = ToolbarComboBox(180);
        csvColumnCombo = ToolbarComboBox(180);
        csvValueBox = ToolbarTextBox(260);
        saveCsvButton = Button("Save CSV", (_, _) => SaveCsvChanges(), primary: true);
        saveCsvButton.ForeColor = Color.White;
        saveCsvButton.Enabled = false;
        pagePreviewButton = Button("Page Preview", (_, _) => SwitchPreviewMode(DesignerPreviewMode.Page));
        singleTagPreviewButton = Button("Single Tag", (_, _) => SwitchPreviewMode(DesignerPreviewMode.SingleTag), primary: true);
        previousPreviewRowButton = Button("Previous", (_, _) => MovePreviewRow(-1));
        previousPreviewRowButton.Text = PreviewNavigationButtonText(previous: true);
        nextPreviewRowButton = Button("Next", (_, _) => MovePreviewRow(1));
        nextPreviewRowButton.Text = PreviewNavigationButtonText(previous: false);
        gridSizeBox = Number(0.5m, 50, 0.5m, 1);
        gridSizeBox.AutoSize = false;
        gridSizeBox.Width = 58;
        gridSizeBox.Height = ToolbarControlHeight;
        gridSizeBox.MinimumSize = new Size(58, ToolbarControlHeight);
        gridSizeBox.Margin = new Padding(4, 4, 4, 4);
        showGridCheck.CheckedChanged += (_, _) => RedrawPreview();
        snapCheck.CheckedChanged += (_, _) => RedrawPreview();
        previewRowCombo.SelectedIndexChanged += (_, _) => SelectPreviewRow();
        csvColumnCombo.SelectedIndexChanged += (_, _) => UpdateCsvValueEditor();
        csvValueBox.TextChanged += (_, _) => ApplyCsvValueEditor();
        ConfigurePreviewRowCombo();
        ConfigureCsvColumnEditor();
        ConfigureArrangeCombo();
        gridSizeBox.ValueChanged += (_, _) => RedrawPreview();
        layoutTools.Controls.AddRange([
            LabelText("Layout"),
            layoutCombo,
            Button("New", (_, _) => NewLayout()),
            Button("Save", (_, _) => SaveLayout(), primary: true),
            Button("Save As", (_, _) => SaveLayoutAs()),
            Button("Duplicate Layout", (_, _) => DuplicateLayout()),
            Button("Rename", (_, _) => RenameLayout()),
            Button("Delete Layout", (_, _) => DeleteLayout()),
            showGridCheck,
            snapCheck
        ]);
        elementTools.Controls.AddRange([
            Button("Add Element", (_, _) => AddElement()),
            Button("Add Field", (_, _) => AddFieldElement()),
            Button("Add Image", (_, _) => AddImageElement()),
            Button("Add Barcode", (_, _) => AddBarcodeElement()),
            Button("Duplicate", (_, _) => DuplicateElement()),
            Button("Delete Element", (_, _) => DeleteElement()),
            undoButton,
            redoButton,
            arrangeCombo,
            Button("Arrange", (_, _) => ArrangeSelectedElements()),
            LabelText("Grid mm", left: 8),
            gridSizeBox
        ]);
        top.Controls.Add(layoutTools, 0, 0);
        top.Controls.Add(elementTools, 0, 1);
        root.Controls.Add(top, 0, 0);

        editorCanvasSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = DefaultEditorCanvasSplitterDistance,
            SplitterWidth = 8,
            BackColor = PageBackColor
        };
        root.Controls.Add(editorCanvasSplit, 0, 1);

        var topEditors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = PageBackColor, Margin = new Padding(0, 0, 0, 10) };
        topEditors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        topEditors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        editorCanvasSplit.Panel1.Controls.Add(topEditors);

        elementsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = elementRows,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true
        };
        ApplyGridStyle(elementsGrid);
        elementsGrid.Columns.Add(TextColumn("Name", "Name", 130));
        elementsGrid.Columns.Add(TextColumn("ElementType", "Type", 90));
        elementsGrid.Columns.Add(TextColumn("ZIndex", "Z", 44));
        elementsGrid.Columns.Add(CheckColumn("Locked", "Locked", 58));
        elementsGrid.Columns.Add(CheckColumn("Visible", "Visible", 58));
        elementsGrid.SelectionChanged += (_, _) => LoadSelectedElementIntoEditor();
        elementsGrid.CellBeginEdit += (_, _) => CaptureUndo();
        elementsGrid.CellValueChanged += (_, _) => { if (!loading) RedrawPreview(); };
        elementsGrid.DataError += (_, e) => e.ThrowException = false;
        topEditors.Controls.Add(Section("Elements", elementsGrid), 0, 0);

        var propertiesSection = Section("Properties", BuildPropertyEditor());
        propertiesSection.Margin = new Padding(10, 0, 0, 0);
        topEditors.Controls.Add(propertiesSection, 1, 0);

        previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(233, 237, 242),
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Cross
        };
        previewBox.MouseDown += PreviewBox_MouseDown;
        previewBox.MouseDoubleClick += PreviewBox_MouseDoubleClick;
        previewBox.MouseMove += PreviewBox_MouseMove;
        previewBox.MouseUp += (_, _) => EndDrag();
        previewBox.MouseLeave += (_, _) => EndDrag();
        var canvasHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = previewBox.BackColor,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        canvasHost.Controls.Add(previewBox);

        var canvasSizeTools = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = HeaderBackColor,
            Margin = new Padding(0),
            Padding = new Padding(4),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var zoomInButton = Button("layout.button.zoomIn", (_, _) => AdjustCanvasAreaZoom(CanvasAreaZoomStepPercent), primary: true);
        var zoomResetButton = Button("layout.button.zoomReset", (_, _) => ResetCanvasAreaZoom());
        var zoomOutButton = Button("layout.button.zoomOut", (_, _) => AdjustCanvasAreaZoom(-CanvasAreaZoomStepPercent));
        ConfigureCanvasZoomButton(zoomInButton);
        ConfigureCanvasZoomButton(zoomResetButton);
        ConfigureCanvasZoomButton(zoomOutButton);
        canvasSizeTools.Controls.Add(zoomInButton);
        canvasSizeTools.Controls.Add(zoomResetButton);
        canvasSizeTools.Controls.Add(zoomOutButton);
        canvasHost.Controls.Add(canvasSizeTools);
        void PositionCanvasSizeTools()
        {
            canvasSizeTools.Left = Math.Max(0, canvasHost.ClientSize.Width - canvasSizeTools.Width - 12);
            canvasSizeTools.Top = Math.Max(0, canvasHost.ClientSize.Height - canvasSizeTools.Height - 12);
            canvasSizeTools.BringToFront();
        }
        canvasHost.Resize += (_, _) => PositionCanvasSizeTools();
        canvasSizeTools.SizeChanged += (_, _) => PositionCanvasSizeTools();
        PositionCanvasSizeTools();

        var canvasTools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 11, BackColor = HeaderBackColor, Margin = new Padding(0), Padding = new Padding(0) };
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        canvasTools.Controls.Add(LabelText("Preview Row", left: 0), 0, 0);
        canvasTools.Controls.Add(previewRowCombo, 1, 0);
        canvasTools.Controls.Add(pagePreviewButton, 2, 0);
        canvasTools.Controls.Add(singleTagPreviewButton, 3, 0);
        canvasTools.Controls.Add(previousPreviewRowButton, 4, 0);
        canvasTools.Controls.Add(nextPreviewRowButton, 5, 0);
        canvasTools.Controls.Add(LabelText("Column", left: 12), 7, 0);
        canvasTools.Controls.Add(csvColumnCombo, 8, 0);
        canvasTools.Controls.Add(csvValueBox, 9, 0);
        canvasTools.Controls.Add(saveCsvButton, 10, 0);
        editorCanvasSplit.Panel2.Controls.Add(Section("Canvas", canvasHost, canvasTools));

        statusLabel = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = MutedTextColor, BackColor = PageBackColor, Padding = new Padding(2, 8, 0, 0) };
        root.Controls.Add(statusLabel, 0, 2);
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var previewIndex = previewRowCombo?.SelectedIndex ?? -1;
        var columnIndex = csvColumnCombo?.SelectedIndex ?? -1;
        var arrangeAction = arrangeCombo?.SelectedItem is ArrangeComboItem arrangeItem ? arrangeItem.Action : (ArrangeAction?)null;
        LocalizationService.Apply(this);
        if (previewRowCombo is not null)
        {
            ConfigurePreviewRowCombo();
            if (previewIndex >= 0 && previewIndex < previewRowCombo.Items.Count)
            {
                previewRowCombo.SelectedIndex = previewIndex;
            }
        }
        if (csvColumnCombo is not null)
        {
            ConfigureCsvColumnEditor();
            if (columnIndex >= 0 && columnIndex < csvColumnCombo.Items.Count)
            {
                csvColumnCombo.SelectedIndex = columnIndex;
            }
        }
        if (arrangeCombo is not null) ConfigureArrangeCombo(arrangeAction);
        if (showGridCheck is not null) showGridCheck.Text = LocalizationService.T("layout.checkbox.grid");
        if (snapCheck is not null) snapCheck.Text = LocalizationService.T("layout.checkbox.snap");
        if (previousPreviewRowButton is not null) previousPreviewRowButton.Text = PreviewNavigationButtonText(previous: true);
        if (nextPreviewRowButton is not null) nextPreviewRowButton.Text = PreviewNavigationButtonText(previous: false);
        UpdatePreviewModeButtons();
        UpdatePreviewRowNavigationButtons();
        UpdateDesignerGridHeaders();
        UpdateTitle();
        UpdateCsvValueEditor();
        RedrawPreview();
    }

    private void UpdateDesignerGridHeaders()
    {
        if (elementsGrid.Columns.Count >= 5)
        {
            elementsGrid.Columns[0].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "名前" : "Name";
            elementsGrid.Columns[1].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "種類" : "Type";
            elementsGrid.Columns[2].HeaderText = "Z";
            elementsGrid.Columns[3].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "固定" : "Locked";
            elementsGrid.Columns[4].HeaderText = LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "表示" : "Visible";
        }
    }

    private void AdjustCanvasAreaZoom(int deltaPercent)
    {
        canvasAreaZoomPercent = Math.Clamp(
            canvasAreaZoomPercent + deltaPercent,
            MinCanvasAreaZoomPercent,
            MaxCanvasAreaZoomPercent);
        ApplyCanvasAreaZoom();
    }

    private void ResetCanvasAreaZoom()
    {
        canvasAreaZoomPercent = 0;
        ApplyCanvasAreaZoom();
    }

    private void ApplyCanvasAreaZoom()
    {
        if (editorCanvasSplit is null || editorCanvasSplit.Height <= 0) return;

        var targetDistance = (int)Math.Round(DefaultEditorCanvasSplitterDistance * (1 - canvasAreaZoomPercent / 100.0));
        var maxDistance = editorCanvasSplit.Height - editorCanvasSplit.Panel2MinSize - editorCanvasSplit.SplitterWidth;
        if (maxDistance < editorCanvasSplit.Panel1MinSize) return;

        editorCanvasSplit.SplitterDistance = Math.Clamp(targetDistance, editorCanvasSplit.Panel1MinSize, maxDistance);
        RedrawPreview();
    }


    private void MarkDirty()
    {
        if (loading || applyingHistory || isDirty) return;
        isDirty = true;
        UpdateTitle();
    }

    private void MarkClean()
    {
        isDirty = false;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrWhiteSpace(currentLayoutPath)
            ? LocalizationService.T("layout.title")
            : $"{LocalizationService.T("layout.title")} - {Path.GetFileNameWithoutExtension(currentLayoutPath)}";
        Text = isDirty ? $"{name} *" : name;
    }

    private bool ConfirmSaveChanges()
    {
        if (!isDirty) return true;
        var result = MessageBox.Show(
            LocalizationService.T("layout.message.unsavedLayout"),
            LocalizationService.T("layout.title.unsavedLayout"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        return result switch
        {
            DialogResult.Yes => SaveLayout(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void LayoutDesignerForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmSaveChanges()) e.Cancel = true;
    }

    private void LayoutDesignerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Control) return;
        if (e.KeyCode == Keys.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Y)
        {
            Redo();
            e.Handled = true;
        }
    }

    private LayoutElement? SelectedElement()
    {
        return elementsGrid.CurrentRow?.DataBoundItem as LayoutElement
            ?? elementsGrid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.DataBoundItem as LayoutElement;
    }

    private void SelectElement(LayoutElement element)
    {
        for (var i = 0; i < elementsGrid.Rows.Count; i++)
        {
            if (!ReferenceEquals(elementsGrid.Rows[i].DataBoundItem, element)) continue;
            elementsGrid.ClearSelection();
            elementsGrid.Rows[i].Selected = true;
            elementsGrid.CurrentCell = elementsGrid.Rows[i].Cells[0];
            return;
        }
    }

    private void SyncEditorPositionAndSize(LayoutElement element)
    {
        loading = true;
        xBox.Value = Clamp(element.X, xBox);
        yBox.Value = Clamp(element.Y, yBox);
        widthBox.Value = Clamp(element.Width, widthBox);
        heightBox.Value = Clamp(element.Height, heightBox);
        loading = false;
    }

    private void SetEditorEnabled(bool enabled)
    {
        foreach (Control control in new Control[] { nameBox, typeCombo, xBox, yBox, widthBox, heightBox, zIndexBox, fieldIdCombo, textBox, imagePathBox, imageBrowseButton, fontFamilyCombo, fontSizeBox, boldCheck, lockedCheck, visibleCheck, barcodeShowTextCheck, barcodeTypeCombo, barcodeSizeGuideLabel, textColorButton, backgroundColorButton, borderColorButton, borderWidthBox, alignCombo, decimalStyleCombo, decimalPlacesBox, fractionScaleBox, fractionYOffsetBox })
        {
            control.Enabled = enabled;
        }
    }

    private double Snap(double value)
    {
        if (!snapCheck.Checked) return value;
        var grid = Math.Max(0.1, (double)gridSizeBox.Value);
        return Math.Round(value / grid) * grid;
    }

    private static RectangleF ElementBounds(LayoutElement element) => new((float)element.X, (float)element.Y, (float)element.Width, (float)element.Height);

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    private static List<LayoutElement> CloneElements(IEnumerable<LayoutElement> elements) => elements.Select(CloneElement).ToList();

    private static LayoutElement CloneElement(LayoutElement element) => new()
    {
        ElementId = element.ElementId,
        Name = element.Name,
        ZIndex = element.ZIndex,
        ElementType = element.ElementType,
        FieldId = element.FieldId,
        Text = element.Text,
        ImagePath = element.ImagePath,
        X = element.X,
        Y = element.Y,
        Width = element.Width,
        Height = element.Height,
        FontFamily = element.FontFamily,
        FontSize = element.FontSize,
        FontSizeSource = element.FontSizeSource,
        FontSizeFieldId = element.FontSizeFieldId,
        MinFontSize = element.MinFontSize,
        MaxFontSize = element.MaxFontSize,
        Bold = element.Bold,
        TextColor = element.TextColor,
        BackgroundColor = element.BackgroundColor,
        BorderColor = element.BorderColor,
        BorderWidth = element.BorderWidth,
        TextAlign = element.TextAlign,
        DecimalDisplayStyle = element.DecimalDisplayStyle,
        DecimalPlaces = element.DecimalPlaces,
        FractionFontScale = element.FractionFontScale,
        FractionYOffset = element.FractionYOffset,
        BarcodeType = element.BarcodeType,
        BarcodeShowText = element.BarcodeShowText,
        KeepAspectRatio = element.KeepAspectRatio,
        Locked = element.Locked,
        Visible = element.Visible
    };

    private static void NormalizeElementOrder(LayoutFile layout)
    {
        for (var i = 0; i < layout.Elements.Count; i++)
        {
            if (layout.Elements[i].ZIndex == 0 && i > 0) layout.Elements[i].ZIndex = i * 10;
        }
    }

    private static bool SelectComboByPath(ComboBox combo, string filePath)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is FileComboItem item && string.Equals(item.Path, filePath, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private void ConfigureArrangeCombo(ArrangeAction? selectedAction = null)
    {
        if (arrangeCombo is null) return;

        arrangeCombo.BeginUpdate();
        arrangeCombo.Items.Clear();
        arrangeCombo.Items.AddRange(
        [
            new ArrangeComboItem { Action = ArrangeAction.AlignLeft, Text = LocalizationService.T("layout.arrange.left") },
            new ArrangeComboItem { Action = ArrangeAction.AlignCenter, Text = LocalizationService.T("layout.arrange.center") },
            new ArrangeComboItem { Action = ArrangeAction.AlignRight, Text = LocalizationService.T("layout.arrange.right") },
            new ArrangeComboItem { Action = ArrangeAction.AlignMiddle, Text = LocalizationService.T("layout.arrange.middle") },
            new ArrangeComboItem { Action = ArrangeAction.DistributeHorizontal, Text = LocalizationService.T("layout.arrange.distributeH") },
            new ArrangeComboItem { Action = ArrangeAction.DistributeVertical, Text = LocalizationService.T("layout.arrange.distributeV") }
        ]);
        arrangeCombo.EndUpdate();

        var targetAction = selectedAction ?? ArrangeAction.AlignLeft;
        for (var i = 0; i < arrangeCombo.Items.Count; i++)
        {
            if (arrangeCombo.Items[i] is ArrangeComboItem item && item.Action == targetAction)
            {
                arrangeCombo.SelectedIndex = i;
                return;
            }
        }

        if (arrangeCombo.Items.Count > 0) arrangeCombo.SelectedIndex = 0;
    }

    private void SwitchPreviewMode(DesignerPreviewMode mode)
    {
        if (previewMode == mode) return;
        previewMode = mode;
        EndDrag();
        UpdatePreviewModeButtons();
        RedrawPreview();
    }

    private void UpdatePreviewModeButtons()
    {
        SetToolbarButtonState(pagePreviewButton, previewMode == DesignerPreviewMode.Page);
        SetToolbarButtonState(singleTagPreviewButton, previewMode == DesignerPreviewMode.SingleTag);
    }

    private static string ToSafeFileName(string value) => SafeFileNameService.ToSafeFileName(value);

    private static decimal Clamp(double value, NumericUpDown box)
    {
        var decimalValue = (decimal)value;
        if (decimalValue < box.Minimum) return box.Minimum;
        if (decimalValue > box.Maximum) return box.Maximum;
        return decimalValue;
    }

    private sealed class ArrangeComboItem
    {
        public required ArrangeAction Action { get; init; }
        public required string Text { get; init; }
        public override string ToString() => Text;
    }

    private sealed class CsvColumnComboItem(FieldDefinition field)
    {
        public FieldDefinition Field { get; } = field;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Field.DisplayName)
                ? Field.FieldId
                : Field.DisplayName;
    }
}
