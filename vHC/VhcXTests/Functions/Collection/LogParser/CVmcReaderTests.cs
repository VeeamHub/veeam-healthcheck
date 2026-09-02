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
            CVmcReader reader = new(VmcLogMode.Vb365)
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
            CVmcReader reader = new(VmcLogMode.Vb365)
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
            CVmcReader reader = new(VmcLogMode.Vb365)
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
        public void PopulateVmc_InstallationIdLineShiftedByLeadingSpace_StillExtractsId()
        {
            // Arrange - a prefix one character narrower than expected lands Substring(40) on
            // a leading space before "InstallationId:". The label-token lookup tolerates any
            // amount of leading whitespace instead of assuming the label sits at a fixed
            // offset, so this is correctly parsed rather than rejected.
            string line = new string('X', 40) + " InstallationId: realtoken";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new(VmcLogMode.Vb365)
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Equal("realtoken", reader.INSTALLID);
            Assert.Empty(warnings);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_InstallationIdLineShiftedByMultipleLeadingSpaces_StillExtractsId()
        {
            // Arrange - PR #210 review round 2, finding #1: a shift of 2+ leading whitespace
            // characters used to make Split() emit two leading empty entries, so id[1] became
            // "" instead of either the label or the real token - silently blanking an
            // otherwise-good install ID with no warning. Verified via an isolated Split()
            // repro before writing this test.
            string line = new string('X', 40) + "   InstallationId: realtoken";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new(VmcLogMode.Vb365)
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Equal("realtoken", reader.INSTALLID);
            Assert.Empty(warnings);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_InstallIdContainsColon_StillExtractsId()
        {
            // Arrange - PR #210 review round 2, finding #6: the previous guard rejected any
            // token containing ':' on the assumption install IDs never contain one, even
            // though that format is unverified anywhere in this repo. The label-token lookup
            // makes no assumption about the ID's shape, so a colon-containing token is
            // extracted like any other.
            string line = new string('X', 40) + "InstallationId: real:token";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new(VmcLogMode.Vb365)
            {
                Vb365LogsDir = this.tempDir,
                WarningSink = warnings.Add,
                ErrorSink = errors.Add,
            };

            // Act
            reader.PopulateVmc();

            // Assert
            Assert.Equal("real:token", reader.INSTALLID);
            Assert.Empty(warnings);
            Assert.Empty(errors);
        }

        [Fact]
        public void PopulateVmc_PrefixWiderThanExpectedWithNoSeparator_WarnsAndLeavesInstallIdNull()
        {
            // Arrange - the label-token lookup still needs a genuine malformed-line guard: if
            // the assumed 40-char prefix is too NARROW (real prefix wider, with no whitespace
            // boundary at the cut point), Substring(40) lands mid-prefix and glues leftover
            // prefix characters directly onto the label, so no token equals "InstallationId:"
            // exactly. This must warn and leave INSTALLID null, not silently do nothing.
            string line = new string('X', 41) + "InstallationId: realtoken";
            File.WriteAllText(Path.Combine(this.tempDir, "VMC.log"), line);
            List<string> warnings = new();
            List<string> errors = new();
            CVmcReader reader = new(VmcLogMode.Vb365)
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
            Assert.Contains("did not contain an install ID token after the label", warnings[0]);
            Assert.Empty(errors);
        }
    }
}
