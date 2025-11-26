// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html.VBR
{
    /// <summary>
    /// Provides methods for generating HTML tables and sections for Veeam HealthCheck reporting.
    /// </summary>
    internal class CHtmlTables
    {
        private readonly CCsvParser csv;
        private readonly CLogger log = CGlobals.Logger;
        private readonly CHtmlTablesHelper helper;

        private readonly CDataFormer df;
        private readonly Scrubber.CScrubHandler scrub;

        private readonly CHtmlFormatting form;
        private readonly CVbrSummaries sum;

        /// <summary>
        /// Initializes a new instance of the <see cref="CHtmlTables"/> class.
        /// </summary>
        public CHtmlTables()
        {
            this.log.Info("[CHtmlTables] Constructor started");
            try
            {
                this.csv = new(CVariables.vb365dir);
                this.log.Info("[CHtmlTables] CCsvParser initialized");
                this.helper = new();
                this.log.Info("[CHtmlTables] CHtmlTablesHelper initialized");
                this.df = new();
                this.log.Info("[CHtmlTables] CDataFormer initialized");
                this.scrub = CGlobals.Scrubber;
                this.log.Info("[CHtmlTables] Scrubber initialized");
                this.form = new();
                this.log.Info("[CHtmlTables] CHtmlFormatting initialized");
                this.sum = new();
                this.log.Info("[CHtmlTables] CVbrSummaries initialized");
                this.log.Info("[CHtmlTables] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                this.log.Error("[CHtmlTables] Constructor failed: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Generates the HTML navigation table for the Veeam HealthCheck report.
        /// </summary>
        /// <returns>A string containing the HTML navigation table.</returns>
        public string MakeNavTable()
        {
            return this.form.FormNavRows(VbrLocalizationHelper.NavLicInfoLink, "license", VbrLocalizationHelper.NavLicInfoDetail) +
                this.form.FormNavRows(VbrLocalizationHelper.NavBkpSrvLink, "vbrserver", VbrLocalizationHelper.NavBkpSrvDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSecSumLink, "secsummary", VbrLocalizationHelper.NavSecSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSrvSumLink, "serversummary", VbrLocalizationHelper.NavSrvSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobSumLink, "jobsummary", VbrLocalizationHelper.NavJobSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavMissingJobLink, "missingjobs", VbrLocalizationHelper.NavMissingDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavProtWrkld, "protectedworkloads", VbrLocalizationHelper.NavProtWkldDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSrvInfoLink, "managedServerInfo", VbrLocalizationHelper.NavSrvInfoDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavRegKeyLink, "regkeys", VbrLocalizationHelper.NavRegKeyDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavProxyInfoLink, "proxies", VbrLocalizationHelper.NavProxyDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSobrInfoLink, "sobr", VbrLocalizationHelper.NavSobrDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSobrExtLink, "extents", VbrLocalizationHelper.NavSobrExtDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavRepoInfoLink, "repos", VbrLocalizationHelper.NavRepoDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobConLink, "jobcon", VbrLocalizationHelper.NavJobConDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavTaskConLink, "taskcon", VbrLocalizationHelper.NavTaskConDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobSessSumLink, "jobsesssum", VbrLocalizationHelper.NavJobSessSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobInfoLink, "jobs", VbrLocalizationHelper.NavJobInfoDeet);
        }

        public string MakeSecurityNavTable()
        {
            return // _form.FormNavRows(ResourceHandler.NavLicInfoLink, "license", ResourceHandler.NavLicInfoDetail) +
                this.form.FormNavRows(VbrLocalizationHelper.NavBkpSrvLink, "vbrserver", VbrLocalizationHelper.NavBkpSrvDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSecSumLink, "secsummary", VbrLocalizationHelper.NavSecSumDeet) +

                // _form.FormNavRows(ResourceHandler.NavSrvSumLink, "serversummary", ResourceHandler.NavSrvSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobSumLink, "jobsummary", VbrLocalizationHelper.NavJobSumDeet) +

                // _form.FormNavRows(ResourceHandler.NavMissingJobLink, "missingjobs", ResourceHandler.NavMissingDeet) +
                // _form.FormNavRows(ResourceHandler.NavProtWrkld, "protectedworkloads", ResourceHandler.NavProtWkldDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSrvInfoLink, "managedServerInfo", VbrLocalizationHelper.NavSrvInfoDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavRegKeyLink, "regkeys", VbrLocalizationHelper.NavRegKeyDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavProxyInfoLink, "proxies", VbrLocalizationHelper.NavProxyDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSobrInfoLink, "sobr", VbrLocalizationHelper.NavSobrDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavSobrExtLink, "extents", VbrLocalizationHelper.NavSobrExtDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavRepoInfoLink, "repos", VbrLocalizationHelper.NavRepoDeet) +

                // _form.FormNavRows(ResourceHandler.NavJobConLink, "jobcon", ResourceHandler.NavJobConDeet) +
                // _form.FormNavRows(ResourceHandler.NavTaskConLink, "taskcon", ResourceHandler.NavTaskConDeet) +
                // _form.FormNavRows(ResourceHandler.NavJobSessSumLink, "jobsesssum", ResourceHandler.NavJobSessSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobInfoLink, "jobs", VbrLocalizationHelper.NavJobInfoDeet);
        }

        public string LicTable(bool scrub)
        {
            string s = this.form.SectionStart("license", VbrLocalizationHelper.LicTableHeader);
            string summary = this.sum.LicSum();

            s += this.form.TableHeader(VbrLocalizationHelper.LicTblLicTo, string.Empty) +
            this.form.TableHeader(VbrLocalizationHelper.LicTblEdition, VbrLocalizationHelper.LtEdTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblStatus, VbrLocalizationHelper.LtStatusTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblType, VbrLocalizationHelper.LtTypeTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblLicInst, VbrLocalizationHelper.LtInstLicTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblUsedInst, VbrLocalizationHelper.LtInstUsedTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblNewInst, VbrLocalizationHelper.LtInstNewTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblRentInst, VbrLocalizationHelper.LtInstRentalTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblLicSock, VbrLocalizationHelper.LtSocLicTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblUsedSock, VbrLocalizationHelper.LtSocUsedTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblLicNas, VbrLocalizationHelper.LtNasLicTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblUsedNas, VbrLocalizationHelper.LtNasUsedTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblExpDate, VbrLocalizationHelper.LicExpTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblSupExpDate, VbrLocalizationHelper.LicSupExpTT) +
                this.form.TableHeader(VbrLocalizationHelper.LicTblCc, VbrLocalizationHelper.LicCcTT) +
                "</tr>";
            s += "</thead><tbody>";
            try
            {
                CCsvParser csv = new();
                var lic = csv.GetDynamicLicenseCsv();

                // init / clear json license collection
                if (CGlobals.FullReportJson == null)
                {
                    CGlobals.FullReportJson = new();
                }

                CGlobals.FullReportJson.Licenses.Clear();

                foreach (var l in lic)
                {

                    s += "<tr>";
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(l.licensedto, ScrubItemType.Item), string.Empty);
                    }

                    if (!scrub)
                    {
                        s += this.form.TableData(l.licensedto, string.Empty);
                    }

                    s += this.form.TableData(l.edition, string.Empty);
                    s += this.form.TableData(l.status, string.Empty);
                    s += this.form.TableData(l.type, string.Empty);
                    s += this.form.TableData(l.licensedinstances, string.Empty);
                    s += this.form.TableData(l.usedinstances, string.Empty);
                    s += this.form.TableData(l.newinstances, string.Empty);
                    s += this.form.TableData(l.rentalinstances, string.Empty);
                    s += this.form.TableData(l.licensedsockets, string.Empty);
                    s += this.form.TableData(l.usedsockets, string.Empty);
                    s += this.form.TableData(l.licensedcapacitytb, string.Empty);
                    s += this.form.TableData(l.usedcapacitytb, string.Empty);
                    s += this.form.TableData(l.expirationdate, string.Empty);
                    s += this.form.TableData(l.supportexpirationdate, string.Empty);
                    s += this.form.TableData(l.cloudconnect, string.Empty);
                    s += "</tr>";

                    // add to json
                    try
                    {
                        CGlobals.FullReportJson.Licenses.Add(new License
                        {
                            LicensedTo = scrub ? this.scrub.ScrubItem(l.licensedto, ScrubItemType.Item) : l.licensedto,
                            Edition = l.edition,
                            Status = l.status,
                            Type = l.type,
                            LicensedInstances = l.licensedinstances,
                            UsedInstances = l.usedinstances,
                            NewInstances = l.newinstances,
                            RentalInstances = l.rentalinstances,
                            LicensedSockets = l.licensedsockets,
                            UsedSockets = l.usedsockets,
                            LicensedNas = l.licensedcapacitytb,
                            UsedNas = l.usedcapacitytb,
                            ExpirationDate = l.expirationdate,
                            SupportExpirationDate = l.supportexpirationdate,
                            CloudConnect = l.cloudconnect,
                        });
                    }
                    catch (Exception exRow)
                    {
                        this.log.Error("Failed to add license JSON row: " + exRow.Message);
                    }
                }
            }
            catch (Exception e)
            {
                this.log.Error("License Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            return s;
        }

        // helper to set a generic section in JSON aggregation
        private static void SetSection(string key, List<string> headers, List<List<string>> rows, string summary)
        {
            if (CGlobals.FullReportJson == null)
            {
                CGlobals.FullReportJson = new();
            }

            CGlobals.FullReportJson.Sections[key] = new HtmlSection
            {
                SectionName = key,
                Headers = headers,
                Rows = rows,

                // Summary = summary,
            };
        }

        private static string WriteTupleListToHtml(List<Tuple<string, string>> list)
        {
            string headers = string.Empty;
            string data = string.Empty;
            string s = string.Empty;
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

        private static string AddBackupServerDetails(BackupServer b)
        {
            CVbrServerTable t = new(b);
            List<Tuple<string, string>> vbrServerData = new(); // t.VbrFullTables();

            if (!CGlobals.RunSecReport)
            {
                vbrServerData = t.VbrFullTables();
            }
            else
            {
                vbrServerData = t.VbrSecurityTables();
            }

            return WriteTupleListToHtml(vbrServerData);
        }

        private string ConfigDbTable(BackupServer b)
        {
            string s = string.Empty;
            s += this.form.header3("Config DB Info");
            s += this.form.Table();

            // config DB Table
            s += this.form.TableHeader("DataBase Type", "MS SQL or PostgreSQL");
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlLocal, VbrLocalizationHelper.BstSqlLocTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlName, VbrLocalizationHelper.BstSqlNameTT);
            if (b.DbType == CGlobals.SqlTypeName)
            {
                s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlVersion, VbrLocalizationHelper.BstSqlVerTT);
                s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlEdition, VbrLocalizationHelper.BstSqlEdTT);
            }

            if (b.DbType == CGlobals.SqlTypeName && b.IsLocal == false)
            {
                s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlCores, VbrLocalizationHelper.BstSqlCpuTT);
                s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblSqlRam, VbrLocalizationHelper.BstSqlRamTT);
            }

            s += this.form.TableHeaderEnd();

            // CDataFormer cd = new(true);
            try
            {

                s += this.form.TableBodyStart();
                s += "<tr>";
                s += this.form.TableData(b.DbType, string.Empty);
                if (b.IsLocal)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                s += this.form.TableData(b.DbHostName, string.Empty);
                if (b.DbType == CGlobals.SqlTypeName)
                {
                    s += this.form.TableData(b.DbVersion, string.Empty);
                    s += this.form.TableData(b.Edition, string.Empty);
                }

                if (b.DbType == CGlobals.SqlTypeName && b.IsLocal == false)
                {
                    s += this.AddDbCoresRam(b);
                }

                s += this.form.EndTable();
            }
            catch (Exception e)
            {
                this.log.Error("Failed to add backup server table. Error:");
                this.log.Error("\t" + e.Message);

                // return "";
            }

            return s;
        }

        private string AddDbCoresRam(BackupServer b)
        {
            string s = string.Empty;
            string dbCoresToolTip = "CPU Cores detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.";
            string dbRamToolTip = "RAM detected on SQL. 0 indicates SQL is local to VBR or there was an error in collection.";
            if (b.DbCores == 0)
            {
                s += this.form.TableData(string.Empty, dbCoresToolTip);
            }
            else
            {
                s += this.form.TableData(b.DbCores.ToString(), dbCoresToolTip);
            }

            if (b.DbRAM == 0)
            {
                s += this.form.TableData(string.Empty, dbRamToolTip);
            }
            else
            {
                s += this.form.TableData(b.DbRAM.ToString(), dbRamToolTip);
            }

            return s;
        }

        public string AddBkpSrvTable(bool scrub)
        {
            string s = this.form.SectionStart("vbrserver", VbrLocalizationHelper.BkpSrvTblHead);
            string summary = this.sum.SetVbrSummary();

            // CDataFormer cd = new(true);
            BackupServer b = this.df.BackupServerInfoToXml(scrub);
            if (String.IsNullOrEmpty(b.Version))
            {
                b.Version = CGlobals.VBRFULLVERSION;
            }

            // test area
            s += AddBackupServerDetails(b);
            s += this.form.EndTable();

            s += this.form.header3("Config Backup Info");
            s += "<table border=\"1\" class=\"content-table\">";
            s += "<tr>";
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEnabled, VbrLocalizationHelper.BstCfgEnabledTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgLastRes, VbrLocalizationHelper.BstCfgLastResTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEncrypt, VbrLocalizationHelper.BstCfgEncTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblTarget, VbrLocalizationHelper.BstCfgTarTT);
            s += "</tr>";
            s += this.form.TableBodyStart();
            s += "<tr>";
            if (b.ConfigBackupEnabled)
            {
                s += this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                s += this.form.TableData(this.form.False, string.Empty);
            }

            s += this.form.TableData(b.ConfigBackupLastResult, string.Empty);
            if (b.ConfigBackupEncryption)
            {
                s += this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                s += this.form.TableData(this.form.False, string.Empty);
            }

            s += this.form.TableData(b.ConfigBackupTarget, string.Empty);
            s += "</tr>";
            s += this.form.EndTable();

            // s += _form.LineBreak();
            s += this.ConfigDbTable(b);

            // if (CGlobals.RunSecReport)
            //    s += InstalledAppsTable();
            s += this.form.SectionEnd(summary);

            // JSON section capture (backup server core info)
            try
            {
                List<string> headers = new() { "Version", "DbType", "DbHost", "ConfigBackupEnabled", "ConfigBackupLastResult", "ConfigBackupEncryption", "ConfigBackupTarget" };
                List<List<string>> rows = new()
                {
                    new List<string>
                    {
                        b.Version,
                        b.DbType,
                        b.DbHostName,
                        b.ConfigBackupEnabled ? "True" : "False",
                        b.ConfigBackupLastResult,
                        b.ConfigBackupEncryption ? "True" : "False",
                        b.ConfigBackupTarget,
                    },
                };
                SetSection("backupServer", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture backupServer JSON section: " + ex.Message);
            }

            return s;

        }

        private string InstalledAppsTable()
        {
            string s = string.Empty;

            // Table & Header
            // s += _form.header3("Installed Applications");
            s += this.form.Table();
            s += "<tr>";

            // header
            s += this.form.TableHeader("Installed Apps", string.Empty);
            s += this.form.TableHeaderEnd();
            s += this.form.TableData("See <a href=\"C:\\\\temp\\\\vHC\\\\Original\\\\Log\\\\\">Veeam.HealthCheck.ServerApplications log file</a> at C:\\temp\\vHC\\Log\\", string.Empty);
            s += this.form.EndTable();
            return s;
        }

        public string AddSecSummaryTable(bool scrub)
        {
            CGlobals.Scrub = scrub;

            string s = this.form.SectionStart("secsummary", VbrLocalizationHelper.SSTitle);
            string summary = this.sum.SecSum();

            s += this.form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0) +
                this.form.TableHeader(VbrLocalizationHelper.SSHdr1, VbrLocalizationHelper.SSHdrTT1) +
                this.form.TableHeader(VbrLocalizationHelper.SSHdr2, VbrLocalizationHelper.SSHdrTT2) +
                this.form.TableHeader(VbrLocalizationHelper.SSHdr3, VbrLocalizationHelper.SSHdrTT3) +
                this.form.TableHeader("MFA Enabled", "Is MFA enabled for console access to VBR");
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            s += "<tr>";

            try
            {
                // table data
                CSecuritySummaryTable t = this.df.SecSummary();

                if (t.ImmutabilityEnabled)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                if (t.TrafficEncrptionEnabled)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                if (t.BackupFileEncrptionEnabled)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                if (t.ConfigBackupEncrptionEnabled)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                if (t.MFAEnabled)
                {
                    s += this.form.TableData(this.form.True, string.Empty);
                }
                else
                {
                    s += this.form.TableData(this.form.False, string.Empty);
                }

                s += "</tr>";

                // s += _form.TableData((t.ImmutabilityEnabled : _form.True ? _form.False), "");
                s += this.form.EndTable();
            }

            catch (Exception e)
            {
                this.log.Error("Security Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
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
                    this.log.Error("Malware Settings Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
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
                    this.log.Error("Security Compliance Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
                }
            }

            s += this.form.SectionEnd(summary);

            // JSON section capture security summary
            try
            {
                var t = this.df.SecSummary();
                List<string> headers = new() { "ImmutabilityEnabled", "TrafficEncryptionEnabled", "BackupFileEncryptionEnabled", "ConfigBackupEncryptionEnabled", "MFAEnabled" };
                List<List<string>> rows = new()
                {
                    new List<string>
                    {
                        t.ImmutabilityEnabled ? "True" : "False",
                        t.TrafficEncrptionEnabled ? "True" : "False",
                        t.BackupFileEncrptionEnabled ? "True" : "False",
                        t.ConfigBackupEncrptionEnabled ? "True" : "False",
                        t.MFAEnabled ? "True" : "False"
                    },
                };
                SetSection("securitySummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture securitySummary JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddSecurityReportSecuritySummaryTable()
        {
            string s = this.form.SectionStart("secsummary", VbrLocalizationHelper.SSTitle);
            string summary = this.sum.SecSum();

            // s += AddSecuritySummaryDetails();

            // Tables!

            /* 1. components
             * 2. repositories
             * 3. accounts & perms
             * 4. encryption
             * 5. other
             * 6. NAS Specific
             * 7. Orchestrated recovery
             */

            // 1. Components
            s += this.AddTable("Backup Server", this.helper.AddSecurityServerInfo());
            s += this.AddTable("Immutability & Encryption", this.helper.AddSecuritySummaryDetails());

            s += this.AddTable("Immutability", string.Empty);
            s += this.AddTable("Config Backup", this.helper.AddConfigBackupDetails());

            try
            {
                s += this.AddTable("Detected OS", this.helper.CollectedOsInfo());

            }
            catch (Exception e)
            {
                this.log.Error("Failed to add OS info to HTML report", false);
                this.log.Error(e.Message, false);
            }

            s += this.AddTable("Installed Applications", this.InstalledAppsTable());

            // s += _form.Table();
            // s += _form.TableHeader("Found Operating Systems", "");
            // s += "</tr><tr>";
            // foreach(var v in _helper.CollectedOsInfo())
            // {
            //    s += "<tr>" + v + "</tr>";
            // }
            // s += _form.EndTable();
            s += this.form.SectionEnd(summary);

            return s;
        }

        private string AddTable(string title, string data)
        {
            string s = string.Empty;
            s += this.form.header3(title);
            s += this.form.Table();
            s += data;
            s += this.form.EndTable();

            return s;
        }

        private string AddTable(string title, List<string> data)
        {
            string s = "<tr>";
            s += this.form.header3(title);
            s += this.form.Table();
            s += this.form.TableHeader("Detected Operating Systems", string.Empty);
            s += "</tr>";
            s += this.form.TableBodyStart();
            foreach (var d in data)
            {
                if (!String.IsNullOrEmpty(d))
                {
                    s += "<tr>";
                    s += this.form.TableData(d, string.Empty);
                    s += "</tr>";
                }

            }

            s += this.form.EndTable();

            return s;
        }

        public string AddSrvSummaryTable(bool scrub)
        {
            string summary = this.sum.SrvSum();

            string s = this.form.SectionStart("serversummary", VbrLocalizationHelper.MssTitle);

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.MssHdr1, VbrLocalizationHelper.MssHdr1TT) +
                            this.form.TableHeader(VbrLocalizationHelper.MssHdr2, VbrLocalizationHelper.MssHdr2TT) +
                            "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                Dictionary<string, int> list = this.df.ServerSummaryToXml();

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += this.form.TableDataLeftAligned(d.Key, string.Empty);
                    s += this.form.TableData(d.Value.ToString(), string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Server Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON capture server summary
            try
            {
                var list = this.df.ServerSummaryToXml();
                List<string> headers = new() { "ServerType", "Count" };
                List<List<string>> rows = list.Select(d => new List<string> { d.Key, d.Value.ToString() }).ToList();
                SetSection("serverSummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture serverSummary JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddJobSummaryTable(bool scrub)
        {
            string summary = this.sum.JobSummary();
            string s = this.form.SectionStart("jobsummary", VbrLocalizationHelper.JobSumTitle);

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, VbrLocalizationHelper.JobSum0TT, 0) +
                this.form.TableHeader(VbrLocalizationHelper.JobSum1, VbrLocalizationHelper.JobSum1TT, 1) +
                "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                CJobSummaryTable st = new();

                Dictionary<string, int> list = st.JobSummaryTable();

                int totalJobs = list.Sum(x => x.Value);

                // foreach (var c in list)
                // {
                //    totalJobs += c.Value;
                // }
                foreach (var d in list)
                {
                    if (d.Value == 0)
                    {
                        continue;
                    }

                    s += "<tr>";
                    s += this.form.TableDataLeftAligned(d.Key, string.Empty);
                    s += this.form.TableData(d.Value.ToString(), string.Empty);
                    s += "</tr>";
                }

                s += "<tr>";
                s += this.form.TableDataLeftAligned("<b>Total Jobs", string.Empty);
                s += this.form.TableData(totalJobs.ToString() + "</b>", string.Empty);
                s += "</tr>";
            }
            catch (Exception e)
            {
                this.log.Error("Job Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON job summary
            try
            {
                CJobSummaryTable st = new();
                var list = st.JobSummaryTable();
                int totalJobs = list.Sum(x => x.Value);
                List<string> headers = new() { "JobType", "Count" };
                List<List<string>> rows = list.Where(d => d.Value > 0).Select(d => new List<string> { d.Key, d.Value.ToString() }).ToList();
                rows.Add(new List<string> { "Total Jobs", totalJobs.ToString() });
                SetSection("jobSummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobSummary JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddMissingJobsTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("missingjobs", VbrLocalizationHelper.NpTitle, VbrLocalizationHelper.NpButton);

            string summary = this.sum.MissingJobsSUmmary();

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, string.Empty) +

                // _form.TableHeader("Count", "Total detected of this type") +
                "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                // List<string> list = _df.ParseNonProtectedTypes();
                CJobSummaryTable st = new();
                Dictionary<string, int> types = st.JobSummaryTable();

                foreach (var t in types)
                {
                    if (t.Value == 0)
                    {
                        s += "<tr>";
                        s += this.form.TableDataLeftAligned(t.Key, string.Empty);
                        s += "</tr>";
                    }
                }

                // for (int i = 0; i < list.Count(); i++)
                // {
                //    s += "<tr>";

                // s += _form.TableData(list[i], "");
                //    s += "</tr>";

                // }
            }
            catch (Exception e)
            {
                this.log.Error("Missing Jobs Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON missing jobs
            try
            {
                CJobSummaryTable st = new();
                Dictionary<string, int> types = st.JobSummaryTable();
                List<string> headers = new() { "MissingJobType" };
                List<List<string>> rows = types.Where(t => t.Value == 0).Select(t => new List<string> { t.Key }).ToList();
                SetSection("missingJobs", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture missingJobs JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddProtectedWorkLoadsTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("protectedworkloads", VbrLocalizationHelper.PlTitle, VbrLocalizationHelper.PlButton);
            string summary = this.sum.ProtectedWorkloads();
            try
            {
                this.df.ProtectedWorkloadsToXml();

                // vi table
                s += "<h3>VMware Backups</h3>";
                s += this.form.Table();
                s += "<tr>" +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr0, VbrLocalizationHelper.PlHdrTT0) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr1, VbrLocalizationHelper.PlHdrTT1) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr2, VbrLocalizationHelper.PlHdrTT2) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr3, VbrLocalizationHelper.PlHdrTT3);
                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();
                s += "<tr>";
                s += this.form.TableData((this.df._viProtectedNames.Distinct().Count() + this.df._viNotProtectedNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df._viProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df._viNotProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df._viDupes.ToString(), string.Empty);
                s += "</tr>";
                s += "</table>";

                // hv
                s += "<h3>HV Backups</h3>";
                s += this.form.Table();

                // hv table
                s += "<tr>";
                s += this.form.TableHeader("HV Total", "Total HV VMs found in environment");
                s += this.form.TableHeader("HV Protected", "Total HV VMs found with existing backup");
                s += this.form.TableHeader("HV Unprotected", "Total HV VMs found without backup");
                s += this.form.TableHeader("HV Duplicates", "Total HV VMs potentially found in multiple backups");
                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();
                s += "<tr>";
                s += this.form.TableData((this.df._hvProtectedNames.Distinct().Count() + this.df._hvNotProtectedNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df._hvProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df._hvNotProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df._hvDupes.ToString(), string.Empty);
                s += "</tr></table>";

                // phys
                s += "<h3>Physical Backups</h3>";
                s += this.form.Table();
                s += "<tr>";
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr4, VbrLocalizationHelper.PlHdrTT4);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr5, VbrLocalizationHelper.PlHdrTT5);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr6, VbrLocalizationHelper.PlHdrTT6);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr7, VbrLocalizationHelper.PlHdrTT7);

                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();

                // CDataFormer cd = new(true);
                s += "<tr>";
                s += this.form.TableData(this.df._vmProtectedByPhys.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData((this.df._physNotProtNames.Distinct().Count() + this.df._physProtNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df._physProtNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df._physNotProtNames.Distinct().Count().ToString(), string.Empty);
                s += "</tr>";
                s += "</table>";

                try
                {
                    this.log.Info("Adding NAS table to HTML report", false);
                    CProtectedWorkloads cProtectedWorkloads = new();
                    NasSourceInfo n = new();

                    cProtectedWorkloads.nasWorkloads = n.NasTable().nasWorkloads;

                    s += "<h3>NAS Backups</h3>";
                    if (cProtectedWorkloads.nasWorkloads.Count() == 0)
                    {
                        if (CGlobals.REMOTEEXEC)
                        {
                            this.log.Info("No NAS Workloads detected. This may be due to remote execution mode limitations.", false);
                            s += "<p>No NAS Workloads detected. This may be due to remote execution mode limitations.</p>";
                        }
                        else
                        {
                            this.log.Info("No NAS Workloads detected.", false);
                            s += "<p>No NAS Workloads detected</p>";
                        }
                    }
                    else
                    {
                        s += "<div id=\"nasTable\" border=\"1\" class=\"content-table\"></div>";
                        s += this.form.Table();
                        s += "<tr>";
                        s += this.form.TableHeader("File Share Types", "Total File Share Types found in environment");
                        s += this.form.TableHeader("Total Share Size", "Total size of all shares found in environment");
                        s += this.form.TableHeader("Total Files Count", "Total files found in all shares");
                        s += this.form.TableHeader("Total Folders Count", "Total folders found in all shares");
                        s += this.form.TableHeaderEnd();
                        s += this.form.TableBodyStart();

                        foreach (var load in cProtectedWorkloads.nasWorkloads)
                        {
                            s += "<tr>";
                            s += this.form.TableData(load.FileShareType, string.Empty);
                            s += this.form.TableData(load.TotalShareSize, string.Empty);
                            s += this.form.TableData(load.TotalFilesCount.ToString(), string.Empty);
                            s += this.form.TableData(load.TotalFoldersCount.ToString(), string.Empty);
                            s += "</tr>";
                        }

                        s += this.form.EndTable();
                    }

                }
                catch (Exception e)
                {
                    this.log.Error("Failed to add NAS table to HTML report", false);
                    this.log.Error(e.Message, false);
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
                    s += this.form.Table();
                    s += "<tr>";
                    s += this.form.TableHeader("Tenant Count:", "Number of tenants backed up by this backup server");
                    s += this.form.TableHeaderEnd();
                    s += this.form.TableBodyStart();

                    s += "<tr>";
                    s += this.form.TableData(cProtectedWorkloads.entraWorkloads.Count.ToString(), string.Empty);
                    s += "</tr>";
                    s += this.form.EndTable();

                    // Table for Entra Tenants
                    s += "<div id=\"entraTable\" border=\"1\" class=\"content-table\"></div>";
                    s += this.form.Table();
                    s += "<tr>";
                    s += this.form.TableHeader("Tenant Name", "Name of the Entra ID Tenant being backed up.");
                    s += this.form.TableHeader("Cache Repo", "Cache Repo selected for the tentant");
                    s += this.form.TableHeaderEnd();
                    s += this.form.TableBodyStart();

                    if (cProtectedWorkloads.entraWorkloads.Count == 0)
                    {
                        s += "<tr>";
                        s += this.form.TableData(string.Empty, string.Empty);
                        s += this.form.TableData(string.Empty, string.Empty);
                        s += "</tr>";
                    }

                    foreach (var load in cProtectedWorkloads.entraWorkloads)
                    {
                        s += "<tr>";
                        s += this.form.TableData(load.TenantName, string.Empty);
                        s += this.form.TableData(load.CacheRepoName, string.Empty);
                        s += "</tr>";
                    }

                    s += this.form.EndTable();
                }
                catch (Exception ex)
                {

                }

                s += this.form._endDiv;
            }
            catch (Exception e)
            {
                this.log.Error("Protected Servers Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON protected workloads
            try
            {
                // VMware stats
                int viTotal = this.df._viProtectedNames.Distinct().Count() + this.df._viNotProtectedNames.Distinct().Count();
                int viProtected = this.df._viProtectedNames.Distinct().Count();
                int viUnprotected = this.df._viNotProtectedNames.Distinct().Count();
                int viDupes = this.df._viDupes;

                // HV stats
                int hvTotal = this.df._hvProtectedNames.Distinct().Count() + this.df._hvNotProtectedNames.Distinct().Count();
                int hvProtected = this.df._hvProtectedNames.Distinct().Count();
                int hvUnprotected = this.df._hvNotProtectedNames.Distinct().Count();
                int hvDupes = this.df._hvDupes;

                // Physical stats
                int physVmProtectedByPhys = this.df._vmProtectedByPhys.Distinct().Count();
                int physTotal = this.df._physNotProtNames.Distinct().Count() + this.df._physProtNames.Distinct().Count();
                int physProtected = this.df._physProtNames.Distinct().Count();
                int physUnprotected = this.df._physNotProtNames.Distinct().Count();

                List<string> headers = new() { "WorkloadType", "Total", "Protected", "Unprotected", "Duplicates" };
                List<List<string>> rows = new()
                {
                    new List<string> { "VMware", viTotal.ToString(), viProtected.ToString(), viUnprotected.ToString(), viDupes.ToString() },
                    new List<string> { "Hyper-V", hvTotal.ToString(), hvProtected.ToString(), hvUnprotected.ToString(), hvDupes.ToString() },
                    new List<string> { "Physical", physTotal.ToString(), physProtected.ToString(), physUnprotected.ToString(), "N/A" },
                    new List<string> { "PhysicalVMsProtected", physVmProtectedByPhys.ToString(), "N/A", "N/A", "N/A" },
                };

                // Add NAS & Entra if available
                try
                {
                    CProtectedWorkloads cProtectedWorkloads = new();
                    NasSourceInfo n = new();
                    cProtectedWorkloads.nasWorkloads = n.NasTable().nasWorkloads;

                    foreach (var load in cProtectedWorkloads.nasWorkloads)
                    {
                        rows.Add(new List<string> { $"NAS-{load.FileShareType}", load.TotalShareSize, load.TotalFilesCount.ToString(), load.TotalFoldersCount.ToString(), "N/A" });
                    }
                }
                catch { }

                try
                {
                    CProtectedWorkloads cProtectedWorkloads = new();
                    CEntraTenants n = new();
                    cProtectedWorkloads.entraWorkloads = n.EntraTable().entraWorkloads;

                    foreach (var load in cProtectedWorkloads.entraWorkloads)
                    {
                        rows.Add(new List<string> { $"Entra-{load.TenantName}", load.CacheRepoName, "N/A", "N/A", "N/A" });
                    }
                }
                catch { }

                SetSection("protectedWorkloads", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture protectedWorkloads JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddManagedServersTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("managedServerInfo", VbrLocalizationHelper.ManSrvTitle, VbrLocalizationHelper.ManSrvBtn);
            string summary = this.sum.ManagedServers();
            s +=
           this.form.TableHeader(VbrLocalizationHelper.ManSrv0, VbrLocalizationHelper.ManSrv0TT, 0) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv1, VbrLocalizationHelper.ManSrv1TT, 1) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv2, VbrLocalizationHelper.ManSrv2TT, 2) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv3, VbrLocalizationHelper.ManSrv3TT, 3) +
           this.form.TableHeader("OS Info", VbrLocalizationHelper.ManSrv3TT, 4) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv4, VbrLocalizationHelper.ManSrv4TT, 5) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv5, VbrLocalizationHelper.ManSrv5TT, 6) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv6, VbrLocalizationHelper.ManSrv6TT, 7) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv7, VbrLocalizationHelper.ManSrv7TT, 8) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv8, VbrLocalizationHelper.ManSrv8TT, 9) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv9, VbrLocalizationHelper.ManSrv9TT, 10) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv10, VbrLocalizationHelper.ManSrv10TT, 11) +
           this.form.TableHeader(VbrLocalizationHelper.ManSrv11, VbrLocalizationHelper.ManSrv11TT, 12);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                List<CManagedServer> list = this.df.ServerXmlFromCsv(scrub);

                foreach (var d in list)
                {

                    s += "<tr>";

                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.Cores.ToString(), string.Empty);
                    s += this.form.TableData(d.Ram.ToString(), string.Empty);
                    s += this.form.TableData(d.Type, string.Empty);
                    s += this.form.TableData(d.OsInfo, string.Empty);
                    s += this.form.TableData(d.ApiVersion, string.Empty);
                    s += this.form.TableData(d.ProtectedVms.ToString(), string.Empty);
                    s += this.form.TableData(d.NotProtectedVms.ToString(), string.Empty);
                    s += this.form.TableData(d.TotalVms.ToString(), string.Empty);
                    if (d.IsProxy)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsRepo)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsWan)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.IsUnavailable.ToString(), string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Managed Server Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON managed servers
            try
            {
                var list = this.df.ServerXmlFromCsv(scrub);
                List<string> headers = new() { "Name", "Cores", "Ram", "Type", "OsInfo", "ApiVersion", "ProtectedVms", "NotProtectedVms", "TotalVms", "IsProxy", "IsRepo", "IsWan", "IsUnavailable" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.Cores.ToString(),
                    d.Ram.ToString(),
                    d.Type,
                    d.OsInfo,
                    d.ApiVersion,
                    d.ProtectedVms.ToString(),
                    d.NotProtectedVms.ToString(),
                    d.TotalVms.ToString(),
                    d.IsProxy ? "True" : "False",
                    d.IsRepo ? "True" : "False",
                    d.IsWan ? "True" : "False",
                    d.IsUnavailable.ToString(),
                }).ToList();
                SetSection("managedServers", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture managedServers JSON section: " + ex.Message);
            }

            return s;
        }

        public static string SerializeToJson(object obj)
        {
            return JsonSerializer.Serialize(obj);
        }

        public string AddRegKeysTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("regkeys", VbrLocalizationHelper.RegTitle, VbrLocalizationHelper.RegBtn);
            string summary = this.sum.RegKeys();

            try
            {

                Dictionary<string, string> list = this.df.RegOptions();
                if (list.Count == 0)
                {
                    if (CGlobals.REMOTEEXEC) // remote exec does not support registry and VBR could be linux based without regsitry
                    {
                        s += "<p>Registry key collection not supported in remote execution mode</p>";
                    }
                    else
                    {
                        s += "<p>No modified registry keys found</p>";
                    }
                }
                else
                {
                    s += "<tr>" +
                    this.form.TableHeader(VbrLocalizationHelper.Reg0, VbrLocalizationHelper.Reg0TT) +
                    this.form.TableHeader(VbrLocalizationHelper.Reg1, VbrLocalizationHelper.Reg1TT) +
                    "</tr>";
                    s += this.form.TableHeaderEnd();
                    s += this.form.TableBodyStart();
                    s += "<tr>";
                    foreach (var d in list)
                    {
                        s += "<tr>";
                        s += this.form.TableData(d.Key, string.Empty);
                        s += this.form.TableData(d.Value.ToString(), string.Empty);
                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                this.log.Error("Registry Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON reg keys
            try
            {
                var list = this.df.RegOptions();
                List<string> headers = new() { "Key", "Value" };
                List<List<string>> rows = list.Select(kv => new List<string> { kv.Key, kv.Value }).ToList();
                SetSection("regKeys", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture regKeys JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddProxyTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = this.sum.Proxies();
            s += "<tr>" +
           this.form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT) +
   "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                List<string[]> list = this.df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {

                    var prov = d[12];
                    int shade = 0;
                    if (prov == "under")
                    {
                        shade = 2;
                    }

                    if (prov == "over")
                    {
                        shade = 1;
                    }

                    s += "<tr>";
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[0], ScrubItemType.Server), string.Empty); // server name
                    }
                    else
                    {
                        s += this.form.TableData(d[0], string.Empty);
                    }

                    s += this.form.TableData(d[1], string.Empty);
                    s += this.form.TableData(d[2], string.Empty, shade);
                    s += this.form.TableData(d[3], string.Empty);
                    s += this.form.TableData(d[4], string.Empty);
                    s += this.form.TableData(d[5], string.Empty);

                    // s += _form.TableData(d[6], "");
                    if (d[6] == "True")
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d[7], string.Empty);
                    s += this.form.TableData(d[8], string.Empty);
                    s += this.form.TableData(d[9], string.Empty);
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[10], ScrubItemType.Server), string.Empty); // host
                    }
                    else
                    {
                        s += this.form.TableData(d[10], string.Empty);
                    }

                    if (d[11] == "True")
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("PROXY Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON proxies
            try
            {
                var list = this.df.ProxyXmlFromCsv(scrub);
                List<string> headers = new() { "Name", "Type", "Tasks", "Cores", "Ram", "IsOnHost", "TransportMode", "NetBufferSize", "MaxConcurrentJobs", "Host", "IsHvOffload" };

                // mapping indices from original array
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d[0], d[1], d[2], d[3], d[4], d[6], d[7], d[8], d[9], d[10], d[11],
                }).ToList();
                SetSection("proxies", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture proxies JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddMultiRoleTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = this.sum.Proxies();
            s += "<tr>" +
           this.form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT) +
           this.form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT) +
   "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                List<string[]> list = this.df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[0], ScrubItemType.Server), string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(d[0], string.Empty);
                    }

                    s += this.form.TableData(d[1], string.Empty);
                    s += this.form.TableData(d[2], string.Empty);
                    s += this.form.TableData(d[3], string.Empty);
                    s += this.form.TableData(d[4], string.Empty);
                    s += this.form.TableData(d[5], string.Empty);
                    s += this.form.TableData(d[6], string.Empty);
                    s += this.form.TableData(d[7], string.Empty);
                    s += this.form.TableData(d[8], string.Empty);
                    s += this.form.TableData(d[9], string.Empty);
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[10], ScrubItemType.Server), string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(d[10], string.Empty);
                    }

                    s += this.form.TableData(d[11], string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("PROXY Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            return s;
        }

        public string AddSobrTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("sobr", VbrLocalizationHelper.SbrTitle, VbrLocalizationHelper.SbrBtn);
            string summary = this.sum.Sobr();
            s += "<tr>" +
           this.form.TableHeader(VbrLocalizationHelper.Sbr0, VbrLocalizationHelper.Sbr0TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr1, VbrLocalizationHelper.Sbr1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo1TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr2, VbrLocalizationHelper.Sbr2TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr3, VbrLocalizationHelper.Sbr3TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr4, VbrLocalizationHelper.Sbr4TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr5, VbrLocalizationHelper.Sbr5TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr6, VbrLocalizationHelper.Sbr6TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr7, VbrLocalizationHelper.Sbr7TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr8, VbrLocalizationHelper.Sbr8TT) +
           this.form.TableHeader("CapTier Immutable", VbrLocalizationHelper.Sbr9TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr10, VbrLocalizationHelper.Sbr10TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr11, VbrLocalizationHelper.Sbr11TT) +
           this.form.TableHeader(VbrLocalizationHelper.Sbr12, VbrLocalizationHelper.Sbr12TT) +
           "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            // CDataFormer cd = new(true);
            try
            {
                this.log.Info("Attempting to load SOBR data...");
                List<CSobrTypeInfos> list = this.df.SobrInfoToXml(scrub);

                if (list == null || list.Count == 0)
                {
                    this.log.Warning("No SOBR data found. The SOBRs CSV file may be missing or empty.");
                    this.log.Info("This could indicate: 1) No SOBRs configured, 2) Collection script failed, or 3) CSV file not found");
                }

                foreach (var d in list)
                {

                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.ExtentCount.ToString(), string.Empty);
                    s += this.form.TableData(d.JobCount.ToString(), string.Empty);
                    s += this.form.TableData(d.PolicyType, string.Empty);
                    if (d.EnableCapacityTier)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.CapacityTierCopyPolicyEnabled)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.CapacityTierMovePolicyEnabled)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.ArchiveTierEnabled)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.UsePerVMBackupFiles)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.CapTierType, string.Empty);
                    if (d.ImmuteEnabled)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.ImmutePeriod, string.Empty);
                    if (d.SizeLimitEnabled)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.SizeLimit, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("SOBR Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON SOBR
            try
            {
                var list = this.df.SobrInfoToXml(scrub) ?? new List<CSobrTypeInfos>();
                List<string> headers = new() { "Name", "ExtentCount", "JobCount", "PolicyType", "EnableCapacityTier", "CapacityTierCopy", "CapacityTierMove", "ArchiveTierEnabled", "UsePerVMFiles", "CapTierType", "ImmutableEnabled", "ImmutablePeriod", "SizeLimitEnabled", "SizeLimit" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.ExtentCount.ToString(),
                    d.JobCount.ToString(),
                    d.PolicyType,
                    d.EnableCapacityTier ? "True" : "False",
                    d.CapacityTierCopyPolicyEnabled ? "True" : "False",
                    d.CapacityTierMovePolicyEnabled ? "True" : "False",
                    d.ArchiveTierEnabled ? "True" : "False",
                    d.UsePerVMBackupFiles ? "True" : "False",
                    d.CapTierType,
                    d.ImmuteEnabled ? "True" : "False",
                    d.ImmutePeriod,
                    d.SizeLimitEnabled ? "True" : "False",
                    d.SizeLimit,
                }).ToList();
                SetSection("sobr", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture sobr JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddSobrExtTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("extents", VbrLocalizationHelper.SbrExtTitle, VbrLocalizationHelper.SbrExtBtn);
            string summary = this.sum.Extents();
            s += "<tr>" +
this.form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt1, VbrLocalizationHelper.SbrExt1TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT) +
this.form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
this.form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT) +
"</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                this.log.Info("Attempting to load SOBR Extent data...");
                List<CRepository> list = this.df.ExtentXmlFromCsv(scrub);

                if (list == null || list.Count == 0)
                {
                    this.log.Warning("No SOBR Extent data found. The SOBRExtents CSV file may be missing or empty.");
                    this.log.Info("This could indicate: 1) No SOBRs configured, 2) Collection script failed, or 3) CSV file not found");
                }

                foreach (var d in list)
                {
                    var prov = d.Provisioning;
                    int shade = 0;
                    if (prov == "under")
                    {
                        shade = 2;
                    }

                    if (prov == "over")
                    {
                        shade = 1;
                    }

                    int freeSpaceShade = 0;

                    // decimal.TryParse(d.FreeSpacePercent, out decimal i);
                    if (d.FreeSpacePercent < 20) { freeSpaceShade = 1; }

                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.SobrName, string.Empty);
                    s += this.form.TableData(d.MaxTasks.ToString(), string.Empty, shade);
                    s += this.form.TableData(d.Cores.ToString(), string.Empty);
                    s += this.form.TableData(d.Ram.ToString(), string.Empty);

                    if (d.IsAutoGate)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.Host, string.Empty);
                    s += this.form.TableData(d.Path, string.Empty);
                    s += this.form.TableData(d.FreeSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.TotalSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.FreeSpacePercent.ToString(), string.Empty, freeSpaceShade);

                    if (d.IsDecompress)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.AlignBlocks)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsRotatedDrives)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsImmutabilitySupported)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.Type, string.Empty);

                    // s += _form.TableData(d[16], "");
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Extents Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON extents
            try
            {
                var list = this.df.ExtentXmlFromCsv(scrub) ?? new List<CRepository>();
                List<string> headers = new() { "Name", "SobrName", "MaxTasks", "Cores", "Ram", "IsAutoGate", "Host", "Path", "FreeSpace", "TotalSpace", "FreeSpacePercent", "IsDecompress", "AlignBlocks", "IsRotatedDrives", "IsImmutabilitySupported", "Type" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.SobrName,
                    d.MaxTasks.ToString(),
                    d.Cores.ToString(),
                    d.Ram.ToString(),
                    d.IsAutoGate ? "True" : "False",
                    d.Host,
                    d.Path,
                    d.FreeSpace.ToString(),
                    d.TotalSpace.ToString(),
                    d.FreeSpacePercent.ToString(),
                    d.IsDecompress ? "True" : "False",
                    d.AlignBlocks ? "True" : "False",
                    d.IsRotatedDrives ? "True" : "False",
                    d.IsImmutabilitySupported ? "True" : "False",
                    d.Type,
                }).ToList();
                SetSection("extents", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture extents JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddRepoTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("repos", VbrLocalizationHelper.RepoTitle, VbrLocalizationHelper.RepoBtn);
            string summary = this.sum.Repos();
            s +=
this.form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT, 0) +
this.form.TableHeader(VbrLocalizationHelper.Repo0, VbrLocalizationHelper.Repo0TT, 1) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT, 2) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT, 3) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT, 4) +
this.form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT) +
this.form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT, 7) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT, 8) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT, 9) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT, 10) +
this.form.TableHeader(VbrLocalizationHelper.Repo1, VbrLocalizationHelper.Repo1TT, 11) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT, 12) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT, 13) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT, 14) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT, 15) +
this.form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT, 16);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                List<CRepository> list = this.df.RepoInfoToXml(scrub);

                foreach (var d in list)
                {
                    var prov = d.Provisioning;
                    int shade = 0;
                    if (prov == "under")
                    {
                        shade = 2;
                    }

                    if (prov == "over")
                    {
                        shade = 1;
                    }

                    int freeSpaceShade = 0;

                    // decimal.TryParse(d.FreeSpacePercent, out decimal i);

                    // int perVmShade = 0;
                    // if (d[11] == "False")
                    //    perVmShade = 3;
                    if (d.FreeSpacePercent < 20) { freeSpaceShade = 1; }
                    s += "<tr>";
                    s += this.form.TableData(d.Name, string.Empty);
                    s += this.form.TableData(d.JobCount.ToString(), string.Empty);
                    s += this.form.TableData(d.MaxTasks.ToString(), string.Empty, shade);
                    s += this.form.TableData(d.Cores.ToString(), string.Empty);
                    s += this.form.TableData(d.Ram.ToString(), string.Empty);
                    if (d.IsAutoGate)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.Host, string.Empty);
                    s += this.form.TableData(d.Path, string.Empty);
                    s += this.form.TableData(d.FreeSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.TotalSpace.ToString(), string.Empty);
                    s += this.form.TableData(d.FreeSpacePercent.ToString(), string.Empty, freeSpaceShade);
                    if (d.IsPerVmBackupFiles)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.Warn, string.Empty);
                    }

                    if (d.IsDecompress)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.AlignBlocks)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsRotatedDrives)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    if (d.IsImmutabilitySupported)
                    {
                        s += this.form.TableData(this.form.True, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(this.form.False, string.Empty);
                    }

                    s += this.form.TableData(d.Type, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("REPO Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON repos
            try
            {
                var list = this.df.RepoInfoToXml(scrub) ?? new List<CRepository>();
                List<string> headers = new() { "Name", "JobCount", "MaxTasks", "Cores", "Ram", "IsAutoGate", "Host", "Path", "FreeSpace", "TotalSpace", "FreeSpacePercent", "IsPerVmBackupFiles", "IsDecompress", "AlignBlocks", "IsRotatedDrives", "IsImmutabilitySupported", "Type" };
                List<List<string>> rows = list.Select(d => new List<string>
                {
                    d.Name,
                    d.JobCount.ToString(),
                    d.MaxTasks.ToString(),
                    d.Cores.ToString(),
                    d.Ram.ToString(),
                    d.IsAutoGate ? "True" : "False",
                    d.Host,
                    d.Path,
                    d.FreeSpace.ToString(),
                    d.TotalSpace.ToString(),
                    d.FreeSpacePercent.ToString(),
                    d.IsPerVmBackupFiles ? "True" : "False",
                    d.IsDecompress ? "True" : "False",
                    d.AlignBlocks ? "True" : "False",
                    d.IsRotatedDrives ? "True" : "False",
                    d.IsImmutabilitySupported ? "True" : "False",
                    d.Type,
                }).ToList();
                SetSection("repos", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture repos JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddJobConTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("jobcon", VbrLocalizationHelper.JobConTitle, VbrLocalizationHelper.JobConBtn, CGlobals.ReportDays);
            string summary = this.sum.JobCon();
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon0, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon1, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon2, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon3, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon4, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon5, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon6, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.JobCon7, string.Empty);
            s += "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {

                var stuff = this.df.JobConcurrency(true);

                foreach (var stu in stuff)
                {
                    s += "<tr>";
                    s += this.form.TableData(stu.Key.ToString(), string.Empty);

                    foreach (var st in stu.Value)
                    {
                        s += this.form.TableData(st, string.Empty);
                    }

                    s += "</tr>";
                }

            }
            catch (Exception e)
            {
                this.log.Error("JOB CONCURRENCY Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON job concurrency
            try
            {
                var stuff = this.df.JobConcurrency(true);
                List<string> headers = new() { "TimeWindow", "Values" };
                List<List<string>> rows = stuff.Select(stu => new List<string> { stu.Key.ToString(), string.Join(",", stu.Value) }).ToList();
                SetSection("jobConcurrency", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobConcurrency JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddTaskConTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("taskcon", VbrLocalizationHelper.TaskConTitle, VbrLocalizationHelper.TaskConBtn, CGlobals.ReportDays);
            string summary = this.sum.TaskCon();

            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon0, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon1, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon2, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon3, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon4, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon5, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon6, string.Empty);
            s += this.form.TableHeader(VbrLocalizationHelper.TaskCon7, string.Empty);
            s += "</tr>";
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                var stuff = this.df.JobConcurrency(false);

                foreach (var stu in stuff)
                {
                    s += "<tr>";
                    s += this.form.TableData(stu.Key.ToString(), string.Empty);

                    foreach (var st in stu.Value)
                    {
                        s += this.form.TableData(st, string.Empty);
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("Task Concurrency Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON task concurrency
            try
            {
                var stuff = this.df.JobConcurrency(false);
                List<string> headers = new() { "TimeWindow", "Values" };
                List<List<string>> rows = stuff.Select(stu => new List<string> { stu.Key.ToString(), string.Join(",", stu.Value) }).ToList();
                SetSection("taskConcurrency", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture taskConcurrency JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddJobSessSummTable(bool scrub)
        {
            this.log.Info("Adding Job Session Summary Table");
            string s = this.form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            string summary = this.sum.JobSessSummary();

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT); // job name
            s += this.form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);// items
            s += this.form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT); // min time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);// max time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);// avg time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT); // total sessions
            s += this.form.TableHeader("Fails", "Total times job failed"); // fails
            s += this.form.TableHeader("Retries", "Total times job retried");// retries
            s += this.form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);// success rate
            s += this.form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT); // avg backup size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);// max backup size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT); // avg data size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss10, "Used size of all objects in job."); // max data size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss11, "Avg Data Size divided by Max Data Size (average processed data divided by total consumed size of all VMs in the job)"); // avg change rate
            s += this.form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT); // wait for res count
            s += this.form.TableHeader(VbrLocalizationHelper.Jss13, VbrLocalizationHelper.Jss13TT); // max wait
            s += this.form.TableHeader(VbrLocalizationHelper.Jss14, VbrLocalizationHelper.Jss14TT);// avg wait
            s += this.form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT); // job types
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);

                foreach (var stu in stuff)
                {
                    try
                    {
                        string t = string.Empty;
                        t += "<tr>";

                        t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += this.form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += this.form.TableData(stu.Fails.ToString(), "Fails");
                        t += this.form.TableData(stu.Retries.ToString(), "Retries");
                        t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += this.form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += this.form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                        t += this.form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Name = " + stu.JobName);
                            this.log.Debug("Job Type = " + stu.JobType);
                        }

                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += this.form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;
                    }
                    catch (Exception ex)
                    {
                        this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        this.log.Error("\t" + ex.Message);

                    }

                }
            }
            catch (Exception e)
            {
                this.log.Error("Job Session Summary Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            this.log.Info("Job Session Summary Table added");

            // JSON job session summary
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
                List<List<string>> rows = stuff.Select(stu => new List<string>
                {
                    stu.JobName,
                    stu.ItemCount.ToString(),
                    stu.MinJobTime,
                    stu.MaxJobTime,
                    stu.AvgJobTime,
                    stu.sessionCount.ToString(),
                    stu.Fails.ToString(),
                    stu.Retries.ToString(),
                    stu.SuccessRate.ToString(),
                    stu.AvgBackupSize.ToString(),
                    stu.MaxBackupSize.ToString(),
                    stu.AvgDataSize.ToString(),
                    stu.MaxDataSize.ToString(),
                    stu.AvgChangeRate.ToString(),
                    stu.waitCount.ToString(),
                    stu.maxWait,
                    stu.avgwait,
                    CJobTypesParser.GetJobType(stu.JobType),
                }).ToList();
                SetSection("jobSessionSummary", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobSessionSummary JSON section: " + ex.Message);
            }

            return s;
        }

        // Export the aggregated JSON report to a file
        public string ExportJson(string outputPath, bool indented = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = Path.Combine(CVariables.vb365dir, "FullReport.json");
                }

                var options = new JsonSerializerOptions { WriteIndented = indented };
                string json = JsonSerializer.Serialize(CGlobals.FullReportJson, options);
                File.WriteAllText(outputPath, json);
                this.log.Info("JSON report exported to: " + outputPath);
                return outputPath;
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to export JSON report: " + ex.Message);
                return string.Empty;
            }
        }

        public string AddJobSessSummTableByJob(bool scrub)
        {
            string s = this.form.SectionStartWithButton("jobsesssum", VbrLocalizationHelper.JssTitle, VbrLocalizationHelper.JssBtn, CGlobals.ReportDays);
            s += "</table>";

            string summary = this.sum.JobInfo();

            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                source.OrderBy(x => x.Name);
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var jobTypes = stuff.Select(x => x.JobType).Distinct().ToList();

                List<CJobSummaryTypes> OffloadJobs = new();

                // log.Debug("Sessions count = " + stuff.Count);
                try
                {
                    foreach (var jType in jobTypes)
                    {
                        if (CGlobals.DEBUG)
                        {
                            this.log.Debug("Job Type = " + jType);
                        }

                        bool skipTotals = false;
                        var jobType = jType;

                        // change job type for "Totals" line of all jobs:
                        if (jType == string.Empty || jType == null)
                        {
                            // log.Debug("Job Type is empty...");
                            // jobType = "Summary of All";
                        }

                        var realType = CJobTypesParser.GetJobType(jobType);

                        // log.Debug("Real Job Type = " + realType);
                        string sectionHeader = realType;
                        if (jType == null)
                        {
                            sectionHeader = "Summary of All";
                        }

                        string jobTable = this.form.SectionStartWithButton("jobTable", sectionHeader + " Jobs", string.Empty);
                        s += jobTable;
                        s += this.SetJobSessionsHeaders();
                        var res = stuff.Where(x => x.JobType == jobType).ToList();

                        // log.Debug("Jobs of type " + jType + " found:" + res.Count);

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

                            // log.Debug("job name = " + stu.JobName);
                            if (stu.JobType != jobType && stu.JobName != "Totals")
                            {
                                // log.Debug("Job Type: " + stu.JobType + " does not match " + jobType + "... skipping");
                                continue;
                            }

                            try
                            {
                                // log.Debug("Adding row for job: " + stu.JobName);
                                string t = string.Empty;
                                t += "<tr>";

                                t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                                t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                                t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                                t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                                t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                                t += this.form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                                t += this.form.TableData(stu.Fails.ToString(), "Fails");
                                t += this.form.TableData(stu.Retries.ToString(), "Retries");
                                t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                                t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                                t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                                t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                                t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                                t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                                t += this.form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                                t += this.form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                                t += this.form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                                string jt = CJobTypesParser.GetJobType(stu.JobType);
                                t += this.form.TableData(jt, VbrLocalizationHelper.Jss15);

                                t += "</tr>";

                                s += t;

                                totalItemsCount += stu.ItemCount;
                                totalSessionCount += stu.sessionCount;
                                totalFails += stu.Fails;
                                totalRetries += stu.Retries;

                                // double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                                // totalSuccessRate += (double)Math.Round(successPercent, 2);
                                totalAvgBackupSize += stu.AvgBackupSize;
                                totalMaxBackupSize += stu.MaxBackupSize;
                                totalAvgDataSize += stu.AvgDataSize;
                                totalMaxDataSize += stu.MaxDataSize;

                                // totalAvgChangeRate += Math.Round(totalAvgDataSize / totalMaxDataSize * stu.AvgChangeRate, 2);
                                totalWaitCount += stu.waitCount;
                            }

                            catch (Exception ex)
                            {
                                this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                                this.log.Error("\t" + ex.Message);

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

                        // log.Debug("Total avg change rate = " + totalAvgChangeRate);
                        // add totals line:
                        if (!skipTotals)
                        {
                            string totalRow = string.Empty;
                            totalRow += "<tr>";
                            totalRow += this.form.TableDataLeftAligned("TOTALS", string.Empty);
                            totalRow += this.form.TableData(totalItemsCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(totalSessionCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalFails.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalRetries.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalSuccessRate.ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), string.Empty);
                            totalRow += this.form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), string.Empty);
                            if (totalAvgChangeRate == double.NaN)
                            {
                                totalAvgChangeRate = 0;
                            }

                            totalRow += this.form.TableData(totalAvgChangeRate.ToString(), string.Empty);
                            totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            totalRow += this.form.TableData(string.Empty, string.Empty);
                            s += totalRow;
                        }

                        // table summary/totals

                        // end each table/section
                        s += this.form.SectionEnd(summary);

                    }

                    s += this.AddOffloadsTable(OffloadJobs);
                }
                catch (Exception e)
                {
                    this.log.Error("Job Info Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
                }

                // end of FE up one line...
            }
            catch (Exception e)
            {
                this.log.Error("Jobs Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            
            // JSON job session summary by job
            try
            {
                var stuff = this.df.ConvertJobSessSummaryToXml(scrub);
                var ordered = stuff.OrderBy(stu => stu.JobName).ToList();
                List<string> headers = new() { "JobName", "ItemCount", "MinJobTime", "MaxJobTime", "AvgJobTime", "SessionCount", "Fails", "Retries", "SuccessRate", "AvgBackupSize", "MaxBackupSize", "AvgDataSize", "MaxDataSize", "AvgChangeRate", "WaitCount", "MaxWait", "AvgWait", "JobTypes" };
                List<List<string>> rows = ordered.Select(stu => new List<string>
                {
                    stu.JobName,
                    stu.ItemCount.ToString(),
                    stu.MinJobTime,
                    stu.MaxJobTime,
                    stu.AvgJobTime,
                    stu.sessionCount.ToString(),
                    stu.Fails.ToString(),
                    stu.Retries.ToString(),
                    stu.SuccessRate.ToString(),
                    stu.AvgBackupSize.ToString(),
                    stu.MaxBackupSize.ToString(),
                    stu.AvgDataSize.ToString(),
                    stu.MaxDataSize.ToString(),
                    stu.AvgChangeRate.ToString(),
                    stu.waitCount.ToString(),
                    stu.maxWait,
                    stu.avgwait,
                    CJobTypesParser.GetJobType(stu.JobType),
                }).ToList();
                SetSection("jobSessionSummaryByJob", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobSessionSummaryByJob JSON section: " + ex.Message);
            }

            return s;
        }

        private string AddOffloadsTable(List<CJobSummaryTypes> offloadJobs)
        {
            string s = string.Empty;
            try
            {

                // log.Debug("Checking Job Type: " + jType);
                // var translatedJobType = CJobTypesParser.GetJobType(jType);
                // log.Debug("\tTranslated Job Type: " + translatedJobType);
                var realType = "Offload";
                string jobTable = this.form.SectionStartWithButton("jobTable", realType + " Jobs", string.Empty);
                s += jobTable;
                s += this.SetJobSessionsHeaders();

                // var res = stuff.Where(x => x.JobType == jType).ToList();
                // log.Debug("Jobs of type " + jType + " found:" + res.Count);

                // var res2 = stuff.Where(x => x.JobType == realType).ToList();

                // log.Debug("Jobs of type " + jType + " found:" + res2.Count);
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

                    // log.Debug("job type of individual session = " + stu.JobType);

                    // if (stu.JobType != jType)
                    // {
                    //     log.Debug("Job Type: " + stu.JobType + " does not match " + jType + "... skipping");
                    //     continue;
                    // }
                    try
                    {
                        // log.Debug("Adding row for job: " + stu.JobName);
                        string t = string.Empty;
                        t += "<tr>";

                        t += this.form.TableDataLeftAligned(stu.JobName, VbrLocalizationHelper.Jss0);
                        t += this.form.TableData(stu.ItemCount.ToString(), VbrLocalizationHelper.Jss1);
                        t += this.form.TableData(stu.MinJobTime, VbrLocalizationHelper.Jss2);
                        t += this.form.TableData(stu.MaxJobTime, VbrLocalizationHelper.Jss3);
                        t += this.form.TableData(stu.AvgJobTime, VbrLocalizationHelper.Jss4);
                        t += this.form.TableData(stu.sessionCount.ToString(), VbrLocalizationHelper.Jss5);
                        t += this.form.TableData(stu.Fails.ToString(), "Fails");
                        t += this.form.TableData(stu.Retries.ToString(), "Retries");
                        t += this.form.TableData(stu.SuccessRate.ToString(), VbrLocalizationHelper.Jss6);
                        t += this.form.TableData(stu.AvgBackupSize.ToString(), VbrLocalizationHelper.Jss7);
                        t += this.form.TableData(stu.MaxBackupSize.ToString(), VbrLocalizationHelper.Jss8);
                        t += this.form.TableData(stu.AvgDataSize.ToString(), VbrLocalizationHelper.Jss9);
                        t += this.form.TableData(stu.MaxDataSize.ToString(), VbrLocalizationHelper.Jss10);
                        t += this.form.TableData(stu.AvgChangeRate.ToString(), VbrLocalizationHelper.Jss11);
                        t += this.form.TableData(stu.waitCount.ToString(), VbrLocalizationHelper.Jss12);
                        t += this.form.TableData(stu.maxWait, VbrLocalizationHelper.Jss13);
                        t += this.form.TableData(stu.avgwait, VbrLocalizationHelper.Jss14);
                        string jobType = CJobTypesParser.GetJobType(stu.JobType);
                        t += this.form.TableData(jobType, VbrLocalizationHelper.Jss15);

                        t += "</tr>";

                        s += t;

                        totalItemsCount += stu.ItemCount;
                        totalSessionCount += stu.sessionCount;
                        totalFails += stu.Fails;
                        totalRetries += stu.Retries;

                        // double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                        // totalSuccessRate += (double)Math.Round(successPercent, 2);
                        totalAvgBackupSize += stu.AvgBackupSize;
                        totalMaxBackupSize += stu.MaxBackupSize;
                        totalAvgDataSize += stu.AvgDataSize;
                        totalMaxDataSize += stu.MaxDataSize;

                        // totalAvgChangeRate += Math.Round(totalAvgDataSize / totalMaxDataSize * stu.AvgChangeRate, 2);
                        totalWaitCount += stu.waitCount;
                    }

                    catch (Exception ex)
                    {
                        this.log.Error("Job Session Summary Table failed to add row for job: " + stu.JobName);
                        this.log.Error("\t" + ex.Message);

                    }

                }

                // clean up totals:
                double successPercent = (totalSessionCount - (double)totalFails + totalRetries) / totalSessionCount * 100;
                totalSuccessRate = (double)Math.Round(successPercent, 2);

                totalAvgChangeRate = Math.Round(totalAvgDataSize / totalMaxDataSize * 100, 2);

                // add totals line:
                string totalRow = string.Empty;
                totalRow += "<tr>";
                totalRow += this.form.TableDataLeftAligned("TOTALS", string.Empty);
                totalRow += this.form.TableData(totalItemsCount.ToString(), string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(totalSessionCount.ToString(), string.Empty);
                totalRow += this.form.TableData(totalFails.ToString(), string.Empty);
                totalRow += this.form.TableData(totalRetries.ToString(), string.Empty);
                totalRow += this.form.TableData(totalSuccessRate.ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalAvgBackupSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalMaxBackupSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalAvgDataSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(Math.Round(totalMaxDataSize, 2).ToString(), string.Empty);
                totalRow += this.form.TableData(totalAvgChangeRate.ToString(), string.Empty);
                totalRow += this.form.TableData(totalWaitCount.ToString(), string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                totalRow += this.form.TableData(string.Empty, string.Empty);
                s += totalRow;

                // table summary/totals

                // end each table/section
                s += this.form.SectionEnd(string.Empty);

            }
            catch (Exception e)
            {
                this.log.Error("Job Info Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            return s;
        }

        public string AddJobInfoTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("jobs", VbrLocalizationHelper.JobInfoTitle, VbrLocalizationHelper.JobInfoBtn);
            s += "</table>";

            string summary = this.sum.JobInfo();

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

                        bool useSourceSize = !(jType == "NasBackupCopy" || jType == "Copy");

                        var realType = CJobTypesParser.GetJobType(jType);
                        string jobTable = this.form.SectionStartWithButton("jobTable", realType + " Jobs", string.Empty);
                        s += jobTable;
                        s += this.SetGenericJobTablHeader(useSourceSize);
                        var res = source.Where(x => x.JobType == jType).ToList();
                        foreach (var job in res)
                        {
                            // object x = null;
                            double onDiskGB = 0;
                            double sourceSizeGB = 0;

                            if (job.JobType != jType)
                            {
                                continue;
                            }

                            string row = string.Empty;
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
                            row += this.form.TableDataLeftAligned(jobName, string.Empty);
                            row += this.form.TableData(repoName, string.Empty);

                            if (useSourceSize)
                            {
                                if (jType == "NasBackup")
                                {
                                    row += this.form.TableData(sourceSizeGB.ToString(), string.Empty);
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
                                        row += this.form.TableData(trueSizeTB.ToString() + " TB", string.Empty);
                                    }
                                    else if (trueSizeGB < 1)
                                    {
                                        row += this.form.TableData(trueSizeMB.ToString() + " MB", string.Empty);
                                    }
                                    else
                                    {
                                        row += this.form.TableData(trueSizeGB.ToString() + " GB", string.Empty);
                                    }
                                }
                            }
                            else
                            {
                            }

                            row += this.form.TableData(onDiskGB.ToString(), string.Empty);

                            // row+= _form.TableData(trueSizeGB.ToString() + " GB", "");
                            // row+= _form.TableData(job.RetentionType, "");
                            row += job.RetentionType == "Cycles" ? this.form.TableData("Points", string.Empty) : this.form.TableData(job.RetentionType, string.Empty);
                            row += this.form.TableData(job.RetainDaysToKeep, string.Empty);

                            // row += _form.TableData(job.StgEncryptionEnabled, "");
                            row += job.StgEncryptionEnabled == "True" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                            var jobType = CJobTypesParser.GetJobType(job.JobType);
                            row += this.form.TableData(jobType, string.Empty);

                            // row += _form.TableData("", "");
                            string compressionLevel = string.Empty;
                            if (job.CompressionLevel == "9")
                            {
                                compressionLevel = "Extreme";
                            }
                            else if (job.CompressionLevel == "6")
                            {
                                compressionLevel = "High";
                            }
                            else if (job.CompressionLevel == "5")
                            {
                                compressionLevel = "Optimal";
                            }
                            else if (job.CompressionLevel == "4")
                            {
                                compressionLevel = "Dedupe-Friendly";
                            }
                            else if (job.CompressionLevel == "0")
                            {
                                compressionLevel = "None";
                            }

                            row += this.form.TableData(compressionLevel, string.Empty);

                            string blockSize = string.Empty;
                            if (job.BlockSize == "KbBlockSize1024")
                            {
                                blockSize = "1 MB";
                            }
                            else if (job.BlockSize == "KbBlockSize512")
                            {
                                blockSize = "512 KB";
                            }
                            else if (job.BlockSize == "KbBlockSize256")
                            {
                                blockSize = "256 KB";
                            }
                            else if (job.BlockSize == "KbBlockSize4096")
                            {
                                blockSize = "4 MB";
                            }
                            else if (job.BlockSize == "KbBlockSize8192")
                            {
                                blockSize = "8 MB";
                            }

                            row += this.form.TableData(blockSize, string.Empty);
                            if (job.GfsMonthlyEnabled || job.GfsWeeklyIsEnabled || job.GfsYearlyEnabled)
                            {
                                row += this.form.TableData(this.form.True, string.Empty);
                                string GfsString = "Weekly: " + job.GfsWeeklyCount + "<br> Monthly: " + job.GfsMonthlyCount + "<br> Yearly: " + job.GfsYearlyCount;
                                row += this.form.TableData(GfsString, string.Empty);
                            }
                            else
                            {
                                row += this.form.TableData(this.form.False, string.Empty);
                                row += this.form.TableData(string.Empty, string.Empty);
                            }

                            row += job.EnableFullBackup ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);
                            try
                            {
                                if (job.Algorithm == "Increment" && job.TransformFullToSyntethic == true)
                                {
                                    row += this.form.TableData(this.form.True, string.Empty);
                                }
                                else
                                {
                                    row += this.form.TableData(this.form.False, string.Empty);
                                }
                            }
                            catch
                            {
                                row += this.form.TableData(this.form.False, string.Empty);
                            }

                            row += job.Algorithm == "Syntethic" ? this.form.TableData("Reverse Incremental", string.Empty) : this.form.TableData("Forward Incremental", string.Empty);

                            row += job.IndexingType != "None" ? this.form.TableData(this.form.True, string.Empty) : this.form.TableData(this.form.False, string.Empty);

                            row += "</tr>";

                            s += row;

                        }

                        // table summary/totals
                        if (useSourceSize)
                        {
                            s += "<tr>";
                            s += this.form.TableDataLeftAligned("Totals", string.Empty);
                            s += this.form.TableData(string.Empty, string.Empty);

                            // double totalSizeGB = Math.Round(tSizeGB / 1024 / 1024 / 1024, 2);
                            double totalSizeTB = Math.Round(tSizeGB / 1024, 2);
                            double totalSizeMB = Math.Round(tSizeGB * 1024, 2);

                            double diskTotalTB = Math.Round(onDiskTotalGB / 1024, 2);
                            double diskTotalMB = Math.Round(onDiskTotalGB * 1024, 2);
                            if (tSizeGB > 999)
                            {
                                s += this.form.TableData(totalSizeTB.ToString() + " TB", string.Empty);
                            }
                            else if (tSizeGB < 1)
                            {
                                s += this.form.TableData(totalSizeMB.ToString() + " MB", string.Empty);
                            }
                            else
                            {
                                s += this.form.TableData(tSizeGB.ToString() + " GB", string.Empty);
                            }

                            if (diskTotalTB > 1)
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "TB");
                            }
                            else if (onDiskTotalGB > 1)
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "GB");
                            }
                            else
                            {
                                s += this.form.TableData(onDiskTotalGB.ToString(), "MB");
                            }
                        }

                        // end each table/section
                        s += this.form.SectionEnd(summary);

                    }

                    // add tape table
                    try
                    {
                        CTapeJobInfoTable tapeTable = new();
                        string tt = tapeTable.TapeJobTable();
                        if (tt != string.Empty)
                        {
                            string tableButton = this.form.SectionStartWithButton("jobTable", "Tape Jobs", string.Empty);
                            s += tableButton;
                            s += tt;
                        }

                    }
                    catch (Exception e)
                    {
                        this.log.Error("Tape Job Data import failed. ERROR:");
                        this.log.Error("\t" + e.Message);
                    }

                    // Add Entra Table
                    try
                    {
                        CEntraJobsTable entraTable = new();
                        string et = entraTable.Table();
                        if (et != null && et.Length > 0)
                        {
                            // debug
                            this.log.Debug("Entra jobs table length: " + et.Length);
                            string tableButton = this.form.SectionStartWithButton("jobTable", "Entra Jobs", string.Empty);
                            s += tableButton;
                            s += et;
                            s += this.form.SectionEnd(summary);
                        }
                        else
                        {
                            this.log.Info("No Entra backup jobs detected - skipping Entra jobs table", false);
                        }
                    }
                    catch (Exception e)
                    {
                        this.log.Error("Entra Job Data import failed. ERROR:");
                        this.log.Error("\t" + e.Message);
                    }

                    s += this.form.SectionEnd(summary);
                }
                catch (Exception e)
                {
                    this.log.Error("Job Info Data import failed. ERROR:");
                    this.log.Error("\t" + e.Message);
                }

                // end of FE up one line...
            }
            catch (Exception e)
            {
                this.log.Error("Jobs Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);

            // JSON job info capture
            try
            {
                CCsvParser csvparser = new();
                var source = csvparser.JobCsvParser().ToList();
                List<string> headers = new() { "JobName", "RepoName", "SourceSizeGB", "OnDiskGB", "RetentionScheme", "RetainDays", "Encrypted", "JobType", "CompressionLevel", "BlockSize", "GfsEnabled", "GfsDetails", "ActiveFullEnabled", "SyntheticFullEnabled", "BackupChainType", "IndexingEnabled" };
                List<List<string>> rows = new();

                foreach (var job in source)
                {
                    string jobName = scrub ? CGlobals.Scrubber.ScrubItem(job.Name, ScrubItemType.Job) : job.Name;
                    string repoName = scrub ? CGlobals.Scrubber.ScrubItem(job.RepoName, ScrubItemType.Repository) : job.RepoName;

                    double sourceSizeGB = Math.Round(job.OriginalSize / 1024.0 / 1024.0 / 1024.0, 2);
                    string compressionLevel = job.CompressionLevel switch
                    {
                        "9" => "Extreme",
                        "6" => "High",
                        "5" => "Optimal",
                        "4" => "Dedupe-Friendly",
                        "0" => "None",
                        _ => job.CompressionLevel,
                    };

                    string blockSize = job.BlockSize switch
                    {
                        "KbBlockSize1024" => "1 MB",
                        "KbBlockSize512" => "512 KB",
                        "KbBlockSize256" => "256 KB",
                        "KbBlockSize4096" => "4 MB",
                        "KbBlockSize8192" => "8 MB",
                        _ => job.BlockSize,
                    };

                    bool gfsEnabled = job.GfsMonthlyEnabled || job.GfsWeeklyIsEnabled || job.GfsYearlyEnabled;
                    string gfsDetails = gfsEnabled ? $"Weekly:{job.GfsWeeklyCount},Monthly:{job.GfsMonthlyCount},Yearly:{job.GfsYearlyCount}" : string.Empty;
                    bool syntheticFull = job.Algorithm == "Increment" && job.TransformFullToSyntethic;
                    string backupChainType = job.Algorithm == "Syntethic" ? "Reverse Incremental" : "Forward Incremental";
                    bool indexingEnabled = job.IndexingType != "None";

                    rows.Add(new List<string>
                    {
                        jobName,
                        repoName,
                        sourceSizeGB.ToString(),
                        "0", // OnDisk calculated separately for NAS
                        job.RetentionType == "Cycles" ? "Points" : job.RetentionType,
                        job.RetainDaysToKeep,
                        job.StgEncryptionEnabled,
                        CJobTypesParser.GetJobType(job.JobType),
                        compressionLevel,
                        blockSize,
                        gfsEnabled.ToString(),
                        gfsDetails,
                        job.EnableFullBackup.ToString(),
                        syntheticFull.ToString(),
                        backupChainType,
                        indexingEnabled.ToString(),
                    });
                }

                SetSection("jobInfo", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture jobInfo JSON section: " + ex.Message);
            }

            return s;
        }

        private string SetJobSessionsHeaders()
        {
            string s = string.Empty;
            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.Jss0, VbrLocalizationHelper.Jss0TT); // job name
            s += this.form.TableHeader(VbrLocalizationHelper.Jss1, VbrLocalizationHelper.Jss1TT);// items
            s += this.form.TableHeader(VbrLocalizationHelper.Jss2, VbrLocalizationHelper.Jss2TT); // min time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss3, VbrLocalizationHelper.Jss3TT);// max time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss4, VbrLocalizationHelper.Jss4TT);// avg time
            s += this.form.TableHeader(VbrLocalizationHelper.Jss5, VbrLocalizationHelper.Jss5TT); // total sessions
            s += this.form.TableHeader("Fails", "Total times job failed"); // fails
            s += this.form.TableHeader("Retries", "Total times job retried");// retries
            s += this.form.TableHeader(VbrLocalizationHelper.Jss6, VbrLocalizationHelper.Jss6TT);// success rate
            s += this.form.TableHeader(VbrLocalizationHelper.Jss7, VbrLocalizationHelper.Jss7TT); // avg backup size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss8, VbrLocalizationHelper.Jss8TT);// max backup size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss9, VbrLocalizationHelper.Jss9TT); // avg data size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss10, "Used size of all objects in job."); // max data size
            s += this.form.TableHeader(VbrLocalizationHelper.Jss11, "Avg Data Size divided by Max Data Size (average processed data divided by total consumed size of all VMs in the job)"); // avg change rate
            s += this.form.TableHeader(VbrLocalizationHelper.Jss12, VbrLocalizationHelper.Jss12TT); // wait for res count
            s += this.form.TableHeader(VbrLocalizationHelper.Jss13, VbrLocalizationHelper.Jss13TT); // max wait
            s += this.form.TableHeader(VbrLocalizationHelper.Jss14, VbrLocalizationHelper.Jss14TT);// avg wait
            s += this.form.TableHeader(VbrLocalizationHelper.Jss15, VbrLocalizationHelper.Jss15TT); // job types
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            return s;

        }

        private string SetGenericJobTablHeader(bool useSourceSize)
        {
            string s = string.Empty;
            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobInfo0, VbrLocalizationHelper.JobInfo0TT); // Name
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo1, VbrLocalizationHelper.JobInfo1TT); // Repo
            if (useSourceSize)
            {
                s += this.form.TableHeader(VbrLocalizationHelper.JobInfo2, VbrLocalizationHelper.JobInfo2TT); // Source Size (GB)
            }

            s += this.form.TableHeader("Est. On Disk GB", "Estimated size of the backup data on-disk.");
            s += this.form.TableHeader("Retention Scheme", "Is the job set to keep backups for X number of Days or Points");
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo3, VbrLocalizationHelper.JobInfo3TT); // Restore Point Target
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo4, VbrLocalizationHelper.JobInfo4TT); // Encrypted
            s += this.form.TableHeader(VbrLocalizationHelper.JobInfo5, VbrLocalizationHelper.JobInfo5TT); // Job Type

            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo6, VbrLocalizationHelper.JobInfo6TT); // Algorithm
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo7, VbrLocalizationHelper.JobInfo7TT); // Scheudle Enabled Time
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo8, VbrLocalizationHelper.JobInfo8TT); // Full Backup Days
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo9, VbrLocalizationHelper.JobInfo9TT); // Full Backup Schedule
            ////s += _form.TableHeader(ResourceHandler.JobInfo10, ResourceHandler.JobInfo10TT);
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo11, VbrLocalizationHelper.JobInfo11TT); // transform full to synth
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo12, VbrLocalizationHelper.JobInfo12TT); // thransform inc to synth
            // s += _form.TableHeader(VbrLocalizationHelper.JobInfo13, VbrLocalizationHelper.JobInfo13TT); // transform days

            // New Table Area:
            // s += _form.TableHeader("", "");
            s += this.form.TableHeader("Compression Level", "Level of compression used in the job");
            s += this.form.TableHeader("Block Size", "Block Size set for the job");
            s += this.form.TableHeader("GFS Enabled", "True if any GFS Periods are enabled");
            s += this.form.TableHeader("GFS Retention", "Details about the GFS Retention period");
            s += this.form.TableHeader("Active Full Enabled", string.Empty);
            s += this.form.TableHeader("Synthetic Full Enabled", string.Empty);
            s += this.form.TableHeader("Backup Chain Type", "Type of backup chain used in the job");
            s += this.form.TableHeader("Indexing Enabled", string.Empty);

            // s += _form.TableData("Totals", "");
            // s += _form.TableHeader("", "");
            // s += _form.TableHeader("", "");
            // s += _form.TableHeader("", "");
            // s += _form.TableHeader("", "");
            // s += _form.TableHeader("", "");
            // s += _form.TableHeader("", "");
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            return s;
        }

        public void AddSessionsFiles(bool scrub)
        {
            this.df.JobSessionInfoToXml(scrub);
        }

    }

}
