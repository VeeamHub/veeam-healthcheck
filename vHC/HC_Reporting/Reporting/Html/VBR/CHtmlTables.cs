using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Reporting.Html;

namespace VeeamHealthCheck.Html.VBR
{
    internal class CHtmlTables
    {
        public CHtmlTables()
        {

        }
        public string MakeNavTable()
        {
            return FormNavRows(ResourceHandler.NavLicInfoLink, "license", ResourceHandler.NavLicInfoDetail) +
                FormNavRows(ResourceHandler.NavBkpSrvLink, "backupserver", ResourceHandler.NavBkpSrvDeet) +
                FormNavRows(ResourceHandler.NavSecSumLink, "", ResourceHandler.NavSecSumDeet) +
                FormNavRows(ResourceHandler.NavSrvSumLink, "", ResourceHandler.NavSrvSumDeet) +
                FormNavRows(ResourceHandler.NavJobSumLink, "", ResourceHandler.NavJobSumDeet) +
                FormNavRows(ResourceHandler.NavMissingJobLink, "", ResourceHandler.NavMissingDeet) +
                FormNavRows(ResourceHandler.NavProtWrkld, "", ResourceHandler.NavProtWkldDeet) +
                FormNavRows(ResourceHandler.NavSrvInfoLink, "", ResourceHandler.NavSrvInfoDeet) +
                FormNavRows(ResourceHandler.NavRegKeyLink, "", ResourceHandler.NavRegKeyDeet) +
                FormNavRows(ResourceHandler.NavProxyInfoLink, "", ResourceHandler.NavProxyDeet) +
                FormNavRows(ResourceHandler.NavSobrInfoLink, "", ResourceHandler.NavSobrDeet) +
                FormNavRows(ResourceHandler.NavSobrExtLink, "", ResourceHandler.NavSobrExtDeet) +
                FormNavRows(ResourceHandler.NavRepoInfoLink, "", ResourceHandler.NavRepoDeet) +
                FormNavRows(ResourceHandler.NavJobConLink, "", ResourceHandler.NavJobConDeet) +
                FormNavRows(ResourceHandler.NavTaskConLink, "", ResourceHandler.NavTaskConDeet) +
                FormNavRows(ResourceHandler.NavJobSessSumLink, "", ResourceHandler.NavJobSessSumDeet) +
                FormNavRows(ResourceHandler.NavJobInfoLink, "", ResourceHandler.NavJobInfoDeet);
        }

        public string AddLicHeaderToTable()
        {
            return "<tr>" +
            TableHeader(ResourceHandler.LicTblLicTo, "") +
            TableHeader(ResourceHandler.LicTblEdition, ResourceHandler.LtEdTT) +
                TableHeader(ResourceHandler.LicTblStatus, ResourceHandler.LtStatusTT) +
                TableHeader(ResourceHandler.LicTblType, ResourceHandler.LtTypeTT) +
                TableHeader(ResourceHandler.LicTblLicInst, ResourceHandler.LtInstLicTT) +
                TableHeader(ResourceHandler.LicTblUsedInst, ResourceHandler.LtInstUsedTT) +
                TableHeader(ResourceHandler.LicTblNewInst, ResourceHandler.LtInstNewTT) +
                TableHeader(ResourceHandler.LicTblRentInst, ResourceHandler.LtInstRentalTT) +
                TableHeader(ResourceHandler.LicTblLicSock, ResourceHandler.LtSocLicTT) +
                TableHeader(ResourceHandler.LicTblUsedSock, ResourceHandler.LtSocUsedTT) +
                TableHeader(ResourceHandler.LicTblLicNas, ResourceHandler.LtNasLicTT) +
                TableHeader(ResourceHandler.LicTblUsedNas, ResourceHandler.LtNasUsedTT) +
                TableHeader(ResourceHandler.LicTblExpDate, ResourceHandler.LicExpTT) +
                TableHeader(ResourceHandler.LicTblSupExpDate, ResourceHandler.LicSupExpTT) +
                TableHeader(ResourceHandler.LicTblCc, ResourceHandler.LicCcTT) +
                "</tr>";

        }
        public string AddLicDataToTable()
        {
            string tableString = "";
            CCsvParser csv = new();
            var lic = csv.GetDynamicLicenseCsv();
            foreach (var l in lic)
            {
                tableString +=
                "<tr>" +
                TableData(l.licensedto, "") +
                TableData(l.edition, "") +
                TableData(l.status, "") +
                TableData(l.type, "") +
                TableData(l.licensedinstances, "") +
                TableData(l.usedinstances, "") +
                TableData(l.newinstances, "") +
                TableData(l.rentalinstances, "") +
                TableData(l.licensedsockets, "") +
                TableData(l.usedsockets, "") +
                TableData(l.licensedcapacitytb, "") +
                TableData(l.usedcapacitytb, "") +
                TableData(l.expirationdate, "") +
                TableData(l.supportexpirationdate, "") +
                TableData(l.cloudconnect, "") +
                "</tr>";
            }
            return tableString;
        }
        public string AddBkpSrvTable()
        {
            string s = "<tr>" +
                TableHeader(ResourceHandler.BkpSrvTblName, ResourceHandler.BstNameTT) +
                TableHeader(ResourceHandler.BkpSrvTblVersion, ResourceHandler.BstVerTT) +
                TableHeader(ResourceHandler.BkpSrvTblCore, ResourceHandler.BstCpuTT) +
                TableHeader(ResourceHandler.BkpSrvTblRam, ResourceHandler.BstRamTT) +
                TableHeader(ResourceHandler.BkpSrvTblCfgEnabled, ResourceHandler.BstCfgEnabledTT) +
                TableHeader(ResourceHandler.BkpSrvTblCfgLastRes, ResourceHandler.BstCfgLastResTT) +
                TableHeader(ResourceHandler.BkpSrvTblCfgEncrypt, ResourceHandler.BstCfgEncTT) +
                TableHeader(ResourceHandler.BkpSrvTblTarget, ResourceHandler.BstCfgTarTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlLocal, ResourceHandler.BstSqlLocTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlName, ResourceHandler.BstSqlNameTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlVersion, ResourceHandler.BstSqlVerTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlEdition, ResourceHandler.BstSqlEdTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlCores, ResourceHandler.BstSqlCpuTT) +
                TableHeader(ResourceHandler.BkpSrvTblSqlRam, ResourceHandler.BstSqlRamTT) +
                TableHeader(ResourceHandler.BkpSrvTblProxyRole, ResourceHandler.BstPrxTT) +
                TableHeader(ResourceHandler.BkpSrvTblRepoRole, ResourceHandler.BstRepTT) +
                TableHeader(ResourceHandler.BkpSrvTblWanRole, ResourceHandler.BstWaTT) +
                "</tr>";
            CDataFormer cd = new(true);
            List<string> list = cd.BackupServerInfoToXml();
            s += "<tr>";

            for (int i = 0; i < list.Count(); i++)
            {
                s += TableData(list[i], "");

            }
            s += "</tr>";

            return s;

        }
        public string AddSecSummaryTable()
        {
            string s = "<tr>" +
                TableHeader(ResourceHandler.SSHdr0, ResourceHandler.SSHdrTT0) +
                TableHeader(ResourceHandler.SSHdr1, ResourceHandler.SSHdrTT1) +
                TableHeader(ResourceHandler.SSHdr2, ResourceHandler.SSHdrTT2) +
                TableHeader(ResourceHandler.SSHdr3, ResourceHandler.SSHdrTT3) +
                "</tr>" +
                "<tr>";
            CDataFormer df = new(true);
            List<int> list = df.SecSummary();

            for (int i = 0; i < list.Count(); i++)
            {
                if (list[i] == 0)
                    s += TableData("False", "");
                else if (list[i] == 1)
                    s += TableData("True", "");
            }
            s += "</tr>";

            return s;
        }
        public string AddSrvSummaryTable()
        {
            string s = "<tr>" +
                TableHeader(ResourceHandler.MssHdr1, ResourceHandler.MssHdr1TT) +
                TableHeader(ResourceHandler.MssHdr2, ResourceHandler.MssHdr2TT) +
                "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, int> list = cd.ServerSummaryToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d.Key, "");
                s += TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddJobSummaryTable()
        {
            string s = "<tr>" +
    TableHeader(ResourceHandler.JobSum0, ResourceHandler.JobSum0TT) +
    TableHeader(ResourceHandler.JobSum1, ResourceHandler.JobSum1TT) +
    "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, int> list = cd.JobSummaryInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d.Key, "");
                s += TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddMissingJobsTable()
        {
            string s = "<tr>" +
                TableHeader(ResourceHandler.JobSum0, "") +
                //TableHeader("Count", "Total detected of this type") +
                "</tr>";
            CDataFormer cd = new(true);
            List<string> list = cd.ParseNonProtectedTypes();

            for (int i = 0; i < list.Count(); i++)
            {
                s += "<tr>";

                s += TableData(list[i], "");
                s += "</tr>";

            }

            return s;
        }
        public string AddProtectedWorkLoadsTable()
        {
            string s = "<tr>" +
            TableHeader(ResourceHandler.PlHdr0, ResourceHandler.PlHdrTT0) +
            TableHeader(ResourceHandler.PlHdr1, ResourceHandler.PlHdrTT1) +
            TableHeader(ResourceHandler.PlHdr2, ResourceHandler.PlHdrTT2) +
            TableHeader(ResourceHandler.PlHdr3, ResourceHandler.PlHdrTT3) +
            TableHeader(ResourceHandler.PlHdr4, ResourceHandler.PlHdrTT4) +
            TableHeader(ResourceHandler.PlHdr5, ResourceHandler.PlHdrTT5) +
            TableHeader(ResourceHandler.PlHdr6, ResourceHandler.PlHdrTT6) +
            TableHeader(ResourceHandler.PlHdr7, ResourceHandler.PlHdrTT7) +

    "</tr>";
            CDataFormer cd = new(true);
            cd.ProtectedWorkloadsToXml();
            s += "<tr>";
            s += TableData((cd._viProtectedNames.Distinct().Count() + cd._viNotProtectedNames.Distinct().Count()).ToString(), "");
            s += TableData(cd._viProtectedNames.Distinct().Count().ToString(), "");
            s += TableData(cd._viNotProtectedNames.Distinct().Count().ToString(), "");
            s += TableData(cd._viDupes.ToString(), "");
            s += TableData((cd._physNotProtNames.Distinct().Count() + cd._physProtNames.Distinct().Count()).ToString(), "");
            s += TableData(cd._physProtNames.Distinct().Count().ToString(), "");
            s += TableData(cd._physNotProtNames.Distinct().Count().ToString(), "");

            s += "</tr>";

            return s;
        }
        public string AddManagedServersTable()
        {
            string s = "<tr>" +
            TableHeader(ResourceHandler.ManSrv0, ResourceHandler.ManSrv0TT) +
            TableHeader(ResourceHandler.ManSrv1, ResourceHandler.ManSrv1TT) +
            TableHeader(ResourceHandler.ManSrv2, ResourceHandler.ManSrv2TT) +
            TableHeader(ResourceHandler.ManSrv3, ResourceHandler.ManSrv3TT) +
            TableHeader(ResourceHandler.ManSrv4, ResourceHandler.ManSrv4TT) +
            TableHeader(ResourceHandler.ManSrv5, ResourceHandler.ManSrv5TT) +
            TableHeader(ResourceHandler.ManSrv6, ResourceHandler.ManSrv6TT) +
            TableHeader(ResourceHandler.ManSrv7, ResourceHandler.ManSrv7TT) +
            TableHeader(ResourceHandler.ManSrv8, ResourceHandler.ManSrv8TT) +
            TableHeader(ResourceHandler.ManSrv9, ResourceHandler.ManSrv9TT) +
            TableHeader(ResourceHandler.ManSrv10, ResourceHandler.ManSrv10TT) +
            TableHeader(ResourceHandler.ManSrv11, ResourceHandler.ManSrv11TT) +
            "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.ServerXmlFromCsv();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d[0], "");
                s += TableData(d[1], "");
                s += TableData(d[2], "");
                s += TableData(d[3], "");
                s += TableData(d[4], "");
                s += TableData(d[5], "");
                s += TableData(d[6], "");
                s += TableData(d[7], "");
                s += TableData(d[8], "");
                s += TableData(d[9], "");
                s += TableData(d[10], "");
                s += TableData(d[11], "");
                s += "</tr>";
            }


            return s;
        }
        public string AddRegKeysTable()
        {
            string s = "<tr>" +
                TableHeader(ResourceHandler.Reg0, ResourceHandler.Reg0TT) +
                TableHeader(ResourceHandler.Reg1, ResourceHandler.Reg1TT) +
                "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, string> list = cd.RegOptions();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d.Key, "");
                s += TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddProxyTable()
        {
            string s = "<tr>" +
            TableHeader(ResourceHandler.Prx0, ResourceHandler.Prx0TT) +
            TableHeader(ResourceHandler.Prx1, ResourceHandler.Prx1TT) +
            TableHeader(ResourceHandler.Prx2, ResourceHandler.Prx2TT) +
            TableHeader(ResourceHandler.Prx3, ResourceHandler.Prx3TT) +
            TableHeader(ResourceHandler.Prx4, ResourceHandler.Prx4TT) +
            TableHeader(ResourceHandler.Prx5, ResourceHandler.Prx5TT) +
            TableHeader(ResourceHandler.Prx6, ResourceHandler.Prx6TT) +
            TableHeader(ResourceHandler.Prx7, ResourceHandler.Prx7TT) +
            TableHeader(ResourceHandler.Prx8, ResourceHandler.Prx8TT) +
            TableHeader(ResourceHandler.Prx9, ResourceHandler.Prx9TT) +
            TableHeader(ResourceHandler.Prx10, ResourceHandler.Prx10TT) +
            TableHeader(ResourceHandler.Prx11, ResourceHandler.Prx11TT) +
    "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.ProxyXmlFromCsv();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d[0], "");
                s += TableData(d[1], "");
                s += TableData(d[2], "");
                s += TableData(d[3], "");
                s += TableData(d[4], "");
                s += TableData(d[5], "");
                s += TableData(d[6], "");
                s += TableData(d[7], "");
                s += TableData(d[8], "");
                s += TableData(d[9], "");
                s += TableData(d[10], "");
                s += TableData(d[11], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddSobrTable()
        {
            string s = "<tr>" +
            TableHeader(ResourceHandler.Sbr0, ResourceHandler.Sbr0TT) +
            TableHeader(ResourceHandler.Sbr1, ResourceHandler.Sbr1TT) +
            TableHeader(ResourceHandler.Sbr2, ResourceHandler.Sbr2TT) +
            TableHeader(ResourceHandler.Sbr3, ResourceHandler.Sbr3TT) +
            TableHeader(ResourceHandler.Sbr4, ResourceHandler.Sbr4TT) +
            TableHeader(ResourceHandler.Sbr5, ResourceHandler.Sbr5TT) +
            TableHeader(ResourceHandler.Sbr6, ResourceHandler.Sbr6TT) +
            TableHeader(ResourceHandler.Sbr7, ResourceHandler.Sbr7TT) +
            TableHeader(ResourceHandler.Sbr8, ResourceHandler.Sbr8TT) +
            TableHeader(ResourceHandler.Sbr9, ResourceHandler.Sbr9TT) +
            TableHeader(ResourceHandler.Sbr10, ResourceHandler.Sbr10TT) +
            TableHeader(ResourceHandler.Sbr11, ResourceHandler.Sbr11TT) +
            TableHeader(ResourceHandler.Sbr12, ResourceHandler.Sbr12TT) +
            "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.SobrInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d[0], "");
                s += TableData(d[3], "");
                s += TableData(d[29], "");
                s += TableData(d[1], "");
                s += TableData(d[4], "");
                s += TableData(d[6], "");
                s += TableData(d[6], "");
                s += TableData(d[7], "");
                s += TableData(d[8], "");
                s += TableData(d[9], "");
                s += TableData(d[10], "");
                s += TableData(d[11], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddSobrExtTable()
        {
            string s = "<tr>" +
TableHeader(ResourceHandler.SbrExt0, ResourceHandler.SbrExt0TT) +
TableHeader(ResourceHandler.SbrExt1, ResourceHandler.SbrExt1TT) +
TableHeader(ResourceHandler.SbrExt2, ResourceHandler.SbrExt2TT) +
TableHeader(ResourceHandler.SbrExt3, ResourceHandler.SbrExt3TT) +
TableHeader(ResourceHandler.SbrExt4, ResourceHandler.SbrExt4TT) +
TableHeader(ResourceHandler.SbrExt5, ResourceHandler.SbrExt5TT) +
TableHeader(ResourceHandler.SbrExt6, ResourceHandler.SbrExt6TT) +
TableHeader(ResourceHandler.SbrExt7, ResourceHandler.SbrExt7TT) +
TableHeader(ResourceHandler.SbrExt8, ResourceHandler.SbrExt8TT) +
TableHeader(ResourceHandler.SbrExt9, ResourceHandler.SbrExt9TT) +
TableHeader(ResourceHandler.SbrExt10, ResourceHandler.SbrExt10TT) +
TableHeader(ResourceHandler.SbrExt11, ResourceHandler.SbrExt11TT) +
TableHeader(ResourceHandler.SbrExt12, ResourceHandler.SbrExt12TT) +
TableHeader(ResourceHandler.SbrExt13, ResourceHandler.SbrExt13TT) +
TableHeader(ResourceHandler.SbrExt14, ResourceHandler.SbrExt14TT) +
TableHeader(ResourceHandler.SbrExt15, ResourceHandler.SbrExt15TT) +
"</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.SobrInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d[0], "");
                s += TableData(d[3], "");
                s += TableData(d[29], "");
                s += TableData(d[1], "");
                s += TableData(d[4], "");
                s += TableData(d[6], "");
                s += TableData(d[6], "");
                s += TableData(d[7], "");
                s += TableData(d[8], "");
                s += TableData(d[9], "");
                s += TableData(d[10], "");
                s += TableData(d[11], "");
                s += TableData(d[12], "");
                s += TableData(d[13], "");
                s += TableData(d[14], "");
                s += TableData(d[15], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddRepoTable()
        {
            string s = "<tr>" +
TableHeader(ResourceHandler.SbrExt0, ResourceHandler.SbrExt0TT) +
TableHeader(ResourceHandler.SbrExt2, ResourceHandler.SbrExt2TT) +
TableHeader(ResourceHandler.Repo0, ResourceHandler.Repo0TT) + 
TableHeader(ResourceHandler.SbrExt3, ResourceHandler.SbrExt3TT) +
TableHeader(ResourceHandler.SbrExt4, ResourceHandler.SbrExt4TT) +
TableHeader(ResourceHandler.SbrExt5, ResourceHandler.SbrExt5TT) +
TableHeader(ResourceHandler.SbrExt6, ResourceHandler.SbrExt6TT) +
TableHeader(ResourceHandler.SbrExt7, ResourceHandler.SbrExt7TT) +
TableHeader(ResourceHandler.SbrExt8, ResourceHandler.SbrExt8TT) +
TableHeader(ResourceHandler.SbrExt9, ResourceHandler.SbrExt9TT) +
TableHeader(ResourceHandler.SbrExt10, ResourceHandler.SbrExt10TT) +
TableHeader(ResourceHandler.Repo1, ResourceHandler.Repo1TT) +
TableHeader(ResourceHandler.SbrExt11, ResourceHandler.SbrExt11TT) +
TableHeader(ResourceHandler.SbrExt12, ResourceHandler.SbrExt12TT) +
TableHeader(ResourceHandler.SbrExt13, ResourceHandler.SbrExt13TT) +
TableHeader(ResourceHandler.SbrExt14, ResourceHandler.SbrExt14TT) +
TableHeader(ResourceHandler.SbrExt15, ResourceHandler.SbrExt15TT) +
"</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.RepoInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += TableData(d[0], "");
                s += TableData(d[3], "");
                s += TableData(d[16], "");
                s += TableData(d[1], "");
                s += TableData(d[4], "");
                s += TableData(d[6], "");
                s += TableData(d[6], "");
                s += TableData(d[7], "");
                s += TableData(d[8], "");
                s += TableData(d[9], "");
                s += TableData(d[10], "");
                s += TableData(d[11], "");
                s += TableData(d[12], "");
                s += TableData(d[13], "");
                s += TableData(d[14], "");
                s += TableData(d[15], "");
                s += TableData(d[16], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddJobConTable()
        {
            return null;
        }
        public string AddTaskConTable()
        {
            return null;
        }
        public string AddJobSessSummTable()
        {
            return null;
        }
        public string AddJobInfoTable()
        {
            return null;
        }


        private string TableHeader(string header, string tooltip)
        {
            return String.Format("<th title=\"{0}\">{1}</th>", tooltip, header);
        }
        private string TableData(string data, string toolTip)
        {
            return String.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
        }
        private string FormNavRows(string linkText, string sectionLink, string info)
        {
            return "<tr>" +
                "<td>" +
                "<li>" +
                String.Format("<a class=\"smoothscroll\" data-link=\"{0}\" href=\"#{0}\">{1}</a>", sectionLink, linkText) +
                "</li>" +
                "</td>" +
                String.Format("<td>{0}</td>", info) +
                "</tr>";
        }
    }
}
