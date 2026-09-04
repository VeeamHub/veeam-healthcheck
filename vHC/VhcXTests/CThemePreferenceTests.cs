// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Styling;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    public class CThemePreferenceTests
    {
        [Fact]
        public void ToVariant_Dark_ReturnsThemeVariantDark()
        {
            Assert.Equal(ThemeVariant.Dark, CThemePreference.ToVariant("Dark"));
        }

        [Fact]
        public void ToVariant_Light_ReturnsThemeVariantLight()
        {
            Assert.Equal(ThemeVariant.Light, CThemePreference.ToVariant("Light"));
        }

        [Theory]
        [InlineData("System")]
        [InlineData("anything-unexpected")]
        [InlineData("")]
        public void ToVariant_SystemOrUnexpectedValue_ReturnsThemeVariantDefault(string preference)
        {
            Assert.Equal(ThemeVariant.Default, CThemePreference.ToVariant(preference));
        }

        [Fact]
        public void FromVariant_Dark_ReturnsDarkString()
        {
            Assert.Equal("Dark", CThemePreference.FromVariant(ThemeVariant.Dark));
        }

        [Fact]
        public void FromVariant_Light_ReturnsLightString()
        {
            Assert.Equal("Light", CThemePreference.FromVariant(ThemeVariant.Light));
        }

        [Fact]
        public void FromVariant_Default_ReturnsSystemString()
        {
            Assert.Equal(CThemePreference.Default, CThemePreference.FromVariant(ThemeVariant.Default));
        }

        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        [InlineData("System")]
        public void ToVariant_ThenFromVariant_RoundTrips(string preference)
        {
            var variant = CThemePreference.ToVariant(preference);

            Assert.Equal(preference, CThemePreference.FromVariant(variant));
        }
    }
}
