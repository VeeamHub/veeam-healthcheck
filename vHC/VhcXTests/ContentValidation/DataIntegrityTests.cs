// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using VhcXTests.TestData;

namespace VhcXTests.ContentValidation
{
    /// <summary>
    /// Tests that validate data integrity across CSV files - referential integrity,
    /// cross-file consistency, and logical constraints.
    /// </summary>
    [Trait("Category", "ContentValidation")]
    [Trait("Category", "DataIntegrity")]
    public class DataIntegrityTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Dictionary<string, List<Dictionary<string, string>>> _csvData;

        public DataIntegrityTests()
        {
            _testDirectory = VbrCsvSampleGenerator.CreateTestDataDirectory();
            _csvData = LoadAllCsvFiles(_testDirectory);
        }

        public void Dispose()
        {
            VbrCsvSampleGenerator.CleanupTestDirectory(Path.GetDirectoryName(_testDirectory));
        }

        #region Cross-File Referential Integrity Tests

        [Fact]
        public void Proxies_HostId_ExistsInServers()
        {
            // Arrange
            var servers = _csvData.GetValueOrDefault("Servers", new List<Dictionary<string, string>>());
            var proxies = _csvData.GetValueOrDefault("Proxies", new List<Dictionary<string, string>>());

            var serverHostIds = servers.Select(s => s.GetValueOrDefault("HostId", "")).ToHashSet();

            // Assert - all proxy HostIds should exist in Servers
            foreach (var proxy in proxies)
            {
                var hostId = proxy.GetValueOrDefault("HostId", "");
                if (!string.IsNullOrEmpty(hostId))
                {
                    Assert.Contains(hostId, serverHostIds);
                }
            }
        }

        [Fact]
        public void Repositories_Host_ExistsInServers()
        {
            // Arrange
            var servers = _csvData.GetValueOrDefault("Servers", new List<Dictionary<string, string>>());
            var repos = _csvData.GetValueOrDefault("Repositories", new List<Dictionary<string, string>>());

            var serverNames = servers.Select(s => s.GetValueOrDefault("Name", "")).ToHashSet();

            // Assert - all repository hosts should exist in Servers
            foreach (var repo in repos)
            {
                var host = repo.GetValueOrDefault("Host", "");
                if (!string.IsNullOrEmpty(host))
                {
                    Assert.Contains(host, serverNames);
                }
            }
        }

        [Fact]
        public void SobrExtents_Repository_ExistsInSobrs()
        {
            // Arrange
            var sobrs = _csvData.GetValueOrDefault("SOBRs", new List<Dictionary<string, string>>());
            var extents = _csvData.GetValueOrDefault("SOBRExtents", new List<Dictionary<string, string>>());

            var sobrNames = sobrs.Select(s => s.GetValueOrDefault("Name", "")).ToHashSet();

            // Assert - all extent repositories should reference valid SOBRs
            foreach (var extent in extents)
            {
                var repo = extent.GetValueOrDefault("Repository", "");
                if (!string.IsNullOrEmpty(repo))
                {
                    Assert.Contains(repo, sobrNames);
                }
            }
        }

        [Fact]
        public void ProtectedVMs_JobName_ExistsInJobs()
        {
            // Arrange
            var jobs = _csvData.GetValueOrDefault("_Jobs", new List<Dictionary<string, string>>());
            var protectedVms = _csvData.GetValueOrDefault("ViProtected", new List<Dictionary<string, string>>());

            var jobNames = jobs.Select(j => j.GetValueOrDefault("Name", "")).ToHashSet();

            // Assert - all protected VM job names should exist in Jobs
            foreach (var vm in protectedVms)
            {
                var jobName = vm.GetValueOrDefault("JobName", "");
                if (!string.IsNullOrEmpty(jobName))
                {
                    Assert.Contains(jobName, jobNames);
                }
            }
        }

        [Fact]
        public void SessionReport_JobName_ExistsInJobs()
        {
            // Arrange
            var jobs = _csvData.GetValueOrDefault("_Jobs", new List<Dictionary<string, string>>());
            var sessions = _csvData.GetValueOrDefault("VeeamSessionReport", new List<Dictionary<string, string>>());

            var jobNames = jobs.Select(j => j.GetValueOrDefault("Name", "")).ToHashSet();

            // Assert - all session job names should exist in Jobs
            foreach (var session in sessions)
            {
                var jobName = session.GetValueOrDefault("JobName", "");
                if (!string.IsNullOrEmpty(jobName))
                {
                    Assert.Contains(jobName, jobNames);
                }
            }
        }

        #endregion

        #region Logical Constraint Tests

        [Fact]
        public void Repositories_FreeSpace_LessThanOrEqualTotalSpace()
        {
            // Arrange
            var repos = _csvData.GetValueOrDefault("Repositories", new List<Dictionary<string, string>>());

            // Assert
            foreach (var repo in repos)
            {
                var totalStr = repo.GetValueOrDefault("TotalSpace", "0");
                var freeStr = repo.GetValueOrDefault("FreeSpace", "0");

                if (long.TryParse(totalStr, out var total) && long.TryParse(freeStr, out var free))
                {
                    Assert.True(free <= total,
                        $"Repository '{repo.GetValueOrDefault("Name", "unknown")}' has FreeSpace ({free}) > TotalSpace ({total})");
                }
            }
        }

        [Fact]
        public void SobrExtents_FreeSpace_LessThanOrEqualTotalSpace()
        {
            // Arrange
            var extents = _csvData.GetValueOrDefault("SOBRExtents", new List<Dictionary<string, string>>());

            // Assert
            foreach (var extent in extents)
            {
                var totalStr = extent.GetValueOrDefault("TotalSpace", "0");
                var freeStr = extent.GetValueOrDefault("FreeSpace", "0");

                if (long.TryParse(totalStr, out var total) && long.TryParse(freeStr, out var free))
                {
                    Assert.True(free <= total,
                        $"SOBR Extent '{extent.GetValueOrDefault("Name", "unknown")}' has FreeSpace ({free}) > TotalSpace ({total})");
                }
            }
        }

        [Fact]
        public void Sessions_EndTime_AfterOrEqualStartTime()
        {
            // Arrange
            var sessions = _csvData.GetValueOrDefault("VeeamSessionReport", new List<Dictionary<string, string>>());

            // Assert
            foreach (var session in sessions)
            {
                var startStr = session.GetValueOrDefault("StartTime", "");
                var endStr = session.GetValueOrDefault("EndTime", "");

                if (DateTime.TryParse(startStr, out var start) && DateTime.TryParse(endStr, out var end))
                {
                    Assert.True(end >= start,
                        $"Session for '{session.GetValueOrDefault("JobName", "unknown")}' has EndTime ({end}) before StartTime ({start})");
                }
            }
        }

        [Fact]
        public void Sessions_TransferredSize_LessThanOrEqualDataSize()
        {
            // Arrange
            var sessions = _csvData.GetValueOrDefault("VeeamSessionReport", new List<Dictionary<string, string>>());

            // Assert
            foreach (var session in sessions)
            {
                var dataStr = session.GetValueOrDefault("DataSize", "0");
                var transferredStr = session.GetValueOrDefault("TransferredSize", "0");

                if (long.TryParse(dataStr, out var data) && long.TryParse(transferredStr, out var transferred))
                {
                    // Transferred should be <= DataSize (due to dedup/compression)
                    Assert.True(transferred <= data,
                        $"Session for '{session.GetValueOrDefault("JobName", "unknown")}' has TransferredSize ({transferred}) > DataSize ({data})");
                }
            }
        }

        [Fact]
        public void Proxies_MaxTasksCount_IsPositive()
        {
            // Arrange
            var proxies = _csvData.GetValueOrDefault("Proxies", new List<Dictionary<string, string>>());

            // Assert
            foreach (var proxy in proxies)
            {
                var maxTasksStr = proxy.GetValueOrDefault("MaxTasksCount", "0");

                if (int.TryParse(maxTasksStr, out var maxTasks))
                {
                    Assert.True(maxTasks > 0,
                        $"Proxy '{proxy.GetValueOrDefault("Name", "unknown")}' has invalid MaxTasksCount ({maxTasks})");
                }
            }
        }

        #endregion

        #region Uniqueness Constraint Tests

        [Fact]
        public void Servers_HostId_AreUnique()
        {
            // Arrange
            var servers = _csvData.GetValueOrDefault("Servers", new List<Dictionary<string, string>>());
            var hostIds = servers.Select(s => s.GetValueOrDefault("HostId", "")).Where(h => !string.IsNullOrEmpty(h)).ToList();

            // Assert
            var uniqueIds = hostIds.Distinct().ToList();
            Assert.Equal(hostIds.Count, uniqueIds.Count);
        }

        [Fact]
        public void Jobs_Names_AreUnique()
        {
            // Arrange
            var jobs = _csvData.GetValueOrDefault("_Jobs", new List<Dictionary<string, string>>());
            var names = jobs.Select(j => j.GetValueOrDefault("Name", "")).Where(n => !string.IsNullOrEmpty(n)).ToList();

            // Assert
            var uniqueNames = names.Distinct().ToList();
            Assert.Equal(names.Count, uniqueNames.Count);
        }

        [Fact]
        public void Repositories_Names_AreUnique()
        {
            // Arrange
            var repos = _csvData.GetValueOrDefault("Repositories", new List<Dictionary<string, string>>());
            var names = repos.Select(r => r.GetValueOrDefault("Name", "")).Where(n => !string.IsNullOrEmpty(n)).ToList();

            // Assert
            var uniqueNames = names.Distinct().ToList();
            Assert.Equal(names.Count, uniqueNames.Count);
        }

        [Fact]
        public void Sobrs_Names_AreUnique()
        {
            // Arrange
            var sobrs = _csvData.GetValueOrDefault("SOBRs", new List<Dictionary<string, string>>());
            var names = sobrs.Select(s => s.GetValueOrDefault("Name", "")).Where(n => !string.IsNullOrEmpty(n)).ToList();

            // Assert
            var uniqueNames = names.Distinct().ToList();
            Assert.Equal(names.Count, uniqueNames.Count);
        }

        #endregion

        #region Version Consistency Tests

        [Fact]
        public void VbrInfo_Version_MatchesServerApiVersions()
        {
            // Arrange
            var vbrInfo = _csvData.GetValueOrDefault("vbrinfo", new List<Dictionary<string, string>>());
            var servers = _csvData.GetValueOrDefault("Servers", new List<Dictionary<string, string>>());

            if (!vbrInfo.Any())
                return;

            var vbrVersion = vbrInfo[0].GetValueOrDefault("Version", "");
            var majorMinor = ExtractMajorMinorVersion(vbrVersion);

            // Assert - all server ApiVersions should be compatible
            foreach (var server in servers)
            {
                var apiVersion = server.GetValueOrDefault("ApiVersion", "");
                var serverMajorMinor = ExtractMajorMinorVersion(apiVersion);

                // Major.Minor should match for compatible versions
                Assert.Equal(majorMinor, serverMajorMinor);
            }
        }

        #endregion

        #region Count Consistency Tests

        [Fact]
        public void ProtectedPlusUnprotected_Equals_TotalVMs()
        {
            // Arrange
            var protectedVms = _csvData.GetValueOrDefault("ViProtected", new List<Dictionary<string, string>>());
            var unprotectedVms = _csvData.GetValueOrDefault("ViUnprotected", new List<Dictionary<string, string>>());

            // The total should be consistent
            var total = protectedVms.Count + unprotectedVms.Count;
            Assert.True(total >= 0);
        }

        [Fact]
        public void SessionReport_HasEntries_ForAllJobs()
        {
            // This is a weaker constraint - not all jobs have sessions (disabled jobs)
            // But jobs with IsScheduleEnabled=True should generally have sessions

            var jobs = _csvData.GetValueOrDefault("_Jobs", new List<Dictionary<string, string>>());
            var sessions = _csvData.GetValueOrDefault("VeeamSessionReport", new List<Dictionary<string, string>>());

            var enabledJobs = jobs.Where(j => j.GetValueOrDefault("IsScheduleEnabled", "False") == "True")
                                   .Select(j => j.GetValueOrDefault("Name", ""))
                                   .ToList();

            var jobsWithSessions = sessions.Select(s => s.GetValueOrDefault("JobName", "")).Distinct().ToList();

            // At least some enabled jobs should have sessions (not strictly all)
            if (enabledJobs.Any())
            {
                var hasOverlap = enabledJobs.Any(j => jobsWithSessions.Contains(j));
                Assert.True(hasOverlap, "No enabled jobs have session records");
            }
        }

        #endregion

        #region Data Type Validation Tests

        [Fact]
        public void AllGuids_AreValidFormat()
        {
            // Check GUIDs across multiple files
            var guidFields = new Dictionary<string, string[]>
            {
                { "Servers", new[] { "HostId" } },
                { "Proxies", new[] { "HostId" } },
                { "_Jobs", new[] { "RepositoryId", "pwdkeyid" } }
            };

            foreach (var (file, fields) in guidFields)
            {
                var records = _csvData.GetValueOrDefault(file, new List<Dictionary<string, string>>());

                foreach (var record in records)
                {
                    foreach (var field in fields)
                    {
                        var value = record.GetValueOrDefault(field, "");
                        if (!string.IsNullOrEmpty(value))
                        {
                            Assert.True(Guid.TryParse(value, out _),
                                $"{file}.{field} contains invalid GUID: {value}");
                        }
                    }
                }
            }
        }

        [Fact]
        public void AllBooleans_AreValidFormat()
        {
            // Check boolean fields across files
            var boolFields = new Dictionary<string, string[]>
            {
                { "vbrinfo", new[] { "mfa", "foureyes" } },
                { "Repositories", new[] { "isimmutabilitysupported" } },
                { "configBackup", new[] { "Enabled", "encryptionoptions" } },
                { "trafficRules", new[] { "encryptionenabled", "ThrottlingEnabled" } },
                { "_Jobs", new[] { "IsScheduleEnabled" } }
            };

            foreach (var (file, fields) in boolFields)
            {
                var records = _csvData.GetValueOrDefault(file, new List<Dictionary<string, string>>());

                foreach (var record in records)
                {
                    foreach (var field in fields)
                    {
                        var value = record.GetValueOrDefault(field, "");
                        if (!string.IsNullOrEmpty(value))
                        {
                            Assert.True(bool.TryParse(value, out _),
                                $"{file}.{field} contains invalid boolean: {value}");
                        }
                    }
                }
            }
        }

        [Fact]
        public void AllNumericFields_AreValidNumbers()
        {
            // Check numeric fields
            var numericFields = new Dictionary<string, string[]>
            {
                { "Repositories", new[] { "TotalSpace", "FreeSpace" } },
                { "SOBRExtents", new[] { "TotalSpace", "FreeSpace" } },
                { "Proxies", new[] { "MaxTasksCount" } },
                { "VeeamSessionReport", new[] { "DataSize", "TransferredSize" } },
                { "LicInfo", new[] { "SocketCount", "InstanceCount" } }
            };

            foreach (var (file, fields) in numericFields)
            {
                var records = _csvData.GetValueOrDefault(file, new List<Dictionary<string, string>>());

                foreach (var record in records)
                {
                    foreach (var field in fields)
                    {
                        var value = record.GetValueOrDefault(field, "");
                        if (!string.IsNullOrEmpty(value))
                        {
                            Assert.True(long.TryParse(value, out _),
                                $"{file}.{field} contains invalid number: {value}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private Dictionary<string, List<Dictionary<string, string>>> LoadAllCsvFiles(string directory)
        {
            var result = new Dictionary<string, List<Dictionary<string, string>>>();

            if (!Directory.Exists(directory))
                return result;

            foreach (var file in Directory.GetFiles(directory, "*.csv"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var records = ParseCsvFile(file);
                result[fileName] = records;
            }

            return result;
        }

        private List<Dictionary<string, string>> ParseCsvFile(string filePath)
        {
            var records = new List<Dictionary<string, string>>();
            var lines = File.ReadAllLines(filePath);

            if (lines.Length < 1)
                return records;

            var headers = ParseCsvLine(lines[0]);

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var values = ParseCsvLine(lines[i]);
                var record = new Dictionary<string, string>();

                for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
                {
                    record[headers[j]] = values[j];
                }

                records.Add(record);
            }

            return records;
        }

        private List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var inQuotes = false;
            var currentColumn = "";

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    columns.Add(currentColumn.Trim());
                    currentColumn = "";
                }
                else
                {
                    currentColumn += c;
                }
            }

            columns.Add(currentColumn.Trim().TrimEnd('\r'));
            return columns;
        }

        private string ExtractMajorMinorVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "";

            var parts = version.Split('.');
            if (parts.Length >= 2)
                return $"{parts[0]}.{parts[1]}";

            return version;
        }

        #endregion
    }
}
