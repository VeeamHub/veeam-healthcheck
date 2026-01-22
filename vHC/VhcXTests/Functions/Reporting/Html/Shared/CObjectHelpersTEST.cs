using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.Shared
{
    /// <summary>
    /// Tests for CObjectHelpers utility methods.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CObjectHelpersTEST
    {
        #region ParseBool Tests

        [Theory]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("TRUE", true)]
        public void ParseBool_TrueValues_ReturnsTrue(string input, bool expected)
        {
            var result = CObjectHelpers.ParseBool(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("FALSE", false)]
        public void ParseBool_FalseValues_ReturnsFalse(string input, bool expected)
        {
            var result = CObjectHelpers.ParseBool(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseBool_NullValue_ReturnsFalse()
        {
            var result = CObjectHelpers.ParseBool(null);
            Assert.False(result);
        }

        [Fact]
        public void ParseBool_EmptyString_ReturnsFalse()
        {
            var result = CObjectHelpers.ParseBool(string.Empty);
            Assert.False(result);
        }

        [Theory]
        [InlineData("yes")]
        [InlineData("no")]
        [InlineData("1")]
        [InlineData("0")]
        [InlineData("invalid")]
        [InlineData("truse")]  // typo
        [InlineData("TrUe")]   // mixed case not supported
        public void ParseBool_InvalidValues_ReturnsFalse(string input)
        {
            var result = CObjectHelpers.ParseBool(input);
            Assert.False(result);
        }

        [Theory]
        [InlineData(" true")]   // leading space
        [InlineData("true ")]   // trailing space
        [InlineData(" true ")]  // both
        public void ParseBool_WhitespaceAroundValue_ReturnsFalse(string input)
        {
            // Current implementation doesn't trim, so these return false
            var result = CObjectHelpers.ParseBool(input);
            Assert.False(result);
        }

        #endregion

        #region ParseInt Tests

        [Theory]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("42", 42)]
        [InlineData("100", 100)]
        [InlineData("-1", -1)]
        [InlineData("-100", -100)]
        public void ParseInt_ValidIntegers_ReturnsExpectedValue(string input, int expected)
        {
            var result = CObjectHelpers.ParseInt(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ParseInt_NullValue_ReturnsZero()
        {
            var result = CObjectHelpers.ParseInt(null);
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        [InlineData("abc")]
        [InlineData("12.5")]      // decimal
        [InlineData("1,000")]     // with comma
        [InlineData("1 000")]     // with space
        public void ParseInt_InvalidValues_ReturnsZero(string input)
        {
            var result = CObjectHelpers.ParseInt(input);
            Assert.Equal(0, result);
        }

        [Fact]
        public void ParseInt_LargePositiveNumber_ReturnsExpectedValue()
        {
            var result = CObjectHelpers.ParseInt("2147483647"); // int.MaxValue
            Assert.Equal(int.MaxValue, result);
        }

        [Fact]
        public void ParseInt_LargeNegativeNumber_ReturnsExpectedValue()
        {
            var result = CObjectHelpers.ParseInt("-2147483648"); // int.MinValue
            Assert.Equal(int.MinValue, result);
        }

        [Fact]
        public void ParseInt_OverflowValue_ReturnsZero()
        {
            var result = CObjectHelpers.ParseInt("2147483648"); // int.MaxValue + 1
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData(" 42")]   // leading space
        [InlineData("42 ")]   // trailing space
        public void ParseInt_WhitespaceAroundValue_ParsesCorrectly(string input)
        {
            // int.TryParse handles leading/trailing whitespace
            var result = CObjectHelpers.ParseInt(input);
            Assert.Equal(42, result);
        }

        #endregion
    }
}
