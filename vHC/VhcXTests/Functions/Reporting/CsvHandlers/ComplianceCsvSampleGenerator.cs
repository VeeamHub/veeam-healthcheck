using System.Collections.Generic;
using System.IO;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// Helper class to generate sample CSV files for testing different VBR versions and scenarios.
    /// Use this to create test data for integration testing without needing actual VBR environments.
    /// </summary>
    public static class ComplianceCsvSampleGenerator
    {
        /// <summary>
        /// Generate a sample Security & Compliance CSV for VBR v12
        /// </summary>
        public static string GenerateVbr12Sample(string hostname = "localhost")
        {
            var lines = new List<string>
            {
                @"""Best Practice"",""Status""",
                @"""Windows Script Host is disabled"",""Passed""",
                @"""Backup services run under the LocalSystem account"",""Passed""",
                @"""Outdated SSL And TLS are Disabled"",""Passed""",
                @"""Unknown Linux servers are not trusted automatically"",""Unable to detect""",
                @"""SMB v3 signing is enabled"",""Not Implemented""",
                @"""Host to proxy traffic encryption should be enabled for the Network transport mode"",""Passed""",
                @"""Backup jobs to cloud repositories is encrypted"",""Unable to detect""",
                @"""Link-Local Multicast Name Resolution (LLMNR) is disabled"",""Passed""",
                @"""Immutable or offline media is used"",""Not Implemented""",
                @"""Backup Server is Up To Date"",""Passed""",
                @"""Computer is Workgroup member"",""Passed""",
                @"""Reverse incremental backup mode is not used"",""Passed""",
                @"""Configuration backup encryption is enabled"",""Not Implemented""",
                @"""WDigest credentials caching is disabled"",""Passed""",
                @"""Web Proxy Auto-Discovery service (WinHttpAutoProxySvc) is disabled"",""Passed""",
                @"""All backups have at least one copy (the 3-2-1 backup rule)"",""Passed""",
                @"""SMB 1.0 is disabled"",""Passed""",
                @"""Email notifications are enabled"",""Not Implemented""",
                @"""Remote registry service is disabled"",""Passed""",
                @"""Credentials and encryption passwords rotates annually"",""Not Implemented""",
                @"""Remote powershell is disabled (WinRM service)"",""Not Implemented""",
                @"""MFA is enabled"",""Not Implemented""",
                @"""Hardened repositories have SSH disabled"",""Unable to detect""",
                @"""Linux servers have password-based authentication disabled"",""Unable to detect""",
                @"""Remote desktop protocol is disabled"",""Not Implemented""",
                @"""Configuration backup is enabled"",""Not Implemented""",
                @"""Windows firewall is enabled"",""Passed""",
                @"""Configuration backup is enabled and use encryption"",""Not Implemented""",
                @"""Hardened repositories are not hosted in virtual machines"",""Unable to detect""",
                @"""The configuration backup is not stored on the backup server"",""Not Implemented""",
                @"""PostgreSQL server uses recommended settings"",""Passed""",
                @"""Password loss protection is enabled"",""Passed""",
                @"""Encryption network rules added for LAN traffic"",""Not Implemented""",
                @"""NetBIOS protocol should be disabled on all network interfaces"",""Not Implemented""",
                @"""Hardened repository should not be used as proxy"",""Unable to detect""",
                @"""Local Security Authority Server Service (LSASS) running as protected process"",""Not Implemented""",
                @"""Backup encryption password length and complexity recommendations should be followed"",""Passed"""
            };

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Generate a sample Security & Compliance CSV for VBR v13 with new compliance checks
        /// </summary>
        public static string GenerateVbr13Sample(string hostname = "vbr-v13-server")
        {
            var lines = new List<string>
            {
                @"""Best Practice"",""Status""",
                // All VBR 12 checks (with updated names)
                @"""Windows Script Host is disabled"",""Passed""",
                @"""Backup services run under the LocalSystem account"",""Passed""",
                @"""Outdated SSL And TLS are Disabled"",""Passed""",
                @"""Unknown Linux servers are not trusted automatically"",""Unable to detect""",
                @"""SMB v3 signing is enabled"",""Not Implemented""",
                @"""Host to proxy traffic encryption should be enabled for the Network transport mode"",""Passed""",
                @"""Backup jobs to cloud repositories is encrypted"",""Unable to detect""",
                @"""Link-Local Multicast Name Resolution (LLMNR) is disabled"",""Passed""",
                @"""Immutable or offline media is used"",""Not Implemented""",
                @"""Backup Server is Up To Date"",""Passed""",
                @"""Computer is Workgroup member"",""Passed""",
                @"""Reverse incremental backup mode is not used"",""Passed""",
                @"""Configuration backup encryption is enabled"",""Not Implemented""",
                @"""WDigest credentials caching is disabled"",""Passed""",
                @"""Web Proxy Auto-Discovery service (WinHttpAutoProxySvc) is disabled"",""Passed""",
                @"""All backups have at least one copy (the 3-2-1 backup rule)"",""Passed""",
                @"""SMB 1.0 is disabled"",""Passed""",
                @"""Email notifications are enabled"",""Not Implemented""",
                @"""Remote registry service is disabled"",""Passed""",
                @"""Credentials and encryption passwords rotates annually"",""Not Implemented""",
                @"""Remote powershell is disabled (WinRM service)"",""Not Implemented""",
                @"""MFA is enabled"",""Not Implemented""",
                @"""Hardened repositories have SSH disabled"",""Unable to detect""",
                @"""Linux servers have password-based authentication disabled"",""Unable to detect""",
                @"""Remote desktop protocol is disabled"",""Not Implemented""",
                @"""Configuration backup is enabled"",""Not Implemented""",
                @"""Windows firewall is enabled"",""Passed""",
                @"""Configuration backup is enabled and use encryption"",""Not Implemented""",
                @"""Hardened repositories are not hosted in virtual machines"",""Unable to detect""",
                @"""The configuration backup is not stored on the backup server"",""Not Implemented""",
                @"""PostgreSQL server uses recommended settings"",""Passed""",
                @"""Password loss protection is enabled"",""Passed""",
                @"""Encryption network rules added for LAN traffic"",""Not Implemented""",
                @"""NetBIOS protocol should be disabled on all network interfaces"",""Not Implemented""",
                @"""Hardened repository should not be used as proxy"",""Unable to detect""",
                @"""Local Security Authority Server Service (LSASS) running as protected process"",""Not Implemented""",
                // VBR 13 NEW: Split password checks
                @"""Backup encryption password length and complexity recommendations should be followed"",""Passed""",
                @"""Credentials password length and complexity recommendations should be followed"",""Passed""",
                // VBR 13 NEW: Linux-specific checks
                @"""Linux audit binaries owner is root"",""Unable to detect""",
                @"""Linux auditd is configured"",""Unable to detect""",
                @"""Linux problematic services are disabled"",""Unable to detect""",
                @"""Linux OS has VA randomization enabled"",""Unable to detect""",
                @"""Linux OS has FIPS enabled"",""Unable to detect""",
                @"""Linux OS uses TCP syncookies"",""Unable to detect""",
                @"""Linux uses password policy"",""Unable to detect""",
                @"""Secure Boot is enabled"",""Not Implemented""",
                @"""Linux uses security module (SELinux/AppArmor)"",""Unable to detect""",
                @"""Linux world-writable directories have proper permissions"",""Unable to detect"""
            };

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Generate a minimal sample with only a few checks for quick testing
        /// </summary>
        public static string GenerateMinimalSample()
        {
            var lines = new List<string>
            {
                @"""Best Practice"",""Status""",
                @"""Windows firewall is enabled"",""Passed""",
                @"""MFA is enabled"",""Not Implemented""",
                @"""Configuration backup is enabled"",""Not Implemented"""
            };

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Generate a sample with all status types for testing
        /// </summary>
        public static string GenerateAllStatusTypesSample()
        {
            var lines = new List<string>
            {
                @"""Best Practice"",""Status""",
                @"""Test check passed"",""Passed""",
                @"""Test check not implemented"",""Not Implemented""",
                @"""Test check unable to detect"",""Unable to detect""",
                @"""Test check suppressed"",""Suppressed"""
            };

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Generate an empty sample (headers only) for testing edge cases
        /// </summary>
        public static string GenerateEmptySample()
        {
            return @"""Best Practice"",""Status""";
        }

        /// <summary>
        /// Save a generated sample to a file
        /// </summary>
        public static void SaveSampleToFile(string directoryPath, string fileName, string content)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var filePath = Path.Combine(directoryPath, fileName);
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Generate and save all sample types to a directory for manual testing
        /// </summary>
        public static void GenerateAllSamples(string outputDirectory)
        {
            SaveSampleToFile(outputDirectory, "VBR12_localhost_SecurityCompliance.csv", GenerateVbr12Sample());
            SaveSampleToFile(outputDirectory, "VBR13_vbr-server_SecurityCompliance.csv", GenerateVbr13Sample());
            SaveSampleToFile(outputDirectory, "Minimal_SecurityCompliance.csv", GenerateMinimalSample());
            SaveSampleToFile(outputDirectory, "AllStatusTypes_SecurityCompliance.csv", GenerateAllStatusTypesSample());
            SaveSampleToFile(outputDirectory, "Empty_SecurityCompliance.csv", GenerateEmptySample());
        }
    }
}
