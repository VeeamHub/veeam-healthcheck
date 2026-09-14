using System;
using System.Globalization;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataTypes
{
    /// <summary>
    /// Tests for CDataTypesParser DateTime parsing functionality.
    /// Focus: Ensuring correct parsing of DateTime values from different locales (Issue #41)
    /// and from different day-first/month-first collecting-machine cultures (Issue #217).
    /// </summary>
    [Trait("Category", "Unit")]
    public class CDataTypesParserTEST
    {
        /// <summary>
        /// Test for Issue #41: DateTime parsing with Chinese locale formats, exercised via the
        /// real parser pinned to en-US so the collecting-machine-culture rung behaves like
        /// InvariantCulture did before Issue #217's fix.
        /// Chinese systems export DateTime with "??" where AM/PM should be (上午/下午).
        /// </summary>
        [Theory]
        [InlineData("2024/12/27 ?? 08:53:50", 2024, 12, 27)] // Corrupted Chinese AM/PM -> "??" stripped, parses as 08:53:50
        [InlineData("2024/12/27 上午 08:53:50", 2024, 12, 27)] // Actual Chinese AM (literal marker, matched by the explicit format list)
        [InlineData("2024/12/27 下午 08:53:50", 2024, 12, 27)] // Actual Chinese PM (literal marker - the format is not a real PM designator, so the hour is NOT shifted)
        [InlineData("2024/11/27 04:18:06", 2024, 11, 27)]    // Standard format without AM/PM
        [InlineData("2024-11-27 04:18:06", 2024, 11, 27)]    // ISO format with dash
        [InlineData("24.02.2025 21:16:15", 2025, 2, 24)]     // European format (DD.MM.YYYY)
        public void TryParseDateTime_VariousFormats_ReturnsValidDateTime(string dateTimeString, int year, int month, int day)
        {
            DateTime result = CDataTypesParser.TryParseDateTime(dateTimeString, new CultureInfo("en-US"));

            Assert.NotEqual(DateTime.MinValue, result);
            Assert.Equal(year, result.Year);
            Assert.Equal(month, result.Month);
            Assert.Equal(day, result.Day);
        }

        /// <summary>
        /// Test: DateTime with "??" characters should have the corrupted AM/PM indicator
        /// stripped and the date/time parsed as-is (no AM/PM shift applied).
        /// </summary>
        [Fact]
        public void TryParseDateTime_CorruptedAMPM_ParsesDateAndTime()
        {
            string dateTimeString = "2024/12/27 ?? 08:53:50";

            DateTime result = CDataTypesParser.TryParseDateTime(dateTimeString, new CultureInfo("en-US"));

            Assert.Equal(2024, result.Year);
            Assert.Equal(12, result.Month);
            Assert.Equal(27, result.Day);
            Assert.Equal(8, result.Hour);
            Assert.Equal(53, result.Minute);
            Assert.Equal(50, result.Second);
        }

        /// <summary>
        /// Regression test for Issue #217: on a day-first locale, an ambiguous date like
        /// "2/09/2026" (day/month, both &lt;= 12) must parse as day-first (2 September), not be
        /// silently swapped to month-first (9 February) by falling through to InvariantCulture.
        /// </summary>
        [Fact]
        public void TryParseDateTime_DayFirstLocaleAmbiguousDate_ParsesAsDayFirst()
        {
            DateTime result = CDataTypesParser.TryParseDateTime("2/09/2026 11:00:06 PM", new CultureInfo("en-AU"));

            Assert.Equal(new DateTime(2026, 9, 2, 23, 0, 6), result);
            Assert.Equal(9, result.Month);
        }

        /// <summary>
        /// More ambiguous day-first-locale dates (day-of-month &lt;= 12), pinned to en-AU, plus
        /// one unambiguous case (day > 12) confirming the InvariantCulture fallback rung still
        /// isn't needed/doesn't interfere when the culture rung already succeeds.
        /// </summary>
        [Theory]
        [InlineData("1/03/2026 08:15:00 AM", 2026, 3, 1)]    // ambiguous: day-first -> 1 March
        [InlineData("5/11/2026 06:30:45 PM", 2026, 11, 5)]   // ambiguous: day-first -> 5 November
        [InlineData("12/01/2026 12:00:00 AM", 2026, 1, 12)]  // ambiguous: day-first -> 12 January
        [InlineData("25/12/2026 09:00:00 AM", 2026, 12, 25)] // unambiguous: day > 12, only valid as day-first
        public void TryParseDateTime_DayFirstLocaleAmbiguousDates_ParseAsDayFirst(string dateTimeString, int year, int month, int day)
        {
            DateTime result = CDataTypesParser.TryParseDateTime(dateTimeString, new CultureInfo("en-AU"));

            Assert.Equal(year, result.Year);
            Assert.Equal(month, result.Month);
            Assert.Equal(day, result.Day);
        }

        /// <summary>
        /// A US-shaped date (MM/dd/yyyy, unambiguous because day > 12) read on a day-first
        /// machine must still parse correctly via the InvariantCulture fallback rung.
        /// </summary>
        [Fact]
        public void TryParseDateTime_UsFormatOnDayFirstLocale_FallsBackToInvariantCulture()
        {
            DateTime result = CDataTypesParser.TryParseDateTime("02/24/2025 21:16:15", new CultureInfo("en-AU"));

            Assert.Equal(2025, result.Year);
            Assert.Equal(2, result.Month);
            Assert.Equal(24, result.Day);
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
            DateTime result = CDataTypesParser.TryParseDateTime(dateTimeString, CultureInfo.InvariantCulture);

            Assert.Equal(DateTime.MinValue, result);
        }
    }
}
