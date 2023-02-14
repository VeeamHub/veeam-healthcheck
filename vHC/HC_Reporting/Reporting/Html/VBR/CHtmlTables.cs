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
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Reporting.TableDatas;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Html.VBR
{
    internal class CHtmlTables
    {
        private CCsvParser _csv = new(CVariables.vb365dir);
        private readonly CLogger log = CGlobals.Logger;

        CDataFormer _df = new(true);
        Scrubber.CScrubHandler _scrub = CGlobals.Scrubber;

        private CHtmlFormatting _form = new();
        private CVbrSummaries _sum = new();


        public CHtmlTables()
        {

        }
        public string MakeNavTable()
        {
            return _form.FormNavRows(VbrLocalizationHelper.NavLicInfoLink, "license", VbrLocalizationHelper.NavLicInfoDetail) +
                _form.FormNavRows(VbrLocalizationHelper.NavBkpSrvLink, "vbrserver", VbrLocalizationHelper.NavBkpSrvDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSecSumLink, "secsummary", VbrLocalizationHelper.NavSecSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSrvSumLink, "serversummary", VbrLocalizationHelper.NavSrvSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobSumLink, "jobsummary", VbrLocalizationHelper.NavJobSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavMissingJobLink, "missingjobs", VbrLocalizationHelper.NavMissingDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavProtWrkld, "protectedworkloads", VbrLocalizationHelper.NavProtWkldDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSrvInfoLink, "managedServerInfo", VbrLocalizationHelper.NavSrvInfoDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavRegKeyLink, "regkeys", VbrLocalizationHelper.NavRegKeyDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavProxyInfoLink, "proxies", VbrLocalizationHelper.NavProxyDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSobrInfoLink, "sobr", VbrLocalizationHelper.NavSobrDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSobrExtLink, "extents", VbrLocalizationHelper.NavSobrExtDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavRepoInfoLink, "repos", VbrLocalizationHelper.NavRepoDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobConLink, "jobcon", VbrLocalizationHelper.NavJobConDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavTaskConLink, "taskcon", VbrLocalizationHelper.NavTaskConDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobSessSumLink, "jobsesssum", VbrLocalizationHelper.NavJobSessSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobInfoLink, "jobs", VbrLocalizationHelper.NavJobInfoDeet);
        }
        public string MakeSecurityNavTable()
        {
            return //_form.FormNavRows(ResourceHandler.NavLicInfoLink, "license", ResourceHandler.NavLicInfoDetail) +
                _form.FormNavRows(VbrLocalizationHelper.NavBkpSrvLink, "vbrserver", VbrLocalizationHelper.NavBkpSrvDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSecSumLink, "secsummary", VbrLocalizationHelper.NavSecSumDeet) +
                //_form.FormNavRows(ResourceHandler.NavSrvSumLink, "serversummary", ResourceHandler.NavSrvSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobSumLink, "jobsummary", VbrLocalizationHelper.NavJobSumDeet) +
                //_form.FormNavRows(ResourceHandler.NavMissingJobLink, "missingjobs", ResourceHandler.NavMissingDeet) +
                //_form.FormNavRows(ResourceHandler.NavProtWrkld, "protectedworkloads", ResourceHandler.NavProtWkldDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSrvInfoLink, "managedServerInfo", VbrLocalizationHelper.NavSrvInfoDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavRegKeyLink, "regkeys", VbrLocalizationHelper.NavRegKeyDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavProxyInfoLink, "proxies", VbrLocalizationHelper.NavProxyDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSobrInfoLink, "sobr", VbrLocalizationHelper.NavSobrDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavSobrExtLink, "extents", VbrLocalizationHelper.NavSobrExtDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavRepoInfoLink, "repos", VbrLocalizationHelper.NavRepoDeet) +
                //_form.FormNavRows(ResourceHandler.NavJobConLink, "jobcon", ResourceHandler.NavJobConDeet) +
                //_form.FormNavRows(ResourceHandler.NavTaskConLink, "taskcon", ResourceHandler.NavTaskConDeet) +
                //_form.FormNavRows(ResourceHandler.NavJobSessSumLink, "jobsesssum", ResourceHandler.NavJobSessSumDeet) +
                _form.FormNavRows(VbrLocalizationHelper.NavJobInfoLink, "jobs", VbrLocalizationHelper.NavJobInfoDeet);
        }


        public string LicTable(bool scrub)
        {
            string s = _form.SectionStart("license", VbrLocalizationHelper.LicTableHeader);
            string summary = _sum.LicSum();

            s += _form.TableHeader(VbrLocalizationHelper.LicTblLicTo, "") +
            _form.TableHeader(VbrLocalizationHelper.LicTblEdition, VbrLocalizationHelper.LtEdTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblStatus, VbrLocalizationHelper.LtStatusTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblType, VbrLocalizationHelper.LtTypeTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblLicInst, VbrLocalizationHelper.LtInstLicTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblUsedInst, VbrLocalizationHelper.LtInstUsedTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblNewInst, VbrLocalizationHelper.LtInstNewTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblRentInst, VbrLocalizationHelper.LtInstRentalTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblLicSock, VbrLocalizationHelper.LtSocLicTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblUsedSock, VbrLocalizationHelper.LtSocUsedTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblLicNas, VbrLocalizationHelper.LtNasLicTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblUsedNas, VbrLocalizationHelper.LtNasUsedTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblExpDate, VbrLocalizationHelper.LicExpTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblSupExpDate, VbrLocalizationHelper.LicSupExpTT) +
                _form.TableHeader(VbrLocalizationHelper.LicTblCc, VbrLocalizationHelper.LicCcTT) +
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
            string s = _form.SectionStart("vbrserver", VbrLocalizationHelper.BkpSrvTblHead);
            string summary = _sum.SetVbrSummary();
            CDataFormer cd = new(true);
            BackupServer b = cd.BackupServerInfoToXml(scrub);

            // Backup Server table
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblName, VbrLocalizationHelper.BstNameTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblVersion, VbrLocalizationHelper.BstVerTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCore, VbrLocalizationHelper.BstCpuTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblRam, VbrLocalizationHelper.BstRamTT);
            
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblProxyRole, VbrLocalizationHelper.BstPrxTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblRepoRole, VbrLocalizationHelper.BstRepTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblWanRole, VbrLocalizationHelper.BstWaTT);
            s += _form.TableHeader(VbrLocalizationHelper.BackupServerConsoleInstalled, "");
            s += _form.TableHeader(VbrLocalizationHelper.BackupServerRdpEnabled, "");
            s += _form.TableHeader(VbrLocalizationHelper.BackupServerDomainJoined, "");
            s += "</tr><<tr>";
            s += _form.TableData(b.Name, "");
            s += _form.TableData(b.Version, "");
            s += _form.TableData(b.Cores.ToString(), "");
            s += _form.TableData(b.RAM.ToString(), "");
            s += _form.TableData(b.HasProxyRole.ToString(), "");
            s += _form.TableData(b.HasRepoRole.ToString(), "");
            s += _form.TableData(b.HasWanAccRole.ToString(), "");
            s += _form.TableData(CGlobals.isConsoleLocal.ToString(), "");
            s += _form.TableData(CGlobals._isRdpEnabled.ToString(), "");
            s += _form.TableData(CGlobals._isDomainJoined.ToString(), "");
            s += "</table><br>";
            
            s += "<h3>Config Backup Info</h3>";
            s += "<table border=\"1\">";
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEnabled, VbrLocalizationHelper.BstCfgEnabledTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgLastRes, VbrLocalizationHelper.BstCfgLastResTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEncrypt, VbrLocalizationHelper.BstCfgEncTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblTarget, VbrLocalizationHelper.BstCfgTarTT);
            s += "<tr></tr>";
            s += _form.TableData(b.ConfigBackupEnabled.ToString(), "");
            s += _form.TableData(b.ConfigBackupLastResult, "");
            s += _form.TableData(b.ConfigBackupEncryption.ToString(), "");
            s += _form.TableData(b.ConfigBackupTarget, "");
            s += "</table>";

            s += "<h3>Config DB Info</h3>";
            s += "<table border=\"1\">";
            s += _form.LineBreak();
            // config DB Table
            s += _form.TableHeader("DataBase Type", "MS SQL or PostgreSQL");
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlLocal, VbrLocalizationHelper.BstSqlLocTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlName, VbrLocalizationHelper.BstSqlNameTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlVersion, VbrLocalizationHelper.BstSqlVerTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlEdition, VbrLocalizationHelper.BstSqlEdTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlCores, VbrLocalizationHelper.BstSqlCpuTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlRam, VbrLocalizationHelper.BstSqlRamTT);

            s += "</tr>";
            //CDataFormer cd = new(true);
            try
            {


                s += "<tr>";

                s += _form.TableData(b.DbType, "");
                s += _form.TableData(b.IsLocal.ToString(), "");
                s += _form.TableData(b.DbHostName, "");
                s += _form.TableData(b.DbVersion, "");
                s += _form.TableData(b.Edition, "");
                s += _form.TableData(b.DbCores.ToString(), "CPU Cores detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.");
                s += _form.TableData(b.DbRAM.ToString(), "RAM detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.");

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

            string s = _form.SectionStart("secsummary", VbrLocalizationHelper.SSTitle);
            string summary = _sum.SecSum();

            s += _form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr1, VbrLocalizationHelper.SSHdrTT1) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr2, VbrLocalizationHelper.SSHdrTT2) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr3, VbrLocalizationHelper.SSHdrTT3) +
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

            string s = _form.SectionStart("serversummary", VbrLocalizationHelper.MssTitle);

            s += _form.TableHeader(VbrLocalizationHelper.MssHdr1, VbrLocalizationHelper.MssHdr1TT) +
                            _form.TableHeader(VbrLocalizationHelper.MssHdr2, VbrLocalizationHelper.MssHdr2TT) +
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
            string s = _form.SectionStart("jobsummary", VbrLocalizationHelper.JobSumTitle);

            s += _form.TableHeader(VbrLocalizationHelper.JobSum0, VbrLocalizationHelper.JobSum0TT) +
                _form.TableHeader(VbrLocalizationHelper.JobSum1, VbrLocalizationHelper.JobSum1TT) +
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
            string s = _form.SectionStartWithButton("missingjobs", VbrLocalizationHelper.NpTitle, VbrLocalizationHelper.NpButton);


            string summary = _sum.MissingJobsSUmmary();

            s += _form.TableHeader(VbrLocalizationHelper.JobSum0, "") +
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
            string s = _form.SectionStartWithButton("protectedworkloads", VbrLocalizationHelper.PlTitle, VbrLocalizationHelper.PlButton);
            string summary = _sum.ProtectedWorkloads();
            try
            {
                _df.ProtectedWorkloadsToXml();


                // vi table
                s += "<h3>VMware Backups</h3>";
                s += _form.Table();
                s += "<tr>" +
                _form.TableHeader(VbrLocalizationHelper.PlHdr0, VbrLocalizationHelper.PlHdrTT0) +
                _form.TableHeader(VbrLocalizationHelper.PlHdr1, VbrLocalizationHelper.PlHdrTT1) +
                _form.TableHeader(VbrLocalizationHelper.PlHdr2, VbrLocalizationHelper.PlHdrTT2) +
                _form.TableHeader(VbrLocalizationHelper.PlHdr3, VbrLocalizationHelper.PlHdrTT3) +
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
                s += _form.TableHeader(VbrLocalizationHelper.PlHdr4, VbrLocalizationHelper.PlHdrTT4);
                s += _form.TableHeader(VbrLocalizationHelper.PlHdr5, VbrLocalizationHelper.PlHdrTT5);
                s += _form.TableHeader(VbrLocalizationHelper.PlHdr6, VbrLocalizationHelper.PlHdrTT6);
                s += _form.TableHeader(VbrLocalizationHelper.PlHdr7, VbrLocalizationHelper.PlHdrTT7);

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
            string s = _form.SectionStartWithButton("managedServerInfo", VbrLocalizationHelper.ManSrvTitle, VbrLocalizationHelper.ManSrvBtn);
            string summary = _sum.ManagedServers();
            s += "<tr>" +
           _form.TableHeader(VbrLocalizationHelper.ManSrv0, VbrLocalizationHelper.ManSrv0TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv1, VbrLocalizationHelper.ManSrv1TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv2, VbrLocalizationHelper.ManSrv2TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv3, VbrLocalizationHelper.ManSrv3TT) +
           _form.TableHeader("OS Info", VbrLocalizationHelper.ManSrv3TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv4, VbrLocalizationHelper.ManSrv4TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv5, VbrLocalizationHelper.ManSrv5TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv6, VbrLocalizationHelper.ManSrv6TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv7, VbrLocalizationHelper.ManSrv7TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv8, VbrLocalizationHelper.ManSrv8TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv9, VbrLocalizationHelper.ManSrv9TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv10, VbrLocalizationHelper.ManSrv10TT) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv11, VbrLocalizationHelper.ManSrv11TT) +
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
                    s += _form.TableData(d[1], ""); // name
                    s += _form.TableData(d[2], ""); // cores
                    s += _form.TableData(d[3], ""); // ram
                    s += _form.TableData(d[12], ""); // OS Info
                    s += _form.TableData(d[4], ""); // api version
                    s += _form.TableData(d[5], ""); // protected vms
                    s += _form.TableData(d[6], ""); // not protected vms
                    s += _form.TableData(d[7], ""); // total vms
                    s += _form.TableData(d[8], ""); // is proxy
                    s += _form.TableData(d[9], ""); // is repo
                    s += _form.TableData(d[10], ""); // is wan
                    s += _form.TableData(d[11], ""); // is unavailable
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
            string s = _form.SectionStartWithButton("regkeys", VbrLocalizationHelper.RegTitle, VbrLocalizationHelper.RegBtn);
            string summary = _sum.RegKeys();
            s += "<tr>" +
                _form.TableHeader(VbrLocalizationHelper.Reg0, VbrLocalizationHelper.Reg0TT) +
                _form.TableHeader(VbrLocalizationHelper.Reg1, VbrLocalizationHelper.Reg1TT) +
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
            string s = _form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = _sum.Proxies();
            s += "<tr>" +
           _form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT) +
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
        public string AddMultiRoleTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = _sum.Proxies();
            s += "<tr>" +
           _form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT) +
           _form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT) +
   "</tr>";
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
            catch (Exception e)
            {
                log.Error("PROXY Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddSobrTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("sobr", VbrLocalizationHelper.SbrTitle, VbrLocalizationHelper.SbrBtn);
            string summary = _sum.Sobr();
            s += "<tr>" +
           _form.TableHeader(VbrLocalizationHelper.Sbr0, VbrLocalizationHelper.Sbr0TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr1, VbrLocalizationHelper.Sbr1TT) +
           _form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo1TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr2, VbrLocalizationHelper.Sbr2TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr3, VbrLocalizationHelper.Sbr3TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr4, VbrLocalizationHelper.Sbr4TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr5, VbrLocalizationHelper.Sbr5TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr6, VbrLocalizationHelper.Sbr6TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr7, VbrLocalizationHelper.Sbr7TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr8, VbrLocalizationHelper.Sbr8TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr9, VbrLocalizationHelper.Sbr9TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr10, VbrLocalizationHelper.Sbr10TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr11, VbrLocalizationHelper.Sbr11TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr12, VbrLocalizationHelper.Sbr12TT) +
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
            string s = _form.SectionStartWithButton("extents", VbrLocalizationHelper.SbrExtTitle, VbrLocalizationHelper.SbrExtBtn);
            string summary = _sum.Extents();
            s += "<tr>" +
_form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt1, VbrLocalizationHelper.SbrExt1TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt5, VbrLocalizationHelper.SbrExt5TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt6, VbrLocalizationHelper.SbrExt6TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT) +
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
            string s = _form.SectionStartWithButton("repos", VbrLocalizationHelper.RepoTitle, VbrLocalizationHelper.RepoBtn);
            string summary = _sum.Repos();
            s += "<tr>" +
_form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT) +
_form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo0TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt5, VbrLocalizationHelper.SbrExt5TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt6, VbrLocalizationHelper.SbrExt6TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT) +
_form.TableHeader(VbrLocalizationHelper.Repo1, VbrLocalizationHelper.Repo1TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT) +
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
            string s = _form.SectionStartWithButton("jobcon", VbrLocalizationHelper.JobConTitle, VbrLocalizationHelper.JobConBtn, CGlobals.ReportDays);
            string summary = _sum.JobCon();
            s += _form.TableHeader(VbrLocalizationHelper.JobCon0, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon1, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon2, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon3, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon4, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon5, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon6, "");
            s += _form.TableHeader(VbrLocalizationHelper.JobCon7, "");
            s += "</tr>";

            try
            {


                var stuff = _df.JobConcurrency(true, CGlobals.ReportDays);

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
            string s = _form.SectionStartWithButton("taskcon", VbrLocalizationHelper.TaskConTitle, VbrLocalizationHelper.TaskConBtn, CGlobals.ReportDays);
            string summary = _sum.TaskCon();

            s += _form.TableHeader(VbrLocalizationHelper.TaskCon0, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon1, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon2, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon3, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon4, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon5, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon6, "");
            s += _form.TableHeader(VbrLocalizationHelper.TaskCon7, "");
            s += "</tr>";

            try
            {
                var stuff = _df.JobConcurrency(false, CGlobals.ReportDays);

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
            string s = _form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            string summary = _sum.JobSessSummary();

            s += _form.TableHeader(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss10, VbrLocalizationHelper.Jss10TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss11, VbrLocalizationHelper.Jss11TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss13, VbrLocalizationHelper.Jss13TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss14, VbrLocalizationHelper.Jss14TT);
            s += _form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT);

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
            string s = _form.SectionStartWithButton("jobs", VbrLocalizationHelper.JobInfoTitle, VbrLocalizationHelper.JobInfoBtn);
            string summary = _sum.JobInfo();

            s += _form.TableHeader(VbrLocalizationHelper.JobInfo0, VbrLocalizationHelper.JobInfo0TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo1, VbrLocalizationHelper.JobInfo1TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo2, VbrLocalizationHelper.JobInfo2TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo3, VbrLocalizationHelper.JobInfo3TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo4, VbrLocalizationHelper.JobInfo4TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo5, VbrLocalizationHelper.JobInfo5TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo6, VbrLocalizationHelper.JobInfo6TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo7, VbrLocalizationHelper.JobInfo7TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo8, VbrLocalizationHelper.JobInfo8TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo9, VbrLocalizationHelper.JobInfo9TT);
            //s += _form.TableHeader(ResourceHandler.JobInfo10, ResourceHandler.JobInfo10TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo11, VbrLocalizationHelper.JobInfo11TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo12, VbrLocalizationHelper.JobInfo12TT);
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo13, VbrLocalizationHelper.JobInfo13TT);

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
