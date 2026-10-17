// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            // Must run before VhcGui is constructed below: its constructor reads
            // Application.Current.RequestedThemeVariant to set the theme-toggle
            // button's initial label.
            RequestedThemeVariant = CThemePreference.ToVariant(CAppSettings.Get().ThemePreference);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new VhcGui();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
