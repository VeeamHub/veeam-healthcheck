// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class Vb365ImportDetectionTests
    {
        [Fact]
        public void FindCsvDirectory_FlatVb365Folder_ReturnsThatFolder()
        {
            using var h = new Vb365ReportHarness();

            Assert.Equal(h.CsvDir, CImportPathResolver.FindCsvDirectory(h.CsvDir));
        }

        [Fact]
        public void FindCsvDirectory_NestedServerTimestampLayout_FindsTimestampFolderAndSetsVb365()
        {
            using var h = new Vb365ReportHarness();
            bool origVb365 = CGlobals.IsVb365;
            bool origVbr = CGlobals.IsVbr;
            try
            {
                string leaf = Path.Combine(h.Root, "import", "VB365", "srv01", "20261014");
                Directory.CreateDirectory(leaf);
                foreach (string f in Directory.GetFiles(h.CsvDir))
                {
                    File.Copy(f, Path.Combine(leaf, Path.GetFileName(f)));
                }

                string found = CImportPathResolver.FindCsvDirectory(Path.Combine(h.Root, "import"));

                Assert.Equal(leaf, found);
                Assert.True(CGlobals.IsVb365);
            }
            finally
            {
                CGlobals.IsVb365 = origVb365;
                CGlobals.IsVbr = origVbr;
            }
        }

        [Fact]
        public void ValidateCsvFiles_Vb365Fixture_ReportsVb365Product()
        {
            using var h = new Vb365ReportHarness();

            var result = CImportPathResolver.ValidateCsvFiles(h.CsvDir);

            Assert.True(result.IsValid);
            Assert.Equal("VB365", result.ProductType);
            Assert.Contains("Organizations.csv", result.PresentFiles);
            Assert.Contains("Proxies.csv", result.PresentFiles);
        }

        [Fact]
        public void ValidateCsvFiles_CompleteVb365Export_HasNoMissingCriticalFiles()
        {
            using var h = new Vb365ReportHarness();

            var result = CImportPathResolver.ValidateCsvFiles(h.CsvDir);

            Assert.Empty(result.MissingCriticalFiles);
        }

        [Fact]
        public void ValidateCsvFiles_EmptyFolder_IsInvalidAndUnknown()
        {
            using var h = new Vb365ReportHarness();
            string empty = Path.Combine(h.Root, "empty");
            Directory.CreateDirectory(empty);

            var result = CImportPathResolver.ValidateCsvFiles(empty);

            Assert.False(result.IsValid);
            Assert.Equal("Unknown", result.ProductType);
        }

        [Fact]
        public void ModeSelector_Vb365Import_WritesVb365Report()
        {
            using var h = new Vb365ReportHarness();
            bool origFull = CGlobals.RunFullReport;
            bool origSec = CGlobals.RunSecReport;
            try
            {
                CGlobals.RunFullReport = true;
                CGlobals.RunSecReport = false;

                new CReportModeSelector().Run();

                Assert.NotEmpty(Directory.GetFiles(Path.Combine(h.Root, "vHC-Report"), "*_VB365_*.html"));
            }
            finally
            {
                CGlobals.RunFullReport = origFull;
                CGlobals.RunSecReport = origSec;
            }
        }

        [Fact]
        public void ModeSelector_Vb365Import_DoesNotProduceVbrReport()
        {
            using var h = new Vb365ReportHarness();
            bool origFull = CGlobals.RunFullReport;
            bool origSec = CGlobals.RunSecReport;
            try
            {
                CGlobals.RunFullReport = true;
                CGlobals.RunSecReport = false;

                new CReportModeSelector().Run();

                var vbr = Directory.GetFiles(Path.Combine(h.Root, "vHC-Report"), "*_VBR_*");
                Assert.True(vbr.Length == 0, "unexpected VBR output: " + string.Join(", ", vbr.Select(Path.GetFileName)));
            }
            finally
            {
                CGlobals.RunFullReport = origFull;
                CGlobals.RunSecReport = origSec;
            }
        }
    }
}
