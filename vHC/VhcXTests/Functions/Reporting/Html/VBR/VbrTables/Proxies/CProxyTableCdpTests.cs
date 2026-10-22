// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Proxies;
using VeeamHealthCheck.Shared;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.Proxies
{
    /// <summary>
    /// The CDP proxy columns must land under their own headers in the rendered proxy table and in
    /// the JSON "proxies" section, using the header order <c>Get-VhciCdpProxy.ps1</c> actually writes.
    /// </summary>
    [Collection("GlobalState")]
    public class CProxyTableCdpTests : VbrTableScrubTestBase
    {
        private const string CdpName = "CDP-PROXY01";
        private const string CachePath = @"E:\CDPCache";

        private readonly CDataTypesParser originalParser = CGlobals.DtParser;

        public CProxyTableCdpTests() : base("VhcProxyCdp_")
        {
            File.WriteAllText(
                Path.Combine(VbrDir, "_CdpProxy.csv"),
                "\"ServerId\",\"CacheSize\",\"CachePath\",\"IsEnabled\",\"SourceProxyTrafficPort\",\"TargetProxyTrafficPort\",\"Id\",\"Name\",\"Description\"\r\n" +
                $"\"33333333-4444-5555-6666-777777777777\",\"100\",\"{CachePath}\",\"True\",\"1\",\"2\",\"cccccccc-dddd-eeee-ffff-000000000000\",\"{CdpName}\",\"\"");
            CGlobals.DtParser = new CDataTypesParser();
        }

        public new void Dispose()
        {
            CGlobals.DtParser = originalParser;
            base.Dispose();
        }

        private string Cell(string header)
        {
            new CProxyTable().Render(false);
            var section = CGlobals.FullReportJson.Sections["proxies"];
            int col = section.Headers.IndexOf(header);
            Assert.True(col >= 0, $"JSON proxies section has no '{header}' header; headers: {string.Join(",", section.Headers)}");
            var row = section.Rows.Single(r => r.Contains(CdpName));
            return row[col];
        }

        [Fact]
        public void Render_CdpProxy_JsonSectionTypeHeaderHoldsCdp() => Assert.Equal("CDP", Cell("Type"));

        [Fact]
        public void Render_CdpProxy_JsonSectionNameHeaderHoldsProxyName() => Assert.Equal(CdpName, Cell("Name"));

        [Fact]
        public void Render_CdpProxy_JsonSectionCachePathHeaderHoldsCachePath() => Assert.Equal(CachePath, Cell("CachePath"));

        [Fact]
        public void Render_CdpProxy_JsonSectionCacheSizeHeaderHoldsCacheSize() => Assert.Equal("100", Cell("CacheSize"));

        [Fact]
        public void Render_CdpProxy_HtmlRowShowsNameTypeAndCache()
        {
            string html = new CProxyTable().Render(false);

            Assert.Contains(CdpName, html);
            Assert.Contains("CDP", html);
            Assert.Contains(CachePath, html);
        }
    }
}
