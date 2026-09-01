using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaGuiSpike;

public partial class CompletionDialog : Window
{
    public CompletionDialog() => InitializeComponent();

    public CompletionDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);
}
