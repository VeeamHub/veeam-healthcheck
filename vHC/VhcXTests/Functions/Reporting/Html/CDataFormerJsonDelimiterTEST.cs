using System.Collections.Generic;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.Html;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Tests for issue #171: JSON export must not leak HTML markup.
    /// CDataFormer's multi-value producers must join with a plain delimiter (|),
    /// not the HTML "&lt;br&gt;" separator that used to be captured verbatim into JSON.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class CDataFormerJsonDelimiterTEST
    {
        [Fact]
        public void RegOptions_MultiValueRegistryKey_JoinsWithPipeNotHtmlBreak()
        {
            // Arrange
            var originalKeys = CGlobals.DEFAULTREGISTRYKEYS;
            CGlobals.DEFAULTREGISTRYKEYS = new Dictionary<string, object>
            {
                ["VhcTest_MultiValueKey"] = new[] { "austriaeast", "brazilsoutheast" },
            };

            try
            {
                var df = new CDataFormer();

                // Act
                Dictionary<string, string> result = df.RegOptions();

                // Assert
                Assert.Equal("austriaeast|brazilsoutheast", result["VhcTest_MultiValueKey"]);
                Assert.DoesNotContain("<br>", result["VhcTest_MultiValueKey"]);
            }
            finally
            {
                CGlobals.DEFAULTREGISTRYKEYS = originalKeys;
            }
        }

        [Fact]
        public void SummarizeRoleTypes_MultipleRoles_JoinsWithPipeNotHtmlBreak()
        {
            // Arrange
            var df = new CDataFormer();

            // Act
            string result = df.SummarizeRoleTypes("Gateway/ Gateway/ Gateway/ Repository/ Gateway");

            // Assert
            Assert.DoesNotContain("<br>", result);
            Assert.Contains("|", result);
        }

        [Fact]
        public void SetGateHosts_MultipleHosts_JoinsWithPipeNotHtmlBreak()
        {
            // Arrange
            var df = new CDataFormer();

            // Act
            string result = df.SetGateHosts("gateway1 gateway2", false);

            // Assert
            Assert.Equal("gateway1,|gateway2|", result);
            Assert.DoesNotContain("<br>", result);
        }
    }
}
