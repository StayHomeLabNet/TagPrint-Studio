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
                "TagPrint Studio is already running.",
                "TagPrint Studio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }
}
