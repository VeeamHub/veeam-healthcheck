// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Text.Json;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup;

public class AppSettings
{
    public string ThemePreference { get; set; } = "System";
}

public static class CAppSettings
{
    // Internal + settable so tests can point this at an isolated temp path instead
    // of the real %APPDATA%/VeeamHealthCheck/settings.json. Production code never
    // sets this; the default preserves real behavior exactly. Mirrors
    // CredentialStore.StorePath's own test seam.
    internal static string StorePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VeeamHealthCheck", "settings.json");

    public static AppSettings Get()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Warning($"App settings file is malformed or unreadable, using defaults. Error: {ex.Message}");
            return new AppSettings();
        }
    }

    public static void Set(string themePreference)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var settings = Get();
            settings.ThemePreference = themePreference;
            File.WriteAllText(StorePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to persist app settings: {ex.Message}");
        }
    }
}
