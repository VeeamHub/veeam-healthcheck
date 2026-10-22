// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using Xunit;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// The golden fixtures must have the headers and column order the collectors write
    /// (Get-VhciCdpProxy.ps1, Get-VhcEntraId.ps1), because the readers are positional.
    /// </summary>
    [Collection("GlobalState")]
    public class GoldenFixtureCollectorShapeTests : VbrTableScrubTestBase
    {
        public GoldenFixtureCollectorShapeTests() : base("VhcGoldenShape_") { }

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

        private void Load(string fixture, string file) =>
            File.Copy(Path.Combine(GoldenFolder(fixture), file), Path.Combine(VbrDir, file), overwrite: true);

        [Theory]
        [InlineData("VBRConfig")]
        [InlineData("VBRConfig-v13-MsSql")]
        [InlineData("VBRConfig-v13-PostgreSql")]
        public void CdpProxyFixture_HeaderMatchesCollectorOrder(string fixture)
        {
            string header = File.ReadLines(Path.Combine(GoldenFolder(fixture), "_CdpProxy.csv")).First();

            Assert.Equal(
                "\"ServerId\",\"CacheSize\",\"CachePath\",\"IsEnabled\",\"SourceProxyTrafficPort\",\"TargetProxyTrafficPort\",\"Id\",\"Name\",\"Description\"",
                header);
        }

        [Theory]
        [InlineData("VBRConfig")]
        [InlineData("VBRConfig-v13-MsSql")]
        [InlineData("VBRConfig-v13-PostgreSql")]
        public void CdpProxyFixture_ParsedPositionally_YieldsNameAndCachePath(string fixture)
        {
            Load(fixture, "_CdpProxy.csv");

            var cdp = Assert.Single(new CCsvParser().CdpProxCsvParser());

            Assert.Equal("CDP-PROXY01", cdp.Name);
            Assert.Equal("E:\\CDPCache", cdp.CachePath);
            Assert.Equal("True", cdp.IsEnabled);
            Assert.Matches("^[0-9a-f-]{36}$", cdp.ServerId);
        }

        [Theory]
        [InlineData("VBRConfig")]
        [InlineData("VBRConfig-v13-MsSql")]
        [InlineData("VBRConfig-v13-PostgreSql")]
        public void EntraTenantJobFixture_RetentionPolicyIsNumericAsCollectorWrites(string fixture)
        {
            Load(fixture, "_entraTenantJob.csv");

            var job = Assert.Single(new CCsvParser().GetDynamicEntraTenantJobs());

            Assert.Equal("contoso.onmicrosoft.com", job.Name);
            Assert.Equal(30, job.RetentionPolicy);
        }
    }
}
