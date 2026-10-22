// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using AngleSharp.Html.Parser;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR
{
    /// <summary>
    /// Remote and imported runs never read the local registry, so CGlobals.DBTYPE stays empty and the
    /// Config Database row rendered a blank DATABASE TYPE. The collected vbrinfo CSV carries the type.
    /// </summary>
    [Collection("GlobalState")]
    public class CBackupServerDbTypeTests : VbrTableScrubTestBase
    {
        private readonly string originalDbType = CGlobals.DBTYPE;

        public CBackupServerDbTypeTests() : base("VhcDbType_")
        {
            CGlobals.DBTYPE = null;
        }

        public new void Dispose()
        {
            CGlobals.DBTYPE = originalDbType;
            base.Dispose();
        }

        private static string GoldenFolder(string name)
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "HC_Reporting", "Tools", "GoldenBaselines", name);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            throw new DirectoryNotFoundException("GoldenBaselines/" + name);
        }

        private void LoadFixture(string name)
        {
            foreach (string file in Directory.GetFiles(GoldenFolder(name), "*.csv"))
            {
                File.Copy(file, Path.Combine(VbrDir, "srv" + Path.GetFileName(file)), overwrite: true);
            }
        }

        private string ConfigDatabaseDbType()
        {
            string html = new CHtmlBodyHelper().FormVbrFullReport(string.Empty, false);
            var doc = new HtmlParser().ParseDocument("<html><body>" + html + "</body></html>");
            var headers = doc.QuerySelectorAll("th").ToList();
            int col = headers.FindIndex(h => h.TextContent.Trim().Equals("DataBase Type", StringComparison.OrdinalIgnoreCase));
            Assert.True(col >= 0, "Config Database table not rendered");
            var table = headers[col].Closest("table");
            return table.QuerySelectorAll("tbody tr td")[0].TextContent.Trim();
        }

        [Theory]
        [InlineData("VBRConfig", "MS SQL")]
        [InlineData("VBRConfig-v13-MsSql", "MS SQL")]
        [InlineData("VBRConfig-v13-PostgreSql", "PG SQL")]
        public void Render_NoLiveDbTypeButCollectedVbrInfo_ShowsCollectedDatabaseType(string fixture, string expected)
        {
            LoadFixture(fixture);

            Assert.Equal(expected, ConfigDatabaseDbType());
        }

        // Remote runs: every registry-derived column is empty and DbType comes from REST serverInfo.
        [Theory]
        [InlineData("PostgreSql", "PG SQL")]
        [InlineData("MsSql", "MS SQL")]
        [InlineData("", "Unknown")]
        public void Render_RemoteShapedVbrInfoWithRestDbType_ShowsVendorOrUnknown(string restVendor, string expected)
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "srv_vbrinfo.csv"),
                "\"Version\",\"Fixes\",\"SqlServer\",\"Instance\",\"PgHost\",\"PgDb\",\"MsHost\",\"MsDb\",\"DbType\",\"MFA\"\r\n" +
                $"\"13.0.1.1071\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"{restVendor}\",\"Enabled\"");

            Assert.Equal(expected, ConfigDatabaseDbType());
        }

        [Fact]
        public void Render_NoLiveDbTypeAndNoVbrInfo_ShowsUnknownNotBlank()
        {
            Assert.Equal("Unknown", ConfigDatabaseDbType());
        }

        [Fact]
        public void Render_LiveDbTypeSet_WinsOverCollectedVbrInfo()
        {
            LoadFixture("VBRConfig-v13-MsSql");
            CGlobals.DBTYPE = CGlobals.PgTypeName;

            Assert.Equal("PG SQL", ConfigDatabaseDbType());
        }

        [Theory]
        [InlineData("MsSql", "MS SQL")]
        [InlineData("PostgreSql", "PG SQL")]
        [InlineData("\"PostgreSql\"", "PG SQL")]
        [InlineData("", "Unknown")]
        [InlineData(null, "Unknown")]
        [InlineData("Oracle", "Unknown")]
        [InlineData("postgresql", "PG SQL")]
        [InlineData(" MsSql ", "MS SQL")]
        public void ResolveDbType_NoLiveValue_MapsCollectedValue(string collected, string expected)
        {
            Assert.Equal(expected, CBackupServerTableHelper.ResolveDbType(null, collected));
        }
    }
}
