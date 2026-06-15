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
                LocalizationService.T("program.singleInstance.message"),
                LocalizationService.T("common.appTitle"),
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
            var message = LocalizationService.T("program.unexpectedError.message")
                + Environment.NewLine
                + Environment.NewLine
                + (string.IsNullOrWhiteSpace(detail) ? LocalizationService.T("program.unexpectedError.noDetail") : detail)
                + Environment.NewLine
                + Environment.NewLine
                + (string.IsNullOrWhiteSpace(logPath)
                    ? LocalizationService.T("program.unexpectedError.crashLogWriteFailed")
                    : LocalizationService.T("program.unexpectedError.crashLogSaved", logPath))
                + (isTerminating
                    ? Environment.NewLine + Environment.NewLine + LocalizationService.T("program.unexpectedError.terminating")
                    : string.Empty);

            MessageBox.Show(
                message,
                LocalizationService.T("program.unexpectedError.title"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Avoid recursive failures while handling an unexpected exception.
        }
    }
}
