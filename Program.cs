using TagPrintStudio.Services;

namespace TagPrintStudio;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\TagPrintStudio.SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "TagPrint Studio is already running." + Environment.NewLine + "TagPrint Studio はすでに起動しています。",
                "TagPrint Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            Application.Run(new UI.MainForm());
        }
        catch (Exception exception)
        {
            ShowUnexpectedError(exception, "Application.Run");
        }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        ShowUnexpectedError(e.Exception, "Windows Forms UI thread");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logPath = TryWriteCrashLog(e.ExceptionObject, "Non-UI application domain");
        TryShowUnexpectedErrorMessage(e.ExceptionObject, logPath, e.IsTerminating);
    }

    private static void ShowUnexpectedError(Exception exception, string context)
    {
        var logPath = TryWriteCrashLog(exception, context);
        TryShowUnexpectedErrorMessage(exception, logPath, isTerminating: false);
    }

    private static string? TryWriteCrashLog(object exceptionObject, string context)
    {
        try
        {
            var paths = new AppStorageLocationService().ResolvePaths();
            return CrashLogService.WriteCrashLog(exceptionObject, context, paths);
        }
        catch
        {
            try
            {
                return CrashLogService.WriteCrashLog(exceptionObject, context);
            }
            catch
            {
                return null;
            }
        }
    }

    private static void TryShowUnexpectedErrorMessage(object exceptionObject, string? logPath, bool isTerminating)
    {
        try
        {
            var detail = exceptionObject is Exception exception
                ? exception.Message
                : exceptionObject.ToString();
            var message = "An unexpected error occurred."
                + Environment.NewLine
                + "予期しないエラーが発生しました。"
                + Environment.NewLine
                + Environment.NewLine
                + (string.IsNullOrWhiteSpace(detail) ? "No additional error detail was available. / 追加のエラー詳細はありません。" : detail)
                + Environment.NewLine
                + Environment.NewLine
                + (string.IsNullOrWhiteSpace(logPath)
                    ? "A crash log could not be written. / クラッシュログを保存できませんでした。"
                    : $"A crash log was saved to / クラッシュログの保存先:{Environment.NewLine}{logPath}")
                + (isTerminating
                    ? Environment.NewLine + Environment.NewLine + "The application will now close. / アプリケーションを終了します。"
                    : string.Empty);

            MessageBox.Show(
                message,
                "Unexpected error / 予期しないエラー - TagPrint Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Avoid recursive failures while handling an unexpected exception.
        }
    }
}
