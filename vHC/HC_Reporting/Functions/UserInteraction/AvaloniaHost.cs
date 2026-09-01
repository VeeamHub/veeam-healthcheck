// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Controls;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// Holds the main window reference so AvaloniaUiNotifier/AvaloniaCredentialPrompter
    /// can own their dialogs (Window.ShowDialog requires an owner). Set once in
    /// App.axaml.cs's OnFrameworkInitializationCompleted.
    /// </summary>
    internal static class AvaloniaHost
    {
        public static Window MainWindow { get; set; }
    }
}
