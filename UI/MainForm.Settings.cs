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

    private void ConfigureAppDataLocation()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = paths.AppDataRoot
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedRoot = dialog.SelectedPath.Trim();
        if (string.IsNullOrWhiteSpace(selectedRoot))
        {
            return;
        }

        var normalizedSelected = Path.GetFullPath(selectedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCurrent = Path.GetFullPath(paths.AppDataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedSelected, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "選択した場所は、現在のアプリデータ保存場所と同じです。"
                    : "The selected location is already the current app data storage location.",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var copyPrompt = MessageBox.Show(
            LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? $"新しいアプリデータ保存場所を設定します。\n\n{selectedRoot}\n\n現在の layouts / presets / recent / settings をコピーしますか？\n\nはい: コピーして切り替え\nいいえ: 空の保存先として切り替え\nキャンセル: 変更しない"
                : $"Set a new app data storage location:\n\n{selectedRoot}\n\nCopy the current layouts / presets / recent / settings?\n\nYes: copy current data and switch\nNo: switch to an empty location\nCancel: keep the current location",
            LocalizationService.T("common.confirm"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (copyPrompt == DialogResult.Cancel)
        {
            return;
        }

        try
        {
            appStorageLocationService.PrepareLocation(paths, selectedRoot, copyPrompt == DialogResult.Yes);
            appStorageLocationService.UseCustomLocation(selectedRoot);

            var restartNow = MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "アプリデータ保存場所を更新しました。反映するには再起動が必要です。\n\n今すぐ再起動しますか？"
                    : "The app data storage location has been updated. A restart is required to apply it.\n\nRestart now?",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (restartNow == DialogResult.Yes)
            {
                Application.Restart();
                Close();
            }
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "アプリデータ保存場所エラー" : "App Data Location Error",
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "アプリデータ保存場所の変更中にエラーが発生しました。"
                    : "An error occurred while changing the app data storage location.",
                ex);
        }
    }

    private void OpenAppDataLocation()
    {
        Directory.CreateDirectory(paths.AppDataRoot);
        Process.Start(new ProcessStartInfo
        {
            FileName = paths.AppDataRoot,
            UseShellExecute = true
        });
    }

    private void ResetAppDataLocation()
    {
        if (appStorageLocationService.IsUsingDefaultLocation())
        {
            MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "アプリデータ保存場所は、すでに既定の場所です。"
                    : "The app data storage location is already using the default location.",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "アプリデータ保存場所を既定に戻します。反映するには再起動が必要です。\n\n続行しますか？"
                    : "Reset the app data storage location to the default. A restart is required to apply it.\n\nDo you want to continue?",
                LocalizationService.T("common.confirm"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            appStorageLocationService.ResetToDefaultLocation();
            var restartNow = MessageBox.Show(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "既定のアプリデータ保存場所に戻しました。\n\n今すぐ再起動しますか？"
                    : "The app data storage location was reset to the default.\n\nRestart now?",
                LocalizationService.T("common.appTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (restartNow == DialogResult.Yes)
            {
                Application.Restart();
                Close();
            }
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(
                LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "アプリデータ保存場所エラー" : "App Data Location Error",
                LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? "アプリデータ保存場所のリセット中にエラーが発生しました。"
                    : "An error occurred while resetting the app data storage location.",
                ex);
        }
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
                ? "設定をインポートすると、ZIPに含まれる layouts / presets / recent / settings が現在のデータへ反映され、画面を再読み込みします。続行しますか？"
                : "Importing settings will apply the layouts / presets / recent / settings folders contained in the ZIP and reload the app state. Do you want to continue?",
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
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.LastBackupCleanupFolderPath = dialog.FolderPath;
            settings.BackupCleanupRetentionDays = dialog.RetentionDays;
            settings.BackupCleanupMaxCountPerCsv = dialog.MaxCountPerCsv;
            SaveCurrentSettings();
        }
    }

    private void RunStartupBackupCleanupIfNeeded()
    {
        if (startupBackupCleanupChecked)
        {
            return;
        }

        startupBackupCleanupChecked = true;
        if (settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Disabled)
        {
            return;
        }

        var folderPath = ResolveStartupBackupCleanupFolder();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        try
        {
            var preview = backupCleanupService.Preview(
                folderPath,
                settings.BackupCleanupRetentionDays,
                settings.BackupCleanupMaxCountPerCsv);
            if (preview.DeleteCount <= 0)
            {
                return;
            }

            if (settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Ask)
            {
                var message = LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"起動時チェックで、整理対象のバックアップが {preview.DeleteCount} 件見つかりました。今すぐ整理しますか？"
                    : $"{preview.DeleteCount} backup file(s) can be cleaned up on startup. Run cleanup now?";
                using var dialog = new StartupBackupCleanupDialog(
                    BackupCleanupStartupDialogTitle(),
                    message,
                    preview.FolderPath,
                    preview.DeleteCount,
                    preview.DeleteBytes,
                    preview.Files.Where(file => file.DeleteCandidate).ToList());
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (dialog.EnableAutoRunChecked)
                {
                    settings.BackupCleanupStartupMode = BackupCleanupStartupMode.Auto;
                    SaveCurrentSettings();
                    UpdateBackupCleanupStartupMenu();
                }
                else if (dialog.DisableStartupCleanupChecked)
                {
                    settings.BackupCleanupStartupMode = BackupCleanupStartupMode.Disabled;
                    SaveCurrentSettings();
                    UpdateBackupCleanupStartupMenu();
                }
            }

            var result = backupCleanupService.Cleanup(preview);
            if (settings.BackupCleanupStartupMode == BackupCleanupStartupMode.Ask)
            {
                var message = LocalizationService.CurrentLanguage == AppLanguage.Japanese
                    ? $"バックアップを {result.DeletedCount} 件整理しました。"
                    : $"Cleaned up {result.DeletedCount} backup file(s).";
                MessageBox.Show(
                    message,
                    BackupCleanupStartupDialogTitle(),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ShowExceptionDetails(
                BackupCleanupStartupErrorTitle(),
                BackupCleanupStartupErrorSummary(),
                ex);
        }
    }

    private string ResolveStartupBackupCleanupFolder()
    {
        if (!string.IsNullOrWhiteSpace(settings.LastBackupCleanupFolderPath))
        {
            return settings.LastBackupCleanupFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(settings.StartupCsvFolderPath))
        {
            return settings.StartupCsvFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(settings.StartupCsvPath))
        {
            return Path.GetDirectoryName(settings.StartupCsvPath) ?? "";
        }

        return "";
    }

    private static string BackupCleanupStartupDialogTitle() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "バックアップ整理"
        : "Backup Cleanup";

    private static string BackupCleanupStartupErrorTitle() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "起動時バックアップ整理エラー"
        : "Startup Backup Cleanup Error";

    private static string BackupCleanupStartupErrorSummary() => LocalizationService.CurrentLanguage == AppLanguage.Japanese
        ? "起動時のバックアップ整理中にエラーが発生しました。"
        : "An error occurred while cleaning up backups on startup.";


}
