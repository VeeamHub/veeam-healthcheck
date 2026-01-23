using System;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using Xunit;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// Tests for CEntraObjects CSV parsing.
    /// Focus: Ensuring graceful handling of empty CSV values (Issue #41).
    /// </summary>
    [Trait("Category", "Unit")]
    public class CEntraObjectsTEST
    {
        /// <summary>
        /// Test for Issue #41: "The conversion cannot be performed" error when CSV contains empty values.
        /// This simulates the scenario where Entra job CSVs have headers but empty data rows.
        /// </summary>
        [Fact]
        public void CEntraLogJobs_EmptyCSVValues_DoesNotThrowConversionError()
        {
            // Arrange: Create CSV with empty values (as seen in localhost_entraLogJob.csv)
            var csvContent = @"""Name"",""Tenant"",""shortTermRetType"",""ShortTermRepo"",""ShortTermRepoRetention"",""CopyModeEnabled"",""SecondaryTarget""
,,,,,,";

            // Act & Assert: Should parse without throwing "The conversion cannot be performed" error
            using (var reader = new StringReader(csvContent))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var exception = Record.Exception(() =>
                {
                    var records = csv.GetRecords<CEntraLogJobs>().ToList();
                });

                // Should not throw exception
                Assert.Null(exception);
            }
        }

        /// <summary>
        /// Test for Issue #41: Entra tenant job CSV with empty values.
        /// </summary>
        [Fact]
        public void CEntraTenantJobs_EmptyCSVValues_DoesNotThrowConversionError()
        {
            // Arrange: Create CSV with empty values (as seen in localhost_entraTenantJob.csv)
            var csvContent = @"""Name"",""RetentionPolicy""
,";

            // Act & Assert: Should parse without throwing conversion error
            using (var reader = new StringReader(csvContent))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var exception = Record.Exception(() =>
                {
                    var records = csv.GetRecords<CEntraTenantJobs>().ToList();
                });

                // Should not throw exception
                Assert.Null(exception);
            }
        }

        /// <summary>
        /// Test: Valid Entra job data parses correctly.
        /// </summary>
        [Fact]
        public void CEntraLogJobs_ValidData_ParsesCorrectly()
        {
            // Arrange
            var csvContent = @"""Name"",""Tenant"",""shortTermRetType"",""ShortTermRepo"",""ShortTermRepoRetention"",""CopyModeEnabled"",""SecondaryTarget""
""TestJob"",""TestTenant"",""Days"",""TestRepo"",30,true,""BackupRepo""";

            // Act
            using (var reader = new StringReader(csvContent))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                var records = csv.GetRecords<CEntraLogJobs>().ToList();

                // Assert
                Assert.Single(records);
                Assert.Equal("TestJob", records[0].Name);
                Assert.Equal("TestTenant", records[0].Tenant);
                Assert.Equal(30, records[0].ShortTermRepoRetention);
                Assert.True(records[0].CopyModeEnabled);
            }
        }
    }
}
