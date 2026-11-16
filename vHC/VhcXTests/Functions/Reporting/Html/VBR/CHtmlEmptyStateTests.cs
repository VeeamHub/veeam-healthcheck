// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// A VBR table with no collected rows must say so. A header-only table reads as a rendering
    /// bug (SOBR, extents, object storage on labs that have none).
    /// </summary>
    [Collection("GlobalState")]
    public class CHtmlEmptyStateTests : VbrTableScrubTestBase
    {
        public CHtmlEmptyStateTests() : base("VhcEmptyState_") { }

        private static IDocument Render() =>
            new HtmlParser().ParseDocument(
                "<html><body><main>" + new CHtmlBodyHelper().FormVbrFullReport(string.Empty, false) + "</main></body></html>");

        [Fact]
        public void FormVbrFullReport_NoCollectedData_NoTableRendersHeaderOnly()
        {
            var doc = Render();

            var headerOnly = doc.QuerySelectorAll("table")
                .Where(t => t.QuerySelectorAll("tr").Count(r => r.QuerySelector("td") != null) == 0)
                .Select(t => (t.Closest(".section-card")?.Id ?? "(no card)") + "/" + (t.Closest(".section-card")?.QuerySelector("h2")?.TextContent.Trim() ?? ""))
                .ToList();

            Assert.True(headerOnly.Count == 0, "Header-only tables: " + string.Join(" | ", headerOnly));
        }

        [Theory]
        [InlineData("sobr")]
        [InlineData("extents")]
        [InlineData("capextents")]
        [InlineData("archextents")]
        [InlineData("objstorage")]
        [InlineData("locations")]
        public void FormVbrFullReport_SectionWithoutData_RendersSingleNoDataRow(string id)
        {
            var rows = Render().GetElementById(id).QuerySelectorAll("tbody tr");

            Assert.Single(rows);
            Assert.Contains("No ", rows[0].TextContent);
        }

        [Theory]
        [InlineData("_SOBRs.csv", "\"Name\",\"ExtentCount\"\r\n", "sobr")]
        [InlineData("_SOBRExtents.csv", "\"Name\",\"SobrName\"\r\n", "extents")]
        [InlineData("_ObjectStorageRepos.csv", "\"Name\",\"Type\",\"Bucket\",\"Folder\",\"Region\",\"Endpoint\",\"Account\",\"ConnectionType\",\"Gateway\"\r\n", "objstorage")]
        [InlineData("_Locations.csv", "\"Location\",\"LocationId\",\"ObjectType\",\"ObjectName\",\"ObjectId\"\r\n", "locations")]
        public void FormVbrFullReport_HeaderOnlyCsv_RendersNoDataRowNotBlankRows(string csv, string header, string id)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(VbrDir, "srv" + csv), header);

            var rows = Render().GetElementById(id).QuerySelectorAll("tbody tr");

            Assert.Single(rows);
            Assert.Contains("No ", rows[0].TextContent);
        }

        [Fact]
        public void FormVbrFullReport_LocationsCsv_RendersOneRowPerAssignmentAndKeepsEmptyLocation()
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(VbrDir, "srv_Locations.csv"),
                "\"Location\",\"LocationId\",\"ObjectType\",\"ObjectName\",\"ObjectId\"\r\n" +
                "\"Columbus\",\"11111111-1111-1111-1111-111111111111\",\"Repository\",\"Hardened Repo 01\",\"a\"\r\n" +
                "\"Columbus\",\"11111111-1111-1111-1111-111111111111\",\"Server\",\"esx-01.example.test\",\"b\"\r\n" +
                "\"Dublin\",\"22222222-2222-2222-2222-222222222222\",\"\",\"\",\"\"\r\n");

            var rows = Render().GetElementById("locations").QuerySelectorAll("tbody tr");

            Assert.Equal(3, rows.Length);
            Assert.Contains("Columbus", rows[0].TextContent);
            Assert.Contains("Hardened Repo 01", rows[0].TextContent);
            Assert.Contains("Repository", rows[0].TextContent);
            Assert.Contains("esx-01.example.test", rows[1].TextContent);
            Assert.Contains("Dublin", rows[2].TextContent);
            Assert.DoesNotContain("No locations", string.Concat(rows.Select(r => r.TextContent)));
        }

        [Fact]
        public void FormVbrFullReport_ObjectStorageRowsAllBlank_RendersNoDataRow()
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(VbrDir, "srv_ObjectStorageRepos.csv"),
                "\"Name\",\"Type\",\"Bucket\",\"Folder\",\"Region\",\"Endpoint\",\"Account\",\"ConnectionType\",\"Gateway\"\r\n" +
                ",,,,,,,,\r\n,,,,,,,,\r\n");

            var rows = Render().GetElementById("objstorage").QuerySelectorAll("tbody tr");

            Assert.Single(rows);
            Assert.Contains("No ", rows[0].TextContent);
        }
    }
}
