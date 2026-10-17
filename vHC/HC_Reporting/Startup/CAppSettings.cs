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

    private const string LocalhostName = "localhost";

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
    /// at a call site. Also drops any null-or-whitespace element from a non-null list
    /// before persisting, reusing <see cref="IsUsableServerName"/> - the same
    /// predicate <see cref="NormalizeServers"/> applies on read - rather than a
    /// second copy of the same rule. Deliberately does not trim surviving elements;
    /// that normalization is <see cref="NormalizeServers"/>'s job on read, not this
    /// method's job on write.
    ///
    /// Unlike <see cref="Set"/> and <see cref="AddServer"/>, this has no
    /// <c>Unreadable</c> guard: it commits the list regardless, logging instead of
    /// refusing. Refusing an explicit, deliberate user action (a dialog commit)
    /// because an unrelated part of the file is corrupt is worse than the actual
    /// consequence, which is that <c>ThemePreference</c> silently resets to its
    /// default alongside the commit - and the seed-signal hazard that motivates the
    /// guard on <see cref="Set"/> does not apply here, since this method is the one
    /// that sets the signal.
    ///
    /// Returns <c>false</c> (having logged) if the write failed. Not
    /// synchronized: this, <c>AddServer</c>, and <see cref="Set"/> are all
    /// Get() -&gt; mutate -&gt; write read-modify-writes over one shared file, so a GUI
    /// commit racing a CLI <c>/savecreds</c> AddServer, or two GUI instances, loses an
    /// update - and a lost update can resurrect a just-removed server. Stage C assumes
    /// a single instance.
    /// </summary>
    public static bool SetServers(IEnumerable<string> servers)
    {
        if (Load(out var settings) == SettingsLoadResult.Unreadable)
        {
            CGlobals.Logger.Warning(
                "Settings file was unreadable; committing the new server list anyway rather than silently refusing an explicit user action. ThemePreference resets to its default as a result.");
        }

        settings.Servers = servers?.Where(IsUsableServerName).ToList() ?? new List<string>();
        return Write(settings);
    }

    /// <summary>
    /// Called from the two production <c>CredentialStore.Set</c> call sites so a host
    /// that gains credentials outside the GUI (a <c>/savecreds</c> run, a CLI
    /// collection) does not stay invisible in the picker.
    ///
    /// NO-OP while <see cref="AppSettings.Servers"/> is <c>null</c>, and that is not an
    /// optimisation. <c>null</c> means "never seeded"; writing here would flip it to
    /// authoritative and permanently suppress the one-time seed, silently discarding
    /// every server the user already had. The credential itself is already persisted
    /// by the caller, so the eventual seed picks this host up anyway.
    ///
    /// Also a no-op, logged, when the settings file is <c>Unreadable</c> - writing here
    /// would turn a transient, recoverable read failure into a permanent one, the same
    /// hazard <see cref="Set"/> guards against.
    ///
    /// Knows nothing about localhost by design - see
    /// <c>AddServer_DoesNotSpecialCaseLocalhost</c>.
    /// </summary>
    public static void AddServer(string server)
    {
        if (!IsUsableServerName(server))
        {
            return;
        }

        var trimmed = server.Trim();

        if (Load(out var settings) == SettingsLoadResult.Unreadable)
        {
            CGlobals.Logger.Warning($"Settings unreadable; not recording server '{trimmed}' in the picker list.");
            return;
        }

        // Servers == null means "never seeded" - see the no-op remarks above. Checked
        // after the Unreadable branch so the two distinct reasons for not writing are
        // both explicit rather than one masquerading as the other.
        if (settings.Servers == null)
        {
            return;
        }

        // s?.Trim() rather than s.Trim(): the persisted list can contain a null element
        // (e.g. a hand-edited settings.json). Trimming both sides also catches a
        // persisted-but-padded duplicate, e.g. a hand-edited "  vbr01  " against an
        // AddServer("vbr01") call.
        if (settings.Servers.Any(s => string.Equals(s?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.Servers.Add(trimmed);
        Write(settings);
    }

    /// <summary>
    /// Resolves the effective server list, seeding once from the credential store on
    /// the first launch after upgrade. <paramref name="credentialStoreServers"/> is a
    /// parameter rather than an internal <c>CredentialStore.GetAllServers()</c> call
    /// so this stays a pure function of its inputs and is directly unit-testable,
    /// matching the <see cref="StorePath"/> seam pattern this class and
    /// <c>CredentialStore</c> already use.
    ///
    /// <paramref name="excludeLocalhost"/> is the GUI's localhost-injection
    /// predicate. It filters the RETURNED list on every path, not just the seed -
    /// that is what makes the "localhost is injected, never persisted" invariant
    /// self-healing rather than dependent on every writer behaving. <see
    /// cref="AddServer"/> has no localhost special case, so a stray persisted
    /// localhost is possible; ignoring it at read time neutralises it wherever it
    /// would be injected and honours it wherever it would not.
    ///
    /// Returns an empty list, and writes nothing, if the settings file is
    /// unreadable - seeding over a file that could not be parsed would turn a
    /// transient read failure into a permanent resurrection of removed servers.
    ///
    /// Also writes nothing - and leaves <see cref="AppSettings.Servers"/> null - if
    /// the seed itself normalizes to empty. <paramref name="credentialStoreServers"/>
    /// being empty is indistinguishable from <c>CredentialStore</c>'s static
    /// constructor having swallowed a transient failure (a locked or momentarily
    /// unreadable <c>creds.json</c>) and installed an empty cache, with the actual
    /// credentials still intact on disk. Persisting <c>[]</c> here would read back as
    /// an authoritative "user emptied it" on every later launch and never retry -
    /// permanent, silent loss of the server list on the exact path this method exists
    /// to protect. Only a non-empty seed is worth burning the never-seeded signal for;
    /// a genuinely credential-less machine simply converges on a later launch's seed
    /// once credentials exist, the same convergence argument behind <see
    /// cref="AddServer"/>'s no-op-while-null guard.
    /// </summary>
    public static List<string> LoadOrSeedServers(IEnumerable<string> credentialStoreServers, bool excludeLocalhost)
    {
        var state = Load(out var settings);

        if (state == SettingsLoadResult.Unreadable)
        {
            CGlobals.Logger.Warning(
                "Settings file unreadable; server list unavailable this session. Not seeding, to avoid resurrecting removed servers.");
            return new List<string>();
        }

        if (settings.Servers == null)
        {
            var seeded = NormalizeServers(credentialStoreServers, excludeLocalhost);

            // Only a non-empty seed is worth burning the never-seeded signal - see the
            // summary. Written directly via settings/Write rather than SetServers: this
            // is a single read (the Load above) rather than a second Get()/Load that
            // would also collapse Unreadable into defaults, the exact thing the guard
            // three lines up exists to prevent. seeded is already normalized, so
            // SetServers' own element filter would add nothing here.
            if (seeded.Count > 0)
            {
                settings.Servers = seeded;
                Write(settings);
            }

            return seeded;
        }

        return NormalizeServers(settings.Servers, excludeLocalhost);
    }

    /// <summary>
    /// True if <paramref name="servers"/> contains at least one entry that isn't a local
    /// address or hostname - not just the literal string "localhost". Shared by
    /// <c>VhcGui</c>'s <c>SetUiSync</c> and its cold-start recovery branch in
    /// <c>SetUiAsync</c> - both need the identical "is there a remote server to fall back
    /// to" check, and this is one of two pieces of that logic directly unit-testable
    /// outside the Avalonia code-behind (see also <see cref="ChooseDefaultServer"/>).
    ///
    /// Uses <see cref="CHostNameHelper.IsLocalHost"/> (recognizes "127.0.0.1", this
    /// machine's own name, DNS hostname, and FQDN variants - not just "localhost")
    /// rather than a literal string comparison, so a persisted "127.0.0.1" or this
    /// machine's own hostname gets the same protection the literal "localhost" case
    /// already has: neither is a usable remote target on a machine with no local Veeam
    /// installed, which is exactly the state this predicate exists to detect. This
    /// deliberately does NOT change how <see cref="NormalizeServers"/>,
    /// <see cref="AddServer"/>, or <see cref="LoadOrSeedServers"/> persist or dedupe
    /// server names - those only ever special-case the literal "localhost" (see
    /// AddServer's remarks), a narrower, deliberate concern about injection/persistence,
    /// not about whether a given name is a valid remote target to run against.
    /// </summary>
    public static bool HasNonLocalhostServer(IEnumerable<string> servers) =>
        servers.Any(s => !CHostNameHelper.IsLocalHost(s));

    /// <summary>
    /// Picks the server a picker should default to, given the list it's about to
    /// display, an optional prior selection to preserve, and whether "localhost" is
    /// genuinely this machine's own injected default (i.e. local Veeam is actually
    /// installed). Extracted from <c>VhcGui.InitializeServerList</c> so this decision -
    /// exactly the logic a real regression once lived in (a persisted, non-injected
    /// "localhost" winning over a real remote server on repeat launches) - is directly
    /// unit-testable outside the untestable Avalonia code-behind, the same reasoning
    /// behind extracting <see cref="HasNonLocalhostServer"/>.
    ///
    /// Precedence: the preserved prior selection, if it survived a repopulate; else
    /// "localhost" only when it's the injected default; else the first genuinely
    /// non-local entry (via <see cref="CHostNameHelper.IsLocalHost"/>, not a literal
    /// string match, so a persisted "127.0.0.1" or this machine's own hostname can't
    /// win here either); else whatever is first. Returns <c>null</c> only when
    /// <paramref name="displayServers"/> is empty.
    /// </summary>
    public static string ChooseDefaultServer(
        IEnumerable<string> displayServers, string previousSelection, bool localhostIsInjected)
    {
        var servers = displayServers as IReadOnlyList<string> ?? displayServers.ToList();

        if (previousSelection != null)
        {
            var keep = servers.FirstOrDefault(
                s => string.Equals(s, previousSelection, StringComparison.OrdinalIgnoreCase));
            if (keep != null)
            {
                return keep;
            }
        }

        if (localhostIsInjected)
        {
            var localhost = servers.FirstOrDefault(
                s => string.Equals(s, LocalhostName, StringComparison.OrdinalIgnoreCase));
            if (localhost != null)
            {
                return localhost;
            }
        }

        var nonLocal = servers.FirstOrDefault(s => !CHostNameHelper.IsLocalHost(s));
        if (nonLocal != null)
        {
            return nonLocal;
        }

        return servers.Count > 0 ? servers[0] : null;
    }

    // The single rule for "not a usable server name", shared by NormalizeServers
    // (read), SetServers (write), and AddServer's input guard, so the three paths
    // cannot drift into different definitions of the same rule.
    private static bool IsUsableServerName(string server) => !string.IsNullOrWhiteSpace(server);

    // Named for what it does to the values, not just how it selects them: it trims
    // survivors, which SetServers deliberately does not - a name like "Filter" would
    // wrongly imply the caller's own strings come back unchanged.
    private static List<string> NormalizeServers(IEnumerable<string> servers, bool excludeLocalhost)
    {
        if (servers == null)
        {
            return new List<string>();
        }

        // Clause order is load-bearing, twice over.
        //
        // The IsNullOrWhiteSpace Where MUST come first: the persisted list can still
        // contain a null element via a hand-edited settings.json - SetServers itself
        // drops null/whitespace elements on write (see IsUsableServerName), but a
        // file edited directly bypasses that guard - and any Equals below would
        // throw on it. LINQ chains lazily, so this ordering IS the null-safety, not
        // incidental tidiness.
        //
        // The Trim Select must precede the localhost Where, or a persisted
        // "  localhost  " survives excludeLocalhost and renders BESIDE the injected
        // localhost row - exactly the duplicate this filter exists to prevent.
        // Trimming on read also stops the picker showing a padded hostname.
        var query = servers
            .Where(IsUsableServerName)
            .Select(s => s.Trim());

        if (excludeLocalhost)
        {
            query = query.Where(s => !s.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase));
        }

        // A persisted or seeded ["vbr01", "  vbr01  "] would otherwise survive as two
        // rows that render identically once trimmed - the same case-insensitive
        // comparator AddServer uses to prevent the duplicate at write time, applied
        // here so a duplicate that reaches disk by some other route (a hand-edited
        // file, a future writer) still can't render twice. Distinct preserves
        // first-occurrence order, so which of two case variants "wins" matches
        // whichever appeared first in the source list.
        return query.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
        var dir = Path.GetDirectoryName(StorePath)!;
        var tempPath = StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(dir);

            // Guid temp names are not self-healing the way a fixed name was, so a process
            // killed between the write and the move leaves an orphan no catch block can
            // reach. Best-effort sweep; never let cleanup failure block a real write.
            // This can also delete a concurrent writer's still-in-flight temp file;
            // acceptable only under the single-instance assumption stated above.
            try
            {
                foreach (var stale in Directory.GetFiles(dir, Path.GetFileName(StorePath) + ".*.tmp"))
                {
                    File.Delete(stale);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }

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
