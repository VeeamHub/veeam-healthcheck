// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Threading.Tasks;
using Avalonia.Threading;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// Avalonia has no built-in MessageBox and Window.ShowDialog&lt;T&gt; is
    /// async-only. Dispatcher.UIThread.InvokeAsync(Func&lt;Task&lt;T&gt;&gt;) correctly
    /// resolves to Task&lt;T&gt; (not Task&lt;Task&lt;T&gt;&gt;), and blocking on it from a
    /// non-UI thread - which is exactly what IUiNotifier.ShowError/Confirm's
    /// default-interface-method wrappers do - works with no deadlock (verified
    /// in a throwaway spike branch).
    /// </summary>
    internal sealed class AvaloniaUiNotifier : IUiNotifier
    {
        public async Task ShowErrorAsync(string message, string title)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new NotifierDialog(message, title, isConfirm: false);
                await dialog.ShowDialog<bool>(AvaloniaHost.MainWindow);
            });
        }

        public async Task<bool> ConfirmAsync(string message, string title)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new NotifierDialog(message, title, isConfirm: true);
                return await dialog.ShowDialog<bool>(AvaloniaHost.MainWindow);
            });
        }
    }
}
