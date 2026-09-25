// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CAppSettingsTests : IDisposable
    {
        private readonly string _testStorePath;
        private readonly string _originalStorePath;

        public CAppSettingsTests()
        {
            _originalStorePath = CAppSettings.StorePath;

            // Point CAppSettings at an isolated temp path instead of the real
            // %APPDATA%/VeeamHealthCheck/settings.json, so these tests never touch
            // a real user's saved preferences. Mirrors CredentialStoreSecurityTests'
            // isolation seam for CredentialStore.StorePath.
            _testStorePath = Path.Combine(Path.GetTempPath(), $"vhc-settings-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testStorePath);

            CAppSettings.StorePath = Path.Combine(_testStorePath, "settings.json");
        }

        public void Dispose()
        {
            CAppSettings.StorePath = _originalStorePath;

            if (Directory.Exists(_testStorePath))
            {
                try
                {
                    Directory.Delete(_testStorePath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void Get_WhenNoFileExists_ReturnsDefaultSystemPreference()
        {
            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void Set_ThenGet_RoundTripsThemePreference()
        {
            CAppSettings.Set("Dark");

            var settings = CAppSettings.Get();

            Assert.Equal("Dark", settings.ThemePreference);
        }

        [Fact]
        public void Set_CalledTwice_OverwritesPreviousPreference()
        {
            CAppSettings.Set("Dark");
            CAppSettings.Set("Light");

            var settings = CAppSettings.Get();

            Assert.Equal("Light", settings.ThemePreference);
        }

        [Fact]
        public void Get_WhenFileIsMalformedJson_ReturnsDefault()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");

            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void Get_WhenFileIsEmpty_ReturnsDefault()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, string.Empty);

            var settings = CAppSettings.Get();

            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void Get_WhenNoFileExists_ReturnsNullServers()
        {
            var settings = CAppSettings.Get();

            // null is load-bearing: it means "never seeded" and is what triggers
            // the one-time seed. An empty list would mean "user removed everything".
            Assert.Null(settings.Servers);
        }

        [Fact]
        public void SetServers_ThenGet_RoundTripsServers()
        {
            bool ok = CAppSettings.SetServers(new[] { "vbr01", "vbr02" });

            var settings = CAppSettings.Get();

            Assert.True(ok);
            Assert.Equal(new[] { "vbr01", "vbr02" }, settings.Servers);
        }

        [Fact]
        public void SetServers_WithEmptyList_PersistsEmptyNotNull()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.SetServers(System.Array.Empty<string>());

            var settings = CAppSettings.Get();
            Assert.NotNull(settings.Servers);
            Assert.Empty(settings.Servers);
        }

        [Fact]
        public void SetServers_PreservesThemePreference()
        {
            CAppSettings.Set("Dark");

            CAppSettings.SetServers(new[] { "vbr01" });

            var settings = CAppSettings.Get();
            Assert.Equal("Dark", settings.ThemePreference);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }

        [Fact]
        public void Set_PreservesServers()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.Set("Light");

            var settings = CAppSettings.Get();
            Assert.Equal("Light", settings.ThemePreference);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }

        [Fact]
        public void Load_DistinguishesAbsentFromUnreadable()
        {
            // Absent: no file at all.
            var absent = CAppSettings.Load(out _);

            // Unreadable: file exists but is not parseable.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var unreadable = CAppSettings.Load(out _);

            // Loaded: a real file.
            CAppSettings.SetServers(new[] { "vbr01" });
            var loaded = CAppSettings.Load(out var settings);

            // One Fact with several asserts rather than a Theory: SettingsLoadResult
            // is internal, and an internal enum as an [InlineData] parameter produces
            // CS0051 on the generated public test method.
            Assert.Equal(SettingsLoadResult.Absent, absent);
            Assert.Equal(SettingsLoadResult.Unreadable, unreadable);
            Assert.Equal(SettingsLoadResult.Loaded, loaded);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }

        [Fact]
        public void Load_WhenFileIsEmpty_ReturnsAbsent()
        {
            // Nothing observes this distinction through Get() (which collapses Absent
            // and Unreadable to defaults), so without this test the branch could flip
            // silently and a fresh install with a zero-byte settings file would never
            // seed, with the suite staying green throughout.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, string.Empty);

            var result = CAppSettings.Load(out _);

            Assert.Equal(SettingsLoadResult.Absent, result);
        }

        [Fact]
        public void Load_WhenFileContentIsJsonNull_ReturnsAbsent()
        {
            // The literal `null` is well-formed JSON that deserializes to no object.
            // It conveys "no content", the same meaning as an empty file, so this is a
            // deliberate choice of Absent (triggers the one-time seed) rather than
            // Unreadable (a worse outcome here: LoadOrSeedServers would return empty
            // every session and never write, since nothing ever turns Unreadable back
            // into Absent - a permanently stuck state rather than a retry-recoverable
            // one).
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "null");

            var result = CAppSettings.Load(out _);

            Assert.Equal(SettingsLoadResult.Absent, result);
        }

        [Fact]
        public void SetServers_WithNull_PersistsEmptyList()
        {
            // Throwing on null would violate this class's never-throws contract, so
            // null is normalized to an authoritative empty list instead - but that
            // means SetServers(null) converts "never seeded" into "user emptied it",
            // since null is the natural way to spell "clear it" at a call site. Pinned
            // here so that conversion is a documented decision, not an accident.
            CAppSettings.SetServers(null);

            var settings = CAppSettings.Get();
            Assert.NotNull(settings.Servers);
            Assert.Empty(settings.Servers);
        }

        [Fact]
        public void SetServers_OnSuccess_LeavesNoTempFileBehind()
        {
            bool ok = CAppSettings.SetServers(new[] { "vbr01" });

            Assert.True(ok);
            Assert.True(File.Exists(CAppSettings.StorePath));

            // The temp name is a fresh Guid per write (see CAppSettings.Write), so
            // assert on the absence of any *.tmp file in the store directory rather
            // than one exact name - this also catches debris under any future naming
            // scheme.
            Assert.Empty(Directory.GetFiles(_testStorePath, "*.tmp"));
        }

        [Fact]
        public void SetServers_WhenCreateDirectoryFails_ReturnsFalseAndDoesNotThrow()
        {
            // Parent path is a file, so Directory.CreateDirectory throws:
            // ENOTDIR on Unix, IOException on Windows. This fails before WriteAllText
            // ever runs, so it never exercises the temp file or the Move step - see
            // SetServers_WhenMoveFails_ReturnsFalseAndLeavesNoTempFile for that.
            var blocker = Path.Combine(_testStorePath, "blocker");
            File.WriteAllText(blocker, "not a directory");
            CAppSettings.StorePath = Path.Combine(blocker, "settings.json");

            Assert.False(CAppSettings.SetServers(new[] { "vbr01" }));
        }

        [Fact]
        public void SetServers_WhenMoveFails_ReturnsFalseAndLeavesNoTempFile()
        {
            // A directory where the file belongs: CreateDirectory and WriteAllText both
            // succeed, so this exercises the Move failure - the one path where a temp
            // file genuinely exists and the catch block's cleanup has something to
            // delete, unlike the CreateDirectory-failure test above (which fails before
            // any temp file is created).
            Directory.CreateDirectory(CAppSettings.StorePath);

            Assert.False(CAppSettings.SetServers(new[] { "vbr01" }));
            Assert.Empty(Directory.GetFiles(_testStorePath, "*.tmp"));
        }

        [Fact]
        public void SetServers_SweepsStaleTempFilesFromPreviousRuns()
        {
            // A process killed between WriteAllText and Move leaves a Guid-named orphan
            // that no catch block can reach; the sweep at the top of Write is what
            // eventually clears it. Name must match Write's glob:
            // Path.GetFileName(StorePath) + ".*.tmp".
            var stale = CAppSettings.StorePath + ".deadbeef00000000000000000000dead.tmp";
            File.WriteAllText(stale, "orphan");

            Assert.True(CAppSettings.SetServers(new[] { "vbr01" }));
            Assert.False(File.Exists(stale));
        }

        [Fact]
        public void Set_WhenSettingsFileIsUnreadable_ReturnsFalseAndLeavesFileUnchanged()
        {
            // THE guard this whole follow-up exists to add: Set must not route around
            // Load's Unreadable result via Get()'s defaults. If it did, this call would
            // write {ThemePreference:"Dark", Servers:null} over the corrupt file,
            // turning a transient, recoverable read failure into a permanent one - the
            // next launch reads Loaded with Servers == null, indistinguishable from
            // "never seeded", and the one-time seed resurrects every removed server.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            const string corrupt = "{ not valid json ";
            File.WriteAllText(CAppSettings.StorePath, corrupt);

            bool ok = CAppSettings.Set("Dark");

            Assert.False(ok);
            Assert.Equal(corrupt, File.ReadAllText(CAppSettings.StorePath));
        }

        [Fact]
        public void AddServer_WhenServersNeverSeeded_IsNoOp()
        {
            // THE upgrade-data-loss guard. If AddServer materialised the list here,
            // Servers would flip null -> non-null, non-null is authoritative, and the
            // one-time seed would never run - silently dropping every server the user
            // already had credentials for. CredentialStore.Set has already persisted
            // the credential by this point, so the eventual seed picks the host up.
            CAppSettings.AddServer("newhost");

            Assert.Null(CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WhenSeeded_AppendsServer()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.AddServer("vbr02");

            Assert.Equal(new[] { "vbr01", "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WhenSeededEmpty_AppendsServer()
        {
            // An empty list is authoritative, not "never seeded", so AddServer applies.
            CAppSettings.SetServers(System.Array.Empty<string>());

            CAppSettings.AddServer("vbr02");

            Assert.Equal(new[] { "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithExistingNameInDifferentCase_DoesNotDuplicate()
        {
            CAppSettings.SetServers(new[] { "VBR01" });

            CAppSettings.AddServer("vbr01");

            Assert.Equal(new[] { "VBR01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithNullOrWhitespace_IsNoOp()
        {
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.AddServer(null);
            CAppSettings.AddServer("   ");

            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_DoesNotSpecialCaseLocalhost()
        {
            // CAppSettings deliberately knows nothing about localhost. The injection
            // policy lives in the GUI layer, and a blanket filter here would silently
            // discard a legitimate entry on a machine with no local Veeam product.
            // A stray localhost is neutralised at read time by LoadOrSeedServers.
            CAppSettings.SetServers(System.Array.Empty<string>());

            CAppSettings.AddServer("localhost");

            Assert.Equal(new[] { "localhost" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithNullElementInPersistedList_DoesNotThrowAndDetectsDuplicate()
        {
            // A hand-edited settings.json can contain a null element. The instance-based
            // s.Equals(...) used to throw NullReferenceException on it; the static
            // string.Equals(...) form returns false for a null left-hand side instead.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{\"Servers\":[null,\"vbr01\"]}");

            var ex = Record.Exception(() => CAppSettings.AddServer("vbr01"));

            Assert.Null(ex);
            Assert.Equal(new[] { null, "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WhenSettingsFileIsUnreadable_LeavesFileUnchanged()
        {
            // AddServer explicitly checks Load's Unreadable result (matching Set) and
            // logs rather than silently swallowing it, so a user with a corrupt
            // settings file gets an explanation in the log for why their /savecreds
            // host never appeared in the picker.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            const string corrupt = "{ not valid json ";
            File.WriteAllText(CAppSettings.StorePath, corrupt);

            CAppSettings.AddServer("newhost");

            Assert.Equal(corrupt, File.ReadAllText(CAppSettings.StorePath));
        }

        [Fact]
        public void AddServer_WithWhitespacePaddedExistingName_DoesNotDuplicate()
        {
            // Matches ServerListEditor.Add's trimming behavior, so both entry points
            // treat padding the same way rather than leaving AddServer the odd one out.
            CAppSettings.SetServers(new[] { "vbr01" });

            CAppSettings.AddServer("  vbr01  ");

            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_WithWhitespacePaddedStoredName_DoesNotDuplicate()
        {
            // The other direction of the padding fix: a hand-edited settings.json can
            // hold a padded name. Comparing both sides trimmed catches this the same
            // way the input-side test above catches a padded call argument.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{\"Servers\":[\"  vbr01  \"]}");

            CAppSettings.AddServer("vbr01");

            Assert.Equal(new[] { "  vbr01  " }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void AddServer_PreservesThemePreference()
        {
            // Seed first: AddServer no-ops while Servers is null, and a no-op would
            // make this test pass without ever exercising the write path it's meant
            // to pin.
            CAppSettings.SetServers(new[] { "vbr01" });
            CAppSettings.Set("Dark");

            CAppSettings.AddServer("vbr02");

            var settings = CAppSettings.Get();
            Assert.Equal("Dark", settings.ThemePreference);
            Assert.Equal(new[] { "vbr01", "vbr02" }, settings.Servers);
        }

        [Fact]
        public void SetServers_WithNullAndWhitespaceElements_PersistsEmptyList()
        {
            // Deferred from Task 2: SetServers must drop null/whitespace elements on
            // write, reusing IsUsableServerName - the same predicate NormalizeServers
            // applies on read - rather than a second copy of the same rule. Under the
            // null-vs-empty rule this is the meaningful, irreversible statement "the
            // user emptied the list" - not "one blank entry survives as a phantom
            // persisted server".
            bool ok = CAppSettings.SetServers(new[] { null, "  " });

            var settings = CAppSettings.Get();
            Assert.True(ok);
            Assert.NotNull(settings.Servers);
            Assert.Empty(settings.Servers);
        }

        [Fact]
        public void SetServers_WhenSettingsFileIsUnreadable_StillPersistsNewList()
        {
            // SetServers deliberately has no refusing Unreadable guard, unlike Set and
            // AddServer: refusing an explicit, deliberate user action (a dialog commit)
            // because an unrelated part of the file is corrupt would be worse than the
            // actual consequence, which is ThemePreference silently resetting to its
            // default alongside the commit. Logged, not silent - but not refused either.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");

            bool ok = CAppSettings.SetServers(new[] { "vbr01" });

            var settings = CAppSettings.Get();
            Assert.True(ok);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
            Assert.Equal("System", settings.ThemePreference);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNeverSeeded_SeedsFromCredentialServersAndPersists()
        {
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01", "vbr02" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01", "vbr02" }, result);
            Assert.Equal(new[] { "vbr01", "vbr02" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNeverSeededAndCredentialListEmpty_LeavesServersNullAndDoesNotWrite()
        {
            // A CredentialStore whose static constructor swallowed a transient failure
            // (a locked or momentarily unreadable creds.json) installs an empty cache
            // with the real credentials still intact on disk - indistinguishable, from
            // this method's side of the seam, from a genuinely credential-less machine.
            // Persisting [] would read back as an authoritative "user emptied it" on
            // every later launch and never retry. Leaving Servers null instead means a
            // later launch, once the credential store is readable again, still seeds.
            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: true);

            Assert.Empty(result);
            Assert.Null(CAppSettings.Get().Servers);
            Assert.False(File.Exists(CAppSettings.StorePath));
        }

        [Fact]
        public void LoadOrSeedServers_WhenNeverSeededAndCredentialListNull_LeavesServersNullAndDoesNotWrite()
        {
            // Same guard, the other empty-input route: NormalizeServers(null, ...)
            // also normalizes to [], so this must not be distinguishable from the
            // empty-array case above - both leave the never-seeded signal alone.
            var result = CAppSettings.LoadOrSeedServers(null, excludeLocalhost: true);

            Assert.Empty(result);
            Assert.Null(CAppSettings.Get().Servers);
            Assert.False(File.Exists(CAppSettings.StorePath));
        }

        [Fact]
        public void LoadOrSeedServers_WhenSeedingWithCaseInsensitiveDuplicate_DedupesToFirstOccurrence()
        {
            // Without Distinct, a credential store (or a persisted list, via the same
            // NormalizeServers call) containing both casings would feed the picker two
            // rows that render identically once trimmed - the exact indistinguishable
            // duplicate NormalizeServers otherwise still exists to prevent.
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01", "VBR01" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void LoadOrSeedServers_WhenFileExistsWithNullServers_Seeds()
        {
            // The literal upgrade state: an existing settings.json (from before Servers
            // existed) has a ThemePreference but no Servers property, which is Loaded
            // with Servers == null - not Absent. Every other seed-firing test above
            // reaches the seed via Absent (no file at all); this is the one a real
            // upgrading user actually hits, and the null-vs-empty rule exists
            // specifically to protect it. (Set("Dark") actually serializes an explicit
            // "Servers": null rather than omitting the property - deserialization-
            // identical to an absent property, so the coverage is the same either way.)
            CAppSettings.Set("Dark");

            var result = CAppSettings.LoadOrSeedServers(new[] { "vbr01" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);

            var settings = CAppSettings.Get();
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
            Assert.Equal("Dark", settings.ThemePreference);
        }

        [Fact]
        public void LoadOrSeedServers_WhenAlreadySeeded_IgnoresCredentialServers()
        {
            CAppSettings.SetServers(new[] { "kept" });

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "ignored" }, excludeLocalhost: true);

            Assert.Equal(new[] { "kept" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenSeededEmpty_StaysEmpty()
        {
            // The null-vs-empty rule, end to end: a user who removed everything must
            // not have their list rebuilt from the credential store on next launch.
            CAppSettings.SetServers(System.Array.Empty<string>());

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            Assert.Empty(result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenExcludingLocalhost_FiltersSeedInput()
        {
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "LocalHost", "vbr01" }, excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
            Assert.Equal(new[] { "vbr01" }, CAppSettings.Get().Servers);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNotExcludingLocalhost_RetainsIt()
        {
            // A VB365-only machine has IsVbrInstalled == false, so nothing injects
            // localhost - and RunSaveCredsFlow defaults its host to "localhost", so a
            // credential for it legitimately exists. Filtering unconditionally here
            // would leave the picker blank.
            var result = CAppSettings.LoadOrSeedServers(
                new[] { "localhost" }, excludeLocalhost: false);

            Assert.Equal(new[] { "localhost" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenExcludingLocalhost_FiltersAlreadyPersistedList()
        {
            // The self-healing half. AddServer has no localhost special case, so
            // /savecreds against localhost on an already-seeded injecting machine
            // really does persist the name. Filtering only the seed would then let
            // injection render it a second time as a duplicate row.
            CAppSettings.SetServers(new[] { "localhost", "vbr01" });

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_ToleratesNullElementsAndTrimsPaddedEntries()
        {
            // Arranged via a direct file write rather than SetServers: SetServers now
            // drops null/whitespace elements on write (the item this task also owns),
            // so routing this through SetServers would never put a null on disk to
            // begin with. A hand-edited settings.json is the realistic route to both a
            // null element and un-trimmed padding surviving to a read. The null
            // element must not throw (NormalizeServers' IsNullOrWhiteSpace clause has
            // to run first), and "  localhost  " must still be excluded when localhost is
            // injected - otherwise it renders beside the injected row as a duplicate.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(
                CAppSettings.StorePath,
                "{\"Servers\":[null,\"  vbr01  \",\"  localhost  \",\"   \"]}");

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: true);

            Assert.Equal(new[] { "vbr01" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenNotExcludingLocalhost_ReturnsPaddedLocalhostTrimmed()
        {
            // SetServers only drops null/whitespace elements on write - it does not
            // trim survivors (only NormalizeServers does, on read) - so a padded
            // "localhost" legitimately reaches disk this way and this still exercises
            // the read-time trim in NormalizeServers, not a write-time one.
            CAppSettings.SetServers(new[] { "  localhost  " });

            var result = CAppSettings.LoadOrSeedServers(
                System.Array.Empty<string>(), excludeLocalhost: false);

            Assert.Equal(new[] { "localhost" }, result);
        }

        [Fact]
        public void LoadOrSeedServers_WhenSettingsUnreadable_ReturnsEmptyAndDoesNotWrite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var before = File.ReadAllText(CAppSettings.StorePath);

            var result = CAppSettings.LoadOrSeedServers(
                new[] { "vbr01" }, excludeLocalhost: true);

            // Seeding over an unreadable file would turn a transient read failure into
            // a permanent resurrection of removed servers. Better to show nothing this
            // session and leave the file alone so a retry can recover.
            Assert.Empty(result);
            Assert.Equal(before, File.ReadAllText(CAppSettings.StorePath));
        }

        [Fact]
        public void HasNonLocalhostServer_WithEmptyList_ReturnsFalse()
        {
            Assert.False(CAppSettings.HasNonLocalhostServer(System.Array.Empty<string>()));
        }

        [Fact]
        public void HasNonLocalhostServer_WithOnlyLocalhostAnyCasing_ReturnsFalse()
        {
            Assert.False(CAppSettings.HasNonLocalhostServer(
                new[] { "localhost", "LOCALHOST", "LocalHost" }));
        }

        [Fact]
        public void HasNonLocalhostServer_WithRealHostPresent_ReturnsTrue()
        {
            Assert.True(CAppSettings.HasNonLocalhostServer(new[] { "vbr01" }));
        }

        [Fact]
        public void HasNonLocalhostServer_WithMixedLocalhostAndRealHost_ReturnsTrue()
        {
            Assert.True(CAppSettings.HasNonLocalhostServer(new[] { "localhost", "vbr01" }));
        }
    }
}
