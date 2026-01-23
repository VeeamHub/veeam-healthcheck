using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html
{
    /// <summary>
    /// Tests for CBackupServerTableHelper functionality.
    /// Focus: Ensuring graceful handling of missing/empty CSV data (Issue #47).
    /// </summary>
    [Trait("Category", "Unit")]
    public class CBackupServerTableHelperTEST : IDisposable
    {
        private CDataTypesParser originalParser;
        private bool hadOriginalParser;

        public CBackupServerTableHelperTEST()
        {
            // Save original parser state
            hadOriginalParser = CGlobals.DtParser != null;
            originalParser = CGlobals.DtParser;

            // Initialize logger if needed
            if (CGlobals.Logger == null)
            {
                CGlobals.Logger = new VeeamHealthCheck.Shared.Logging.CLogger();
            }
        }

        public void Dispose()
        {
            // Restore original parser
            if (hadOriginalParser)
            {
                CGlobals.DtParser = originalParser;
            }
            else
            {
                CGlobals.DtParser = null;
            }
        }

        /// <summary>
        /// Test for Issue #47: NullReferenceException when ServerInfos is empty.
        /// This simulates the scenario where PowerShell fails and CSVs are empty.
        /// </summary>
        [Fact]
        public void SetBackupServerData_EmptyServerList_DoesNotThrowNullReference()
        {
            // Arrange: Set up empty server list (simulates failed PowerShell collection)
            CGlobals.DtParser = new CDataTypesParser();
            CGlobals.DtParser.ServerInfos = new List<CServerTypeInfos>(); // Empty list

            // Act & Assert: Should not throw NullReferenceException
            var exception = Record.Exception(() =>
            {
                var helper = new CBackupServerTableHelper(scrub: false);
                var result = helper.SetBackupServerData();
            });

            // The method should handle missing data gracefully without throwing NullReferenceException
            Assert.Null(exception);
        }

        /// <summary>
        /// Test for Issue #47: NullReferenceException when backup server not found in list.
        /// This simulates the scenario where the backup server ID doesn't match any server in CSV.
        /// </summary>
        [Fact]
        public void SetBackupServerData_BackupServerNotInList_DoesNotThrowNullReference()
        {
            // Arrange: Set up server list without the backup server ID
            CGlobals.DtParser = new CDataTypesParser();
            CGlobals.DtParser.ServerInfos = new List<CServerTypeInfos>
            {
                new CServerTypeInfos
                {
                    Id = "different-server-id",
                    Name = "SomeOtherServer",
                    Cores = 8,
                    Ram = 16
                }
            };

            // Act & Assert: Should not throw NullReferenceException
            var exception = Record.Exception(() =>
            {
                var helper = new CBackupServerTableHelper(scrub: false);
                var result = helper.SetBackupServerData();
            });

            // The method should handle missing backup server gracefully
            Assert.Null(exception);
        }

        /// <summary>
        /// Test: Normal scenario with valid backup server data.
        /// </summary>
        [Fact]
        public void SetBackupServerData_ValidBackupServer_SetsDataCorrectly()
        {
            // Arrange: Set up valid server list with backup server
            CGlobals.DtParser = new CDataTypesParser();
            CGlobals.DtParser.ServerInfos = new List<CServerTypeInfos>
            {
                new CServerTypeInfos
                {
                    Id = CGlobals.backupServerId, // Matches the expected ID
                    Name = "VBR-Server",
                    Cores = 16,
                    Ram = 64,
                    ApiVersion = "12.3.1.1139"
                }
            };

            // Act
            var helper = new CBackupServerTableHelper(scrub: false);
            var result = helper.SetBackupServerData();

            // Assert: Should complete without exception
            Assert.NotNull(result);
            Assert.Equal("VBR-Server", result.Name);
            Assert.Equal(16, result.Cores);
            Assert.Equal(64, result.RAM);
        }
    }
}
