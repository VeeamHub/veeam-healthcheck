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
        private CCsvParser _csv = new(CVariables.vb365dir);


        private string html = "<html>";
        private string htmlend = "</html>";
        private string body = "<body>";
        private string bodyend = "</body>";
        private string _endDiv = "</div>";
        private string _collapsible = "collapsible";
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

        #region VB365 Tables
        public string Globals()
        {
            string s = "<div class=\"Global\" id=\"Global\">";
            s += header2("Global Configuration");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("License Status", "License Status");
            s += TableHeader("License Expiry", "License Expiry");
            s += TableHeader("License Type", "License Type");
            s += TableHeader("Licensed To", "Licensed To");
            s += TableHeader("License Contact", "License Contact");
            s += TableHeader("Licensed For", "Licensed For");
            s += TableHeader("Licenses Used", "Licenses Used");
            s += TableHeader("Support Expiry", "Support Expiry");
            s += TableHeader("Global Folder Exclusions", "Global Folder Exclusions");
            s += TableHeader("Global Ret. Exclusions", "Global Ret. Exclusions");
            s += TableHeader("Log Retention", "Log Retention");
            s += TableHeader("Notification Enabled", "Notification Enabled");
            s += TableHeader("Notifify On", "Notifify On");
            s += TableHeader("Automatic Updates?", "Automatic Updates?");
            s += "</tr>";

            var global = _csv.GetDynamicVboGlobal().ToList();
            s += "<tr>";
            foreach (var gl in global)
            {
                foreach (var g in gl)
                    s += TableData(g.Value, "");
            }
            s += "</tr></table>";

            // summary
            s += GlobalSummary();

            s += "</div>";
            return s;
        }
        public string Vb365Proxies()
        {
            string s = "<div class=\"Proxies\" id=\"Proxies\">";
            s += header2("Proxies");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Proxy Name", "");
            s += TableHeader("Description", "");
            s += TableHeader("Threads", "");
            s += TableHeader("Throttling", "");
            s += TableHeader("State", "");
            s += TableHeader("Type", "");
            s += TableHeader("Outdated", "");
            s += TableHeader("Internet Proxy", "");
            s += TableHeader("Objects Managed", "");
            s += TableHeader("OS Version", "");
            s += TableHeader("RAM", "");
            s += TableHeader("CPUs", "");

            s += "</tr>";

            var global = _csv.GetDynamicVboProxies().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Repos()
        {
            string s = "<div class=\"Repos\" id=\"Repos\">";
            s += header2("Repositories");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Bound Proxy", "Bound Proxy");
            s += TableHeader("Name", "Name");
            s += TableHeader("Description", "Description");
            s += TableHeader("Type", "Type");
            s += TableHeader("Path", "Path");
            s += TableHeader("Object Repo", "Object Repo");
            s += TableHeader("Encryption?", "Encryption?");
            s += TableHeader("Out of Sync?", "Out of Sync?");
            s += TableHeader("Outdated?", "Outdated?");
            s += TableHeader("Capacity", "Capacity");
            s += TableHeader("Local Space Used", "Local Space Used");
            s += TableHeader("Cache Space Used", "Cache Space Used");
            s += TableHeader("Object Space Used", "Object Space Used");
            s += TableHeader("Free", "Free");
            s += TableHeader("Retention", "Retention");

            s += "</tr>";

            var global = _csv.GetDynamicVboRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Rbac()
        {
            string s = "<div class=\"rbac\" id=\"rbac\">";
            s += header2("RBAC Roles Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Name", "");
            s += TableHeader("Description", "");
            s += TableHeader("Role Type", "");
            s += TableHeader("Operators", "");
            s += TableHeader("Selected Items", "");
            s += TableHeader("Excluded Items", "");

            s += "</tr>";

            var global = _csv.GetDynamicVboRbac().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Security()
        {
            string s = "<div class=\"security\" id=\"security\">";
            s += header2("Security Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Win. Firewall Enabled?", "Win. Firewall Enabled?");
            s += TableHeader("Internet proxy?", "Internet proxy?");
            s += TableHeader("Server Cert", "Server Cert");
            s += TableHeader("Server Cert PK Exportable?", "Server Cert PK Exportable?");
            s += TableHeader("Server Cert Expires", "Server Cert Expires");
            s += TableHeader("Server Cert Self-Signed?", "Server Cert Self-Signed?");
            s += TableHeader("API Enabled?", "API Enabled?");
            s += TableHeader("API Port", "API Port");
            s += TableHeader("API Cert", "API Cert");
            s += TableHeader("API Cert PK Exportable?", "API Cert PK Exportable?");
            s += TableHeader("API Cert Expires", "API Cert Expires");
            s += TableHeader("API Cert Self-Signed?", "API Cert Self-Signed?");
            s += TableHeader("Tenant Auth Enabled?", "Tenant Auth Enabled?");
            s += TableHeader("Tenant Auth Cert", "Tenant Auth Cert");
            s += TableHeader("Tenant Auth PK Exportable?", "Tenant Auth PK Exportable?");
            s += TableHeader("Tenant Auth Cert Expires", "Tenant Auth Cert Expires");
            s += TableHeader("Tenant Auth Cert Self-Signed?", "Tenant Auth Cert Self-Signed?");
            s += TableHeader("Restore Portal Enabled?", "Restore Portal Enabled?");
            s += TableHeader("Restore Portal Cert", "Restore Portal Cert");
            s += TableHeader("Restore Portal Cert PK Exportable?", "Restore Portal Cert PK Exportable?");
            s += TableHeader("Restore Portal Cert Expires", "Restore Portal Cert Expires");
            s += TableHeader("Restore Portal Cert Self-Signed?", "Restore Portal Cert Self-Signed?");
            s += TableHeader("Operator Auth Enabled?", "Operator Auth Enabled?");
            s += TableHeader("Operator Auth Cert", "Operator Auth Cert");
            s += TableHeader("Operator Auth Cert PK Exportable?", "Operator Auth Cert PK Exportable?");
            s += TableHeader("Operator Auth Cert Expires", "Operator Auth Cert Expires");
            s += TableHeader("Operator Auth Cert Self-Signed?", "Operator Auth Cert Self-Signed?");

            s += "</tr>";

            var global = _csv.GetDynamicVboSec().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Controllers()
        {
            string s = "<div class=\"controllers\" id=\"controllers\">";
            s += header2("Backup Server");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("VB365 Version", "VB365 Version");
            s += TableHeader("OS Version", "OS Version");
            s += TableHeader("RAM", "RAM");
            s += TableHeader("CPUs", "CPUs");
            s += TableHeader("Proxies Managed", "Proxies Managed");
            s += TableHeader("Repos Managed", "Repos Managed");
            s += TableHeader("Orgs Managed", "Orgs Managed");
            s += TableHeader("Jobs Managed", "Jobs Managed");
            s += TableHeader("PowerShell Installed?", "PowerShell Installed?");
            s += TableHeader("Proxy Installed?", "Proxy Installed?");
            s += TableHeader("REST Installed?", "REST Installed?");
            s += TableHeader("Console Installed?", "Console Installed?");
            s += TableHeader("VM Name", "VM Name");
            s += TableHeader("VM Location", "VM Location");
            s += TableHeader("VM SKU", "VM SKU");
            s += TableHeader("VM Size", "VM Size");



            s += "</tr>";

            var global = _csv.GetDynVboController().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ControllerDrivers()
        {
            string s = "<div class=\"controllerdrivers\" id=\"controllerdrivers\">";
            s += header2("Backup Server Disks");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Friendly Name", "Friendly Name");
            s += TableHeader("DeviceId", "DeviceId");
            s += TableHeader("Bus Type", "Bus Type");
            s += TableHeader("Media Type", "Media Type");
            s += TableHeader("Manufacturer", "Manufacturer");
            s += TableHeader("Model", "Model");
            s += TableHeader("Size", "Size");
            s += TableHeader("Allocated Size", "Allocated Size");
            s += TableHeader("Operational Status", "Operational Status");
            s += TableHeader("Health Status", "Health Status");
            s += TableHeader("Boot Drive", "Boot Drive");

            s += "</tr>";

            var global = _csv.GetDynVboControllerDriver().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365JobSessions()
        {
            string s = "<div class=\"jobsessions\" id=\"jobsessions\">";
            s += header2("Job Sessions");
            //s += "<br>";
            s += CollapsibleButton("Show Job Sessions");

            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += TableHeader("Name", "Name");
            s += TableHeader("Status", "Status");
            s += TableHeader("Start Time", "Start Time");
            s += TableHeader("End Time", "End Time");
            s += TableHeader("Duration", "Duration");
            s += TableHeader("Log", "Log");

            s += "</tr>";

            var global = _csv.GetDynVboJobSess().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365JobStats()
        {
            string s = "<div class=\"jobstats\" id=\"jobstats\">";
            s += header2("Job Statistics");
            //s += "<br>";
            s += CollapsibleButton("Show Job Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += TableHeader("Name", "Name");
            s += TableHeader("Average Duration (min)", "Average Duration (min)");
            s += TableHeader("Max Duration (min)", "Max Duration (min)");
            s += TableHeader("Average Data Transferred", "Average Data Transferred");
            s += TableHeader("Max Data Transferred", "Max Data Transferred");
            s += TableHeader("Average Objects (#)", "Average Objects (#)");
            s += TableHeader("Max Objects (#)", "Max Objects (#)");
            s += TableHeader("Average Items (#)", "Average Items (#)");
            s += TableHeader("Max Items (#)", "Max Items (#)");
            s += TableHeader("Typical Bottleneck", "Typical Bottleneck");
            s += TableHeader("Job Avg Throughput", "Job Avg Throughput");
            s += TableHeader("Job Avg Processing Rate", "Job Avg Processing Rate");

            s += "</tr>";

            var global = _csv.GetDynVboJobSess().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ObjectRepos()
        {
            string s = "<div class=\"objrepos\" id=\"objrepos\">";
            s += header2("Object Storage");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Name", "Name");
            s += TableHeader("Description", "Description");
            s += TableHeader("Cloud", "Cloud");
            s += TableHeader("Type", "Type");
            s += TableHeader("Bucket/Container", "Bucket/Container");
            s += TableHeader("Path", "Path");
            s += TableHeader("Size Limit", "Size Limit");
            s += TableHeader("Used Space", "Used Space");
            s += TableHeader("Free Space", "Free Space");

            s += "</tr>";

            var global = _csv.GetDynVboObjRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Orgs()
        {
            string s = "<div class=\"orgs\" id=\"orgs\">";
            s += header2("Organizations");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Friendly Name", "Friendly Name");
            s += TableHeader("Real Name", "Real Name");
            s += TableHeader("Type", "Type");
            s += TableHeader("Protected Apps", "Protected Apps");
            s += TableHeader("EXO Settings", "EXO Settings");
            s += TableHeader("EXO App Cert", "EXO App Cert");
            s += TableHeader("SPO Settings", "SPO Settings");
            s += TableHeader("SPO App Cert", "SPO App Cert");
            s += TableHeader("On-Prem Exch Settings", "On-Prem Exch Settings");
            s += TableHeader("On-Prem SP Settings", "On-Prem SP Settings");
            s += TableHeader("Licensed Users", "Licensed Users");
            s += TableHeader("Grant SC Admin", "Grant SC Admin");
            s += TableHeader("Aux Accounts/Apps", "Aux Accounts/Apps");

            s += "</tr>";

            var global = _csv.GetDynVboOrg().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Permissions()
        {
            string s = "<div class=\"perms\" id=\"perms\">";
            s += header2("Permissions Check");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += TableHeader("Type", "Type");
            s += TableHeader("Organization", "Organization");
            s += TableHeader("API", "API");
            s += TableHeader("Permission", "Permission");

            s += "</tr>";

            var global = _csv.GetDynVboPerms().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ProtStat()
        {
            string s = "<div class=\"protstat\" id=\"protstat\">";
            s += header2("Unprotected Users");
            //s += CollapsibleButton("Show Protection Statistics");
            //s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";
            //s += TableHeader("User", "User");
            //s += TableHeader("E-mail", "E-mail");
            //s += TableHeader("Organization", "Organization");
            //s += TableHeader("Protection Status", "Protection Status");
            //s += TableHeader("Last Backup Date", "Last Backup Date");

            //s += "</tr>";

            s += "<tr>";
            s += TableHeader("Unprotected Users", "");
            s += "</tr>";
            int counter = 0;
            var global = _csv.GetDynVboProtStat().ToList();
            foreach (var gl in global)
            {
                //s += "<tr>";

                foreach (var g in gl)
                    counter++;
                    //s += TableData(g.Value, "");

                //s += "</tr>";
            }
            s += "<tr>";
            s += TableData(counter.ToString(), "");
            s += "</tr>";

            


            s += "</table>";


            s += "</div>";
            return s;
        }
        private string SummaryTemplate()
        {
            string s = CollapsibleButton(ResourceHandler.BkpSrvButton);

            s += "<div class=\"content\">";
            s += AddA("hdr", ResourceHandler.GeneralSummaryHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvSummary1) +
                AddA("i3", ResourceHandler.BkpSrvSummary2) +
                AddA("i3", ResourceHandler.BkpSrvSummary3) +
                AddA("i3", ResourceHandler.BkpSrvSummary4) +
                DoubleLineBreak() +
                AddA("hdr", ResourceHandler.GeneralNotesHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvNotes1) +
                AddA("i2", ResourceHandler.BkpSrvNotes2) +
                AddA("i2", ResourceHandler.BkpSrvNotes3) +
                AddA("i2", ResourceHandler.BkpSrvNotes4) +
                AddA("i2", ResourceHandler.BkpSrvNotes5) +
                AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        private string GlobalSummary()
        {
            string s = CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += AddA("hdr", ResourceHandler.GeneralSummaryHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvSummary1) +
                AddA("i3", ResourceHandler.BkpSrvSummary2) +
                AddA("i3", ResourceHandler.BkpSrvSummary3) +
                AddA("i3", ResourceHandler.BkpSrvSummary4) +
                DoubleLineBreak() +
                AddA("hdr", ResourceHandler.GeneralNotesHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvNotes1) +
                AddA("i2", ResourceHandler.BkpSrvNotes2) +
                AddA("i2", ResourceHandler.BkpSrvNotes3) +
                AddA("i2", ResourceHandler.BkpSrvNotes4) +
                AddA("i2", ResourceHandler.BkpSrvNotes5) +
                AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        private string AddA(string classInfo, string displaytext)
        {
            return String.Format("<a class=\"{0}\">{1}</a>" + LineBreak(), classInfo, displaytext);
        }
        private string header2(string text)
        {
            return String.Format("<h2>{0}:</h2>", text);
        }
        private string LineBreak()
        {
            return "<br/>";
        }
        private string DoubleLineBreak()
        {
            return "<br/><br/>";
        }
        #endregion
        private string SectionButton(string classType, string displayText)
        {
            return String.Format("<button type=\"button\" class=\"{0}\">{1}</button>", classType, displayText);
        }
        private string CollapsibleButton(string buttonText)
        {
            return SectionButton(_collapsible, buttonText);
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
