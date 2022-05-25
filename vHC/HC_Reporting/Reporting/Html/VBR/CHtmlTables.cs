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
        

        #endregion





    }
}
