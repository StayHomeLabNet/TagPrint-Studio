using TagPrintStudio.Models;
using TagPrintStudio.Services;

namespace TagPrintStudio.UI;

internal sealed class OutputProgressDialog : Form
{
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Label statusLabel;
    private readonly Label detailLabel;
    private readonly ProgressBar progressBar;
    private readonly Button cancelButton;
    private readonly TaskCompletionSource closedCompletion = new();

    public OutputProgressDialog(string title, string initialMessage, CancellationTokenSource cancellationTokenSource)
    {
        this.cancellationTokenSource = cancellationTokenSource;
        Text = title;
        Width = 420;
        Height = 170;
        MinimumSize = new Size(420, 170);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = Color.White;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(18, 16, 18, 14),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = initialMessage,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        detailLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(75, 85, 99),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Style = ProgressBarStyle.Continuous
        };
        cancelButton = DialogButton(LocalizationService.T("common.cancel"));
        cancelButton.Click += (_, _) =>
        {
            if (cancelButton.Tag as string == "close")
            {
                Close();
                return;
            }

            cancelButton.Enabled = false;
            statusLabel.Text = LocalizationService.T("output.progress.canceling");
            cancellationTokenSource.Cancel();
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        buttons.Controls.Add(cancelButton);

        root.Controls.Add(statusLabel, 0, 0);
        root.Controls.Add(detailLabel, 0, 1);
        root.Controls.Add(progressBar, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        FormClosed += (_, _) => closedCompletion.TrySetResult();
    }

    public void Report(OutputProgress progress)
    {
        var total = Math.Max(0, progress.Total);
        var current = Math.Max(0, progress.Current);
        var operation = progress.Operation switch
        {
            "Print" => LocalizationService.T("output.progress.print"),
            "PDF" => LocalizationService.T("output.progress.pdf"),
            "PNG" => LocalizationService.T("output.progress.png"),
            _ => progress.Operation
        };

        statusLabel.Text = total > 0
            ? LocalizationService.T("output.progress.page", operation, Math.Min(current, total), total)
            : operation;
        detailLabel.Text = progress.Detail;

        if (total <= 0)
        {
            progressBar.Style = ProgressBarStyle.Marquee;
            return;
        }

        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, (int)Math.Round(current / (double)total * progressBar.Maximum)));
    }

    public void ShowFailure(Exception exception)
    {
        statusLabel.Text = LocalizationService.T("output.progress.failed");
        detailLabel.Text = exception is OutputPageException pageException
            ? LocalizationService.T("output.progress.failedPage", pageException.PageNumber, pageException.TotalPages)
            : exception.Message;
        progressBar.Style = ProgressBarStyle.Continuous;
        cancelButton.Tag = "close";
        cancelButton.Text = LocalizationService.T("common.close");
        cancelButton.Enabled = true;
    }

    public Task WaitForCloseAsync() => closedCompletion.Task;

    private static Button DialogButton(string text) => new()
    {
        Text = text,
        Width = 96,
        Height = 32,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(31, 41, 55),
        Margin = new Padding(6, 0, 0, 0)
    };
}
