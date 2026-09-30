// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// Holds the main window reference so AvaloniaUiNotifier/AvaloniaCredentialPrompter
    /// can own their dialogs (Window.ShowDialog requires an owner). Set once in
    /// VhcGui's constructor, before App.axaml.cs finishes constructing the window.
    /// </summary>
    internal static class AvaloniaHost
    {
        public static Window MainWindow { get; set; }

        /// <summary>
        /// The window a new dialog should be owned by: the most recently opened
        /// visible window, so a notifier raised while CredentialPromptWindow or
        /// ManageServersDialog is up is modal to THAT dialog. Owning everything with
        /// <see cref="MainWindow"/> leaves the dialog underneath interactive (Enter
        /// stacks error dialogs, Cancel can resolve the caller mid-confirm). Must be
        /// called on the UI thread, and before the new dialog is constructed/shown
        /// so the new dialog is not its own owner. Falls back to
        /// <see cref="MainWindow"/> when no desktop lifetime is available.
        /// </summary>
        public static Window CurrentOwner()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.LastOrDefault(w => w.IsVisible) ?? MainWindow;
            }

            return MainWindow;
        }
    }
}
