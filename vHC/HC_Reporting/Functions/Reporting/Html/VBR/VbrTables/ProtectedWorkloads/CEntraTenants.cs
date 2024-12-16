using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads
{
    internal class CEntraTenants
    {
        public CEntraTenants() { }
        public CProtectedWorkloads EntraTable()
        {
            CProtectedWorkloads p = new();
            try
            {
                CCsvParser c = new();
                var n = c.GetDynamicEntraTenants();
                foreach (var rec in n)
                {
                    EntraWorkloads entra = new();
                    entra.TenantName = rec.TenantName;
                    entra.CacheRepoName = rec.CacheRepoName;
                    p.entraWorkloads.Add(entra);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            return p;
        }
    }
}
