// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings
{
    // The tenant name appears ONLY in the three Entra CSVs, so nothing else can register it for a
    // replace-pass backstop: the renderers themselves must scrub it.
    [Trait("Category", "Scrubbing")]
    [Collection("GlobalState")]
    public class CEntraScrubTests : VbrTableScrubTestBase, IDisposable
    {
        private const string Tenant = "unique-entra-tenant-4f7a.onmicrosoft.com";
        private const string CacheRepo = "UniqueEntraCacheRepo-4f7a";
        private const string LogJob = "UniqueEntraLogJob-4f7a";

        private readonly bool originalScrub = CGlobals.Scrub;

        public CEntraScrubTests() : base("VhcEntraScrubTests_")
        {
            File.WriteAllText(Path.Combine(VbrDir, "_entraTenants.csv"),
                $"\"tenantName\",\"CacheRepoName\"\r\n\"{Tenant}\",\"{CacheRepo}\"");
            File.WriteAllText(Path.Combine(VbrDir, "_entraTenantJob.csv"),
                $"\"Name\",\"RetentionPolicy\"\r\n\"{Tenant}\",\"30\"");
            File.WriteAllText(Path.Combine(VbrDir, "_entraLogJob.csv"),
                "\"Name\",\"Tenant\",\"shortTermRetType\",\"ShortTermRepo\",\"ShortTermRepoRetention\",\"CopyModeEnabled\",\"SecondaryTarget\"\r\n" +
                $"\"{LogJob}\",\"{Tenant}\",\"Days\",\"{CacheRepo}\",\"90\",\"False\",\"\"");
        }

        public new void Dispose()
        {
            CGlobals.Scrub = originalScrub;
            base.Dispose();
        }

        [Fact]
        public void EntraJobsTable_ScrubTrue_TenantAndJobNamesAbsent()
        {
            CGlobals.Scrub = true;
            string html = new CEntraJobsTable().Table();

            Assert.NotNull(html);
            Assert.DoesNotContain("unique-entra-tenant", html);
            Assert.DoesNotContain(LogJob, html);
            Assert.DoesNotContain(CacheRepo, html);
            Assert.Contains("Tenant_", html);
        }

        [Fact]
        public void EntraJobsTable_ScrubFalse_TenantPassesThrough()
        {
            CGlobals.Scrub = false;
            string html = new CEntraJobsTable().Table();

            Assert.Contains(Tenant, html);
            Assert.Contains(LogJob, html);
        }

        [Fact]
        public void EntraTenants_ScrubTrue_TenantAndCacheRepoAreTokens()
        {
            CGlobals.Scrub = true;
            var workload = Assert.Single(new CEntraTenants().EntraTable().entraWorkloads);

            Assert.DoesNotContain("unique-entra-tenant", workload.TenantName);
            Assert.StartsWith("Tenant_", workload.TenantName);
            Assert.DoesNotContain(CacheRepo, workload.CacheRepoName);
        }

        [Fact]
        public void EntraTenants_ScrubFalse_ValuesPassThrough()
        {
            CGlobals.Scrub = false;
            var workload = Assert.Single(new CEntraTenants().EntraTable().entraWorkloads);

            Assert.Equal(Tenant, workload.TenantName);
            Assert.Equal(CacheRepo, workload.CacheRepoName);
        }

        [Fact]
        public void EntraTenantAcrossTables_ScrubTrue_SameTenantGetsSameToken()
        {
            CGlobals.Scrub = true;
            string tenantToken = new CEntraTenants().EntraTable().entraWorkloads.Single().TenantName;
            string html = new CEntraJobsTable().Table();

            Assert.Contains(tenantToken, html);
        }
    }
}
