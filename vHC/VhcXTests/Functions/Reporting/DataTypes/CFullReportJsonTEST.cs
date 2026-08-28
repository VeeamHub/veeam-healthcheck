using System.Text.Json;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using Xunit;

namespace VhcXTests.Functions.Reporting.DataTypes
{
    /// <summary>
    /// Tests for the CFullReportJson top-level JSON export contract.
    /// Covers issue #172 (dead null fields removed) and the VhcVersion field.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CFullReportJsonTEST
    {
        [Fact]
        public void Serialize_DefaultInstance_DoesNotContainLegacyNullFields()
        {
            // Arrange
            var report = new CFullReportJson();

            // Act
            string json = JsonSerializer.Serialize(report);

            // Assert: issue #172 - cProtectedWorkloads and LicenseSummary were always
            // null and have been removed from the contract entirely, not just left null.
            Assert.DoesNotContain("cProtectedWorkloads", json);
            Assert.DoesNotContain("LicenseSummary", json);
        }

        [Fact]
        public void VhcVersion_Serialized_RoundTripsThroughJson()
        {
            // Arrange
            var report = new CFullReportJson { VhcVersion = "3.0.1.169" };

            // Act
            string json = JsonSerializer.Serialize(report);
            var roundTripped = JsonSerializer.Deserialize<CFullReportJson>(json);

            // Assert
            Assert.Contains("\"VhcVersion\":\"3.0.1.169\"", json);
            Assert.Equal("3.0.1.169", roundTripped.VhcVersion);
        }
    }
}
