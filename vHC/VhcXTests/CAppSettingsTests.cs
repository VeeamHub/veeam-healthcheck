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
            CAppSettings.SetServers(new[] { "vbr01" });

            // The temp name is a fresh Guid per write (see CAppSettings.Write), so
            // assert on the absence of any *.tmp file in the store directory rather
            // than one exact name - this also catches debris under any future naming
            // scheme.
            Assert.Empty(Directory.GetFiles(_testStorePath, "*.tmp"));
        }

        [Fact]
        public void SetServers_WhenWriteFails_ReturnsFalseAndDoesNotThrow()
        {
            // Parent path is a file, so Directory.CreateDirectory throws:
            // ENOTDIR on Unix, IOException on Windows.
            var blocker = Path.Combine(_testStorePath, "blocker");
            File.WriteAllText(blocker, "not a directory");
            CAppSettings.StorePath = Path.Combine(blocker, "settings.json");

            Assert.False(CAppSettings.SetServers(new[] { "vbr01" }));
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
    }
}
