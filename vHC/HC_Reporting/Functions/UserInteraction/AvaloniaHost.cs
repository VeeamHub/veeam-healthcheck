// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Controls;

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
    }
}
