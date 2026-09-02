// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
//
// Tests for the two guard lines that actually stop the #209 VB365 VMC.log
// NullReferenceException: GetLogDir()'s `if (match == null) return;` and PopulateVmc()'s
// `if (!string.IsNullOrEmpty(this.LOGLOCATION))`. Asserting only "INSTALLID is null and
// nothing threw" would pass even with both guards deleted - PopulateVmc's own catch swallows
// the ArgumentNullException that `new StreamReader(null)` would otherwise throw. These tests
// assert on which sink fired (Warning vs Error) instead, so removing a guard is caught.
//
// Vb365LogsDir/WarningSink/ErrorSink are test-only seams on CVmcReader (production never sets
// them, defaults preserve real behavior) - same pattern as CDbAccessor.RegReader/WarningSink
// in CDbAccessorTests.cs.
//
// These tests require Windows (WPF dependency in VeeamHealthCheck.csproj) and can only be
// compiled/run in CI (windows-latest) or a local Windows box - not on macOS.

using System;
using System.Collections.Generic;
using System.IO;
using VeeamHealthCheck.Functions.Collection.LogParser;
using Xunit;

namespace VeeamHealthCheck.Tests.Functions.Collection.LogParser
{
    public class CVmcReaderTests : IDisposable
    {
        private readonly string tempDir;

        public CVmcReaderTests()
        {
            this.tempDir = Path.Combine(Path.GetTempPath(), "CVmcReaderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(this.tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void PopulateVmc_Vb365DirectoryEmpty_WarnsAndLeavesInstallIdNull()
        {
            // Arrange - the exact condition that crashed CVmcReader.GetLogDir() before #209's
            // fix: Directory.GetFiles() returns an empty array.
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new("vb365")
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert - the "no VMC.log found" Warning fired; nothing reached the catch block.
            Assert.Null(reader.INSTALLID);
            Assert.Single(warnings);
            Assert.Contains("No VMC.log file found", warnings[0]);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_Vb365DirectoryHasNoMatchingFiles_WarnsAndLeavesInstallIdNull()
        {
            // Arrange - files exist, but none of them is a VMC.log.
            File.WriteAllText(Path.Combine(this.tempDir, "Collector.log"), "irrelevant");
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new("vb365")
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Null(reader.INSTALLID);
            Assert.Single(warnings);
            Assert.Contains("No VMC.log file found", warnings[0]);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_ValidVmcLog_ExtractsInstallId()
        {
            // Arrange - pins the parser's own contract (40-char prefix, second whitespace
            // token is the ID), NOT a real Veeam VMC.log sample - that format is unverified
            // anywhere in this repo or the local VBR docs mirror (see commit 9d50adf).
            string line = new string('X', 40) + "InstallationId: abc123token more-stuff";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new("vb365")
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Equal("abc123token", reader.INSTALLID);
            Assert.Empty(warnings);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_InstallationIdLineShiftedByLeadingSpace_RejectsLabelAsToken()
        {
            // Arrange - a prefix one character narrower than expected lands Substring(40) on
            // a leading space before "InstallationId:". string.Split() then emits a leading
            // empty entry, so id[1] becomes the "InstallationId:" label itself instead of the
            // real token - verified with an isolated Split() repro before writing this test.
            // The guard added in commit 9d50adf rejects this without assuming an ID format.
            string line = new string('X', 40) + " InstallationId: realtoken";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new("vb365")
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Null(reader.INSTALLID);
            Assert.Single(warnings);
            Assert.Contains("looked like the label", warnings[0]);
            Assert.Empty(errors);
        }
    }
}
