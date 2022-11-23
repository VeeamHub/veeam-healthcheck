using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Reporting.Html;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html.VBR
{
    internal class CHtmlTables
    {
        private CCsvParser _csv = new(CVariables.vb365dir);
        private readonly CLogger log = VhcGui.log;

        CDataFormer _df = new(true);
        Scrubber.CScrubHandler _scrub = VhcGui._scrubberMain;

        private CHtmlFormatting _form = new();
        private CVbrSummaries _sum = new();


        public CHtmlTables()
        {

        }
        public string MakeNavTable()
        {
            return _form.FormNavRows(ResourceHandler.NavLicInfoLink, "license", ResourceHandler.NavLicInfoDetail) +
                _form.FormNavRows(ResourceHandler.NavBkpSrvLink, "vbrserver", ResourceHandler.NavBkpSrvDeet) +
                _form.FormNavRows(ResourceHandler.NavSecSumLink, "secsummary", ResourceHandler.NavSecSumDeet) +
                _form.FormNavRows(ResourceHandler.NavSrvSumLink, "serversummary", ResourceHandler.NavSrvSumDeet) +
                _form.FormNavRows(ResourceHandler.NavJobSumLink, "jobsummary", ResourceHandler.NavJobSumDeet) +
                _form.FormNavRows(ResourceHandler.NavMissingJobLink, "missingjobs", ResourceHandler.NavMissingDeet) +
                _form.FormNavRows(ResourceHandler.NavProtWrkld, "protectedworkloads", ResourceHandler.NavProtWkldDeet) +
                _form.FormNavRows(ResourceHandler.NavSrvInfoLink, "managedServerInfo", ResourceHandler.NavSrvInfoDeet) +
                _form.FormNavRows(ResourceHandler.NavRegKeyLink, "regkeys", ResourceHandler.NavRegKeyDeet) +
                _form.FormNavRows(ResourceHandler.NavProxyInfoLink, "proxies", ResourceHandler.NavProxyDeet) +
                _form.FormNavRows(ResourceHandler.NavSobrInfoLink, "sobr", ResourceHandler.NavSobrDeet) +
                _form.FormNavRows(ResourceHandler.NavSobrExtLink, "extents", ResourceHandler.NavSobrExtDeet) +
                _form.FormNavRows(ResourceHandler.NavRepoInfoLink, "repos", ResourceHandler.NavRepoDeet) +
                _form.FormNavRows(ResourceHandler.NavJobConLink, "jobcon", ResourceHandler.NavJobConDeet) +
                _form.FormNavRows(ResourceHandler.NavTaskConLink, "taskcon", ResourceHandler.NavTaskConDeet) +
                _form.FormNavRows(ResourceHandler.NavJobSessSumLink, "jobsesssum", ResourceHandler.NavJobSessSumDeet) +
                _form.FormNavRows(ResourceHandler.NavJobInfoLink, "jobs", ResourceHandler.NavJobInfoDeet);
        }


        public string LicTable(bool scrub)
        {
            string s = _form.SectionStart("license", ResourceHandler.LicTableHeader);
            string summary = _sum.LicSum();

            s += _form.TableHeader(ResourceHandler.LicTblLicTo, "") +
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

            try
            {
                CCsvParser csv = new();
                var lic = csv.GetDynamicLicenseCsv();


                foreach (var l in lic)
                {


                    s += "<tr>";
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(l.licensedto), "");
                    if (!scrub)
                        s += _form.TableData(l.licensedto, "");
                    s += _form.TableData(l.edition, "");
                    s += _form.TableData(l.status, "");
                    s += _form.TableData(l.type, "");
                    s += _form.TableData(l.licensedinstances, "");
                    s += _form.TableData(l.usedinstances, "");
                    s += _form.TableData(l.newinstances, "");
                    s += _form.TableData(l.rentalinstances, "");
                    s += _form.TableData(l.licensedsockets, "");
                    s += _form.TableData(l.usedsockets, "");
                    s += _form.TableData(l.licensedcapacitytb, "");
                    s += _form.TableData(l.usedcapacitytb, "");
                    s += _form.TableData(l.expirationdate, "");
                    s += _form.TableData(l.supportexpirationdate, "");
                    s += _form.TableData(l.cloudconnect, "");
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("License Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddBkpSrvTable(bool scrub)
        {
            string s = _form.SectionStart("vbrserver", ResourceHandler.BkpSrvTblHead);
            string summary = _sum.SetVbrSummary();

            s += _form.TableHeader(ResourceHandler.BkpSrvTblName, ResourceHandler.BstNameTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblVersion, ResourceHandler.BstVerTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblCore, ResourceHandler.BstCpuTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblRam, ResourceHandler.BstRamTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblCfgEnabled, ResourceHandler.BstCfgEnabledTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblCfgLastRes, ResourceHandler.BstCfgLastResTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblCfgEncrypt, ResourceHandler.BstCfgEncTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblTarget, ResourceHandler.BstCfgTarTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlLocal, ResourceHandler.BstSqlLocTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlName, ResourceHandler.BstSqlNameTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlVersion, ResourceHandler.BstSqlVerTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlEdition, ResourceHandler.BstSqlEdTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlCores, ResourceHandler.BstSqlCpuTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblSqlRam, ResourceHandler.BstSqlRamTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblProxyRole, ResourceHandler.BstPrxTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblRepoRole, ResourceHandler.BstRepTT);
            s += _form.TableHeader(ResourceHandler.BkpSrvTblWanRole, ResourceHandler.BstWaTT);
            s += "</tr>";
            //CDataFormer cd = new(true);
            try
            {


                List<string> list = _df.BackupServerInfoToXml(scrub);
                s += "<tr>";

                for (int i = 0; i < list.Count(); i++)
                {

                    //if(MainWindow._scrub && i == 0 || i == 7 || i == 9)
                    //{
                    //    s += _form.TableData(_scrub.ScrubItem(list[i]), "");
                    //}
                    //else
                    s += _form.TableData(list[i], "");

                }
                s += _form.SectionEnd(summary);
                return s;
            }
            catch(Exception e)
            {
                log.Error("Failed to add backup server table. Error:");
                log.Error("\t" + e.Message);
                return "";
            }
        }

        public string AddSecSummaryTable(bool scrub)
        {

            string s = _form.SectionStart("secsummary", ResourceHandler.SSTitle);
            string summary = _sum.SecSum();

            s += _form.TableHeader(ResourceHandler.SSHdr0, ResourceHandler.SSHdrTT0) +
                _form.TableHeader(ResourceHandler.SSHdr1, ResourceHandler.SSHdrTT1) +
                _form.TableHeader(ResourceHandler.SSHdr2, ResourceHandler.SSHdrTT2) +
                _form.TableHeader(ResourceHandler.SSHdr3, ResourceHandler.SSHdrTT3) +
                "</tr>" +
                "<tr>";

            try
            {
                //table data
                List<int> list = _df.SecSummary();

                for (int i = 0; i < list.Count(); i++)
                {
                    if (list[i] == 0)
                        s += _form.TableData("False", "", 1);
                    else if (list[i] == 1)
                        s += _form.TableData("True", "");
                }
                s += "</tr>";
            }
            catch(Exception e)
            {
                log.Error("Security Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);

            return s;
        }
        public string AddSrvSummaryTable(bool scrub)
        {
            string summary = _sum.SrvSum();

            string s = _form.SectionStart("serversummary", ResourceHandler.MssTitle);

            s += _form.TableHeader(ResourceHandler.MssHdr1, ResourceHandler.MssHdr1TT) +
                            _form.TableHeader(ResourceHandler.MssHdr2, ResourceHandler.MssHdr2TT) +
                            "</tr>";
            try
            {
                Dictionary<string, int> list = _df.ServerSummaryToXml();

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += _form.TableData(d.Key, "");
                    s += _form.TableData(d.Value.ToString(), "");
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("Server Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddJobSummaryTable(bool scrub)
        {
            string summary = _sum.JobSummary();
            string s = _form.SectionStart("jobsummary", ResourceHandler.JobSumTitle);

            s += _form.TableHeader(ResourceHandler.JobSum0, ResourceHandler.JobSum0TT) +
                _form.TableHeader(ResourceHandler.JobSum1, ResourceHandler.JobSum1TT) +
                "</tr>";

            try
            {
                Dictionary<string, int> list = _df.JobSummaryInfoToXml();

                int totalJobs = 0;
                foreach (var c in list)
                {
                    totalJobs += c.Value;
                }



                foreach (var d in list)
                {
                    s += "<tr>";
                    s += _form.TableData(d.Key, "");
                    s += _form.TableData(d.Value.ToString(), "");
                    s += "</tr>";
                }

                s += "<tr>";
                s += _form.TableData("<b>Total Jobs", "");
                s += _form.TableData(totalJobs.ToString() + "</b>", "");
                s += "</tr>";
            }
            catch(Exception e)
            {
                log.Error("Job Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }

        public string AddMissingJobsTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("missingjobs", ResourceHandler.NpTitle, ResourceHandler.NpButton);


            string summary = _sum.MissingJobsSUmmary();

            s += _form.TableHeader(ResourceHandler.JobSum0, "") +
                //_form.TableHeader("Count", "Total detected of this type") +
                "</tr>";
            //CDataFormer cd = new(true);
            try
            {
                List<string> list = _df.ParseNonProtectedTypes();

                for (int i = 0; i < list.Count(); i++)
                {
                    s += "<tr>";

                    s += _form.TableData(list[i], "");
                    s += "</tr>";

                }
            }
            catch(Exception e)
            {
                log.Error("Missing Jobs Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddProtectedWorkLoadsTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("protectedworkloads", ResourceHandler.PlTitle, ResourceHandler.PlButton);
            string summary = _sum.ProtectedWorkloads();
            try
            {
                _df.ProtectedWorkloadsToXml();


                // vi table
                s += "<h3>VMware Backups</h3>";
                s += _form.Table();
                s += "<tr>" +
                _form.TableHeader(ResourceHandler.PlHdr0, ResourceHandler.PlHdrTT0) +
                _form.TableHeader(ResourceHandler.PlHdr1, ResourceHandler.PlHdrTT1) +
                _form.TableHeader(ResourceHandler.PlHdr2, ResourceHandler.PlHdrTT2) +
                _form.TableHeader(ResourceHandler.PlHdr3, ResourceHandler.PlHdrTT3) +
                "</tr><tr>";
                s += _form.TableData((_df._viProtectedNames.Distinct().Count() + _df._viNotProtectedNames.Distinct().Count()).ToString(), "");
                s += _form.TableData(_df._viProtectedNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._viNotProtectedNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._viDupes.ToString(), "");
                s += "</tr>";
                s += "</table>";


                //hv 
                s += "<h3>HV Backups</h3>";
                s += _form.Table();
                // hv table
                s += "<tr>";
                s += _form.TableHeader("HV Total", "Total HV VMs found in environment");
                s += _form.TableHeader("HV Protected", "Total HV VMs found with existing backup");
                s += _form.TableHeader("HV Unprotected", "Total HV VMs found without backup");
                s += _form.TableHeader("HV Duplicates", "Total HV VMs potentially found in multiple backups");
                s += "</tr>";
                s += "<tr>";
                s += _form.TableData((_df._hvProtectedNames.Distinct().Count() + _df._hvNotProtectedNames.Distinct().Count()).ToString(), "");
                s += _form.TableData(_df._hvProtectedNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._hvNotProtectedNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._hvDupes.ToString(), "");
                s += "</tr></table>";


                // phys
                s += "<h3>Physical Backups</h3>";
                s += _form.Table();
                s += "<tr>";
                s += _form.TableHeader(ResourceHandler.PlHdr4, ResourceHandler.PlHdrTT4);
                s += _form.TableHeader(ResourceHandler.PlHdr5, ResourceHandler.PlHdrTT5);
                s += _form.TableHeader(ResourceHandler.PlHdr6, ResourceHandler.PlHdrTT6);
                s += _form.TableHeader(ResourceHandler.PlHdr7, ResourceHandler.PlHdrTT7);

                s += "</tr>";
                //CDataFormer cd = new(true);
                s += "<tr>";
                s += _form.TableData(_df._vmProtectedByPhys.Distinct().Count().ToString(), "");
                s += _form.TableData((_df._physNotProtNames.Distinct().Count() + _df._physProtNames.Distinct().Count()).ToString(), "");
                s += _form.TableData(_df._physProtNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._physNotProtNames.Distinct().Count().ToString(), "");

                s += "</tr>";
            }
            catch(Exception e)
            {
                log.Error("Protected Servers Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddManagedServersTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("managedServerInfo", ResourceHandler.ManSrvTitle, ResourceHandler.ManSrvBtn);
            string summary = _sum.ManagedServers();
            s += "<tr>" +
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
            //CDataFormer cd = new(true);
            try
            {
                List<string[]> list = _df.ServerXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";
                    //if (scrub)
                    //    s += _form.TableData(_scrub.ScrubItem(d[0]), "");
                    //else
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
            }
            catch(Exception e)
            {
                log.Error("Managed Server Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddRegKeysTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("regkeys", ResourceHandler.RegTitle, ResourceHandler.RegBtn);
            string summary = _sum.RegKeys();
            s += "<tr>" +
                _form.TableHeader(ResourceHandler.Reg0, ResourceHandler.Reg0TT) +
                _form.TableHeader(ResourceHandler.Reg1, ResourceHandler.Reg1TT) +
                "</tr>";
            //CDataFormer cd = new(true);
            try
            {
                Dictionary<string, string> list = _df.RegOptions();

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += _form.TableData(d.Key, "");
                    s += _form.TableData(d.Value.ToString(), "");
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("Registry Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddProxyTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("proxies", ResourceHandler.PrxTitle, ResourceHandler.PrxBtn);
            string summary = _sum.Proxies();
            s += "<tr>" +
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
            //CDataFormer cd = new(true);
            try
            {
                List<string[]> list = _df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(d[0]), "");
                    else
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
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(d[10]), "");
                    else
                        s += _form.TableData(d[10], "");
                    s += _form.TableData(d[11], "");
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("PROXY Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddSobrTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("sobr", ResourceHandler.SbrTitle, ResourceHandler.SbrBtn);
            string summary = _sum.Sobr();
            s += "<tr>" +
           _form.TableHeader(ResourceHandler.Sbr0, ResourceHandler.Sbr0TT) +
           _form.TableHeader(ResourceHandler.Sbr1, ResourceHandler.Sbr1TT) +
           _form.TableHeader(ResourceHandler.Repo0, ResourceHandler.Repo1TT) +
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
            //CDataFormer cd = new(true);

            try
            {

            List<string[]> list = _df.SobrInfoToXml(scrub);

            foreach (var d in list)
            {
                int perVmShade = 0;
                if (d[8] == "False")
                    perVmShade = 3;

                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[2], "");
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "", perVmShade);
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "");
                s += _form.TableData(d[11], "");
                s += _form.TableData(d[12], "");
                s += _form.TableData(d[13], "");
                s += "</tr>";
            }
            }
            catch(Exception e)
            {
                log.Error("SOBR Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddSobrExtTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("extents", ResourceHandler.SbrExtTitle, ResourceHandler.SbrExtBtn);
            string summary = _sum.Extents();
            s += "<tr>" +
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
            try
            {

            List<string[]> list = _df.ExtentXmlFromCsv(scrub);

            foreach (var d in list)
            {
                var prov = d[16];
                int shade = 0;
                if (prov == "under")
                    shade = 2;
                if (prov == "over")
                    shade = 1;

                int freeSpaceShade = 0;
                decimal.TryParse(d[10], out decimal i);
                if (i < 20) { freeSpaceShade = 1; }



                s += "<tr>";
                s += _form.TableData(d[0], "");
                s += _form.TableData(d[1], "");
                s += _form.TableData(d[2], "", shade);
                s += _form.TableData(d[3], "");
                s += _form.TableData(d[4], "");
                s += _form.TableData(d[5], "");
                s += _form.TableData(d[6], "");
                s += _form.TableData(d[7], "");
                s += _form.TableData(d[8], "");
                s += _form.TableData(d[9], "");
                s += _form.TableData(d[10], "", freeSpaceShade);
                s += _form.TableData(d[11], "");
                s += _form.TableData(d[12], "");
                s += _form.TableData(d[13], "");
                s += _form.TableData(d[14], "");
                s += _form.TableData(d[15], "");
                //s += _form.TableData(d[16], "");
                s += "</tr>";
            }
            }
            catch(Exception e)
            {
                log.Error("Extents Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddRepoTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("repos", ResourceHandler.RepoTitle, ResourceHandler.RepoBtn);
            string summary = _sum.Repos();
            s += "<tr>" +
_form.TableHeader(ResourceHandler.SbrExt0, ResourceHandler.SbrExt0TT) +
_form.TableHeader(ResourceHandler.Repo0, ResourceHandler.Repo0TT) +
_form.TableHeader(ResourceHandler.SbrExt2, ResourceHandler.SbrExt2TT) +
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
            try
            {
                List<string[]> list = _df.RepoInfoToXml(scrub);

                foreach (var d in list)
                {
                    var prov = d[17];
                    int shade = 0;
                    if (prov == "under")
                        shade = 2;
                    if (prov == "over")
                        shade = 1;
                    int freeSpaceShade = 0;
                    decimal.TryParse(d[10], out decimal i);

                    int perVmShade = 0;
                    if (d[11] == "False")
                        perVmShade = 3;

                    if (i < 20) { freeSpaceShade = 1; }
                    s += "<tr>";
                    s += _form.TableData(d[0], "");
                    s += _form.TableData(d[2], "");
                    s += _form.TableData(d[1], "", shade);
                    s += _form.TableData(d[3], "");
                    s += _form.TableData(d[4], "");
                    s += _form.TableData(d[5], "");
                    s += _form.TableData(d[6], "");
                    s += _form.TableData(d[7], "");
                    s += _form.TableData(d[8], "");
                    s += _form.TableData(d[9], "");
                    s += _form.TableData(d[10], "", freeSpaceShade);
                    s += _form.TableData(d[11], "", perVmShade);
                    s += _form.TableData(d[12], "");
                    s += _form.TableData(d[13], "");
                    s += _form.TableData(d[14], "");
                    s += _form.TableData(d[15], "");
                    s += _form.TableData(d[16], "");
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("REPO Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddJobConTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("jobcon", ResourceHandler.JobConTitle, ResourceHandler.JobConBtn, VhcGui._reportDays);
            string summary = _sum.JobCon();
            s += _form.TableHeader(ResourceHandler.JobCon0, "");
            s += _form.TableHeader(ResourceHandler.JobCon1, "");
            s += _form.TableHeader(ResourceHandler.JobCon2, "");
            s += _form.TableHeader(ResourceHandler.JobCon3, "");
            s += _form.TableHeader(ResourceHandler.JobCon4, "");
            s += _form.TableHeader(ResourceHandler.JobCon5, "");
            s += _form.TableHeader(ResourceHandler.JobCon6, "");
            s += _form.TableHeader(ResourceHandler.JobCon7, "");
            s += "</tr>";

            try
            {


                var stuff = _df.JobConcurrency(true, VhcGui._reportDays);

                foreach (var stu in stuff)
                {
                    s += "<tr>";
                    s += _form.TableData(stu.Key.ToString(), "");

                    foreach (var st in stu.Value)
                    {
                        s += _form.TableData(st, "");
                    }
                    s += "</tr>";
                }

            }
            catch(Exception e)
            {
                log.Error("JOB CONCURRENCY Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddTaskConTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("taskcon", ResourceHandler.TaskConTitle, ResourceHandler.TaskConBtn, VhcGui._reportDays);
            string summary = _sum.TaskCon();

            s += _form.TableHeader(ResourceHandler.TaskCon0, "");
            s += _form.TableHeader(ResourceHandler.TaskCon1, "");
            s += _form.TableHeader(ResourceHandler.TaskCon2, "");
            s += _form.TableHeader(ResourceHandler.TaskCon3, "");
            s += _form.TableHeader(ResourceHandler.TaskCon4, "");
            s += _form.TableHeader(ResourceHandler.TaskCon5, "");
            s += _form.TableHeader(ResourceHandler.TaskCon6, "");
            s += _form.TableHeader(ResourceHandler.TaskCon7, "");
            s += "</tr>";

            try
            {
                var stuff = _df.JobConcurrency(false, VhcGui._reportDays);

                foreach (var stu in stuff)
                {
                    s += "<tr>";
                    s += _form.TableData(stu.Key.ToString(), "");

                    foreach (var st in stu.Value)
                    {
                        s += _form.TableData(st, "");
                    }
                    s += "</tr>";
                }
            }
            catch(Exception e)
            {
                log.Error("Task Concurrency Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }


            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddJobSessSummTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("jobsesssum", ResourceHandler.JssTitle, ResourceHandler.JssBtn, VhcGui._reportDays);
            string summary = _sum.JobSessSummary();

            s += _form.TableHeader(ResourceHandler.Jss0, ResourceHandler.Jss0TT);
            s += _form.TableHeader(ResourceHandler.Jss1, ResourceHandler.Jss1TT);
            s += _form.TableHeader(ResourceHandler.Jss2, ResourceHandler.Jss2TT);
            s += _form.TableHeader(ResourceHandler.Jss3, ResourceHandler.Jss3TT);
            s += _form.TableHeader(ResourceHandler.Jss4, ResourceHandler.Jss4TT);
            s += _form.TableHeader(ResourceHandler.Jss5, ResourceHandler.Jss5TT);
            s += _form.TableHeader(ResourceHandler.Jss6, ResourceHandler.Jss6TT);
            s += _form.TableHeader(ResourceHandler.Jss7, ResourceHandler.Jss7TT);
            s += _form.TableHeader(ResourceHandler.Jss8, ResourceHandler.Jss8TT);
            s += _form.TableHeader(ResourceHandler.Jss9, ResourceHandler.Jss9TT);
            s += _form.TableHeader(ResourceHandler.Jss10, ResourceHandler.Jss10TT);
            s += _form.TableHeader(ResourceHandler.Jss11, ResourceHandler.Jss11TT);
            s += _form.TableHeader(ResourceHandler.Jss12, ResourceHandler.Jss12TT);
            s += _form.TableHeader(ResourceHandler.Jss13, ResourceHandler.Jss13TT);
            s += _form.TableHeader(ResourceHandler.Jss14, ResourceHandler.Jss14TT);
            s += _form.TableHeader(ResourceHandler.Jss15, ResourceHandler.Jss15TT);

            try
            {
                var stuff = _df.ConvertJobSessSummaryToXml(scrub);

                foreach (var stu in stuff)
                {
                    s += "<tr>";

                    foreach (var st in stu)
                    {
                        s += _form.TableData(st, "");
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Job Session Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddJobInfoTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("jobs", ResourceHandler.JobInfoTitle, ResourceHandler.JobInfoBtn);
            string summary = _sum.JobInfo();

            s += _form.TableHeader(ResourceHandler.JobInfo0, ResourceHandler.JobInfo0TT);
            s += _form.TableHeader(ResourceHandler.JobInfo1, ResourceHandler.JobInfo1TT);
            s += _form.TableHeader(ResourceHandler.JobInfo2, ResourceHandler.JobInfo2TT);
            s += _form.TableHeader(ResourceHandler.JobInfo3, ResourceHandler.JobInfo3TT);
            s += _form.TableHeader(ResourceHandler.JobInfo4, ResourceHandler.JobInfo4TT);
            s += _form.TableHeader(ResourceHandler.JobInfo5, ResourceHandler.JobInfo5TT);
            s += _form.TableHeader(ResourceHandler.JobInfo6, ResourceHandler.JobInfo6TT);
            s += _form.TableHeader(ResourceHandler.JobInfo7, ResourceHandler.JobInfo7TT);
            s += _form.TableHeader(ResourceHandler.JobInfo8, ResourceHandler.JobInfo8TT);
            s += _form.TableHeader(ResourceHandler.JobInfo9, ResourceHandler.JobInfo9TT);
            //s += _form.TableHeader(ResourceHandler.JobInfo10, ResourceHandler.JobInfo10TT);
            s += _form.TableHeader(ResourceHandler.JobInfo11, ResourceHandler.JobInfo11TT);
            s += _form.TableHeader(ResourceHandler.JobInfo12, ResourceHandler.JobInfo12TT);
            s += _form.TableHeader(ResourceHandler.JobInfo13, ResourceHandler.JobInfo13TT);

            try
            {
                var stuff = _df.JobInfoToXml(scrub);

                foreach (var stu in stuff)
                {
                    s += "<tr>";

                    foreach (var st in stu)
                    {
                        s += _form.TableData(st, "");
                    }
                }
            }
            catch(Exception e)
            {
                log.Error("SOBR Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public void AddSessionsFiles(bool scrub)
        {
            _df.JobSessionInfoToXml(scrub);
        }

    }
}
