using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Backup_Server;

namespace VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Security
{
    internal class CSecurityBackupServerTable
    {
        CVbrSecurityTableHelper _tables = new();
        public CSecurityBackupServerTable() { }

        public List<Tuple<string, string>> ServerSpecificInfo()
        {
            CVbrServerTableHelper helper = new();

            List<Tuple<string, string>> tables = new()
            {
                helper.ConsoleStatus(),
                helper.RdpStatus(),
                helper.DomainStatus()
                //_tables.ConsoleInstalled(),
                //_tables.RdpEnabled(),
                //_tables.DomainJoined()


            };

            return tables;
        }
    }
}
