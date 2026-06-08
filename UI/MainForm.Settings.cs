using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;
public sealed partial class MainForm : Form
{
    private void SaveCurrentSettings()
    {
        if (loadingSettings || outputFolderBox is null || outputScopeCombo is null || outputDpiCombo is null || printScalingCombo is null || printSortCombo is null || printSortFieldCombo is null || printSortValueTypeCombo is null) return;

        settings.LastOutputFolder = outputFolderBox.Text;
        if (!string.IsNullOrWhiteSpace(outputFolderBox.Text))
        {
            RememberRecentPath(settings.RecentOutputFolders, outputFolderBox.Text);
            RefreshRecentOutputCombo();
        }
        settings.LastOutputDpi = SelectedOutputDpi();
        settings.LastOutputScope = CurrentOutputScope();
        settings.LastPrintScalingMode = SelectedPrintScalingMode();
        settings.LastPrintSortMode = CurrentPrintSortMode();
        settings.LastPrintSortFieldId = SelectedPrintSortFieldId();
        settings.LastPrintSortValueType = CurrentPrintSortValueType();
        settings.LastPrintOffsetXmm = (double)printOffsetXBox.Value;
        settings.LastPrintOffsetYmm = (double)printOffsetYBox.Value;

        if (presetCombo.SelectedItem is FileComboItem preset)
        {
            settings.LastPresetFile = Path.GetFileName(preset.Path);
        }

        if (layoutCombo.SelectedItem is FileComboItem layout)
        {
            settings.LastLayoutFile = Path.GetFileName(layout.Path);
        }

        JsonFile.Save(Path.Combine(paths.Settings, "appsettings.json"), settings);
    }

    private static bool RememberRecentPath(List<RecentFileEntry> entries, string path)
    {
        var normalized = path.Trim();
        var existing = entries.FirstOrDefault(entry => string.Equals(entry.Path, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.LastOpenedAt = DateTime.Now;
        }
        else
        {
            entries.Add(new RecentFileEntry { Path = normalized, LastOpenedAt = DateTime.Now });
        }

        entries.Sort((left, right) => right.LastOpenedAt.CompareTo(left.LastOpenedAt));
        if (entries.Count > 10)
        {
            entries.RemoveRange(10, entries.Count - 10);
        }

        return true;
    }


    private void ConfigureStartupCsv()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Multiselect = false,
            FileName = settings.StartupCsvPath
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        settings.StartupCsvPath = dialog.FileName;
        SaveCurrentSettings();
        MessageBox.Show(LocalizationService.T("main.message.startupCsvConfigured", dialog.FileName), LocalizationService.T("common.appTitle"));
    }

    private void OpenStartupCsvLocation()
    {
        if (string.IsNullOrWhiteSpace(settings.StartupCsvPath))
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "起動時CSVが設定されていません。"
                    : "Startup CSV is not configured.",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!File.Exists(settings.StartupCsvPath))
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"起動時CSVが見つかりません。\n{settings.StartupCsvPath}"
                    : $"Startup CSV was not found.\n{settings.StartupCsvPath}",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{settings.StartupCsvPath}\"",
            UseShellExecute = true
        });
    }

    private void ClearStartupCsv()
    {
        if (string.IsNullOrWhiteSpace(settings.StartupCsvPath))
        {
            MessageBox.Show(LocalizationService.T("main.message.startupCsvNotConfigured"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        settings.StartupCsvPath = "";
        SaveCurrentSettings();
        MessageBox.Show(LocalizationService.T("main.message.startupCsvCleared"), LocalizationService.T("common.appTitle"));
    }

    private void ConfigureStartupFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = settings.StartupCsvFolderPath
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        settings.StartupCsvFolderPath = dialog.SelectedPath;
        ConfirmBackupCleanupFolderFromStartupFolder(dialog.SelectedPath);
        SaveCurrentSettings();
        MessageBox.Show(LocalizationService.T("main.message.startupFolderConfigured", dialog.SelectedPath), LocalizationService.T("common.appTitle"));
    }

    private void ConfirmBackupCleanupFolderFromStartupFolder(string startupFolder)
    {
        if (string.IsNullOrWhiteSpace(startupFolder))
        {
            return;
        }

        if (string.Equals(settings.LastBackupCleanupFolderPath, startupFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = LocalizationService.CurrentLanguage == AppLanguage.Japanese
            ? $"バックアップファイル整理の対象フォルダーを、起動時フォルダーに変更しますか？\n\n{startupFolder}"
            : $"Set the backup cleanup target folder to the startup folder?\n\n{startupFolder}";
        if (MessageBox.Show(message, LocalizationService.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        settings.LastBackupCleanupFolderPath = startupFolder;
    }

    private void OpenStartupFolder()
    {
        if (string.IsNullOrWhiteSpace(settings.StartupCsvFolderPath))
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "起動時フォルダーが設定されていません。"
                    : "Startup folder is not configured.",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!Directory.Exists(settings.StartupCsvFolderPath))
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"起動時フォルダーが見つかりません。\n{settings.StartupCsvFolderPath}"
                    : $"Startup folder was not found.\n{settings.StartupCsvFolderPath}",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = settings.StartupCsvFolderPath,
            UseShellExecute = true
        });
    }

    private void ClearStartupFolder()
    {
        if (string.IsNullOrWhiteSpace(settings.StartupCsvFolderPath))
        {
            MessageBox.Show(LocalizationService.T("main.message.startupFolderNotConfigured"), LocalizationService.T("common.appTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        settings.StartupCsvFolderPath = "";
        SaveCurrentSettings();
        MessageBox.Show(LocalizationService.T("main.message.startupFolderCleared"), LocalizationService.T("common.appTitle"));
    }

    private void OpenSettingsLocation()
    {
        Directory.CreateDirectory(paths.Settings);
        var settingsPath = Path.Combine(paths.Settings, "appsettings.json");

        if (File.Exists(settingsPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{settingsPath}\"",
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = paths.Settings,
            UseShellExecute = true
        });
    }

    private void BackupSettingsArchive()
    {
        SaveCurrentSettings();
        var outputFolder = ResolveOutputFolder();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show(LocalizationService.T("main.message.outputFolderRequired"), LocalizationService.T("common.appTitle"));
            return;
        }

        try
        {
            Directory.CreateDirectory(outputFolder);

            var zipPath = Path.Combine(outputFolder, $"TagPrintStudio_SettingsBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            settingsArchiveService.CreateBackup(zipPath);

            RememberRecentOutputFolder(outputFolder);
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"設定バックアップを保存しました。\n{zipPath}"
                    : $"Saved the settings backup.\n{zipPath}",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "設定バックアップエラー" : "Settings Backup Error",
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "設定バックアップ ZIP の作成中にエラーが発生しました。"
                    : "An error occurred while creating the settings backup ZIP.",
                ex);
        }
    }

    private void ImportSettingsArchive()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "ZIP files (*.zip)|*.zip",
            Multiselect = false,
            InitialDirectory = Directory.Exists(ResolveOutputFolder()) ? ResolveOutputFolder() : paths.DefaultOutput
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var confirm = MessageBox.Show(
            LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "設定をインポートすると、現在の layouts / presets / recent / settings が上書きされ、画面を再読み込みします。続行しますか？"
                : "Importing settings will overwrite the current layouts / presets / recent / settings and reload the app state. Do you want to continue?",
            LocalizationService.T("common.appTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        try
        {
            settingsArchiveService.ImportBackup(dialog.FileName);
            InitializeData();
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"設定をインポートしました。\n{dialog.FileName}"
                    : $"Imported settings from:\n{dialog.FileName}",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "設定インポートエラー" : "Settings Import Error",
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "設定バックアップ ZIP の読み込み中にエラーが発生しました。"
                    : "An error occurred while importing the settings backup ZIP.",
                ex);
        }
    }

    private void CleanupBackupFiles()
    {
        SaveCurrentSettings();
        var initialFolder = !string.IsNullOrWhiteSpace(settings.LastBackupCleanupFolderPath)
            ? settings.LastBackupCleanupFolderPath
            : ResolveOutputFolder();

        using var dialog = new BackupCleanupDialog(
            backupCleanupService,
            initialFolder,
            settings.BackupCleanupRetentionDays,
            settings.BackupCleanupMaxCountPerCsv);
        dialog.ShowDialog(this);

        settings.LastBackupCleanupFolderPath = dialog.FolderPath;
        settings.BackupCleanupRetentionDays = dialog.RetentionDays;
        settings.BackupCleanupMaxCountPerCsv = dialog.MaxCountPerCsv;
        SaveCurrentSettings();
    }


}
