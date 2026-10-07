// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// Guards the VBR report's left navigation against drifting from the report body
    /// (issue #184). Renders the real sidebar (CHtmlCompiler.BuildSidebar) and the real
    /// body (CHtmlBodyHelper.FormVbrFullReport) against an empty VBR data directory,
    /// then compares the two in both directions. An empty directory is the worst case
    /// for "section only renders sometimes": every table still emits its card with an
    /// empty-state row, so every card must still be reachable from the sidebar.
    /// </summary>
    [Collection("GlobalState")]
    public class CHtmlCompilerSidebarTests : VbrTableScrubTestBase
    {
        public CHtmlCompilerSidebarTests() : base("VhcSidebarTests_") { }

        private static IDocument Render(bool scrub = false)
        {
            string sidebar = new CHtmlCompiler().BuildSidebar();
            string body = new CHtmlBodyHelper().FormVbrFullReport(string.Empty, scrub);
            return new HtmlParser().ParseDocument("<html><body>" + sidebar + "<main>" + body + "</main></body></html>");
        }

        private static string[] NavTargets(IDocument doc) =>
            doc.QuerySelectorAll("a.nav-link")
                .Select(a => a.GetAttribute("href")?.TrimStart('#'))
                .ToArray();

        // Cards nested inside another card (per-job-type tables, per-repo orphaned groups)
        // have runtime-generated ids and are reached through their parent's link.
        private static bool IsTopLevel(IElement card) =>
            card.ParentElement?.Closest(".section-card") == null;

        [Fact]
        public void BuildSidebar_TopLevelSectionCards_HaveMatchingNavLinks()
        {
            var doc = Render();
            var targets = NavTargets(doc);

            var unlinked = doc.QuerySelectorAll(".section-card[id]")
                .Where(IsTopLevel)
                .Select(c => c.Id)
                .Where(id => !targets.Contains(id))
                .ToList();

            Assert.True(unlinked.Count == 0,
                "Top-level section cards with no sidebar link: " + string.Join(", ", unlinked));
        }

        [Fact]
        public void BuildSidebar_NavLinks_AllPointAtRenderedElements()
        {
            var doc = Render();

            var dead = NavTargets(doc)
                .Where(id => !string.IsNullOrEmpty(id) && doc.GetElementById(id) == null)
                .ToList();

            Assert.True(dead.Count == 0,
                "Sidebar links with no matching element in the report body: " + string.Join(", ", dead));
        }

        [Fact]
        public void BuildSidebar_WithCloudConnectData_LinksEveryCloudConnectCard()
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "_CloudGateways.csv"),
                "name,description\ngw1,test gateway\n");

            var doc = Render();
            var targets = NavTargets(doc);

            Assert.NotNull(doc.GetElementById("cloudgateways"));
            var unlinked = doc.QuerySelectorAll(".section-card[id^='cloud']")
                .Where(IsTopLevel)
                .Select(c => c.Id)
                .Where(id => !targets.Contains(id))
                .ToList();
            Assert.True(unlinked.Count == 0,
                "Cloud Connect cards with no sidebar link: " + string.Join(", ", unlinked));

            var dead = targets
                .Where(id => !string.IsNullOrEmpty(id) && doc.GetElementById(id) == null)
                .ToList();
            Assert.True(dead.Count == 0,
                "Sidebar links with no matching element: " + string.Join(", ", dead));
        }

        [Fact]
        public void BuildSidebar_NoComplianceData_OmitsComplianceLinks()
        {
            var doc = Render();

            Assert.DoesNotContain("ComplianceSummary", NavTargets(doc));
            Assert.DoesNotContain("ComplianceTable", NavTargets(doc));
        }

        [Fact]
        public void BuildSidebar_ComplianceMetaOnly_LinksSummaryButNotDetails()
        {
            // A timed-out or failed scan leaves metadata but no rule rows: the Summary card
            // renders (with a status banner) but the Details table does not.
            File.WriteAllText(
                Path.Combine(VbrDir, "_SecurityComplianceMeta.csv"),
                "\"ScanStartedAt\",\"ScanCompletedAt\",\"ScanDurationSeconds\",\"ScanStatus\"\r\n" +
                "\"2024-01-01T10:00:00\",\"2024-01-01T10:00:05\",\"5\",\"TimedOut\"");

            var doc = Render();
            var targets = NavTargets(doc);

            Assert.NotNull(doc.GetElementById("ComplianceSummary"));
            Assert.Contains("ComplianceSummary", targets);
            Assert.Null(doc.GetElementById("ComplianceTable"));
            Assert.DoesNotContain("ComplianceTable", targets);
        }

        [Fact]
        public void BuildSidebar_WithComplianceData_LinksBothComplianceCards()
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "_SecurityCompliance.csv"),
                "\"Best Practice\",\"Status\"\r\n\"Backup Server is Up To Date\",\"Passed\"");
            File.WriteAllText(
                Path.Combine(VbrDir, "_SecurityComplianceMeta.csv"),
                "\"ScanStartedAt\",\"ScanCompletedAt\",\"ScanDurationSeconds\",\"ScanStatus\"\r\n" +
                "\"2024-01-01T10:00:00\",\"2024-01-01T10:00:05\",\"5\",\"Completed\"");

            var doc = Render();
            var targets = NavTargets(doc);

            Assert.NotNull(doc.GetElementById("ComplianceSummary"));
            Assert.NotNull(doc.GetElementById("ComplianceTable"));
            Assert.Contains("ComplianceSummary", targets);
            Assert.Contains("ComplianceTable", targets);
        }

        [Fact]
        public void BuildSidebar_NoCloudConnectData_OmitsCloudConnectLinks()
        {
            var doc = Render();

            Assert.DoesNotContain(NavTargets(doc), id => id != null && id.StartsWith("cloud", StringComparison.Ordinal));
        }

        [Fact]
        public void BuildSidebar_NavLinks_HaveNonEmptyLabels()
        {
            var doc = Render();

            var blank = doc.QuerySelectorAll("a.nav-link")
                .Where(a => string.IsNullOrWhiteSpace(a.TextContent))
                .Select(a => a.GetAttribute("href"))
                .ToList();

            Assert.True(blank.Count == 0,
                "Sidebar links with an empty label (missing localization key?): " + string.Join(", ", blank));
        }
    }
}
