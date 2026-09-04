// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Styling;

namespace VeeamHealthCheck.Startup;

// Single source of truth for the "System"/"Dark"/"Light" <-> ThemeVariant mapping.
// Kept out of CAppSettings.cs deliberately: that file has no Avalonia dependency
// today, and VhcXTests.CrossPlatform.csproj hand-picks individual source files to
// compile without referencing Avalonia - adding an Avalonia.Styling-typed method
// there would create a reason for that file list to need it.
internal static class CThemePreference
{
    internal const string Default = "System";

    internal static ThemeVariant ToVariant(string preference) => preference switch
    {
        "Dark" => ThemeVariant.Dark,
        "Light" => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    internal static string FromVariant(ThemeVariant variant) =>
        variant == ThemeVariant.Dark ? "Dark" :
        variant == ThemeVariant.Light ? "Light" : Default;
}
