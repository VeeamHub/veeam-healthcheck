// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using System.Text.Json;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using System.Management.Automation;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables;
using VeeamHealthCheck.Scrubber;


namespace VeeamHealthCheck.Html.VBR
{
    internal class CHtmlTables
    {
        private CCsvParser _csv = new(CVariables.vb365dir);
        private readonly CLogger log = CGlobals.Logger;
        private CHtmlTablesHelper _helper = new();

        CDataFormer _df = new();
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
            s += "</thead><tbody>";
            try
            {
                CCsvParser csv = new();
                var lic = csv.GetDynamicLicenseCsv();


                foreach (var l in lic)
                {


                    s += "<tr>";
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(l.licensedto, ScrubItemType.Item), "");
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
            catch (Exception e)
            {
                log.Error("License Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            return s;
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
            s += "</tr></thead><tr>";
            s += data;
            s += "</table><br>";

            return s;
        }

        private string AddBackupServerDetails(BackupServer b)
        {
            CVbrServerTable t = new(b);
            List<Tuple<string, string>> vbrServerData = new(); //t.VbrFullTables();

            if (!CGlobals.RunSecReport)
                vbrServerData = t.VbrFullTables();
            else
                vbrServerData = t.VbrSecurityTables();

            return WriteTupleListToHtml(vbrServerData);
        }
        private string ConfigDbTable(BackupServer b)
        {
            string s = "";
            s += _form.header3("Config DB Info");
            s += _form.Table();
            // config DB Table
            s += _form.TableHeader("DataBase Type", "MS SQL or PostgreSQL");
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlLocal, VbrLocalizationHelper.BstSqlLocTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlName, VbrLocalizationHelper.BstSqlNameTT);
            if (b.DbType == CGlobals.SqlTypeName)
            {
                s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlVersion, VbrLocalizationHelper.BstSqlVerTT);
                s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlEdition, VbrLocalizationHelper.BstSqlEdTT);
            }

            if (b.DbType == CGlobals.SqlTypeName && b.IsLocal == false)
            {
                s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlCores, VbrLocalizationHelper.BstSqlCpuTT);
                s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlRam, VbrLocalizationHelper.BstSqlRamTT);
            }


            s += _form.TableHeaderEnd();
            //CDataFormer cd = new(true);
            try
            {


                s += _form.TableBodyStart();
                s += "<tr>";
                s += _form.TableData(b.DbType, "");
                if (b.IsLocal)
                {
                    s += _form.TableData(_form.True, "");
                }
                else
                {
                    s += _form.TableData(_form.False, "");
                }

                s += _form.TableData(b.DbHostName, "");
                if (b.DbType == CGlobals.SqlTypeName)
                {
                    s += _form.TableData(b.DbVersion, "");
                    s += _form.TableData(b.Edition, "");
                }

                if (b.DbType == CGlobals.SqlTypeName && b.IsLocal == false)
                    s += AddDbCoresRam(b);
                s += _form.EndTable();
            }
            catch (Exception e)
            {
                log.Error("Failed to add backup server table. Error:");
                log.Error("\t" + e.Message);
                //return "";
            }
            return s;
        }
        private string AddDbCoresRam(BackupServer b)
        {
            string s = "";
            string dbCoresToolTip = "CPU Cores detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.";
            string dbRamToolTip = "RAM detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.";
            if (b.DbCores == 0)
                s += _form.TableData("", dbCoresToolTip);
            else
                s += _form.TableData(b.DbCores.ToString(), dbCoresToolTip);
            if (b.DbRAM == 0)
                s += _form.TableData("", dbRamToolTip);
            else
                s += _form.TableData(b.DbRAM.ToString(), dbRamToolTip);

            return s;
        }
        public string AddBkpSrvTable(bool scrub)
        {
            string s = _form.SectionStart("vbrserver", VbrLocalizationHelper.BkpSrvTblHead);
            string summary = _sum.SetVbrSummary();
            //CDataFormer cd = new(true);
            BackupServer b = _df.BackupServerInfoToXml(scrub);
            if (String.IsNullOrEmpty(b.Version))
                b.Version = CGlobals.VBRFULLVERSION;
            // test area

            s += AddBackupServerDetails(b);
            s += _form.EndTable();



            s += _form.header3("Config Backup Info");
            s += "<table border=\"1\" class=\"content-table\">";
            s += "<tr>";
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEnabled, VbrLocalizationHelper.BstCfgEnabledTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgLastRes, VbrLocalizationHelper.BstCfgLastResTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEncrypt, VbrLocalizationHelper.BstCfgEncTT);
            s += _form.TableHeader(VbrLocalizationHelper.BkpSrvTblTarget, VbrLocalizationHelper.BstCfgTarTT);
            s += "</tr>";
            s += _form.TableBodyStart();
            s += "<tr>";
            if (b.ConfigBackupEnabled)
            {
                s += _form.TableData(_form.True, "");
            }
            else
            {
                s += _form.TableData(_form.False, "");
            }

            s += _form.TableData(b.ConfigBackupLastResult, "");
            if (b.ConfigBackupEncryption)
            {
                s += _form.TableData(_form.True, "");
            }
            else
            {
                s += _form.TableData(_form.False, "");
            }
            s += _form.TableData(b.ConfigBackupTarget, "");
            s += "</tr>";
            s += _form.EndTable();


            //s += _form.LineBreak();

            s += ConfigDbTable(b);

            //if (CGlobals.RunSecReport)
            //    s += InstalledAppsTable();
            s += _form.SectionEnd(summary);
            return s;

        }
        private string InstalledAppsTable()
        {
            string s = "";

            // Table & Header
            //s += _form.header3("Installed Applications");
            s += _form.Table();
            s += "<tr>";

            // header
            s += _form.TableHeader("Installed Apps", "");
            s += _form.TableHeaderEnd();
            s += _form.TableData("See <a href=\"C:\\\\temp\\\\vHC\\\\Original\\\\Log\\\\\">Veeam.HealthCheck.ServerApplications log file</a> at C:\\temp\\vHC\\Log\\", "");
            s += _form.EndTable();
            return s;
        }

        public string AddSecSummaryTable(bool scrub)
        {
            CGlobals.Scrub = scrub;

            string s = _form.SectionStart("secsummary", VbrLocalizationHelper.SSTitle);
            string summary = _sum.SecSum();

            s += _form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr1, VbrLocalizationHelper.SSHdrTT1) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr2, VbrLocalizationHelper.SSHdrTT2) +
                _form.TableHeader(VbrLocalizationHelper.SSHdr3, VbrLocalizationHelper.SSHdrTT3) +
                _form.TableHeader("MFA Enabled", "Is MFA enabled on VBR");
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            s += "<tr>";

            try
            {
                //table data
                CSecuritySummaryTable t = _df.SecSummary();

                if (t.ImmutabilityEnabled)
                    s += _form.TableData(_form.True, "");
                else
                    s += _form.TableData(_form.False, "");

                if (t.TrafficEncrptionEnabled)
                    s += _form.TableData(_form.True, "");
                else
                    s += _form.TableData(_form.False, "");
                if (t.BackupFileEncrptionEnabled)
                    s += _form.TableData(_form.True, "");
                else
                    s += _form.TableData(_form.False, "");

                if (t.ConfigBackupEncrptionEnabled)
                    s += _form.TableData(_form.True, "");
                else
                    s += _form.TableData(_form.False, "");

                if (t.MFAEnabled)
                    s += _form.TableData(_form.True, "");
                else
                    s += _form.TableData(_form.False, "");
                s += "</tr>";
                //s += _form.TableData((t.ImmutabilityEnabled : _form.True ? _form.False), "");
                s += _form.EndTable();
            }

            catch (Exception e)
            {
                log.Error("Security Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            if (CGlobals.VBRMAJORVERSION > 11)
            {
                // add malware table
                try
                {
                    var malware = new CMalwareTable();
                    s += malware.MalwareSettingsTable();

                    s += malware.MalwareExclusionsTable();
                    s += malware.MalwareEventsTable();
                    s += malware.MalwareInfectedObjectsTable();

                }
                catch (Exception e)
                {
                    log.Error("Malware Settings Data import failed. ERROR:");
                    log.Error("\t" + e.Message);
                }
                try
                {
                    // compliance table
                    CComplianceTable c = new();
                    s += c.ComplianceSummaryTable();
                    s += c.ComplianceTable();
                }
                catch (Exception e)
                {
                    log.Error("Security Compliance Data import failed. ERROR:");
                    log.Error("\t" + e.Message);
                }
            }




            s += _form.SectionEnd(summary);

            return s;
        }
        public string AddSecurityReportSecuritySummaryTable()
        {
            string s = _form.SectionStart("secsummary", VbrLocalizationHelper.SSTitle);
            string summary = _sum.SecSum();

            //s += AddSecuritySummaryDetails();

            // Tables!

            /* 1. components
             * 2. repositories
             * 3. accounts & perms
             * 4. encryption
             * 5. other
             * 6. NAS Specific
             * 7. Orchestrated recovery
             */

            //1. Components
            s += AddTable("Backup Server", _helper.AddSecurityServerInfo());
            s += AddTable("Immutability & Encryption", _helper.AddSecuritySummaryDetails());

            s += AddTable("Immutability", "");
            s += AddTable("Config Backup", _helper.AddConfigBackupDetails());

            try
            {
                s += AddTable("Detected OS", _helper.CollectedOsInfo());

            }
            catch (Exception e)
            {
                log.Error("Failed to add OS info to HTML report", false);
                log.Error(e.Message, false);
            }
            s += AddTable("Installed Applications", InstalledAppsTable());



            //s += _form.Table();
            //s += _form.TableHeader("Found Operating Systems", "");
            //s += "</tr><tr>";
            //foreach(var v in _helper.CollectedOsInfo())
            //{
            //    s += "<tr>" + v + "</tr>";
            //}
            //s += _form.EndTable();
            s += _form.SectionEnd(summary);



            return s;
        }
        private string AddTable(string title, string data)
        {
            string s = "";
            s += _form.header3(title);
            s += _form.Table();
            s += data;
            s += _form.EndTable();

            return s;
        }
        private string AddTable(string title, List<string> data)
        {
            string s = "<tr>";
            s += _form.header3(title);
            s += _form.Table();
            s += _form.TableHeader("Detected Operating Systems", "");
            s += "</tr>";
            s += _form.TableBodyStart();
            foreach (var d in data)
            {
                if (!String.IsNullOrEmpty(d))
                {
                    s += "<tr>";
                    s += _form.TableData(d, "");
                    s += "</tr>";
                }

            }

            s += _form.EndTable();

            return s;
        }
        public string AddSrvSummaryTable(bool scrub)
        {
            string summary = _sum.SrvSum();

            string s = _form.SectionStart("serversummary", VbrLocalizationHelper.MssTitle);

            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.MssHdr1, VbrLocalizationHelper.MssHdr1TT) +
                            _form.TableHeader(VbrLocalizationHelper.MssHdr2, VbrLocalizationHelper.MssHdr2TT) +
                            "</tr>";
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();

            try
            {
                Dictionary<string, int> list = _df.ServerSummaryToXml();

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += _form.TableDataLeftAligned(d.Key, "");
                    s += _form.TableData(d.Value.ToString(), "");
                    s += "</tr>";
                }
            }
            catch (Exception e)
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

            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, VbrLocalizationHelper.JobSum0TT, 0) +
                _form.TableHeader(VbrLocalizationHelper.JobSum1, VbrLocalizationHelper.JobSum1TT, 1) +
                "</tr>";
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {
                CJobSummaryTable st = new();

                Dictionary<string, int> list = st.JobSummaryTable();

                int totalJobs = list.Sum(x => x.Value);
                //foreach (var c in list)
                //{
                //    totalJobs += c.Value;
                //}



                foreach (var d in list)
                {
                    if (d.Value == 0)
                        continue;
                    s += "<tr>";
                    s += _form.TableDataLeftAligned(d.Key, "");
                    s += _form.TableData(d.Value.ToString(), "");
                    s += "</tr>";
                }

                s += "<tr>";
                s += _form.TableDataLeftAligned("<b>Total Jobs", "");
                s += _form.TableData(totalJobs.ToString() + "</b>", "");
                s += "</tr>";
            }
            catch (Exception e)
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

            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, "") +
                //_form.TableHeader("Count", "Total detected of this type") +
                "</tr>";
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            //CDataFormer cd = new(true);
            try
            {
                //List<string> list = _df.ParseNonProtectedTypes();
                CJobSummaryTable st = new();
                Dictionary<string, int> types = st.JobSummaryTable();

                foreach (var t in types)
                {
                    if (t.Value == 0)
                    {
                        s += "<tr>";
                        s += _form.TableDataLeftAligned(t.Key, "");
                        s += "</tr>";
                    }
                }

                //for (int i = 0; i < list.Count(); i++)
                //{
                //    s += "<tr>";

                //    s += _form.TableData(list[i], "");
                //    s += "</tr>";

                //}
            }
            catch (Exception e)
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
                _form.TableHeader(VbrLocalizationHelper.PlHdr3, VbrLocalizationHelper.PlHdrTT3);
                s += _form.TableHeaderEnd();
                s += _form.TableBodyStart();
                s += "<tr>";
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
                s += _form.TableHeaderEnd();
                s += _form.TableBodyStart();
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

                s += _form.TableHeaderEnd();
                s += _form.TableBodyStart();
                //CDataFormer cd = new(true);
                s += "<tr>";
                s += _form.TableData(_df._vmProtectedByPhys.Distinct().Count().ToString(), "");
                s += _form.TableData((_df._physNotProtNames.Distinct().Count() + _df._physProtNames.Distinct().Count()).ToString(), "");
                s += _form.TableData(_df._physProtNames.Distinct().Count().ToString(), "");
                s += _form.TableData(_df._physNotProtNames.Distinct().Count().ToString(), "");
                s += "</tr>";
                s += "</table>";

                try
                {
                    log.Info("Adding NAS table to HTML report", false);
                    CProtectedWorkloads cProtectedWorkloads = new();
                    NasSourceInfo n = new();

                    cProtectedWorkloads.nasWorkloads = n.NasTable().nasWorkloads;


                    s += "<h3>NAS Backups</h3>";
                    if (cProtectedWorkloads.nasWorkloads.Count() == 0)
                    {
                        s += "<p>No NAS Workloads detected</p>";
                    }
                    else
                    {
                        s += "<div id=\"nasTable\" border=\"1\" class=\"content-table\"></div>";
                        s += _form.Table();
                        s += "<tr>";
                        s += _form.TableHeader("File Share Types", "Total File Share Types found in environment");
                        s += _form.TableHeader("Total Share Size", "Total size of all shares found in environment");
                        s += _form.TableHeader("Total Files Count", "Total files found in all shares");
                        s += _form.TableHeader("Total Folders Count", "Total folders found in all shares");
                        s += _form.TableHeaderEnd();
                        s += _form.TableBodyStart();


                        foreach (var load in cProtectedWorkloads.nasWorkloads)
                        {
                            s += "<tr>";
                            s += _form.TableData(load.FileShareType, "");
                            s += _form.TableData(load.TotalShareSize, "");
                            s += _form.TableData(load.TotalFilesCount.ToString(), "");
                            s += _form.TableData(load.TotalFoldersCount.ToString(), "");
                            s += "</tr>";
                        }
                        s += _form.EndTable();
                    }


                }
                catch (Exception e)
                {
                    log.Error("Failed to add NAS table to HTML report", false);
                    log.Error(e.Message, false);
                }

                // add in Entra ID Tenants Protected
                try
                {
                    CProtectedWorkloads cProtectedWorkloads = new();
                    CEntraTenants n = new();

                    cProtectedWorkloads.entraWorkloads = n.EntraTable().entraWorkloads;
                    s += "</tr>";
                    s += "</table>";
                    s += "<h3>Entra Backups</h3>";
                    // Small table for Entra Tenant Count:
                    s += "<div id=\"entraTenantCount\" border=\"1\" class=\"content-table\"></div>";
                    s += _form.Table();
                    s += "<tr>";
                    s += _form.TableHeader("Tenant Count:", "Number of tenants backed up by this backup server");
                    s += _form.TableHeaderEnd();
                    s += _form.TableBodyStart();

                    s += "<tr>";
                    s += _form.TableData(cProtectedWorkloads.entraWorkloads.Count.ToString(), "");
                    s += "</tr>";
                    s += _form.EndTable();

                    // Table for Entra Tenants
                    s += "<div id=\"entraTable\" border=\"1\" class=\"content-table\"></div>";
                    s += _form.Table();
                    s += "<tr>";
                    s += _form.TableHeader("Tenant Name", "Name of the Entra ID Tenant being backed up.");
                    s += _form.TableHeader("Cache Repo", "Cache Repo selected for the tentant");
                    s += _form.TableHeaderEnd();
                    s += _form.TableBodyStart();

                    if (cProtectedWorkloads.entraWorkloads.Count == 0)
                    {
                        s += "<tr>";
                        s += _form.TableData("", "");
                        s += _form.TableData("", "");
                        s += "</tr>";
                    }
                    foreach (var load in cProtectedWorkloads.entraWorkloads)
                    {
                        s += "<tr>";
                        s += _form.TableData(load.TenantName, "");
                        s += _form.TableData(load.CacheRepoName, "");
                        s += "</tr>";
                    }
                    s += _form.EndTable();
                }
                catch (Exception ex)
                {

                }


                s += _form._endDiv;
            }
            catch (Exception e)
            {
                log.Error("Protected Servers Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }
            s += _form.SectionEnd(summary);
            //try
            //{
            //    CProtectedWorkloads cProtectedWorkloads = new();
            //    NasSourceInfo n = new();

            //    cProtectedWorkloads.nasWorkloads = n.NasTable().nasWorkloads;
            //    s += DumpJsonToScript(cProtectedWorkloads, "NasTable");
            //}
            //catch (Exception e)
            //{
            //    log.Error("Failed to add NAS table to HTML report", false);
            //    log.Error(e.Message, false);
            //}
            return s;
        }
        public string AddManagedServersTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("managedServerInfo", VbrLocalizationHelper.ManSrvTitle, VbrLocalizationHelper.ManSrvBtn);
            string summary = _sum.ManagedServers();
            s +=
           _form.TableHeader(VbrLocalizationHelper.ManSrv0, VbrLocalizationHelper.ManSrv0TT, 0) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv1, VbrLocalizationHelper.ManSrv1TT, 1) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv2, VbrLocalizationHelper.ManSrv2TT, 2) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv3, VbrLocalizationHelper.ManSrv3TT, 3) +
           _form.TableHeader("OS Info", VbrLocalizationHelper.ManSrv3TT, 4) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv4, VbrLocalizationHelper.ManSrv4TT, 5) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv5, VbrLocalizationHelper.ManSrv5TT, 6) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv6, VbrLocalizationHelper.ManSrv6TT, 7) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv7, VbrLocalizationHelper.ManSrv7TT, 8) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv8, VbrLocalizationHelper.ManSrv8TT, 9) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv9, VbrLocalizationHelper.ManSrv9TT, 10) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv10, VbrLocalizationHelper.ManSrv10TT, 11) +
           _form.TableHeader(VbrLocalizationHelper.ManSrv11, VbrLocalizationHelper.ManSrv11TT, 12);
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            //CDataFormer cd = new(true);
            try
            {
                List<CManagedServer> list = _df.ServerXmlFromCsv(scrub);


                foreach (var d in list)
                {

                    s += "<tr>";

                    s += _form.TableData(d.Name, "");
                    s += _form.TableData(d.Cores.ToString(), "");
                    s += _form.TableData(d.Ram.ToString(), "");
                    s += _form.TableData(d.Type, "");
                    s += _form.TableData(d.OsInfo, "");
                    s += _form.TableData(d.ApiVersion, "");
                    s += _form.TableData(d.ProtectedVms.ToString(), "");
                    s += _form.TableData(d.NotProtectedVms.ToString(), "");
                    s += _form.TableData(d.TotalVms.ToString(), "");
                    if (d.IsProxy)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.IsRepo)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.IsWan)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");




                    s += _form.TableData(d.IsUnavailable.ToString(), "");
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                log.Error("Managed Server Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            return s;
        }
        public string SerializeToJson(object obj)
        {
            return JsonSerializer.Serialize(obj);
        }
        public string AddRegKeysTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("regkeys", VbrLocalizationHelper.RegTitle, VbrLocalizationHelper.RegBtn);
            string summary = _sum.RegKeys();

            try
            {

                Dictionary<string, string> list = _df.RegOptions();
                if (list.Count == 0)
                {
                    s += "<p>No modified registry keys found</p>";
                }
                else
                {
                    s += "<tr>" +
                    _form.TableHeader(VbrLocalizationHelper.Reg0, VbrLocalizationHelper.Reg0TT) +
                    _form.TableHeader(VbrLocalizationHelper.Reg1, VbrLocalizationHelper.Reg1TT) +
                    "</tr>";
                    s += _form.TableHeaderEnd();
                    s += _form.TableBodyStart();
                    s += "<tr>";
                    foreach (var d in list)
                    {
                        s += "<tr>";
                        s += _form.TableData(d.Key, "");
                        s += _form.TableData(d.Value.ToString(), "");
                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
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
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            //CDataFormer cd = new(true);
            try
            {
                List<string[]> list = _df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {

                    var prov = d[12];
                    int shade = 0;
                    if (prov == "under")
                        shade = 2;
                    if (prov == "over")
                        shade = 1;

                    s += "<tr>";
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(d[0], ScrubItemType.Server), ""); // server name
                    else
                        s += _form.TableData(d[0], "");
                    s += _form.TableData(d[1], "");
                    s += _form.TableData(d[2], "", shade);
                    s += _form.TableData(d[3], "");
                    s += _form.TableData(d[4], "");
                    s += _form.TableData(d[5], "");
                    //s += _form.TableData(d[6], "");
                    if (d[6] == "True")
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    s += _form.TableData(d[7], "");
                    s += _form.TableData(d[8], "");
                    s += _form.TableData(d[9], "");
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(d[10], ScrubItemType.Server), ""); // host
                    else
                        s += _form.TableData(d[10], "");

                    if (d[11] == "True")
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
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
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {
                List<string[]> list = _df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";
                    if (scrub)
                        s += _form.TableData(_scrub.ScrubItem(d[0], ScrubItemType.Server), "");
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
                        s += _form.TableData(_scrub.ScrubItem(d[10], ScrubItemType.Server), "");
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
           _form.TableHeader("CapTier Immutable", VbrLocalizationHelper.Sbr9TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr10, VbrLocalizationHelper.Sbr10TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr11, VbrLocalizationHelper.Sbr11TT) +
           _form.TableHeader(VbrLocalizationHelper.Sbr12, VbrLocalizationHelper.Sbr12TT) +
           "</tr>";
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            //CDataFormer cd = new(true);

            try
            {
                List<CSobrTypeInfos> list = _df.SobrInfoToXml(scrub);

                foreach (var d in list)
                {


                    s += "<tr>";
                    s += _form.TableData(d.Name, "");
                    s += _form.TableData(d.ExtentCount.ToString(), "");
                    s += _form.TableData(d.JobCount.ToString(), "");
                    s += _form.TableData(d.PolicyType, "");
                    if (d.EnableCapacityTier)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");


                    if (d.CapacityTierCopyPolicyEnabled)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.CapacityTierMovePolicyEnabled)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.ArchiveTierEnabled)
                        s += _form.TableData(_form.True, "");

                    else
                        s += _form.TableData(_form.False, "");
                    if (d.UsePerVMBackupFiles)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");




                    s += _form.TableData(d.CapTierType, "");
                    if (d.ImmuteEnabled)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    s += _form.TableData(d.ImmutePeriod, "");
                    if (d.SizeLimitEnabled)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    s += _form.TableData(d.SizeLimit, "");
                    s += "</tr>";
                }
            }
            catch (Exception e)
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
_form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
_form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
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
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {

                List<CRepository> list = _df.ExtentXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    var prov = d.Provisioning;
                    int shade = 0;
                    if (prov == "under")
                        shade = 2;
                    if (prov == "over")
                        shade = 1;

                    int freeSpaceShade = 0;
                    //decimal.TryParse(d.FreeSpacePercent, out decimal i);
                    if (d.FreeSpacePercent < 20) { freeSpaceShade = 1; }



                    s += "<tr>";
                    s += _form.TableData(d.Name, "");
                    s += _form.TableData(d.SobrName, "");
                    s += _form.TableData(d.MaxTasks.ToString(), "", shade);
                    s += _form.TableData(d.Cores.ToString(), "");
                    s += _form.TableData(d.Ram.ToString(), "");

                    if (d.IsAutoGate)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    s += _form.TableData(d.Host, "");
                    s += _form.TableData(d.Path, "");
                    s += _form.TableData(d.FreeSpace.ToString(), "");
                    s += _form.TableData(d.TotalSpace.ToString(), "");
                    s += _form.TableData(d.FreeSpacePercent.ToString(), "", freeSpaceShade);



                    if (d.IsDecompress)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    if (d.AlignBlocks)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    if (d.IsRotatedDrives)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    if (d.IsImmutabilitySupported)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    s += _form.TableData(d.Type, "");
                    //s += _form.TableData(d[16], "");
                    s += "</tr>";
                }
            }
            catch (Exception e)
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
            s +=
_form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT, 0) +
_form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo0TT, 1) +
_form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT, 2) +
_form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT, 3) +
_form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT, 4) +
_form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
_form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
_form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT, 7) +
_form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT, 8) +
_form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT, 9) +
_form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT, 10) +
_form.TableHeader(VbrLocalizationHelper.Repo1, VbrLocalizationHelper.Repo1TT, 11) +
_form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT, 12) +
_form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT, 13) +
_form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT, 14) +
_form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT, 15) +
_form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT, 16);

            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {
                List<CRepository> list = _df.RepoInfoToXml(scrub);

                foreach (var d in list)
                {
                    var prov = d.Provisioning;
                    int shade = 0;
                    if (prov == "under")
                        shade = 2;
                    if (prov == "over")
                        shade = 1;
                    int freeSpaceShade = 0;
                    //decimal.TryParse(d.FreeSpacePercent, out decimal i);

                    //int perVmShade = 0;
                    //if (d[11] == "False")
                    //    perVmShade = 3;

                    if (d.FreeSpacePercent < 20) { freeSpaceShade = 1; }
                    s += "<tr>";
                    s += _form.TableData(d.Name, "");
                    s += _form.TableData(d.JobCount.ToString(), "");
                    s += _form.TableData(d.MaxTasks.ToString(), "", shade);
                    s += _form.TableData(d.Cores.ToString(), "");
                    s += _form.TableData(d.Ram.ToString(), "");
                    if (d.IsAutoGate)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");

                    s += _form.TableData(d.Host, "");
                    s += _form.TableData(d.Path, "");
                    s += _form.TableData(d.FreeSpace.ToString(), "");
                    s += _form.TableData(d.TotalSpace.ToString(), "");
                    s += _form.TableData(d.FreeSpacePercent.ToString(), "", freeSpaceShade);
                    if (d.IsPerVmBackupFiles)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.Warn, "");
                    if (d.IsDecompress)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.AlignBlocks)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.IsRotatedDrives)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    if (d.IsImmutabilitySupported)
                        s += _form.TableData(_form.True, "");
                    else
                        s += _form.TableData(_form.False, "");
                    s += _form.TableData(d.Type, "");





                    s += "</tr>";
                }
            }
            catch (Exception e)
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
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {


                var stuff = _df.JobConcurrency(true);

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
            catch (Exception e)
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
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {
                var stuff = _df.JobConcurrency(false);

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
            catch (Exception e)
            {
                log.Error("Task Concurrency Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }


            s += _form.SectionEnd(summary);
            return s;
        }
        public string AddJobSessSummTable(bool scrub)
        {
            log.Info("Adding Job Session Summary Table");
            string s = _form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            string summary = _sum.JobSessSummary();

            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT); // job name
            s += _form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);// items
            s += _form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT); // min time
            s += _form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);// max time
            s += _form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);// avg time
            s += _form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT); // total sessions
            s += _form.TableHeader("Fails", "Total times job failed"); // fails
            s += _form.TableHeader("Retries", "Total times job retried");// retries
            s += _form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);// success rate
            s += _form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT); // avg backup size
            s += _form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);// max backup size
            s += _form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT); // avg data size
            s += _form.TableHeader(VbrLocalizationHelper.Jss10, "Used size of all objects in job."); // max data size
            s += _form.TableHeader(VbrLocalizationHelper.Jss11, "Avg Data Size divided by Max Data Size (average processed data divided by total consumed size of all VMs in the job)"); // avg change rate
            s += _form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT); // wait for res count
            s += _form.TableHeader(VbrLocalizationHelper.Jss13, VbrLocalizationHelper.Jss13TT); // max wait
            s += _form.TableHeader(VbrLocalizationHelper.Jss14, VbrLocalizationHelper.Jss14TT);// avg wait 
            s += _form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT); // job types
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();
            try
            {
                var stuff = _df.ConvertJobSessSummaryToXml(scrub);


                foreach (var stu in stuff)
                {
                    try
                    {
                        string t = "";
                        t += "<tr>";

                        t += _form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += _form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += _form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += _form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += _form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += _form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += _form.TableData(stu.Fails.ToString(), "Fails");
                        t += _form.TableData(stu.Retries.ToString(), "Retries");
                        t += _form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += _form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += _form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += _form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += _form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += _form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += _form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += _form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                        t += _form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += _form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;
                    }
                    catch (Exception ex)
                    {
                        log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        log.Error("\t" + ex.Message);


                    }


                }
            }
            catch (Exception e)
            {
                log.Error("Job Session Summary Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            s += _form.SectionEnd(summary);
            log.Info("Job Session Summary Table added");
            return s;
        }
        public string AddJobSessSummTableByJob(bool scrub)
        {
            string s = _form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            s += "</table>";

            string summary = _sum.JobInfo();


            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                source.OrderBy(x => x.Name);
                var stuff = _df.ConvertJobSessSummaryToXml(scrub);
                var jobTypes = stuff.Select(x => x.JobType).Distinct().ToList();

                List<CJobSummaryTypes> OffloadJobs = new();
                //log.Debug("Sessions count = " + stuff.Count);
                try
                {
                    foreach (var jType in jobTypes)
                    {
                        bool skipTotals = false;
                        var jobType = jType;
                        // change job type for "Totals" line of all jobs:

                        if (jType == "" || jType == null)
                        {
                            //log.Debug("Job Type is empty...");
                            //jobType = "Summary of All";

                        }




                        var realType = CJobTypesParser.GetJobType(jobType);
                        //log.Debug("Real Job Type = " + realType);
                        string sectionHeader = realType;
                        if (jType == null)
                        {
                            sectionHeader = "Summary of All";
                        }
                        string jobTable = _form.SectionStartWithButton("jobTable", sectionHeader + " Jobs", "");
                        s += jobTable;
                        s += SetJobSessionsHeaders();
                        var res = stuff.Where(x => x.JobType == jobType).ToList();
                        //log.Debug("Jobs of type " + jType + " found:" + res.Count);



                        // define variable to capture the TOTALS
                        int totalItemsCount = 0;
                        double totalSessionCount = 0;
                        int totalFails = 0;
                        int totalRetries = 0;
                        double totalSuccessRate = 0;
                        double totalAvgBackupSize = 0;
                        double totalMaxBackupSize = 0;
                        double totalAvgDataSize = 0;
                        double totalMaxDataSize = 0;
                        double totalAvgChangeRate = 0;
                        int totalWaitCount = 0;




                        foreach (var stu in res)
                        {
                            // if the job name contains "Offload" then add it to the OffloadJobs list
                            if (stu.JobName.Contains("Offload"))
                            {
                                OffloadJobs.Add(stu);
                                continue;
                            }
                            if (stu.JobName == "Total")
                            {
                                skipTotals = true;
                            }

                            //log.Debug("job name = " + stu.JobName);

                            if (stu.JobType != jobType && stu.JobName != "Totals")
                            {
                                //log.Debug("Job Type: " + stu.JobType + " does not match " + jobType + "... skipping");
                                continue;
                            }

                            try
                            {
                                //log.Debug("Adding row for job: " + stu.JobName);
                                string t = "";
                                t += "<tr>";

                                t += _form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                                t += _form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                                t += _form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                                t += _form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                                t += _form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                                t += _form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                                t += _form.TableData(stu.Fails.ToString(), "Fails");
                                t += _form.TableData(stu.Retries.ToString(), "Retries");
                                t += _form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                                t += _form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                                t += _form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                                t += _form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                                t += _form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                                t += _form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                                t += _form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                                t += _form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                                t += _form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                                string jt = CJobTypesParser.GetJobType(stu.JobType);
                                t += _form.TableData(jt, VbrLocalizationHelper.Jss15);

                                t += "</tr>";

                                s += t;

                                totalItemsCount += stu.ItemCount;
                                totalSessionCount += stu.sessionCount;
                                totalFails += stu.Fails;
                                totalRetries += stu.Retries;
                                //double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                                //totalSuccessRate += (double)Math.Round(successPercent, 2);
                                totalAvgBackupSize += stu.AvgBackupSize;
                                totalMaxBackupSize += stu.MaxBackupSize;
                                totalAvgDataSize += stu.AvgDataSize;
                                totalMaxDataSize += stu.MaxDataSize;
                                //totalAvgChangeRate += Math.Round(totalAvgDataSize / totalMaxDataSize * stu.AvgChangeRate, 2);
                                totalWaitCount += stu.waitCount;
                            }

                            catch (Exception ex)
                            {
                                log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                                log.Error("\t" + ex.Message);


                            }




                        }
                        // clean up totals:

                        double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                        totalSuccessRate = (double)Math.Round(successPercent, 2);
                        if (totalAvgDataSize == 0 && totalMaxDataSize == 0)
                        {
                            totalAvgChangeRate = 0;
                        }
                        else
                        {
                            totalAvgChangeRate = Math.Round(totalAvgDataSize / totalMaxDataSize * 100, 2);
                        }


                        //log.Debug("Total avg change rate = " + totalAvgChangeRate);
                        //add totals line:
                        if (!skipTotals)
                        {
                            string totalRow = "";
                            totalRow += "<tr>";
                            totalRow += _form.TableDataLeftAligned("TOTALS", "");
                            totalRow += _form.TableData(totalItemsCount.ToString(), "");
                            totalRow += _form.TableData("", "");
                            totalRow += _form.TableData("", "");
                            totalRow += _form.TableData("", "");
                            totalRow += _form.TableData(totalSessionCount.ToString(), "");
                            totalRow += _form.TableData(totalFails.ToString(), "");
                            totalRow += _form.TableData(totalRetries.ToString(), "");
                            totalRow += _form.TableData(totalSuccessRate.ToString(), "");
                            totalRow += _form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), "");
                            totalRow += _form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), "");
                            totalRow += _form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), "");
                            totalRow += _form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), "");
                            if (totalAvgChangeRate == double.NaN)
                            {
                                totalAvgChangeRate = 0;
                            }
                            totalRow += _form.TableData(totalAvgChangeRate.ToString(), "");
                            totalRow += _form.TableData(totalWaitCount.ToString(), "");
                            totalRow += _form.TableData("", "");
                            totalRow += _form.TableData("", "");
                            totalRow += _form.TableData("", "");
                            s += totalRow;
                        }


                        //table summary/totals


                        // end each table/section
                        s += _form.SectionEnd(summary);

                    }

                    s += AddOffloadsTable(OffloadJobs);
                }
                catch (Exception e)
                {
                    log.Error("Job Info Data import failed. ERROR:");
                    log.Error("\t" + e.Message);
                }
                //end of FE up one line...

            }
            catch (Exception e)
            {
                log.Error("Jobs Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }


            s += _form.SectionEnd(summary);
            return s;
        }
        private string AddOffloadsTable(List<CJobSummaryTypes> offloadJobs)
        {
            string s = "";
            try
            {


                //log.Debug("Checking Job Type: " + jType);
                //var translatedJobType = CJobTypesParser.GetJobType(jType);
                //log.Debug("\tTranslated Job Type: " + translatedJobType);


                var realType = "Offload";
                string jobTable = _form.SectionStartWithButton("jobTable", realType + " Jobs", "");
                s += jobTable;
                s += SetJobSessionsHeaders();
                //var res = stuff.Where(x => x.JobType == jType).ToList();
                // log.Debug("Jobs of type " + jType + " found:" + res.Count);


                //var res2 = stuff.Where(x => x.JobType == realType).ToList();

                //log.Debug("Jobs of type " + jType + " found:" + res2.Count);
                // define variable to capture the TOTALS
                int totalItemsCount = 0;
                double totalSessionCount = 0;
                int totalFails = 0;
                int totalRetries = 0;
                double totalSuccessRate = 0;
                double totalAvgBackupSize = 0;
                double totalMaxBackupSize = 0;
                double totalAvgDataSize = 0;
                double totalMaxDataSize = 0;
                double totalAvgChangeRate = 0;
                int totalWaitCount = 0;




                foreach (var stu in offloadJobs)
                {
                    // if the job name contains "Offload" then add it to the OffloadJobs list

                    //log.Debug("job type of individual session = " + stu.JobType);

                    // if (stu.JobType != jType)
                    // {
                    //     log.Debug("Job Type: " + stu.JobType + " does not match " + jType + "... skipping");
                    //     continue;
                    // }

                    try
                    {
                        //log.Debug("Adding row for job: " + stu.JobName);
                        string t = "";
                        t += "<tr>";

                        t += _form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += _form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += _form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += _form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += _form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += _form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += _form.TableData(stu.Fails.ToString(), "Fails");
                        t += _form.TableData(stu.Retries.ToString(), "Retries");
                        t += _form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += _form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += _form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += _form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += _form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += _form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += _form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += _form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                        t += _form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += _form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;

                        totalItemsCount += stu.ItemCount;
                        totalSessionCount += stu.sessionCount;
                        totalFails += stu.Fails;
                        totalRetries += stu.Retries;
                        //double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                        //totalSuccessRate += (double)Math.Round(successPercent, 2);
                        totalAvgBackupSize += stu.AvgBackupSize;
                        totalMaxBackupSize += stu.MaxBackupSize;
                        totalAvgDataSize += stu.AvgDataSize;
                        totalMaxDataSize += stu.MaxDataSize;
                        //totalAvgChangeRate += Math.Round(totalAvgDataSize / totalMaxDataSize * stu.AvgChangeRate, 2);
                        totalWaitCount += stu.waitCount;
                    }

                    catch (Exception ex)
                    {
                        log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        log.Error("\t" + ex.Message);


                    }




                }
                // clean up totals:

                double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                totalSuccessRate = (double)Math.Round(successPercent, 2);

                totalAvgChangeRate = Math.Round(totalAvgDataSize / totalMaxDataSize * 100, 2);
                //add totals line:
                string totalRow = "";
                totalRow += "<tr>";
                totalRow += _form.TableDataLeftAligned("TOTALS", "");
                totalRow += _form.TableData(totalItemsCount.ToString(), "");
                totalRow += _form.TableData("", "");
                totalRow += _form.TableData("", "");
                totalRow += _form.TableData("", "");
                totalRow += _form.TableData(totalSessionCount.ToString(), "");
                totalRow += _form.TableData(totalFails.ToString(), "");
                totalRow += _form.TableData(totalRetries.ToString(), "");
                totalRow += _form.TableData(totalSuccessRate.ToString(), "");
                totalRow += _form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), "");
                totalRow += _form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), "");
                totalRow += _form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), "");
                totalRow += _form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), "");
                totalRow += _form.TableData(totalAvgChangeRate.ToString(), "");
                totalRow += _form.TableData(totalWaitCount.ToString(), "");
                totalRow += _form.TableData("", "");
                totalRow += _form.TableData("", "");
                totalRow += _form.TableData("", "");
                s += totalRow;

                //table summary/totals


                // end each table/section
                s += _form.SectionEnd("");



            }
            catch (Exception e)
            {
                log.Error("Job Info Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }

            return s;
        }
        public string AddJobInfoTable(bool scrub)
        {
            string s = _form.SectionStartWithButton("jobs", VbrLocalizationHelper.JobInfoTitle, VbrLocalizationHelper.JobInfoBtn);
            s += "</table>";

            string summary = _sum.JobInfo();


            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                source.OrderBy(x => x.Name);
                var jobTypes = source.Select(x => x.JobType).Distinct().ToList();

                try
                {
                    foreach (var jType in jobTypes)
                    {
                        double tSizeGB = 0;
                        double onDiskTotalGB = 0;

                        var useSourceSize = true;
                        if (jType == "NasBackupCopy" || jType == "Copy")
                        {
                            useSourceSize = false;
                        }
                        var realType = CJobTypesParser.GetJobType(jType);
                        string jobTable = _form.SectionStartWithButton("jobTable", realType + " Jobs", "");
                        s += jobTable;
                        s += SetGenericJobTablHeader(useSourceSize);
                        var res = source.Where(x => x.JobType == jType).ToList();
                        foreach (var job in res)
                        {
                            //object x = null;
                            double onDiskGB = 0;
                            double sourceSizeGB = 0;

                            if (job.JobType != jType)
                            {
                                continue;
                            }
                            string row = "";
                            if (jType == "NasBackup")
                            {
                                var x = csvparser.GetDynamicNasBackup().ToList();

                                var diskGb = x.Where(x => x.name == job.Name)
                                    .Select(x => x.ondiskgb)
                                    .FirstOrDefault();
                                double.TryParse(diskGb, out onDiskGB);
                                onDiskGB = Math.Round(onDiskGB, 2);
                                onDiskTotalGB += onDiskGB;

                                var sourceGb = x.Where(x => x.name == job.Name)
                                    .Select(x => x.sourcegb)
                                    .FirstOrDefault();
                                double.TryParse(sourceGb, out sourceSizeGB);
                                sourceSizeGB = Math.Round(sourceSizeGB, 2);


                            }

                            string jobName = job.Name;
                            string repoName = job.RepoName;
                            if (scrub)
                            {
                                jobName = CGlobals.Scrubber.ScrubItem(jobName, ScrubItemType.Job);
                                repoName = CGlobals.Scrubber.ScrubItem(repoName, ScrubItemType.Repository);
                            }

                            row += "<tr>";
                            row += _form.TableDataLeftAligned(jobName, "");
                            row += _form.TableData(repoName, "");

                            if (useSourceSize)
                            {
                                if (jType == "NasBackup")
                                {
                                    row += _form.TableData(sourceSizeGB.ToString(), "");
                                    tSizeGB += sourceSizeGB;
                                }
                                else
                                {

                                    double trueSizeGB = Math.Round(job.OriginalSize / 1024 / 1024 / 1024, 2);
                                    double trueSizeTB = Math.Round(job.OriginalSize / 1024 / 1024 / 1024 / 1024, 2);
                                    double trueSizeMB = Math.Round(job.OriginalSize / 1024 / 1024, 2);
                                    tSizeGB += trueSizeGB;
                                    if (trueSizeGB > 999)
                                    {
                                        row += _form.TableData(trueSizeTB.ToString() + " TB", "");
                                    }
                                    else if (trueSizeGB < 1)
                                    {
                                        row += _form.TableData(trueSizeMB.ToString() + " MB", "");
                                    }
                                    else
                                    {
                                        row += _form.TableData(trueSizeGB.ToString() + " GB", "");
                                    }
                                }
                            }
                            else
                            {
                            }

                            row += _form.TableData(onDiskGB.ToString(), "");
                            //row+= _form.TableData(trueSizeGB.ToString() + " GB", "");
                            //row+= _form.TableData(job.RetentionType, "");
                            row += job.RetentionType == "Cycles" ? _form.TableData("Points", "") : _form.TableData(job.RetentionType, "");
                            row += _form.TableData(job.RestorePoints, "");
                            //row += _form.TableData(job.StgEncryptionEnabled, "");
                            row += job.StgEncryptionEnabled == "True" ? _form.TableData(_form.True, "") : _form.TableData(_form.False, "");
                            var jobType = CJobTypesParser.GetJobType(job.JobType);
                            row += _form.TableData(jobType, "");
                            //row += _form.TableData("", "");



                            string compressionLevel = "";
                            if (job.CompressionLevel == "9")
                                compressionLevel = "Extreme";
                            else if (job.CompressionLevel == "6")
                                compressionLevel = "High";
                            else if (job.CompressionLevel == "5")
                                compressionLevel = "Optimal";

                            else if (job.CompressionLevel == "4")
                                compressionLevel = "Dedupe-Friendly";
                            else if (job.CompressionLevel == "0")
                                compressionLevel = "None";
                            row += _form.TableData(compressionLevel, "");

                            string blockSize = "";
                            if (job.BlockSize == "KbBlockSize1024")
                                blockSize = "1 MB";
                            else if (job.BlockSize == "KbBlockSize512")
                                blockSize = "512 KB";
                            else if (job.BlockSize == "KbBlockSize256")
                                blockSize = "256 KB";
                            else if (job.BlockSize == "KbBlockSize4096")
                                blockSize = "4 MB";
                            else if (job.BlockSize == "KbBlockSize8192")
                                blockSize = "8 MB";


                            row += _form.TableData(blockSize, "");
                            if (job.GfsMonthlyEnabled || job.GfsWeeklyIsEnabled || job.GfsYearlyEnabled)
                            {
                                row += _form.TableData(_form.True, "");
                                string GfsString = "Weekly: " + job.GfsWeeklyCount + "<br> Monthly: " + job.GfsMonthlyCount + "<br> Yearly: " + job.GfsYearlyCount;
                                row += _form.TableData(GfsString, "");
                            }
                            else
                            {
                                row += _form.TableData(_form.False, "");
                                row += _form.TableData("", "");
                            }
                            row += job.EnableFullBackup ? _form.TableData(_form.True, "") : _form.TableData(_form.False, "");
                            try
                            {
                                if (job.Algorithm == "Increment" && job.TransformFullToSyntethic == true)
                                {
                                    row += _form.TableData(_form.True, "");
                                }
                                else
                                {
                                    row += _form.TableData(_form.False, "");
                                }
                            }
                            catch
                            {
                                row += _form.TableData(_form.False, "");
                            }

                            row += job.Algorithm == "Syntethic" ? _form.TableData("Reverse Incremental", "") : _form.TableData("Forward Incremental", "");


                            row += job.IndexingType != "None" ? _form.TableData(_form.True, "") : _form.TableData(_form.False, "");

                            row += "</tr>";

                            s += row;

                        }
                        //table summary/totals
                        if (useSourceSize)
                        {
                            s += "<tr>";
                            s += _form.TableDataLeftAligned("Totals", "");
                            s += _form.TableData("", "");
                            //double totalSizeGB = Math.Round(tSizeGB / 1024 / 1024 / 1024, 2);
                            double totalSizeTB = Math.Round(tSizeGB / 1024, 2);
                            double totalSizeMB = Math.Round(tSizeGB * 1024, 2);

                            double diskTotalTB = Math.Round(onDiskTotalGB / 1024, 2);
                            double diskTotalMB = Math.Round(onDiskTotalGB * 1024, 2);
                            if (tSizeGB > 999)
                            {
                                s += _form.TableData(totalSizeTB.ToString() + " TB", "");
                            }
                            else if (tSizeGB < 1)
                            {
                                s += _form.TableData(totalSizeMB.ToString() + " MB", "");
                            }
                            else
                            {
                                s += _form.TableData(tSizeGB.ToString() + " GB", "");
                            }

                            if (diskTotalTB > 1)
                            {
                                s += _form.TableData(onDiskTotalGB.ToString(), "TB");
                            }
                            else if (onDiskTotalGB > 1)
                            {
                                s += _form.TableData(onDiskTotalGB.ToString(), "GB");
                            }
                            else
                            {
                                s += _form.TableData(onDiskTotalGB.ToString(), "MB");
                            }
                        }

                        // end each table/section
                        s += _form.SectionEnd(summary);

                    }
                    // add tape table
                    try
                    {
                        CTapeJobInfoTable tapeTable = new();
                        string tt = tapeTable.TapeJobTable();
                        if (tt != "")
                        {
                            string tableButton = _form.SectionStartWithButton("jobTable", "Tape Jobs", "");
                            s += tableButton;
                            s += tt;
                        }


                    }
                    catch (Exception e)
                    {
                        log.Error("Tape Job Data import failed. ERROR:");
                        log.Error("\t" + e.Message);
                    }
                    // Add Entra Table
                    try
                    {
                        CEntraJobsTable entraTable = new();
                        string et = entraTable.Table();
                        if (et != null)
                        {
                            string tableButton = _form.SectionStartWithButton("jobTable", "Entra Jobs", "");
                            s += tableButton;
                            s += et;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Entra Job Data import failed. ERROR:");
                        log.Error("\t" + e.Message);
                    }
                    s += _form.SectionEnd(summary);
                }
                catch (Exception e)
                {
                    log.Error("Job Info Data import failed. ERROR:");
                    log.Error("\t" + e.Message);
                }
                //end of FE up one line...

            }
            catch (Exception e)
            {
                log.Error("Jobs Data import failed. ERROR:");
                log.Error("\t" + e.Message);
            }


            s += _form.SectionEnd(summary);
            return s;
        }
        private string SetJobSessionsHeaders()
        {
            string s = "";
            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT); // job name
            s += _form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);// items
            s += _form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT); // min time
            s += _form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);// max time
            s += _form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);// avg time
            s += _form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT); // total sessions
            s += _form.TableHeader("Fails", "Total times job failed"); // fails
            s += _form.TableHeader("Retries", "Total times job retried");// retries
            s += _form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);// success rate
            s += _form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT); // avg backup size
            s += _form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);// max backup size
            s += _form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT); // avg data size
            s += _form.TableHeader(VbrLocalizationHelper.Jss10, "Used size of all objects in job."); // max data size
            s += _form.TableHeader(VbrLocalizationHelper.Jss11, "Avg Data Size divided by Max Data Size (average processed data divided by total consumed size of all VMs in the job)"); // avg change rate
            s += _form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT); // wait for res count
            s += _form.TableHeader(VbrLocalizationHelper.Jss13, VbrLocalizationHelper.Jss13TT); // max wait
            s += _form.TableHeader(VbrLocalizationHelper.Jss14, VbrLocalizationHelper.Jss14TT);// avg wait 
            s += _form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT); // job types
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();

            return s;

        }
        private string SetGenericJobTablHeader(bool useSourceSize)
        {
            string s = "";
            s += _form.TableHeaderLeftAligned(VbrLocalizationHelper.JobInfo0, VbrLocalizationHelper.JobInfo0TT); // Name
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo1, VbrLocalizationHelper.JobInfo1TT); // Repo
            if (useSourceSize)
                s += _form.TableHeader(VbrLocalizationHelper.JobInfo2, VbrLocalizationHelper.JobInfo2TT); // Source Size (GB)
            s += _form.TableHeader("Est. On Disk GB", "Estimated size of the backup data on-disk.");
            s += _form.TableHeader("Retention Scheme", "Is the job set to keep backups for X number of Days or Points");
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo3, VbrLocalizationHelper.JobInfo3TT); // Restore Point Target
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo4, VbrLocalizationHelper.JobInfo4TT); // Encrypted
            s += _form.TableHeader(VbrLocalizationHelper.JobInfo5, VbrLocalizationHelper.JobInfo5TT); // Job Type
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo6, VbrLocalizationHelper.JobInfo6TT); // Algorithm
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo7, VbrLocalizationHelper.JobInfo7TT); // Scheudle Enabled Time
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo8, VbrLocalizationHelper.JobInfo8TT); // Full Backup Days
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo9, VbrLocalizationHelper.JobInfo9TT); // Full Backup Schedule
            ////s += _form.TableHeader(ResourceHandler.JobInfo10, ResourceHandler.JobInfo10TT);
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo11, VbrLocalizationHelper.JobInfo11TT); // transform full to synth
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo12, VbrLocalizationHelper.JobInfo12TT); // thransform inc to synth
            //s += _form.TableHeader(VbrLocalizationHelper.JobInfo13, VbrLocalizationHelper.JobInfo13TT); // transform days

            // New Table Area:
            //s += _form.TableHeader("", "");
            s += _form.TableHeader("Compression Level", "Level of compression used in the job");
            s += _form.TableHeader("Block Size", "Block Size set for the job");
            s += _form.TableHeader("GFS Enabled", "True if any GFS Periods are enabled");
            s += _form.TableHeader("GFS Retention", "Details about the GFS Retention period");
            s += _form.TableHeader("Active Full Enabled", "");
            s += _form.TableHeader("Synthetic Full Enabled", "");
            s += _form.TableHeader("Backup Chain Type", "Type of backup chain used in the job");
            s += _form.TableHeader("Indexing Enabled", "");
            //s += _form.TableData("Totals", "");
            //s += _form.TableHeader("", "");
            //s += _form.TableHeader("", "");
            //s += _form.TableHeader("", "");
            //s += _form.TableHeader("", "");
            //s += _form.TableHeader("", "");
            //s += _form.TableHeader("", "");
            s += _form.TableHeaderEnd();
            s += _form.TableBodyStart();

            return s;
        }



        public void AddSessionsFiles(bool scrub)
        {
            _df.JobSessionInfoToXml(scrub);
        }

    }

}
