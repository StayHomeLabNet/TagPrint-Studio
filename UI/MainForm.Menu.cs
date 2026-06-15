using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

public sealed partial class MainForm : Form
{
    private MenuStrip BuildMenuStrip()
    {
        var strip = new MenuStrip
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 2, 0, 2),
            RenderMode = ToolStripRenderMode.System
        };

        japaneseLanguageMenuItem = Item("common.language.japanese", (_, _) => ChangeLanguage(AppLanguage.Japanese));
        englishLanguageMenuItem = Item("common.language.english", (_, _) => ChangeLanguage(AppLanguage.English));
        settingsBackupMenuItem = new ToolStripMenuItem(SettingsBackupMenuText());
        settingsBackupMenuItem.Click += (_, _) => BackupSettingsArchive();
        settingsImportMenuItem = new ToolStripMenuItem(SettingsImportMenuText());
        settingsImportMenuItem.Click += (_, _) => ImportSettingsArchive();
        backupCleanupMenuItem = new ToolStripMenuItem(BackupCleanupMenuText());
        backupCleanupMenuItem.Click += (_, _) => CleanupBackupFiles();
        backupCleanupStartupDisabledMenuItem = new ToolStripMenuItem(BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Disabled));
        backupCleanupStartupDisabledMenuItem.Click += (_, _) => SetBackupCleanupStartupMode(BackupCleanupStartupMode.Disabled);
        backupCleanupStartupAskMenuItem = new ToolStripMenuItem(BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Ask));
        backupCleanupStartupAskMenuItem.Click += (_, _) => SetBackupCleanupStartupMode(BackupCleanupStartupMode.Ask);
        backupCleanupStartupAutoMenuItem = new ToolStripMenuItem(BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Auto));
        backupCleanupStartupAutoMenuItem.Click += (_, _) => SetBackupCleanupStartupMode(BackupCleanupStartupMode.Auto);
        backupCleanupStartupMenuItem = new ToolStripMenuItem(BackupCleanupStartupMenuText());
        backupCleanupStartupMenuItem.DropDownItems.AddRange([
            backupCleanupStartupDisabledMenuItem,
            backupCleanupStartupAskMenuItem,
            backupCleanupStartupAutoMenuItem
        ]);
        confirmPrintTargetsMenuItem = Item("main.menu.confirmPrintTargets", (_, _) => ToggleConfirmPrintTargets());
        confirmPrintTargetsMenuItem.CheckOnClick = true;
        largePrintWarningSettingsMenuItem = Item("main.menu.largePrintWarning", (_, _) => ConfigureLargePrintWarning());
        csvLoadSettingsMenuItem = Item("main.menu.csvLoadSettings", (_, _) => ConfigureCsvLoadSettings());
        appDataLocationMenuItem = new ToolStripMenuItem(AppDataLocationMenuText());
        appDataLocationMenuItem.Click += (_, _) => ConfigureAppDataLocation();
        openAppDataLocationMenuItem = new ToolStripMenuItem(OpenAppDataLocationMenuText());
        openAppDataLocationMenuItem.Click += (_, _) => OpenAppDataLocation();
        resetAppDataLocationMenuItem = new ToolStripMenuItem(ResetAppDataLocationMenuText());
        resetAppDataLocationMenuItem.Click += (_, _) => ResetAppDataLocation();
        openSettingsLocationMenuItem = new ToolStripMenuItem(OpenSettingsLocationMenuText());
        openSettingsLocationMenuItem.Click += (_, _) => OpenSettingsLocation();
        openStartupCsvLocationMenuItem = new ToolStripMenuItem(OpenStartupCsvLocationMenuText());
        openStartupCsvLocationMenuItem.Click += (_, _) => OpenStartupCsvLocation();
        openStartupFolderMenuItem = new ToolStripMenuItem(OpenStartupFolderMenuText());
        openStartupFolderMenuItem.Click += (_, _) => OpenStartupFolder();

        var settingsMenu = MenuGroup("main.menu.settings",
            Item("main.menu.startupCsv", (_, _) => ConfigureStartupCsv(), Keys.Control | Keys.Alt | Keys.O),
            openStartupCsvLocationMenuItem,
            Item("main.menu.clearStartupCsv", (_, _) => ClearStartupCsv()),
            Item("main.menu.startupFolder", (_, _) => ConfigureStartupFolder(), Keys.Control | Keys.Alt | Keys.F),
            openStartupFolderMenuItem,
            Item("main.menu.clearStartupFolder", (_, _) => ClearStartupFolder()),
            new ToolStripSeparator(),
            confirmPrintTargetsMenuItem,
            largePrintWarningSettingsMenuItem,
            csvLoadSettingsMenuItem,
            new ToolStripSeparator(),
            appDataLocationMenuItem,
            openAppDataLocationMenuItem,
            resetAppDataLocationMenuItem,
            new ToolStripSeparator(),
            settingsImportMenuItem,
            settingsBackupMenuItem,
            backupCleanupMenuItem,
            backupCleanupStartupMenuItem,
            openSettingsLocationMenuItem,
            new ToolStripSeparator(),
            MenuGroup("main.menu.language", japaneseLanguageMenuItem, englishLanguageMenuItem));

        AddRightPaddingToDropDownItems(settingsMenu.DropDownItems, 20);

        strip.Items.AddRange([
            MenuGroup("main.menu.file",
                Item("main.menu.newPreset", (_, _) => NewPreset(), Keys.Control | Keys.N),
                Item("main.menu.savePreset", (_, _) => SavePreset(), Keys.Control | Keys.S),
                Item("main.menu.savePresetAs", (_, _) => SavePresetAs(), Keys.Control | Keys.Shift | Keys.S),
                new ToolStripSeparator(),
                Item("main.menu.openCsv", (_, _) => SelectCsvFiles(), Keys.Control | Keys.O),
                Item("main.menu.openFolder", (_, _) => SelectCsvFolder(), Keys.Control | Keys.Shift | Keys.O),
                Item("main.menu.tagFinder", (_, _) => OpenTagFinder(), Keys.Control | Keys.F),
                Item("main.menu.outputFolder", (_, _) => SelectOutputFolder(), Keys.Control | Keys.Shift | Keys.F),
                new ToolStripSeparator(),
                Item("main.menu.exit", (_, _) => Close(), Keys.Alt | Keys.F4)),
            MenuGroup("main.menu.layout",
                Item("main.menu.layoutDesigner", (_, _) => OpenLayoutDesigner(), Keys.Control | Keys.L)),
            MenuGroup("main.menu.export",
                Item("main.menu.savePng", async (_, _) => await ExportPngAsync(), Keys.Control | Keys.Shift | Keys.P),
                Item("main.menu.savePdf", async (_, _) => await ExportPdfAsync(), Keys.Control | Keys.E),
                Item("main.menu.print", async (_, _) => await PrintDirectAsync(), Keys.Control | Keys.P)),
            MenuGroup("main.menu.view",
                Item("main.menu.pagePreview", (_, _) => SwitchPreviewMode(PreviewMode.Page), Keys.Control | Keys.D1),
                Item("main.menu.singleTagPreview", (_, _) => SwitchPreviewMode(PreviewMode.SingleTag), Keys.Control | Keys.D2),
                new ToolStripSeparator(),
                Item("main.menu.previousPage", (_, _) => MovePreviewPage(-1), Keys.Alt | Keys.Left),
                Item("main.menu.nextPage", (_, _) => MovePreviewPage(1), Keys.Alt | Keys.Right)),
            settingsMenu,
            MenuGroup("main.menu.help",
                Item("main.menu.about", (_, _) => ShowAbout()))
        ]);

        return strip;
    }


    private void ShowAbout()
    {
        MessageBox.Show(
            LocalizationService.T("main.about.body"),
            LocalizationService.T("main.about.title"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }


    private static ToolStripMenuItem MenuGroup(string text, params ToolStripItem[] items)
    {
        var menu = new ToolStripMenuItem(ResolveUiText(text));
        menu.DropDownItems.AddRange(items);
        BindIfLocalized(menu, text);
        return menu;
    }

    private static ToolStripMenuItem Item(string text, EventHandler click, Keys shortcutKeys = Keys.None)
    {
        var item = new ToolStripMenuItem(ResolveUiText(text));
        if (shortcutKeys != Keys.None) item.ShortcutKeys = shortcutKeys;
        item.ShowShortcutKeys = shortcutKeys != Keys.None;
        item.Click += click;
        BindIfLocalized(item, text);
        return item;
    }

    private static void AddRightPaddingToDropDownItems(ToolStripItemCollection items, int rightPadding)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripSeparator)
            {
                continue;
            }

            item.Padding = new Padding(
                item.Padding.Left,
                item.Padding.Top,
                item.Padding.Right + rightPadding,
                item.Padding.Bottom);

            if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
            {
                AddRightPaddingToDropDownItems(menuItem.DropDownItems, rightPadding);
            }
        }
    }

    private static string SettingsBackupMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "設定をバックアップ..."
        : "Backup Settings...";

    private static string SettingsImportMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "設定をインポート..."
        : "Import Settings...";

    private static string BackupCleanupMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "バックアップファイルを整理..."
        : "Clean Up Backup Files...";

    private static string BackupCleanupStartupMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "起動時にバックアップ整理"
        : "Clean Up Backups on Startup";

    private static string BackupCleanupStartupModeMenuText(BackupCleanupStartupMode mode)
    {
        return (LocalizationService.CurrentLanguage, mode) switch
        {
            (AppLanguage.Japanese, BackupCleanupStartupMode.Disabled) => "しない",
            (AppLanguage.Japanese, BackupCleanupStartupMode.Ask) => "確認してから実行",
            (AppLanguage.Japanese, BackupCleanupStartupMode.Auto) => "自動実行",
            (_, BackupCleanupStartupMode.Disabled) => "Disabled",
            (_, BackupCleanupStartupMode.Ask) => "Ask Before Running",
            _ => "Run Automatically"
        };
    }

    private static string AppDataLocationMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "アプリデータ保存場所を変更する"
        : "Change App Data Storage Location";

    private static string OpenAppDataLocationMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "アプリデータ保存場所を開く"
        : "Open App Data Location";

    private static string ResetAppDataLocationMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "アプリデータ保存場所を既定に戻す"
        : "Reset App Data Location";

    private static string OpenSettingsLocationMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "設定ファイルの場所を開く"
        : "Open Settings File Location";

    private static string OpenStartupCsvLocationMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "起動時CSVの場所を開く"
        : "Open Startup CSV Location";

    private static string OpenStartupFolderMenuText() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "起動時フォルダーを開く"
        : "Open Startup Folder";

    private void ToggleConfirmPrintTargets()
    {
        settings.ConfirmPrintTargets = confirmPrintTargetsMenuItem.Checked;
        SaveCurrentSettings();
    }

    private void ConfigureLargePrintWarning()
    {
        using var dialog = new LargePrintWarningSettingsDialog(settings.LargePrintPageWarningThreshold);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        settings.LargePrintPageWarningThreshold = dialog.Threshold;
        SaveCurrentSettings();
        UpdateLargePrintWarningMenuText();
    }

    private void UpdateLargePrintWarningMenuText()
    {
        if (largePrintWarningSettingsMenuItem is null) return;

        largePrintWarningSettingsMenuItem.Text = settings.LargePrintPageWarningThreshold > 0
            ? LocalizationService.T("main.menu.largePrintWarningWithValue", settings.LargePrintPageWarningThreshold)
            : LocalizationService.T("main.menu.largePrintWarningDisabled");
    }

    private void ConfigureCsvLoadSettings()
    {
        using var dialog = new CsvLoadSettingsDialog(settings.CsvMaxDataRowsPerFile, settings.PreviewRowLimit);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        settings.CsvMaxDataRowsPerFile = dialog.MaxDataRowsPerFile;
        settings.PreviewRowLimit = dialog.PreviewRowLimit;
        SaveCurrentSettings();
    }

    private void SetBackupCleanupStartupMode(BackupCleanupStartupMode mode)
    {
        settings.BackupCleanupStartupMode = mode;
        SaveCurrentSettings();
        UpdateBackupCleanupStartupMenu();
    }

    private void UpdateBackupCleanupStartupMenu()
    {
        if (backupCleanupStartupMenuItem is null) return;

        backupCleanupStartupMenuItem.Text = BackupCleanupStartupMenuText();
        if (backupCleanupStartupDisabledMenuItem is not null)
        {
            backupCleanupStartupDisabledMenuItem.Text = BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Disabled);
            backupCleanupStartupDisabledMenuItem.Checked = settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Disabled;
        }

        if (backupCleanupStartupAskMenuItem is not null)
        {
            backupCleanupStartupAskMenuItem.Text = BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Ask);
            backupCleanupStartupAskMenuItem.Checked = settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Ask;
        }

        if (backupCleanupStartupAutoMenuItem is not null)
        {
            backupCleanupStartupAutoMenuItem.Text = BackupCleanupStartupModeMenuText(BackupCleanupStartupMode.Auto);
            backupCleanupStartupAutoMenuItem.Checked = settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Auto;
        }
    }


}
