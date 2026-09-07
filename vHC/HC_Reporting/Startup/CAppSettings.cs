// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup;

public class AppSettings
{
    public string ThemePreference { get; set; } = "System";

    // Deliberately null rather than an empty list, and deliberately un-annotated.
    //
    // null means "never seeded" and triggers CAppSettings.LoadOrSeedServers' one-time
    // seed from the credential store. Any non-null value - INCLUDING an empty list -
    // is authoritative and means the user's own list, even if they emptied it. Without
    // that distinction, "removed everything" and "fresh upgrade" are indistinguishable
    // and the list resurrects itself on the next launch.
    //
    // No `?` annotation: the csproj sets no <Nullable> and this file has no #nullable
    // context, so List<string>? would emit CS8632 (which NoWarn's CA-only list does
    // not cover). A plain List<string> defaulting to null deserializes identically for
    // an absent JSON property. The explicit `= null` is redundant to the compiler and
    // kept purely as documentation, because the null default carries meaning.
    public List<string> Servers { get; set; } = null;
}

// Distinguishes "there is no settings file yet" from "there is one but we could not
// read it". Get() collapses both to defaults, which is fine for a theme preference
// but NOT for Servers: treating a transient read failure as "never seeded" would
// re-seed and resurrect servers the user had removed. Internal because it is a
// detail of LoadOrSeedServers, not part of the public settings surface.
internal enum SettingsLoadResult
{
    Loaded,
    Absent,
    Unreadable,
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

    internal static SettingsLoadResult TryLoad(out AppSettings settings)
    {
        settings = new AppSettings();

        try
        {
            if (!File.Exists(StorePath))
            {
                return SettingsLoadResult.Absent;
            }

            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                // An empty file is indistinguishable from a not-yet-written one and
                // carries no user intent, so treat it as absent rather than unreadable.
                return SettingsLoadResult.Absent;
            }

            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded == null)
            {
                return SettingsLoadResult.Unreadable;
            }

            settings = loaded;
            return SettingsLoadResult.Loaded;
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Warning($"App settings file is malformed or unreadable, using defaults. Error: {ex.Message}");
            return SettingsLoadResult.Unreadable;
        }
    }

    // Unchanged contract: never throws, always returns something usable. Callers that
    // need to tell Absent from Unreadable use TryLoad instead.
    public static AppSettings Get()
    {
        TryLoad(out var settings);
        return settings;
    }

    public static void Set(string themePreference)
    {
        var settings = Get();
        settings.ThemePreference = themePreference;
        Write(settings);
    }

    public static bool SetServers(IEnumerable<string> servers)
    {
        var settings = Get();
        settings.Servers = servers?.ToList() ?? new List<string>();
        return Write(settings);
    }

    // NOT synchronized. This and AddServer and Set(themePreference) are all
    // Get() -> mutate -> write read-modify-writes over one shared file, so a GUI commit
    // racing a CLI /savecreds AddServer, or two GUI instances, loses an update - and a
    // lost update can resurrect a just-removed server. Stage C assumes a single
    // instance. Recorded here deliberately so the resulting bug is not a mystery later.
    //
    // Writes via a temp file plus an atomic move, so an interrupted write can never
    // leave a truncated settings.json behind. That matters more than usual here:
    // a truncated file reads back as Unreadable, and an earlier design that collapsed
    // Unreadable into "never seeded" would have re-seeded and resurrected removed
    // servers. Returns false (having logged) rather than throwing, matching the
    // pre-existing swallow-and-log contract of this class.
    private static bool Write(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = StorePath + ".tmp";

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, StorePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to persist app settings: {ex.Message}");
            return false;
        }
    }
}
