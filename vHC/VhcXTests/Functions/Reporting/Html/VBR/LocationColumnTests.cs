// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// VBR Locations (_Locations.csv) are parsed once and shown as a Location column on the
    /// managed server, proxy, SOBR, extent and repository tables, the same way the managed
    /// server table shows roles. Missing or broken location data must never break a table.
    /// </summary>
    [Collection("GlobalState")]
    public class LocationColumnTests
    {
        private const string Header = "\"Location\",\"LocationId\",\"ObjectType\",\"ObjectName\",\"ObjectId\"\r\n";

        // Ids and names come from Tools/GoldenBaselines/VBRConfig.
        private const string PopulatedCsv = Header +
            "\"Columbus\",\"L1\",\"Server\",\"VEEAM-VBR01\",\"a1b2c3d4-e5f6-7890-abcd-ef1234567890\"\r\n" +
            "\"Columbus\",\"L1\",\"ViProxy\",\"PROXY01\",\"e5f6a7b8-c9d0-1234-ef01-345678901234\"\r\n" +
            "\"Columbus\",\"L1\",\"Repository\",\"Linux Hardened Repo\",\"33333333-3333-3333-3333-333333333333\"\r\n" +
            "\"Columbus\",\"L1\",\"SOBR\",\"Production SOBR\",\"55555555-5555-5555-5555-555555555555\"\r\n" +
            "\"Columbus\",\"L1\",\"Extent\",\"Extent1\",\"77777777-7777-7777-7777-777777777777\"\r\n" +
            // No ObjectId: matched by name within its type.
            "\"Dublin\",\"L2\",\"Server\",\"ESXI01.lab.local\",\"\"\r\n" +
            // PROXY02 has no location of its own; it runs on VCENTER01, so it shows this one.
            "\"R&D Lab\",\"L3\",\"Server\",\"VCENTER01.lab.local\",\"b2c3d4e5-f6a7-8901-bcde-f12345678901\"\r\n" +
            "\"Empty Site\",\"L4\",\"\",\"\",\"\"\r\n";

        private static readonly string[] TableIds = { "managedServerInfo", "proxies", "sobr", "extents", "repos" };

        private static IDocument Parse(string html) => new HtmlParser().ParseDocument(html);

        private static List<string> Headers(IDocument doc, string id) =>
            doc.GetElementById(id)!.QuerySelectorAll("thead th").Select(h => h.TextContent.Trim()).ToList();

        // Location sits right after the name column, so it is visible without scrolling.
        private static string LocationOf(IDocument doc, string tableId, string name)
        {
            var row = doc.GetElementById(tableId)!.QuerySelectorAll("tbody tr")
                .First(r => r.QuerySelector("td")?.TextContent.Trim() == name);
            return row.QuerySelectorAll("td")[1].TextContent.Trim();
        }

        [Fact]
        public void FullReport_LocationsCsv_AddsLocationColumnToEveryInfrastructureTable()
        {
            using var report = new GoldenVbrReport();
            File.WriteAllText(Path.Combine(report.Input, "_Locations.csv"), PopulatedCsv);

            var doc = Parse(report.Generate(scrub: false));

            foreach (var id in TableIds)
            {
                Assert.Equal("Location", Headers(doc, id)[1]);
            }

            Assert.Equal("Columbus", LocationOf(doc, "managedServerInfo", "VEEAM-VBR01"));
            Assert.Equal("Dublin", LocationOf(doc, "managedServerInfo", "ESXI01.lab.local"));
            Assert.Equal(string.Empty, LocationOf(doc, "managedServerInfo", "REPO-SERVER01"));
            Assert.Equal("Columbus", LocationOf(doc, "proxies", "PROXY01"));
            Assert.Equal("R&D Lab", LocationOf(doc, "proxies", "PROXY02"));
            Assert.Equal("Columbus", LocationOf(doc, "sobr", "Production SOBR"));
            Assert.Equal(string.Empty, LocationOf(doc, "sobr", "Archive SOBR"));
            Assert.Equal("Columbus", LocationOf(doc, "extents", "Extent1"));
            Assert.Equal(string.Empty, LocationOf(doc, "extents", "Extent2"));
            Assert.Equal("Columbus", LocationOf(doc, "repos", "Linux Hardened Repo"));
            Assert.Equal(string.Empty, LocationOf(doc, "repos", "ReFS Repository"));

            // Every row keeps the same cell count as its header (no ragged rows).
            foreach (var id in TableIds)
            {
                int columns = Headers(doc, id).Count;
                foreach (var row in doc.GetElementById(id)!.QuerySelectorAll("tbody tr"))
                {
                    Assert.Equal(columns, row.QuerySelectorAll("td").Length);
                }
            }

            // The Locations table stays, fed by the same parsed rows.
            Assert.Equal(8, doc.GetElementById("locations")!.QuerySelectorAll("tbody tr").Length);

            // The JSON export carries the same column.
            string json = File.ReadAllText(report.JsonPath);
            Assert.Contains("\"Location\"", json);
            Assert.Contains("Columbus", json);
        }

        [Fact]
        public void FullReport_NoLocationsCsv_NoLocationColumnAndTablesUnchanged()
        {
            using var report = new GoldenVbrReport();

            var doc = Parse(report.Generate(scrub: false));

            foreach (var id in TableIds)
            {
                Assert.DoesNotContain("Location", Headers(doc, id));
            }

            Assert.Contains("PROXY01", doc.GetElementById("proxies")!.TextContent);
        }

        [Fact]
        public void FullReport_LocationOnlyOnRepository_OtherTablesHaveNoEmptyLocationColumn()
        {
            // The shape a real VBR 13.1 gives: repositories take a location, servers and
            // proxies are rejected ("Type ... is not supported").
            using var report = new GoldenVbrReport();
            File.WriteAllText(
                Path.Combine(report.Input, "_Locations.csv"),
                Header + "\"Columbus\",\"L1\",\"Repository\",\"Linux Hardened Repo\",\"33333333-3333-3333-3333-333333333333\"\r\n");

            var doc = Parse(report.Generate(scrub: false));

            Assert.Equal("Location", Headers(doc, "repos")[1]);
            Assert.Equal("Columbus", LocationOf(doc, "repos", "Linux Hardened Repo"));
            foreach (var id in new[] { "managedServerInfo", "proxies", "sobr", "extents" })
            {
                Assert.DoesNotContain("Location", Headers(doc, id));
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("\"Foo\",\"Bar\"\r\n\"1\",\"2\"\r\n")]
        [InlineData("\"Location\"\r\n\"\"\r\n\"   \"\r\n")]
        [InlineData("not,a\r\n\"broken csv with \"\" stray quotes\r\n")]
        public void FullReport_BrokenLocationsCsv_ReportStillRendersWithoutLocationColumn(string content)
        {
            using var report = new GoldenVbrReport();
            File.WriteAllText(Path.Combine(report.Input, "_Locations.csv"), content);

            var doc = Parse(report.Generate(scrub: false));

            foreach (var id in TableIds)
            {
                Assert.NotNull(doc.GetElementById(id));
                Assert.DoesNotContain("Location", Headers(doc, id));
            }

            Assert.Contains("PROXY01", doc.GetElementById("proxies")!.TextContent);
            Assert.Contains("Linux Hardened Repo", doc.GetElementById("repos")!.TextContent);
        }

        [Fact]
        public void FullReport_LocationsCsvWithoutIdColumns_FallsBackToNames()
        {
            using var report = new GoldenVbrReport();
            File.WriteAllText(
                Path.Combine(report.Input, "_Locations.csv"),
                "\"Location\",\"ObjectType\",\"ObjectName\"\r\n\"Columbus\",\"Repository\",\"Linux Hardened Repo\"\r\n");

            var doc = Parse(report.Generate(scrub: false));

            Assert.Equal("Columbus", LocationOf(doc, "repos", "Linux Hardened Repo"));
            Assert.DoesNotContain("Location", Headers(doc, "managedServerInfo"));
        }

        [Fact]
        public void FullReport_Scrubbed_LocationNamesNeverAppear()
        {
            using var report = new GoldenVbrReport();
            File.WriteAllText(Path.Combine(report.Input, "_Locations.csv"), PopulatedCsv);

            string html = report.Generate(scrub: true);
            var doc = Parse(html);

            foreach (var id in TableIds)
            {
                Assert.Equal("Location", Headers(doc, id)[1]);
            }

            Assert.DoesNotContain("Columbus", html);
            Assert.DoesNotContain("Dublin", html);
            Assert.DoesNotContain("R&amp;D Lab", html);
            Assert.DoesNotContain("Empty Site", html);
            Assert.DoesNotContain("Columbus", File.ReadAllText(report.JsonPath));

            // The same location scrubs to the same token in every table: the repository's
            // location (Columbus) also appears on the managed servers and in the Locations table.
            string LastCells(string id) => string.Join("|", doc.GetElementById(id)!.QuerySelectorAll("tbody tr")
                .Select(r => r.QuerySelectorAll("td")[1].TextContent.Trim()));
            string repoToken = doc.GetElementById("repos")!.QuerySelectorAll("tbody tr")
                .Select(r => r.QuerySelectorAll("td")[1].TextContent.Trim()).Single(t => t.Length > 0);
            Assert.Contains(repoToken, LastCells("managedServerInfo"));
            Assert.Contains(repoToken, LastCells("proxies"));
            Assert.Contains(repoToken, doc.GetElementById("locations")!.TextContent);
        }
    }

    public class CLocationLookupTests
    {
        private static IDictionary<string, object> Row(params (string Key, object Value)[] cells) =>
            cells.ToDictionary(c => c.Key, c => c.Value);

        [Fact]
        public void Find_EmptyOrNullInput_ReturnsBlank()
        {
            Assert.False(new CLocationLookup().HasLocations);
            Assert.Equal(string.Empty, new CLocationLookup().Find("x", "y", "Server"));
            Assert.Equal(string.Empty, new CLocationLookup(null!).Find("x", "y", "Server"));
            Assert.Equal(string.Empty, new CLocationLookup(new IDictionary<string, object>[] { null! }).Find(null!, null!, null!));
        }

        [Fact]
        public void Find_ById_IsCaseInsensitiveAndIgnoresType()
        {
            var lookup = new CLocationLookup(new[] { Row(("location", "Columbus"), ("objecttype", "Server"), ("objectname", "a"), ("objectid", "ABC-1")) });

            Assert.Equal("Columbus", lookup.Find("abc-1", null!));
            Assert.Equal(1, lookup.LocationCount);
        }

        [Fact]
        public void Find_ByName_OnlyWithinGivenTypes()
        {
            var lookup = new CLocationLookup(new[] { Row(("Location", "Columbus"), ("ObjectType", "Server"), ("ObjectName", "host1")) });

            Assert.Equal("Columbus", lookup.Find(null!, "HOST1", "Server"));
            Assert.Equal(string.Empty, lookup.Find(null!, "host1", "Repository"));
        }

        [Fact]
        public void Find_NameInTwoLocations_IsAmbiguousAndBlank()
        {
            var lookup = new CLocationLookup(new[]
            {
                Row(("location", "Columbus"), ("objecttype", "Server"), ("objectname", "host1")),
                Row(("location", "Dublin"), ("objecttype", "Server"), ("objectname", "host1")),
            });

            Assert.Equal(string.Empty, lookup.Find(null!, "host1", "Server"));
            Assert.Equal(2, lookup.LocationCount);
        }

        [Fact]
        public void Ctor_RowsWithoutLocationOrWithNullValues_AreSkipped()
        {
            var lookup = new CLocationLookup(new[]
            {
                Row(("location", ""), ("objectid", "1")),
                Row(("location", null!), ("objectid", "2")),
                Row(("objectid", "3")),
                Row(("location", "Columbus")),
            });

            Assert.Equal(1, lookup.LocationCount);
            Assert.Single(lookup.Rows);
            Assert.Equal(string.Empty, lookup.Find("1", null!));
        }
    }
}
