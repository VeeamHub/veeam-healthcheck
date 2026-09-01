// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// Wraps System.Windows.MessageBox. Marshals to the UI thread only when
    /// the caller isn't already on it, matching the dispatcher-or-direct
    /// pattern ValidatePowerShellVersionMeetsVbrRequirement used inline
    /// before this extraction.
    /// </summary>
    internal sealed class WpfUiNotifier : IUiNotifier
    {
        public Task ShowErrorAsync(string message, string title)
        {
            Invoke(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string message, string title)
        {
            MessageBoxResult result = MessageBoxResult.No;
            Invoke(() => result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning));
            return Task.FromResult(result == MessageBoxResult.Yes);
        }

        private static void Invoke(Action action)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }
    }
}
