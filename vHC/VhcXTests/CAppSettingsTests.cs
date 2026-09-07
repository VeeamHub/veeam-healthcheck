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
        public void TryLoad_DistinguishesAbsentFromUnreadable()
        {
            // Absent: no file at all.
            var absent = CAppSettings.TryLoad(out _);

            // Unreadable: file exists but is not parseable.
            Directory.CreateDirectory(Path.GetDirectoryName(CAppSettings.StorePath)!);
            File.WriteAllText(CAppSettings.StorePath, "{ not valid json ");
            var unreadable = CAppSettings.TryLoad(out _);

            // Loaded: a real file.
            CAppSettings.SetServers(new[] { "vbr01" });
            var loaded = CAppSettings.TryLoad(out var settings);

            // One Fact with several asserts rather than a Theory: SettingsLoadResult
            // is internal, and an internal enum as an [InlineData] parameter produces
            // CS0051 on the generated public test method.
            Assert.Equal(SettingsLoadResult.Absent, absent);
            Assert.Equal(SettingsLoadResult.Unreadable, unreadable);
            Assert.Equal(SettingsLoadResult.Loaded, loaded);
            Assert.Equal(new[] { "vbr01" }, settings.Servers);
        }
    }
}
