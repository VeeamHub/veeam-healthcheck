using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Security
{
    internal class CSecurityBackupServerTable
    {
        CVbrSecurityTableHelper _tables = new();
        public CSecurityBackupServerTable() { }

        public List<Tuple<string, string>> ServerSpecificInfo()
        {
            List<Tuple<string, string>> tables = new()
            {
                _tables.ConsoleInstalled(),
                _tables.RdpEnabled(),
                _tables.DomainJoined()


            };

            return tables;
        }
    }
}
