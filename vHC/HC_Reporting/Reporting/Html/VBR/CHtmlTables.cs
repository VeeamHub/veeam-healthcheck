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

            for(int i = 0; i < list.Count(); i++)
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
            return null;
        }
        public string AddJobSummaryTable()
        {
            return null;
        }
        public string AddMissingJobsTable()
        {
            return null;
        }
        public string AddProtectedWorkLoadsTable()
        {
            return null;
        }
        public string AddManagedServersTable()
        {
            return null;
        }
        public string AddRegKeysTable()
        {
            return null;
        }
        public string AddProxyTable()
        {
            return null;
        }
        public string AddSobrTable()
        {
            return null;
        }
        public string AddSobrExtTable()
        {
            return null;
        }
        public string AddRepoTable()
        {
            return null;
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
