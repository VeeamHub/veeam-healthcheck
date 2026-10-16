// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using VeeamHealthCheck.Functions.Reporting.DataFormers.OrphanedSupersededBackups;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// The Orphaned &amp; Superseded Backups section ships dark behind VHC_EXPERIMENTS.
    /// Flag off: no section card, no left-nav link, no JSON data. Flag on: all three present.
    /// </summary>
    [Collection("GlobalState")]
    [Trait("Category", "OrphanedSupersededBackups")]
    public class OrphanedSupersededExperimentsGateTests : VbrTableScrubTestBase
    {
        private const string SectionId = "orphanedsupersededbackups";

        public OrphanedSupersededExperimentsGateTests() : base("VhcOrphanedGate_") { }

        private static IDocument RenderReport(out CFullReportJson json)
        {
            var original = CGlobals.FullReportJson;
            json = new CFullReportJson();
            CGlobals.FullReportJson = json;
            try
            {
                string sidebar = new CHtmlCompiler().BuildSidebar();
                string body = new CHtmlBodyHelper().FormVbrFullReport(string.Empty, false);
                return new HtmlParser().ParseDocument("<html><body>" + sidebar + "<main>" + body + "</main></body></html>");
            }
            finally
            {
                CGlobals.FullReportJson = original;
            }
        }

        private static bool HasNavLink(IDocument doc) =>
            doc.QuerySelectorAll("a.nav-link").Any(a => a.GetAttribute("href") == "#" + SectionId);

        [Theory]
        [InlineData(null)]
        [InlineData("0")]
        [InlineData("false")]
        public void FormVbrFullReport_ExperimentsOff_OmitsSectionCard(string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            var doc = RenderReport(out _);

            Assert.Null(doc.GetElementById(SectionId));
            Assert.DoesNotContain("Orphaned &amp; Superseded", doc.Body!.InnerHtml);
        }

        [Fact]
        public void BuildSidebar_ExperimentsOff_OmitsNavLinkAndLeavesNoDeadLinks()
        {
            using var scope = new ExperimentsFlagScope(null);

            var doc = RenderReport(out _);

            Assert.False(HasNavLink(doc));
            var dead = doc.QuerySelectorAll("a.nav-link")
                .Select(a => a.GetAttribute("href")?.TrimStart('#'))
                .Where(id => !string.IsNullOrEmpty(id) && doc.GetElementById(id) == null)
                .ToList();
            Assert.True(dead.Count == 0, "Dead nav links: " + string.Join(", ", dead));
        }

        [Fact]
        public void FormVbrFullReport_ExperimentsOff_LeavesJsonOrphanedFieldsEmpty()
        {
            using var scope = new ExperimentsFlagScope(null);

            RenderReport(out var json);

            Assert.Empty(json.OrphanedSupersededBackups);
            Assert.False(json.OrphanedBackupsSweepEvaluated);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        public void FormVbrFullReport_ExperimentsOn_RendersSectionCardAndNavLink(string flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            var doc = RenderReport(out _);

            Assert.NotNull(doc.GetElementById(SectionId));
            Assert.True(HasNavLink(doc));
        }

        [Fact]
        public void FormVbrFullReport_ExperimentsOn_PopulatesJsonOrphanedFields()
        {
            using var scope = new ExperimentsFlagScope("1");
            var sentinel = new System.Collections.Generic.List<OrphanedSupersededBackupRecord>();
            var original = CGlobals.FullReportJson;
            var json = new CFullReportJson { OrphanedSupersededBackups = sentinel };
            CGlobals.FullReportJson = json;
            try
            {
                new CHtmlBodyHelper().FormVbrFullReport(string.Empty, false);
            }
            finally
            {
                CGlobals.FullReportJson = original;
            }

            Assert.NotSame(sentinel, json.OrphanedSupersededBackups);
        }
    }
}
