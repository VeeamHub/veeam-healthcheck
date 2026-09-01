using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvaloniaGuiSpike;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Loaded += (s, e) => Task.Run(VerifyBlockingDialogPattern);
    }

    // Verifies a claim the real migration plan depends on: does
    // Dispatcher.UIThread.InvokeAsync(Func<Task<TResult>>) resolve to the
    // Task<TResult>-returning overload (correct) or does it bind the wrong
    // generic overload and hand back Task<Task<TResult>> (a silent-bug trap -
    // .GetAwaiter().GetResult() on that returns an unstarted/unawaited inner
    // Task, not the real result)? Task.Delay stands in for "user is looking
    // at a real dialog" - if this deadlocked, the process would hang forever
    // instead of printing PASS/FAIL, which is itself part of the proof.
    private void VerifyBlockingDialogPattern()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Task<bool> task = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(800);
            return true;
        });

        // Confirms the static type really is Task<bool>, not Task<Task<bool>> -
        // this line would fail to compile otherwise (CS0266/no implicit conversion).
        bool result = task.GetAwaiter().GetResult();

        sw.Stop();
        bool timingLooksReal = sw.ElapsedMilliseconds >= 700; // proves it actually awaited the delay, not a fire-and-forget no-op
        Console.WriteLine($"[VerifyBlockingDialogPattern] result={result} elapsedMs={sw.ElapsedMilliseconds} " +
                           (result && timingLooksReal ? "PASS" : "FAIL"));
    }

    private void PathBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        Console.WriteLine("Path changed to: " + PathBox.Text);
    }

    // Mirrors VhcGui.run_Click -> Run(): work happens off the UI thread via
    // Task.Run, then marshals back with the dispatcher (Avalonia's analog of
    // WPF's Dispatcher.Invoke), exactly like VhcGui.hideProgressBar()/
    // UpdateCollectionStatusText() do today.
    private void RunButton_Click(object? sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        ProgressBarControl.IsVisible = true;
        StatusText.IsVisible = false;

        Task.Run(() =>
        {
            Thread.Sleep(1500); // simulate collection work
            bool hadWarnings = new Random().Next(2) == 0;

            Dispatcher.UIThread.Post(async () =>
            {
                ProgressBarControl.IsVisible = false;
                StatusText.IsVisible = true;

                if (hadWarnings)
                {
                    StatusText.Text = "Collection complete — 1 collector warning(s)";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xf0, 0xad, 0x4e));
                }
                else
                {
                    StatusText.Text = "Collection complete";
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x5c, 0xb8, 0x5c));
                }

                RunButton.IsEnabled = true;

                // WPF's MessageBox.Show(...) blocks synchronously. Avalonia has no
                // built-in MessageBox and Window.ShowDialog<T> is async-only, so
                // every MessageBox.Show call site in CClientFunctions/CCollections/
                // CredsHandler needs an await, not just a type swap.
                var dialog = new CompletionDialog(hadWarnings
                    ? "1 collector reported errors. The report may have incomplete sections."
                    : "The health check completed successfully.");
                await dialog.ShowDialog<bool>(this);
            });
        });
    }
}
