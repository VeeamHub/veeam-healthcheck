// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
//using VeeamHealthCheck.Reporting.Html.VBR.VbrTables.Security;

namespace VeeamHealthCheck.Reporting.Html.VBR
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
        public string AddImmutabilityTables()
        {
            CImmutabilityTable ct = new();

            return WriteTupleListToHtml(new List<Tuple<string,string>>());
        }
        public List<string> CollectedOsInfo()
        {
            CDataFormer df = new();
            List<CManagedServer> list = df.ServerXmlFromCsv(false);
            List<string> operatingSystems = new();
            foreach (CManagedServer server in list)
            {
                operatingSystems.Add(server.OsInfo);
            }
            operatingSystems.Sort();
            return operatingSystems.Distinct().ToList();
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
