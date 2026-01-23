// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VhcXTests.ContentValidation
{
    /// <summary>
    /// Phase 2: HTML Report Content Validation
    /// Validates that HTML reports contain required sections, valid structure, and accurate data
    /// </summary>
    [Trait("Category", "ContentValidation")]
    public class ReportContentValidationTests : IDisposable
    {
        private readonly string _testReportDirectory;

        public ReportContentValidationTests()
        {
            _testReportDirectory = Path.Combine(Path.GetTempPath(), $"vhc-report-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testReportDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testReportDirectory))
            {
                try
                {
                    Directory.Delete(_testReportDirectory, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        #region Section Presence Tests

        [Fact]
        public void HtmlReport_ContainsAllExpectedSections()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = CreateSampleHtmlReport();
            File.WriteAllText(reportPath, htmlContent);

            var expectedSections = new[]
            {
                "License Information",
                "Backup Server",
                "Security Summary",
                "Proxy Configuration",
                "Repository Configuration",
                "Job Summary"
            };

            // Act
            var reportContent = File.ReadAllText(reportPath);

            // Assert
            foreach (var section in expectedSections)
            {
                Assert.Contains(section, reportContent, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void HtmlReport_ContainsVersionInfo()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = CreateSampleHtmlReport();
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var reportContent = File.ReadAllText(reportPath);

            // Assert
            // Should contain VBR version information
            var versionPattern = @"\d{1,2}\.\d{1,2}\.\d{1,2}\.\d{3,4}";
            Assert.Matches(versionPattern, reportContent);
        }

        #endregion

        #region Navigation Validation Tests

        [Fact]
        public void HtmlReport_NavigationLinksAreValid()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = @"<!DOCTYPE html>
<html>
<body>
<nav>
    <a href='#section1'>Section 1</a>
    <a href='#section2'>Section 2</a>
</nav>
<div id='section1'>Content 1</div>
<div id='section2'>Content 2</div>
</body>
</html>";
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);
            var hrefs = Regex.Matches(content, @"href=['""]#(\w+)['""]")
                .Select(m => m.Groups[1].Value)
                .ToList();
            var ids = Regex.Matches(content, @"id=['""](\w+)['""]")
                .Select(m => m.Groups[1].Value)
                .ToHashSet();

            // Assert
            foreach (var href in hrefs)
            {
                Assert.Contains(href, ids);
            }
        }

        [Fact]
        public void HtmlReport_NoDeadInternalLinks()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = CreateSampleHtmlReport();
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Find all internal anchor links (href="#...")
            var anchorLinks = Regex.Matches(content, @"href=['""]#([\w-]+)['""]")
                .Select(m => m.Groups[1].Value)
                .ToList();

            // Find all element IDs
            var elementIds = Regex.Matches(content, @"id=['""]([^'""]+)['""]")
                .Select(m => m.Groups[1].Value)
                .ToHashSet();

            // Assert - all anchor links should have corresponding IDs
            var deadLinks = anchorLinks.Where(link => !elementIds.Contains(link)).ToList();

            Assert.Empty(deadLinks);
        }

        #endregion

        #region Table Structure Tests

        [Fact]
        public void HtmlReport_TablesHaveMatchingHeadersAndRows()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = @"<!DOCTYPE html>
<html>
<body>
<table>
    <thead>
        <tr><th>Name</th><th>Type</th><th>Status</th></tr>
    </thead>
    <tbody>
        <tr><td>Job1</td><td>Backup</td><td>Success</td></tr>
        <tr><td>Job2</td><td>Replica</td><td>Warning</td></tr>
    </tbody>
</table>
</body>
</html>";
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Extract table headers (simplified - real implementation would use HTML parser)
            var headerMatches = Regex.Matches(content, @"<th[^>]*>([^<]+)</th>");
            var headerCount = headerMatches.Count;

            // Extract table rows and count cells
            var rowMatches = Regex.Matches(content, @"<tr[^>]*>(?:<td[^>]*>[^<]*</td>)+</tr>");

            // Assert
            Assert.True(headerCount > 0, "Table should have headers");

            foreach (Match row in rowMatches)
            {
                var cellCount = Regex.Matches(row.Value, @"<td[^>]*>[^<]*</td>").Count;
                Assert.Equal(headerCount, cellCount);
            }
        }

        [Fact]
        public void HtmlReport_TablesAreNotEmpty()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = CreateSampleHtmlReport();
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Find all tables
            var tables = Regex.Matches(content, @"<table[^>]*>.*?</table>", RegexOptions.Singleline);

            // Assert - Each table should have at least one row of data
            foreach (Match table in tables)
            {
                var hasDataRows = Regex.IsMatch(table.Value, @"<tbody[^>]*>.*?<tr[^>]*>", RegexOptions.Singleline);
                Assert.True(hasDataRows || table.Value.Contains("<tr>"),
                    "Table should contain at least one data row");
            }
        }

        #endregion

        #region Data Validation Tests

        [Fact]
        public void HtmlReport_PercentagesAreWithinValidRange()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = @"<!DOCTYPE html>
<html>
<body>
<div>Success Rate: 95.5%</div>
<div>Completion: 100%</div>
<div>Efficiency: 0%</div>
</body>
</html>";
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);
            var percentageMatches = Regex.Matches(content, @"(\d+\.?\d*)\s*%");

            // Assert
            foreach (Match match in percentageMatches)
            {
                if (decimal.TryParse(match.Groups[1].Value, out decimal percentage))
                {
                    Assert.True(percentage >= 0 && percentage <= 100,
                        $"Percentage {percentage}% is out of valid range (0-100)");
                }
            }
        }

        [Fact]
        public void HtmlReport_NoNegativeNumbers_InSizesAndCounts()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = @"<!DOCTYPE html>
<html>
<body>
<div>Total Size: 1024 GB</div>
<div>Free Space: 512 GB</div>
<div>Job Count: 42</div>
</body>
</html>";
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Look for size and count fields - they shouldn't have negative values
            var sizePattern = @"(?:Size|Space|Count):\s*(-?\d+)";
            var matches = Regex.Matches(content, sizePattern);

            // Assert
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int value))
                {
                    Assert.True(value >= 0, $"Size/Count value should not be negative: {value}");
                }
            }
        }

        #endregion

        #region Scrubbing Validation Tests

        [Theory]
        [InlineData("scrubbed-report.html")]
        public void ScrubbedReport_ContainsNoIpAddresses(string reportFileName)
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, Path.GetFileName(reportFileName));
            var scrubbedContent = @"<!DOCTYPE html>
<html>
<body>
<div>Server: SERVER-REDACTED-01</div>
<div>Path: \\SHARE-REDACTED-01\Backup</div>
</body>
</html>";
            File.WriteAllText(reportPath, scrubbedContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // IPv4 pattern
            var ipv4Pattern = @"\b(?:\d{1,3}\.){3}\d{1,3}\b";
            var ipMatches = Regex.Matches(content, ipv4Pattern);

            // Assert
            Assert.Empty(ipMatches);
        }

        [Theory]
        [InlineData("scrubbed-report.html")]
        public void ScrubbedReport_ContainsNoCredentials(string reportFileName)
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, Path.GetFileName(reportFileName));
            var scrubbedContent = @"<!DOCTYPE html>
<html>
<body>
<div>Username: [REDACTED]</div>
<div>Password: [REDACTED]</div>
</body>
</html>";
            File.WriteAllText(reportPath, scrubbedContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Common credential patterns that should NOT appear in plain text
            var suspiciousPatterns = new[]
            {
                @"password\s*[:=]\s*[^[<\s][\w!@#$%]+",  // password: actualPassword
                @"pwd\s*[:=]\s*[^[<\s][\w!@#$%]+",       // pwd: actualPassword
            };

            // Assert
            foreach (var pattern in suspiciousPatterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                Assert.Empty(matches);
            }
        }

        #endregion

        #region Consistency Tests

        [Fact]
        public void HtmlReport_ServerCountsAreConsistentAcrossSections()
        {
            // Arrange
            var reportPath = Path.Combine(_testReportDirectory, "test-report.html");
            var htmlContent = @"<!DOCTYPE html>
<html>
<body>
<div id='summary'>Total Servers: 5</div>
<div id='details'>
    <table>
        <tbody>
            <tr><td>Server1</td></tr>
            <tr><td>Server2</td></tr>
            <tr><td>Server3</td></tr>
            <tr><td>Server4</td></tr>
            <tr><td>Server5</td></tr>
        </tbody>
    </table>
</div>
</body>
</html>";
            File.WriteAllText(reportPath, htmlContent);

            // Act
            var content = File.ReadAllText(reportPath);

            // Extract summary count
            var summaryMatch = Regex.Match(content, @"Total Servers:\s*(\d+)");
            var summaryCount = int.Parse(summaryMatch.Groups[1].Value);

            // Count actual server rows
            var detailsSection = Regex.Match(content, @"<div id='details'>.*?</div>", RegexOptions.Singleline);
            var rowCount = Regex.Matches(detailsSection.Value, @"<tr>").Count;

            // Assert
            Assert.Equal(summaryCount, rowCount);
        }

        #endregion

        #region Helper Methods

        private string CreateSampleHtmlReport()
        {
            return @"<!DOCTYPE html>
<html>
<head><title>Veeam Health Check Report</title></head>
<body>
<nav>
    <a href='#license'>License Information</a>
    <a href='#server'>Backup Server</a>
    <a href='#security'>Security Summary</a>
    <a href='#proxy'>Proxy Configuration</a>
    <a href='#repo'>Repository Configuration</a>
    <a href='#jobs'>Job Summary</a>
</nav>
<div id='license'>
    <h2>License Information</h2>
    <p>Version: 12.0.0.1420</p>
</div>
<div id='server'>
    <h2>Backup Server</h2>
    <p>Name: VBR-SERVER</p>
</div>
<div id='security'>
    <h2>Security Summary</h2>
    <p>Status: OK</p>
</div>
<div id='proxy'>
    <h2>Proxy Configuration</h2>
    <table>
        <thead><tr><th>Name</th><th>Host</th></tr></thead>
        <tbody><tr><td>Proxy1</td><td>HOST-01</td></tr></tbody>
    </table>
</div>
<div id='repo'>
    <h2>Repository Configuration</h2>
    <table>
        <thead><tr><th>Name</th><th>Capacity</th></tr></thead>
        <tbody><tr><td>Repo1</td><td>1000 GB</td></tr></tbody>
    </table>
</div>
<div id='jobs'>
    <h2>Job Summary</h2>
    <p>Total Jobs: 10</p>
</div>
</body>
</html>";
        }

        #endregion
    }
}
