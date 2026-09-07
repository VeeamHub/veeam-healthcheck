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

    internal static SettingsLoadResult Load(out AppSettings settings)
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
                // Only reachable when the file's entire content is the JSON literal
                // `null` - valid JSON that deserializes to no object. That parses
                // cleanly and conveys "no content", the same meaning as an empty file,
                // so it is Absent rather than Unreadable. The distinction matters:
                // Unreadable is meant for genuinely corrupt/unparseable content, which
                // is retry-recoverable (the next successful write clears it). Absent
                // triggers the one-time seed. Treating literal `null` as Unreadable
                // would make LoadOrSeedServers return empty forever and never write,
                // since nothing ever turns Unreadable back into Absent.
                return SettingsLoadResult.Absent;
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
    // need to tell Absent from Unreadable use Load instead.
    public static AppSettings Get()
    {
        Load(out var settings);
        return settings;
    }

    /// <summary>
    /// Persists the theme preference. Returns <c>false</c> (having logged) rather than
    /// throwing if the settings file could not be read or written - callers that don't
    /// need the result may discard it, matching this class's existing swallow-and-log
    /// contract. Not synchronized: see the remarks on <see cref="SetServers"/>.
    /// </summary>
    public static bool Set(string themePreference)
    {
        // If the existing file is Unreadable rather than Absent, do NOT fall through to
        // Get()'s defaults and write them back. Get() would hand back a fresh
        // AppSettings with Servers == null, and writing that turns a transient,
        // recoverable read failure into a permanent one: the next launch reads this
        // write as Loaded with Servers == null, which is indistinguishable from "never
        // seeded", and the one-time seed resurrects every server the user removed - all
        // triggered by toggling an unrelated cosmetic preference. Leaving the corrupt
        // file in place keeps it retry-recoverable instead.
        if (Load(out var settings) == SettingsLoadResult.Unreadable)
        {
            CGlobals.Logger.Warning(
                "Settings unreadable; not overwriting theme preference, to preserve the server-list seed signal.");
            return false;
        }

        settings.ThemePreference = themePreference;
        return Write(settings);
    }

    /// <summary>
    /// Replaces the persisted server list. A <c>null</c> <paramref name="servers"/> is
    /// normalized to an empty list rather than rejected - throwing would violate this
    /// class's never-throws contract - but note that doing so converts "never seeded"
    /// into "user emptied it", since <c>null</c> is the natural way to spell "clear it"
    /// at a call site. Returns <c>false</c> (having logged) if the write failed.
    /// Not synchronized: this, <c>AddServer</c>, and <see cref="Set"/> are all
    /// Get() -&gt; mutate -&gt; write read-modify-writes over one shared file, so a GUI
    /// commit racing a CLI <c>/savecreds</c> AddServer, or two GUI instances, loses an
    /// update - and a lost update can resurrect a just-removed server. Stage C assumes
    /// a single instance.
    /// </summary>
    public static bool SetServers(IEnumerable<string> servers)
    {
        var settings = Get();
        settings.Servers = servers?.ToList() ?? new List<string>();
        return Write(settings);
    }

    // Writes via a temp file plus an atomic move, so an interrupted write can never
    // leave a truncated settings.json behind. That matters more than usual here: a
    // truncated file reads back as Unreadable, and an earlier design that collapsed
    // Unreadable into "never seeded" would have re-seeded and resurrected removed
    // servers.
    //
    // The temp name is a fresh Guid per call rather than fixed, because Stage C is
    // explicitly NOT synchronized across instances (see SetServers' remarks): a fixed
    // temp name shared by two concurrent writers would let one writer's WriteAllText
    // race the other's Move, putting a partial file into place even though each
    // writer's own move is atomic. A per-instance name would leave the same hole for
    // two concurrent writes from the same process (e.g. this method called
    // re-entrantly); a fresh Guid per call closes it regardless of caller.
    //
    // Returns false (having logged) rather than throwing, matching the pre-existing
    // swallow-and-log contract of this class.
    private static bool Write(AppSettings settings)
    {
        var tempPath = StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, StorePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Error($"Failed to persist app settings: {ex.Message}");

            // Best-effort: don't leave the temp file orphaned if WriteAllText succeeded
            // but the subsequent Move failed (locked destination, AV scan, read-only
            // target). Swallow failures here too - we're already in the error path.
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup only.
            }

            return false;
        }
    }
}
