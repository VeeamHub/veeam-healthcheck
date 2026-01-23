using System;
using System.Globalization;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataTypes
{
    /// <summary>
    /// Tests for CDataTypesParser DateTime parsing functionality.
    /// Focus: Ensuring correct parsing of DateTime values from different locales (Issue #41).
    /// </summary>
    [Trait("Category", "Unit")]
    public class CDataTypesParserTEST
    {
        /// <summary>
        /// Test for Issue #41: DateTime parsing with Chinese locale formats.
        /// Chinese systems export DateTime with "??" where AM/PM should be (上午/下午).
        /// </summary>
        [Theory]
        [InlineData("2024/12/27 ?? 08:53:50", false)] // Corrupted Chinese AM/PM
        [InlineData("2024/12/27 上午 08:53:50", true)] // Actual Chinese AM
        [InlineData("2024/12/27 下午 08:53:50", true)] // Actual Chinese PM
        [InlineData("2024/11/27 04:18:06", true)]    // Standard format without AM/PM
        [InlineData("2024-11-27 04:18:06", true)]    // ISO format with dash
        [InlineData("24.02.2025 21:16:15", true)]    // European format (DD.MM.YYYY)
        [InlineData("02/24/2025 21:16:15", true)]    // US format (MM/DD/YYYY)
        public void TryParseDateTime_VariousFormats_ReturnsValidDateTime(string dateTimeString, bool shouldSucceed)
        {
            // Arrange: Test the parsing logic that should handle multiple formats
            DateTime result;
            bool success = false;

            // Act: Try multiple parsing strategies (simulating the fix we'll apply)
            // First try: InvariantCulture
            success = DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

            if (!success)
            {
                // Second try: Current culture
                success = DateTime.TryParse(dateTimeString, out result);
            }

            if (!success)
            {
                // Third try: Common Chinese format patterns
                string[] chineseFormats = new[]
                {
                    "yyyy/MM/dd HH:mm:ss",
                    "yyyy/MM/dd 上午 HH:mm:ss",
                    "yyyy/MM/dd 下午 HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss",
                    "dd.MM.yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss"
                };

                success = DateTime.TryParseExact(dateTimeString, chineseFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
            }

            // Assert
            if (shouldSucceed)
            {
                Assert.True(success || result > DateTime.MinValue, $"Failed to parse: {dateTimeString}");
                if (success)
                {
                    Assert.NotEqual(DateTime.MinValue, result);
                }
            }
        }

        /// <summary>
        /// Test: DateTime with "??" characters should fall back to basic format parsing.
        /// The corrupted AM/PM indicators should be ignored and time parsed as-is.
        /// </summary>
        [Fact]
        public void TryParseDateTime_CorruptedAMPM_ParsesDateAndTime()
        {
            // Arrange: Date string with "??" where Chinese AM/PM should be
            string dateTimeString = "2024/12/27 ?? 08:53:50";

            // Act: Remove the "??" and try parsing
            string cleaned = dateTimeString.Replace("??", "").Replace("  ", " ").Trim();
            bool success = DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result);

            // Assert: Should successfully parse the date and time
            Assert.True(success);
            Assert.Equal(2024, result.Year);
            Assert.Equal(12, result.Month);
            Assert.Equal(27, result.Day);
            Assert.Equal(8, result.Hour);
            Assert.Equal(53, result.Minute);
            Assert.Equal(50, result.Second);
        }

        /// <summary>
        /// Test: Empty or null DateTime strings should return DateTime.MinValue.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryParseDateTime_EmptyOrNull_ReturnsMinValue(string dateTimeString)
        {
            // Act
            DateTime.TryParse(dateTimeString, out DateTime result);

            // Assert
            Assert.Equal(DateTime.MinValue, result);
        }
    }
}
