using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Security;
using VeeamHealthCheck.Reporting.TableDatas;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables
{
    internal class CHtmlTablesHelper
    {
        public CHtmlTablesHelper()
        {

        }
        public string AddSecurityServerInfo()
        {
            CSecurityBackupServerTable t = new();
            return WriteTupleListToHtml(t.ServerSpecificInfo());
        }
        public string AddSecuritySummaryDetails()
        {
            CVbrSecurityTables t = new();
            return WriteTupleListToHtml(t.SecuritySummaryTables());
        }
        public string AddConfigBackupDetails()
        {
            CVbrSecurityTables t = new();
            return WriteTupleListToHtml(t.ConfigBackupInfo());
        }

        private string WriteTupleListToHtml(List<Tuple<string, string>> list)
        {
            string headers = "";
            string data = "";
            string s = "";
            foreach (var table in list)
            {
                headers += table.Item1;
                data += table.Item2;
            }
            s += headers;
            s += "</tr><tr>";
            s += data;
            s += "</table><br>";

            return s;
        }
    }
}
