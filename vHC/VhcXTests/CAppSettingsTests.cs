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
    }
}
