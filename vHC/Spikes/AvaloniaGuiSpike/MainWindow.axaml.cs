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
    public MainWindow() => InitializeComponent();

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
