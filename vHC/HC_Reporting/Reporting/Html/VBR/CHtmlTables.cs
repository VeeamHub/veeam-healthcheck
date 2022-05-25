using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Reporting.Html;
using VeeamHealthCheck.Reporting.Html.Shared;

namespace VeeamHealthCheck.Html.VBR
{
    internal class CHtmlTables
    {
        private CCsvParser _csv = new(CVariables.vb365dir);

        private CHtmlFormatting _form = new();


        public CHtmlTables()
        {

        }
        public string MakeNavTable()
        {
            return _form.FormNavRows(ResourceHandler.NavLicInfoLink, "license", ResourceHandler.NavLicInfoDetail) +
                _form.FormNavRows(ResourceHandler.NavBkpSrvLink, "backupserver", ResourceHandler.NavBkpSrvDeet) +
                _form.FormNavRows(ResourceHandler.NavSecSumLink, "", ResourceHandler.NavSecSumDeet) +
                _form.FormNavRows(ResourceHandler.NavSrvSumLink, "", ResourceHandler.NavSrvSumDeet) +
                _form.FormNavRows(ResourceHandler.NavJobSumLink, "", ResourceHandler.NavJobSumDeet) +
                _form.FormNavRows(ResourceHandler.NavMissingJobLink, "", ResourceHandler.NavMissingDeet) +
                _form.FormNavRows(ResourceHandler.NavProtWrkld, "", ResourceHandler.NavProtWkldDeet) +
                _form.FormNavRows(ResourceHandler.NavSrvInfoLink, "", ResourceHandler.NavSrvInfoDeet) +
                _form.FormNavRows(ResourceHandler.NavRegKeyLink, "", ResourceHandler.NavRegKeyDeet) +
                _form.FormNavRows(ResourceHandler.NavProxyInfoLink, "", ResourceHandler.NavProxyDeet) +
                _form.FormNavRows(ResourceHandler.NavSobrInfoLink, "", ResourceHandler.NavSobrDeet) +
                _form.FormNavRows(ResourceHandler.NavSobrExtLink, "", ResourceHandler.NavSobrExtDeet) +
                _form.FormNavRows(ResourceHandler.NavRepoInfoLink, "", ResourceHandler.NavRepoDeet) +
                _form.FormNavRows(ResourceHandler.NavJobConLink, "", ResourceHandler.NavJobConDeet) +
                _form.FormNavRows(ResourceHandler.NavTaskConLink, "", ResourceHandler.NavTaskConDeet) +
                _form.FormNavRows(ResourceHandler.NavJobSessSumLink, "", ResourceHandler.NavJobSessSumDeet) +
                _form.FormNavRows(ResourceHandler.NavJobInfoLink, "", ResourceHandler.NavJobInfoDeet);
        }

        public string AddLicHeaderToTable()
        {
            return "<tr>" +
            _form.TableHeader(ResourceHandler.LicTblLicTo, "") +
            _form.TableHeader(ResourceHandler.LicTblEdition, ResourceHandler.LtEdTT) +
                _form.TableHeader(ResourceHandler.LicTblStatus, ResourceHandler.LtStatusTT) +
                _form.TableHeader(ResourceHandler.LicTblType, ResourceHandler.LtTypeTT) +
                _form.TableHeader(ResourceHandler.LicTblLicInst, ResourceHandler.LtInstLicTT) +
                _form.TableHeader(ResourceHandler.LicTblUsedInst, ResourceHandler.LtInstUsedTT) +
                _form.TableHeader(ResourceHandler.LicTblNewInst, ResourceHandler.LtInstNewTT) +
                _form.TableHeader(ResourceHandler.LicTblRentInst, ResourceHandler.LtInstRentalTT) +
                _form.TableHeader(ResourceHandler.LicTblLicSock, ResourceHandler.LtSocLicTT) +
                _form.TableHeader(ResourceHandler.LicTblUsedSock, ResourceHandler.LtSocUsedTT) +
                _form.TableHeader(ResourceHandler.LicTblLicNas, ResourceHandler.LtNasLicTT) +
                _form.TableHeader(ResourceHandler.LicTblUsedNas, ResourceHandler.LtNasUsedTT) +
                _form.TableHeader(ResourceHandler.LicTblExpDate, ResourceHandler.LicExpTT) +
                _form.TableHeader(ResourceHandler.LicTblSupExpDate, ResourceHandler.LicSupExpTT) +
                _form.TableHeader(ResourceHandler.LicTblCc, ResourceHandler.LicCcTT) +
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
                _form.TableData(l.licensedto, "") +
                _form.TableData(l.edition, "") +
                _form.TableData(l.status, "") +
                _form.TableData(l.type, "") +
                _form.TableData(l.licensedinstances, "") +
                _form.TableData(l.usedinstances, "") +
                _form.TableData(l.newinstances, "") +
                _form.TableData(l.rentalinstances, "") +
                _form.TableData(l.licensedsockets, "") +
                _form.TableData(l.usedsockets, "") +
                _form.TableData(l.licensedcapacitytb, "") +
                _form.TableData(l.usedcapacitytb, "") +
                _form.TableData(l.expirationdate, "") +
                _form.TableData(l.supportexpirationdate, "") +
                _form.TableData(l.cloudconnect, "") +
                "</tr>";
            }
            return tableString;
        }
        public string AddBkpSrvTable()
        {
            string s = "<tr>" +
                _form.TableHeader(ResourceHandler.BkpSrvTblName, ResourceHandler.BstNameTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblVersion, ResourceHandler.BstVerTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblCore, ResourceHandler.BstCpuTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblRam, ResourceHandler.BstRamTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblCfgEnabled, ResourceHandler.BstCfgEnabledTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblCfgLastRes, ResourceHandler.BstCfgLastResTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblCfgEncrypt, ResourceHandler.BstCfgEncTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblTarget, ResourceHandler.BstCfgTarTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlLocal, ResourceHandler.BstSqlLocTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlName, ResourceHandler.BstSqlNameTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlVersion, ResourceHandler.BstSqlVerTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlEdition, ResourceHandler.BstSqlEdTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlCores, ResourceHandler.BstSqlCpuTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblSqlRam, ResourceHandler.BstSqlRamTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblProxyRole, ResourceHandler.BstPrxTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblRepoRole, ResourceHandler.BstRepTT) +
                _form.TableHeader(ResourceHandler.BkpSrvTblWanRole, ResourceHandler.BstWaTT) +
                "</tr>";
            CDataFormer cd = new(true);
            List<string> list = cd.BackupServerInfoToXml();
            s += "<tr>";

            for (int i = 0; i < list.Count(); i++)
            {
                s += _form.TableData(list[i], "");

            }
            s += "</tr>";

            return s;

        }
        public string AddSecSummaryTable()
        {
            string s = "<tr>" +
                _form.TableHeader(ResourceHandler.SSHdr0, ResourceHandler.SSHdrTT0) +
                _form.TableHeader(ResourceHandler.SSHdr1, ResourceHandler.SSHdrTT1) +
                _form.TableHeader(ResourceHandler.SSHdr2, ResourceHandler.SSHdrTT2) +
                _form.TableHeader(ResourceHandler.SSHdr3, ResourceHandler.SSHdrTT3) +
                "</tr>" +
                "<tr>";
            CDataFormer df = new(true);
            List<int> list = df.SecSummary();

            for (int i = 0; i < list.Count(); i++)
            {
                if (list[i] == 0)
                    s += _form.TableData("False", "");
                else if (list[i] == 1)
                    s += _form.TableData("True", "");
            }
            s += "</tr>";

            return s;
        }
        public string AddSrvSummaryTable()
        {
            string s = "<tr>" +
                _form.TableHeader(ResourceHandler.MssHdr1, ResourceHandler.MssHdr1TT) +
                _form.TableHeader(ResourceHandler.MssHdr2, ResourceHandler.MssHdr2TT) +
                "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, int> list = cd.ServerSummaryToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d.Key, "");
                s += _form.TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddJobSummaryTable()
        {
            string s = "<tr>" +
    _form.TableHeader(ResourceHandler.JobSum0, ResourceHandler.JobSum0TT) +
    _form.TableHeader(ResourceHandler.JobSum1, ResourceHandler.JobSum1TT) +
    "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, int> list = cd.JobSummaryInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d.Key, "");
                s += _form.TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddMissingJobsTable()
        {
            string s = "<tr>" +
                _form.TableHeader(ResourceHandler.JobSum0, "") +
                //_form.TableHeader("Count", "Total detected of this type") +
                "</tr>";
            CDataFormer cd = new(true);
            List<string> list = cd.ParseNonProtectedTypes();

            for (int i = 0; i < list.Count(); i++)
            {
                s += "<tr>";

                s += _form.TableData(list[i], "");
                s += "</tr>";

            }

            return s;
        }
        public string AddProtectedWorkLoadsTable()
        {
            string s = "<tr>" +
            _form.TableHeader(ResourceHandler.PlHdr0, ResourceHandler.PlHdrTT0) +
            _form.TableHeader(ResourceHandler.PlHdr1, ResourceHandler.PlHdrTT1) +
            _form.TableHeader(ResourceHandler.PlHdr2, ResourceHandler.PlHdrTT2) +
            _form.TableHeader(ResourceHandler.PlHdr3, ResourceHandler.PlHdrTT3) +
            _form.TableHeader(ResourceHandler.PlHdr4, ResourceHandler.PlHdrTT4) +
            _form.TableHeader(ResourceHandler.PlHdr5, ResourceHandler.PlHdrTT5) +
            _form.TableHeader(ResourceHandler.PlHdr6, ResourceHandler.PlHdrTT6) +
            _form.TableHeader(ResourceHandler.PlHdr7, ResourceHandler.PlHdrTT7) +

    "</tr>";
            CDataFormer cd = new(true);
            cd.ProtectedWorkloadsToXml();
            s += "<tr>";
            s += _form.TableData((cd._viProtectedNames.Distinct().Count() + cd._viNotProtectedNames.Distinct().Count()).ToString(), "");
            s += _form.TableData(cd._viProtectedNames.Distinct().Count().ToString(), "");
            s += _form.TableData(cd._viNotProtectedNames.Distinct().Count().ToString(), "");
            s += _form.TableData(cd._viDupes.ToString(), "");
            s += _form.TableData((cd._physNotProtNames.Distinct().Count() + cd._physProtNames.Distinct().Count()).ToString(), "");
            s += _form.TableData(cd._physProtNames.Distinct().Count().ToString(), "");
            s += _form.TableData(cd._physNotProtNames.Distinct().Count().ToString(), "");

            s += "</tr>";

            return s;
        }
        public string AddManagedServersTable()
        {
            string s = "<tr>" +
            _form.TableHeader(ResourceHandler.ManSrv0, ResourceHandler.ManSrv0TT) +
            _form.TableHeader(ResourceHandler.ManSrv1, ResourceHandler.ManSrv1TT) +
            _form.TableHeader(ResourceHandler.ManSrv2, ResourceHandler.ManSrv2TT) +
            _form.TableHeader(ResourceHandler.ManSrv3, ResourceHandler.ManSrv3TT) +
            _form.TableHeader(ResourceHandler.ManSrv4, ResourceHandler.ManSrv4TT) +
            _form.TableHeader(ResourceHandler.ManSrv5, ResourceHandler.ManSrv5TT) +
            _form.TableHeader(ResourceHandler.ManSrv6, ResourceHandler.ManSrv6TT) +
            _form.TableHeader(ResourceHandler.ManSrv7, ResourceHandler.ManSrv7TT) +
            _form.TableHeader(ResourceHandler.ManSrv8, ResourceHandler.ManSrv8TT) +
            _form.TableHeader(ResourceHandler.ManSrv9, ResourceHandler.ManSrv9TT) +
            _form.TableHeader(ResourceHandler.ManSrv10, ResourceHandler.ManSrv10TT) +
            _form.TableHeader(ResourceHandler.ManSrv11, ResourceHandler.ManSrv11TT) +
            "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.ServerXmlFromCsv();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[2], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[5], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += "</tr>";
            }


            return s;
        }
        public string AddRegKeysTable()
        {
            string s = "<tr>" +
                _form.TableHeader(ResourceHandler.Reg0, ResourceHandler.Reg0TT) +
                _form.TableHeader(ResourceHandler.Reg1, ResourceHandler.Reg1TT) +
                "</tr>";
            CDataFormer cd = new(true);
            Dictionary<string, string> list = cd.RegOptions();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d.Key, "");
                s += _form.TableData(d.Value.ToString(), "");
                s += "</tr>";
            }

            return s;
        }
        public string AddProxyTable()
        {
            string s = "<tr>" +
            _form.TableHeader(ResourceHandler.Prx0, ResourceHandler.Prx0TT) +
            _form.TableHeader(ResourceHandler.Prx1, ResourceHandler.Prx1TT) +
            _form.TableHeader(ResourceHandler.Prx2, ResourceHandler.Prx2TT) +
            _form.TableHeader(ResourceHandler.Prx3, ResourceHandler.Prx3TT) +
            _form.TableHeader(ResourceHandler.Prx4, ResourceHandler.Prx4TT) +
            _form.TableHeader(ResourceHandler.Prx5, ResourceHandler.Prx5TT) +
            _form.TableHeader(ResourceHandler.Prx6, ResourceHandler.Prx6TT) +
            _form.TableHeader(ResourceHandler.Prx7, ResourceHandler.Prx7TT) +
            _form.TableHeader(ResourceHandler.Prx8, ResourceHandler.Prx8TT) +
            _form.TableHeader(ResourceHandler.Prx9, ResourceHandler.Prx9TT) +
            _form.TableHeader(ResourceHandler.Prx10, ResourceHandler.Prx10TT) +
            _form.TableHeader(ResourceHandler.Prx11, ResourceHandler.Prx11TT) +
    "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.ProxyXmlFromCsv();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[2], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[5], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddSobrTable()
        {
            string s = "<tr>" +
            _form.TableHeader(ResourceHandler.Sbr0, ResourceHandler.Sbr0TT) +
            _form.TableHeader(ResourceHandler.Sbr1, ResourceHandler.Sbr1TT) +
            _form.TableHeader(ResourceHandler.Sbr2, ResourceHandler.Sbr2TT) +
            _form.TableHeader(ResourceHandler.Sbr3, ResourceHandler.Sbr3TT) +
            _form.TableHeader(ResourceHandler.Sbr4, ResourceHandler.Sbr4TT) +
            _form.TableHeader(ResourceHandler.Sbr5, ResourceHandler.Sbr5TT) +
            _form.TableHeader(ResourceHandler.Sbr6, ResourceHandler.Sbr6TT) +
            _form.TableHeader(ResourceHandler.Sbr7, ResourceHandler.Sbr7TT) +
            _form.TableHeader(ResourceHandler.Sbr8, ResourceHandler.Sbr8TT) +
            _form.TableHeader(ResourceHandler.Sbr9, ResourceHandler.Sbr9TT) +
            _form.TableHeader(ResourceHandler.Sbr10, ResourceHandler.Sbr10TT) +
            _form.TableHeader(ResourceHandler.Sbr11, ResourceHandler.Sbr11TT) +
            _form.TableHeader(ResourceHandler.Sbr12, ResourceHandler.Sbr12TT) +
            "</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.SobrInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[29], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddSobrExtTable()
        {
            string s = "<tr>" +
_form.TableHeader(ResourceHandler.SbrExt0, ResourceHandler.SbrExt0TT) +
_form.TableHeader(ResourceHandler.SbrExt1, ResourceHandler.SbrExt1TT) +
_form.TableHeader(ResourceHandler.SbrExt2, ResourceHandler.SbrExt2TT) +
_form.TableHeader(ResourceHandler.SbrExt3, ResourceHandler.SbrExt3TT) +
_form.TableHeader(ResourceHandler.SbrExt4, ResourceHandler.SbrExt4TT) +
_form.TableHeader(ResourceHandler.SbrExt5, ResourceHandler.SbrExt5TT) +
_form.TableHeader(ResourceHandler.SbrExt6, ResourceHandler.SbrExt6TT) +
_form.TableHeader(ResourceHandler.SbrExt7, ResourceHandler.SbrExt7TT) +
_form.TableHeader(ResourceHandler.SbrExt8, ResourceHandler.SbrExt8TT) +
_form.TableHeader(ResourceHandler.SbrExt9, ResourceHandler.SbrExt9TT) +
_form.TableHeader(ResourceHandler.SbrExt10, ResourceHandler.SbrExt10TT) +
_form.TableHeader(ResourceHandler.SbrExt11, ResourceHandler.SbrExt11TT) +
_form.TableHeader(ResourceHandler.SbrExt12, ResourceHandler.SbrExt12TT) +
_form.TableHeader(ResourceHandler.SbrExt13, ResourceHandler.SbrExt13TT) +
_form.TableHeader(ResourceHandler.SbrExt14, ResourceHandler.SbrExt14TT) +
_form.TableHeader(ResourceHandler.SbrExt15, ResourceHandler.SbrExt15TT) +
"</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.SobrInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[29], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += _form.TableData(d[12], "");
                s += _form.TableData(d[13], "");
                s += _form.TableData(d[14], "");
                s += _form.TableData(d[15], "");
                s += "</tr>";
            }

            return s;
        }
        public string AddRepoTable()
        {
            string s = "<tr>" +
_form.TableHeader(ResourceHandler.SbrExt0, ResourceHandler.SbrExt0TT) +
_form.TableHeader(ResourceHandler.SbrExt2, ResourceHandler.SbrExt2TT) +
_form.TableHeader(ResourceHandler.Repo0, ResourceHandler.Repo0TT) +
_form.TableHeader(ResourceHandler.SbrExt3, ResourceHandler.SbrExt3TT) +
_form.TableHeader(ResourceHandler.SbrExt4, ResourceHandler.SbrExt4TT) +
_form.TableHeader(ResourceHandler.SbrExt5, ResourceHandler.SbrExt5TT) +
_form.TableHeader(ResourceHandler.SbrExt6, ResourceHandler.SbrExt6TT) +
_form.TableHeader(ResourceHandler.SbrExt7, ResourceHandler.SbrExt7TT) +
_form.TableHeader(ResourceHandler.SbrExt8, ResourceHandler.SbrExt8TT) +
_form.TableHeader(ResourceHandler.SbrExt9, ResourceHandler.SbrExt9TT) +
_form.TableHeader(ResourceHandler.SbrExt10, ResourceHandler.SbrExt10TT) +
_form.TableHeader(ResourceHandler.Repo1, ResourceHandler.Repo1TT) +
_form.TableHeader(ResourceHandler.SbrExt11, ResourceHandler.SbrExt11TT) +
_form.TableHeader(ResourceHandler.SbrExt12, ResourceHandler.SbrExt12TT) +
_form.TableHeader(ResourceHandler.SbrExt13, ResourceHandler.SbrExt13TT) +
_form.TableHeader(ResourceHandler.SbrExt14, ResourceHandler.SbrExt14TT) +
_form.TableHeader(ResourceHandler.SbrExt15, ResourceHandler.SbrExt15TT) +
"</tr>";
            CDataFormer cd = new(true);
            List<string[]> list = cd.RepoInfoToXml();

            foreach (var d in list)
            {
                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[16], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += _form.TableData(d[12], "");
                s += _form.TableData(d[13], "");
                s += _form.TableData(d[14], "");
                s += _form.TableData(d[15], "");
                s += _form.TableData(d[16], "");
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
            s += _form.header2("Global Configuration");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("License Status", "License Status");
            s += _form.TableHeader("License Expiry", "License Expiry");
            s += _form.TableHeader("License Type", "License Type");
            s += _form.TableHeader("Licensed To", "Licensed To");
            s += _form.TableHeader("License Contact", "License Contact");
            s += _form.TableHeader("Licensed For", "Licensed For");
            s += _form.TableHeader("Licenses Used", "Licenses Used");
            s += _form.TableHeader("Support Expiry", "Support Expiry");
            s += _form.TableHeader("Global Folder Exclusions", "Global Folder Exclusions");
            s += _form.TableHeader("Global Ret. Exclusions", "Global Ret. Exclusions");
            s += _form.TableHeader("Log Retention", "Log Retention");
            s += _form.TableHeader("Notification Enabled", "Notification Enabled");
            s += _form.TableHeader("Notifify On", "Notifify On");
            s += _form.TableHeader("Automatic Updates?", "Automatic Updates?");
            s += "</tr>";

            var global = _csv.GetDynamicVboGlobal().ToList();
            s += "<tr>";
            foreach (var gl in global)
            {
                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");
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
            s += _form.header2("Proxies");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Proxy Name", "");
            s += _form.TableHeader("Description", "");
            s += _form.TableHeader("Threads", "");
            s += _form.TableHeader("Throttling", "");
            s += _form.TableHeader("State", "");
            s += _form.TableHeader("Type", "");
            s += _form.TableHeader("Outdated", "");
            s += _form.TableHeader("Internet Proxy", "");
            s += _form.TableHeader("Objects Managed", "");
            s += _form.TableHeader("OS Version", "");
            s += _form.TableHeader("RAM", "");
            s += _form.TableHeader("CPUs", "");

            s += "</tr>";

            var global = _csv.GetDynamicVboProxies().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Repos()
        {
            string s = "<div class=\"Repos\" id=\"Repos\">";
            s += _form.header2("Repositories");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Bound Proxy", "Bound Proxy");
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Description", "Description");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Path", "Path");
            s += _form.TableHeader("Object Repo", "Object Repo");
            s += _form.TableHeader("Encryption?", "Encryption?");
            s += _form.TableHeader("Out of Sync?", "Out of Sync?");
            s += _form.TableHeader("Outdated?", "Outdated?");
            s += _form.TableHeader("Capacity", "Capacity");
            s += _form.TableHeader("Local Space Used", "Local Space Used");
            s += _form.TableHeader("Cache Space Used", "Cache Space Used");
            s += _form.TableHeader("Object Space Used", "Object Space Used");
            s += _form.TableHeader("Free", "Free");
            s += _form.TableHeader("Retention", "Retention");

            s += "</tr>";

            var global = _csv.GetDynamicVboRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Rbac()
        {
            string s = "<div class=\"rbac\" id=\"rbac\">";
            s += _form.header2("RBAC Roles Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Name", "");
            s += _form.TableHeader("Description", "");
            s += _form.TableHeader("Role Type", "");
            s += _form.TableHeader("Operators", "");
            s += _form.TableHeader("Selected Items", "");
            s += _form.TableHeader("Excluded Items", "");

            s += "</tr>";

            var global = _csv.GetDynamicVboRbac().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Security()
        {
            string s = "<div class=\"security\" id=\"security\">";
            s += _form.header2("Security Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Win. Firewall Enabled?", "Win. Firewall Enabled?");
            s += _form.TableHeader("Internet proxy?", "Internet proxy?");
            s += _form.TableHeader("Server Cert", "Server Cert");
            s += _form.TableHeader("Server Cert PK Exportable?", "Server Cert PK Exportable?");
            s += _form.TableHeader("Server Cert Expires", "Server Cert Expires");
            s += _form.TableHeader("Server Cert Self-Signed?", "Server Cert Self-Signed?");
            s += _form.TableHeader("API Enabled?", "API Enabled?");
            s += _form.TableHeader("API Port", "API Port");
            s += _form.TableHeader("API Cert", "API Cert");
            s += _form.TableHeader("API Cert PK Exportable?", "API Cert PK Exportable?");
            s += _form.TableHeader("API Cert Expires", "API Cert Expires");
            s += _form.TableHeader("API Cert Self-Signed?", "API Cert Self-Signed?");
            s += _form.TableHeader("Tenant Auth Enabled?", "Tenant Auth Enabled?");
            s += _form.TableHeader("Tenant Auth Cert", "Tenant Auth Cert");
            s += _form.TableHeader("Tenant Auth PK Exportable?", "Tenant Auth PK Exportable?");
            s += _form.TableHeader("Tenant Auth Cert Expires", "Tenant Auth Cert Expires");
            s += _form.TableHeader("Tenant Auth Cert Self-Signed?", "Tenant Auth Cert Self-Signed?");
            s += _form.TableHeader("Restore Portal Enabled?", "Restore Portal Enabled?");
            s += _form.TableHeader("Restore Portal Cert", "Restore Portal Cert");
            s += _form.TableHeader("Restore Portal Cert PK Exportable?", "Restore Portal Cert PK Exportable?");
            s += _form.TableHeader("Restore Portal Cert Expires", "Restore Portal Cert Expires");
            s += _form.TableHeader("Restore Portal Cert Self-Signed?", "Restore Portal Cert Self-Signed?");
            s += _form.TableHeader("Operator Auth Enabled?", "Operator Auth Enabled?");
            s += _form.TableHeader("Operator Auth Cert", "Operator Auth Cert");
            s += _form.TableHeader("Operator Auth Cert PK Exportable?", "Operator Auth Cert PK Exportable?");
            s += _form.TableHeader("Operator Auth Cert Expires", "Operator Auth Cert Expires");
            s += _form.TableHeader("Operator Auth Cert Self-Signed?", "Operator Auth Cert Self-Signed?");

            s += "</tr>";

            var global = _csv.GetDynamicVboSec().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Controllers()
        {
            string s = "<div class=\"controllers\" id=\"controllers\">";
            s += _form.header2("Backup Server");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("VB365 Version", "VB365 Version");
            s += _form.TableHeader("OS Version", "OS Version");
            s += _form.TableHeader("RAM", "RAM");
            s += _form.TableHeader("CPUs", "CPUs");
            s += _form.TableHeader("Proxies Managed", "Proxies Managed");
            s += _form.TableHeader("Repos Managed", "Repos Managed");
            s += _form.TableHeader("Orgs Managed", "Orgs Managed");
            s += _form.TableHeader("Jobs Managed", "Jobs Managed");
            s += _form.TableHeader("PowerShell Installed?", "PowerShell Installed?");
            s += _form.TableHeader("Proxy Installed?", "Proxy Installed?");
            s += _form.TableHeader("REST Installed?", "REST Installed?");
            s += _form.TableHeader("Console Installed?", "Console Installed?");
            s += _form.TableHeader("VM Name", "VM Name");
            s += _form.TableHeader("VM Location", "VM Location");
            s += _form.TableHeader("VM SKU", "VM SKU");
            s += _form.TableHeader("VM Size", "VM Size");



            s += "</tr>";

            var global = _csv.GetDynVboController().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ControllerDrivers()
        {
            string s = "<div class=\"controllerdrivers\" id=\"controllerdrivers\">";
            s += _form.header2("Backup Server Disks");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Friendly Name", "Friendly Name");
            s += _form.TableHeader("DeviceId", "DeviceId");
            s += _form.TableHeader("Bus Type", "Bus Type");
            s += _form.TableHeader("Media Type", "Media Type");
            s += _form.TableHeader("Manufacturer", "Manufacturer");
            s += _form.TableHeader("Model", "Model");
            s += _form.TableHeader("Size", "Size");
            s += _form.TableHeader("Allocated Size", "Allocated Size");
            s += _form.TableHeader("Operational Status", "Operational Status");
            s += _form.TableHeader("Health Status", "Health Status");
            s += _form.TableHeader("Boot Drive", "Boot Drive");

            s += "</tr>";

            var global = _csv.GetDynVboControllerDriver().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365JobSessions()
        {
            string s = "<div class=\"jobsessions\" id=\"jobsessions\">";
            s += _form.header2("Job Sessions");
            //s += "<br>";
            s += _form.CollapsibleButton("Show Job Sessions");

            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Status", "Status");
            s += _form.TableHeader("Start Time", "Start Time");
            s += _form.TableHeader("End Time", "End Time");
            s += _form.TableHeader("Duration", "Duration");
            s += _form.TableHeader("Log", "Log");

            s += "</tr>";

            var global = _csv.GetDynVboJobSess().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365JobStats()
        {
            string s = "<div class=\"jobstats\" id=\"jobstats\">";
            s += _form.header2("Job Statistics");
            //s += "<br>";
            s += _form.CollapsibleButton("Show Job Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Average Duration (min)", "Average Duration (min)");
            s += _form.TableHeader("Max Duration (min)", "Max Duration (min)");
            s += _form.TableHeader("Average Data Transferred", "Average Data Transferred");
            s += _form.TableHeader("Max Data Transferred", "Max Data Transferred");
            s += _form.TableHeader("Average Objects (#)", "Average Objects (#)");
            s += _form.TableHeader("Max Objects (#)", "Max Objects (#)");
            s += _form.TableHeader("Average Items (#)", "Average Items (#)");
            s += _form.TableHeader("Max Items (#)", "Max Items (#)");
            s += _form.TableHeader("Typical Bottleneck", "Typical Bottleneck");
            s += _form.TableHeader("Job Avg Throughput", "Job Avg Throughput");
            s += _form.TableHeader("Job Avg Processing Rate", "Job Avg Processing Rate");

            s += "</tr>";

            var global = _csv.GetDynVboJobSess().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ObjectRepos()
        {
            string s = "<div class=\"objrepos\" id=\"objrepos\">";
            s += _form.header2("Object Storage");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Description", "Description");
            s += _form.TableHeader("Cloud", "Cloud");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Bucket/Container", "Bucket/Container");
            s += _form.TableHeader("Path", "Path");
            s += _form.TableHeader("Size Limit", "Size Limit");
            s += _form.TableHeader("Used Space", "Used Space");
            s += _form.TableHeader("Free Space", "Free Space");

            s += "</tr>";

            var global = _csv.GetDynVboObjRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Orgs()
        {
            string s = "<div class=\"orgs\" id=\"orgs\">";
            s += _form.header2("Organizations");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Friendly Name", "Friendly Name");
            s += _form.TableHeader("Real Name", "Real Name");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Protected Apps", "Protected Apps");
            s += _form.TableHeader("EXO Settings", "EXO Settings");
            s += _form.TableHeader("EXO App Cert", "EXO App Cert");
            s += _form.TableHeader("SPO Settings", "SPO Settings");
            s += _form.TableHeader("SPO App Cert", "SPO App Cert");
            s += _form.TableHeader("On-Prem Exch Settings", "On-Prem Exch Settings");
            s += _form.TableHeader("On-Prem SP Settings", "On-Prem SP Settings");
            s += _form.TableHeader("Licensed Users", "Licensed Users");
            s += _form.TableHeader("Grant SC Admin", "Grant SC Admin");
            s += _form.TableHeader("Aux Accounts/Apps", "Aux Accounts/Apps");

            s += "</tr>";

            var global = _csv.GetDynVboOrg().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365Permissions()
        {
            string s = "<div class=\"perms\" id=\"perms\">";
            s += _form.header2("Permissions Check");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Organization", "Organization");
            s += _form.TableHeader("API", "API");
            s += _form.TableHeader("Permission", "Permission");

            s += "</tr>";

            var global = _csv.GetDynVboPerms().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";


            s += "</div>";
            return s;
        }
        public string Vb365ProtStat()
        {
            string s = "<div class=\"protstat\" id=\"protstat\">";
            s += _form.header2("Unprotected Users");
            //s += CollapsibleButton("Show Protection Statistics");
            //s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";
            //s += _form.TableHeader("User", "User");
            //s += _form.TableHeader("E-mail", "E-mail");
            //s += _form.TableHeader("Organization", "Organization");
            //s += _form.TableHeader("Protection Status", "Protection Status");
            //s += _form.TableHeader("Last Backup Date", "Last Backup Date");

            //s += "</tr>";

            s += "<tr>";
            s += _form.TableHeader("Unprotected Users", "");
            s += "</tr>";
            int counter = 0;
            var global = _csv.GetDynVboProtStat().ToList();
            foreach (var gl in global)
            {
                //s += "<tr>";

                foreach (var g in gl)
                    counter++;
                    //s += _form.TableData(g.Value, "");

                //s += "</tr>";
            }
            s += "<tr>";
            s += _form.TableData(counter.ToString(), "");
            s += "</tr>";

            


            s += "</table>";


            s += "</div>";
            return s;
        }
        private string SummaryTemplate()
        {
            string s = _form.CollapsibleButton(ResourceHandler.BkpSrvButton);

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvSummary1) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary2) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary3) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary4) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes1) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes2) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes3) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes4) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes5) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        private string GlobalSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvSummary1) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary2) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary3) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary4) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes1) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes2) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes3) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes4) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes5) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        #endregion





    }
}
