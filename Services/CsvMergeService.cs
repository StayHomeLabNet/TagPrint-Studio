using Microsoft.VisualBasic.FileIO;
using TagPrintStudio.Enums;
using TagPrintStudio.Models;

namespace TagPrintStudio.Services;

public interface ICsvMergeService
{
    CsvMergePreview Preview(CsvMergeOptions options, PresetFile preset);
    CsvMergeApplyResult Apply(CsvMergePreview preview, PresetFile preset);
}

public sealed class CsvMergeService : ICsvMergeService
{
    private readonly ICsvLoaderService csvLoader = new CsvLoaderService();

    public CsvMergePreview Preview(CsvMergeOptions options, PresetFile preset)
    {
        if (string.IsNullOrWhiteSpace(options.FolderPath) || !Directory.Exists(options.FolderPath))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "対象フォルダーが見つかりません。"
                : "The target folder could not be found.");
        }

        if (string.IsNullOrWhiteSpace(options.ParentCsvPath))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVを選択してください。"
                : "Please choose a parent CSV.");
        }

        var csvPaths = Directory.GetFiles(options.FolderPath, "*.csv")
            .Where(path => !CsvFileUtility.IsPrintQueueCsv(path))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (csvPaths.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "対象フォルダーにCSVがありません。"
                : "No CSV files were found in the target folder.");
        }

        if (!csvPaths.Contains(options.ParentCsvPath, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVが対象フォルダー内にありません。"
                : "The parent CSV is not inside the target folder.");
        }

        var loadResult = csvLoader.LoadFiles(csvPaths, preset, options.HasHeaderRow);
        var files = loadResult.Files
            .OrderBy(file => file.FilePath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var parent = files.FirstOrDefault(file => string.Equals(file.FilePath, options.ParentCsvPath, StringComparison.OrdinalIgnoreCase));
        if (parent is null || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVの読み込みに失敗しました。"
                : "The parent CSV could not be loaded.");
        }

        var items = new List<CsvMergeCandidate>();
        var appendFilePaths = new List<string>();
        var appendRowCount = 0;
        var skipFileCount = 0;

        foreach (var file in files)
        {
            var isParent = string.Equals(file.FilePath, options.ParentCsvPath, StringComparison.OrdinalIgnoreCase);
            var candidate = new CsvMergeCandidate
            {
                FilePath = file.FilePath,
                FileName = file.FileName,
                IsParent = isParent,
                RowCount = file.RowCount,
                HeaderMatches = isParent || HeadersMatch(parent.HeaderRow, file.HeaderRow),
                Status = MergeStatus(file, isParent, parent),
                Note = MergeNote(file, isParent, parent)
            };

            if (!isParent && CanUseForMerge(file) && candidate.HeaderMatches)
            {
                appendFilePaths.Add(file.FilePath);
                appendRowCount += file.RowCount;
            }
            else if (!isParent)
            {
                skipFileCount++;
            }

            items.Add(candidate);
        }

        return new CsvMergePreview
        {
            FolderPath = options.FolderPath,
            ParentCsvPath = options.ParentCsvPath,
            ParentFileName = parent.FileName,
            HasHeaderRow = options.HasHeaderRow,
            DeleteMergedSourceFiles = options.DeleteMergedSourceFiles,
            DeleteBakFileAfterMerge = options.DeleteBakFileAfterMerge,
            Items = items,
            AppendFilePaths = appendFilePaths,
            AppendFileCount = appendFilePaths.Count,
            AppendRowCount = appendRowCount,
            SkipFileCount = skipFileCount,
            ResultRowCount = parent.RowCount + appendRowCount
        };
    }

    public CsvMergeApplyResult Apply(CsvMergePreview preview, PresetFile preset)
    {
        if (preview.AppendFilePaths.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "追記対象のCSVがありません。"
                : "There are no CSV files to append.");
        }

        var targetPaths = new[] { preview.ParentCsvPath }.Concat(preview.AppendFilePaths).ToList();
        var loadResult = csvLoader.LoadFiles(targetPaths, preset, preview.HasHeaderRow);
        var files = loadResult.Files.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        if (!files.TryGetValue(preview.ParentCsvPath, out var parent) || !CanUseForMerge(parent))
        {
            throw new InvalidOperationException(LocalizationService.CurrentLanguage == AppLanguage.Japanese
                ? "親CSVの読み込みに失敗しました。"
                : "The parent CSV could not be loaded.");
        }

        foreach (var childPath in preview.AppendFilePaths)
        {
            if (!files.TryGetValue(childPath, out var child) || !CanUseForMerge(child))
            {
                throw new InvalidOperationException(string.Format(
                    LocalizationService.CurrentLanguage == AppLanguage.Japanese
                        ? "子CSVの読み込みに失敗しました: {0}"
                        : "A child CSV could not be loaded: {0}",
                    Path.GetFileName(childPath)));
            }

            if (!HeadersMatch(parent.HeaderRow, child.HeaderRow))
            {
                throw new InvalidOperationException(string.Format(
                    LocalizationService.CurrentLanguage == AppLanguage.Japanese
                        ? "ヘッダーが一致しないためマージできません: {0}"
                        : "Cannot merge because headers do not match: {0}",
                    Path.GetFileName(childPath)));
            }
        }

        var backupPath = CsvBackupUtility.CreateTimestampedBackup(preview.ParentCsvPath);

        foreach (var childPath in preview.AppendFilePaths)
        {
            parent.Rows.AddRange(files[childPath].Rows.Select(row => row.ToArray()));
        }

        WriteCsvFile(parent, preview.HasHeaderRow);

        var deletedSourceFileCount = 0;
        if (preview.DeleteMergedSourceFiles)
        {
            foreach (var childPath in preview.AppendFilePaths)
            {
                if (!File.Exists(childPath))
                {
                    continue;
                }

                FileSystem.DeleteFile(
                    childPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.DoNothing);
                deletedSourceFileCount++;
            }
        }

        var deletedBakFileCount = 0;
        if (preview.DeleteBakFileAfterMerge && File.Exists(backupPath))
        {
            FileSystem.DeleteFile(
                backupPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.DoNothing);
            deletedBakFileCount = 1;
        }

        return new CsvMergeApplyResult
        {
            ParentCsvPath = preview.ParentCsvPath,
            BackupPath = backupPath,
            AppendedFileCount = preview.AppendFileCount,
            AppendedRowCount = preview.AppendRowCount,
            DeletedSourceFileCount = deletedSourceFileCount,
            DeletedBakFileCount = deletedBakFileCount
        };
    }

    private static bool HeadersMatch(string[]? left, string[]? right)
    {
        if (left is null || right is null) return false;
        return left.Length == right.Length && left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool CanUseForMerge(CsvFileItem file)
    {
        return file.Status != CsvFileStatus.LoadFailed && file.HeaderRow is { Length: > 0 };
    }

    private static string MergeStatus(CsvFileItem file, bool isParent, CsvFileItem parent)
    {
        if (isParent)
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "親CSV" : "Parent";
        }

        if (!CanUseForMerge(file))
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "読込エラー" : "Load Error";
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "追記対象" : "Append")
            : (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "スキップ" : "Skip");
    }

    private static string MergeNote(CsvFileItem file, bool isParent, CsvFileItem parent)
    {
        if (isParent)
        {
            return LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "親CSV" : "Parent CSV";
        }

        if (!CanUseForMerge(file))
        {
            return string.IsNullOrWhiteSpace(file.Note)
                ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "読み込みに失敗しました" : "The file could not be loaded.")
                : file.Note;
        }

        return HeadersMatch(parent.HeaderRow, file.HeaderRow)
            ? (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ヘッダー一致" : "Headers match")
            : (LocalizationService.CurrentLanguage == AppLanguage.Japanese ? "ヘッダー不一致" : "Headers do not match");
    }

    private static void WriteCsvFile(CsvFileItem file, bool hasHeaderRow)
    {
        var lines = new List<string>();
        if (hasHeaderRow && file.HeaderRow is { Length: > 0 })
        {
            lines.Add(ToCsvLine(file.HeaderRow));
        }

        lines.AddRange(file.Rows.Select(ToCsvLine));
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        File.WriteAllText(file.FilePath, content, CsvFileUtility.EncodingForCsv(file.EncodingName));
    }

    private static string ToCsvLine(IEnumerable<string> cells) => string.Join(",", cells.Select(EscapeCsvCell));

    private static string EscapeCsvCell(string? value)
    {
        var text = value ?? "";
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\r') && !text.Contains('\n'))
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
