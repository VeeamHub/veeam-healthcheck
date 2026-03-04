// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Globalization;
using System.Text.Json;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Collection;
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
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Replication;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.TapeInfra;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SureBackup;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.GeneralSettings;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Registry;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Proxies;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Repositories;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Managed_Server_Table;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.SOBR;
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
            return this.form.FormNavRows(VbrLocalizationHelper.NavLicInfoLink, "license", VbrLocalizationHelper.NavLicInfoDetail, isActive: true) +
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
                this.form.FormNavRows(VbrLocalizationHelper.NavCapTierLink, "capextents", VbrLocalizationHelper.NavCapTierDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavArchTierLink, "archextents", VbrLocalizationHelper.NavArchTierDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavRepoInfoLink, "repos", VbrLocalizationHelper.NavRepoDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobConLink, "jobcon", VbrLocalizationHelper.NavJobConDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavTaskConLink, "taskcon", VbrLocalizationHelper.NavTaskConDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobSessSumLink, "jobsesssum", VbrLocalizationHelper.NavJobSessSumDeet) +
                this.form.FormNavRows(VbrLocalizationHelper.NavJobInfoLink, "jobs", VbrLocalizationHelper.NavJobInfoDeet);
        }

        /// <summary>
        /// Generates the HTML navigation table for the Veeam HealthCheck security report.
        /// </summary>
        /// <returns>A string containing the HTML security navigation table.</returns>
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

        /// <summary>
        /// Generates the HTML table for license information.
        /// </summary>
        /// <param name="scrub">Indicates whether to scrub sensitive data from the output.</param>
        /// <returns>A string containing the HTML license table.</returns>
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
                    s += this.form.TableData(Badge(l.status), string.Empty);
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

        /// <summary>
        /// Generates an HTML section showing data collection status and any missing CSV files.
        /// </summary>
        /// <returns>A string containing the HTML data collection summary.</returns>
        public string DataCollectionSummaryTable()
        {
            var results = CGlobals.CsvValidationResults;
            if (results == null || results.Count == 0)
            {
                return string.Empty; // No validation data available
            }

            string s = this.form.SectionStartWithButtonNoTable("datacollection", "Data Collection Summary", string.Empty);
            
            // Calculate summary statistics
            int totalFiles = results.Count;
            int presentCount = results.Count(r => r.IsPresent);
            int missingCount = results.Count(r => !r.IsPresent);
            int criticalMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Critical);
            int warningMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Warning);
            int totalRecords = results.Where(r => r.IsPresent).Sum(r => r.RecordCount);

            // Create summary message
            string summaryMessage;
            if (criticalMissing > 0)
            {
                summaryMessage = $"<span style='color: #d9534f; font-weight: bold;'>⚠ {criticalMissing} critical file(s) missing.</span> Some report sections may be incomplete or missing.";
            }
            else if (missingCount > 0)
            {
                summaryMessage = $"<span style='color: #f0ad4e;'>ℹ {missingCount} optional file(s) not found.</span> Report generated with available data.";
            }
            else
            {
                summaryMessage = $"<span style='color: #5cb85c;'>✓ All data files loaded successfully.</span>";
            }

            // Overview section
            s += $@"
            <div style='margin-bottom: 15px; padding: 10px; background-color: #f9f9f9; border-radius: 5px;'>
                <p><strong>Files Loaded:</strong> {presentCount} of {totalFiles}</p>
                <p><strong>Total Records:</strong> {totalRecords:N0}</p>
                <p><strong>Status:</strong> {summaryMessage}</p>
            </div>";

            // Only show missing files table if there are missing files
            if (missingCount > 0)
            {
                s += "<h4>Missing Data Files</h4>";
                s += "<p style='font-size: 0.9em; color: #666;'>The following data files were not found. Related report sections may show limited or no data.</p>";
                s += "<table class='table table-striped'><thead><tr>";
                s += this.form.TableHeader("File Name", "Name of the CSV data file");
                s += this.form.TableHeader("Severity", "Impact level if this file is missing");
                s += this.form.TableHeader("Impact", "Description of affected report sections");
                s += "</tr></thead><tbody>";

                foreach (var result in results.Where(r => !r.IsPresent).OrderByDescending(r => r.Severity))
                {
                    string severityColor = result.Severity switch
                    {
                        CsvValidationSeverity.Critical => "#d9534f",
                        CsvValidationSeverity.Warning => "#f0ad4e",
                        _ => "#5bc0de"
                    };
                    
                    string impactDescription = GetMissingFileImpact(result.FileName);
                    
                    s += "<tr>";
                    s += this.form.TableData(result.FileName, string.Empty);
                    s += $"<td style='color: {severityColor}; font-weight: bold;'>{result.Severity}</td>";
                    s += this.form.TableData(impactDescription, string.Empty);
                    s += "</tr>";
                }

                s += "</tbody></table>";
            }

            string summary = Functions.Collection.CCsvValidator.GetReportSummary(results);
            s += this.form.SectionEndNoTable(summary);
            return s;
        }

        /// <summary>
        /// Gets a human-readable description of the impact of a missing file.
        /// </summary>
        private static string GetMissingFileImpact(string fileName)
        {
            return fileName switch
            {
                "Proxies" => "VMware proxy information will be missing from the report.",
                "HvProxy" => "Hyper-V proxy information will not be displayed.",
                "NasProxy" => "NAS/File proxy data will be unavailable.",
                "CdpProxy" => "CDP proxy information will not be shown.",
                "Repositories" => "Repository information will be missing.",
                "SOBRs" => "Scale-Out Backup Repository information unavailable.",
                "SOBRExtents" => "SOBR extent details will not be displayed.",
                "Servers" => "Managed server information will be incomplete.",
                "vbrinfo" => "Core VBR server information may be missing.",
                "_Jobs" => "Job configuration details will be unavailable.",
                "LicInfo" => "License information will not be shown.",
                "configBackup" => "Configuration backup status unavailable.",
                "regkeys" => "Registry key analysis will be skipped.",
                "SecurityCompliance" => "Security compliance data unavailable.",
                "WanAcc" => "WAN Accelerator information unavailable.",
                _ => "Related data sections may be empty."
            };
        }

        // helper to set a generic section in JSON aggregation
        private static void SetSection(string key, List<string> headers, List<List<string>> rows, string summary)
        {
            SetSectionPublic(key, headers, rows, summary);
        }

        /// <summary>
        /// Public accessor for SetSection, used by extracted table classes.
        /// </summary>
        internal static void SetSectionPublic(string key, List<string> headers, List<List<string>> rows, string summary)
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

        // helper to render a boolean value as a table data cell
        private string BoolCell(bool value) => this.form.TableData(value ? this.form.True : this.form.False, string.Empty);

        /// <summary>
        /// Renders a text value as a styled status badge.
        /// Maps common status strings to appropriate visual variants.
        /// </summary>
        internal static string Badge(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text ?? string.Empty;

            string variant = text.Trim().ToLowerInvariant() switch
            {
                "success" or "ok" or "enabled" or "yes" or "true" or "valid" or "active" => "success",
                "warning" or "warn" or "partial" => "warning",
                "failed" or "error" or "disabled" or "no" or "false" or "expired" or "invalid" => "danger",
                "info" or "information" => "info",
                _ => "neutral"
            };
            return $"<span class=\"badge badge-{variant}\">{System.Net.WebUtility.HtmlEncode(text)}</span>";
        }

        /// <summary>
        /// Renders a security feature as a grid item with a colored status dot.
        /// </summary>
        private static string SecurityGridItem(string label, bool enabled)
        {
            string dotColor = enabled ? "green" : "red";
            return $"<div class=\"security-item\">" +
                   $"<div class=\"status-dot {dotColor}\"></div>" +
                   $"<div class=\"label\">{System.Net.WebUtility.HtmlEncode(label)}</div>" +
                   "</div>";
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

            s += "<table><thead><tr>";
            s += headers;
            s += "</tr></thead><tbody><tr>";
            s += data;
            s += "</tr></tbody></table>";

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
            string s = this.form.SectionStartWithButtonNoTable("vbrserver", VbrLocalizationHelper.BkpSrvTblHead, string.Empty);
            string summary = this.sum.SetVbrSummary();

            // CDataFormer cd = new(true);
            BackupServer b = this.df.BackupServerInfoToXml(scrub);
            if (String.IsNullOrEmpty(b.Version))
            {
                b.Version = CGlobals.VBRFULLVERSION;
            }

            s += AddBackupServerDetails(b);

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

            s += this.form.TableData(Badge(b.ConfigBackupLastResult), string.Empty);
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
            s += this.form.SectionEndNoTable(summary);

            // JSON section capture (backup server core info)
            try
            {
                List<string> headers = new() { "Name", "Version", "DbType", "DbHost", "ConfigBackupEnabled", "ConfigBackupLastResult", "ConfigBackupEncryption", "ConfigBackupTarget" };
                List<List<string>> rows = new()
                {
                    new List<string>
                    {
                        b.Name,
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

            string s = this.form.SectionStartWithButtonNoTable("secsummary", VbrLocalizationHelper.SSTitle, string.Empty);
            string summary = this.sum.SecSum();

            try
            {
                CSecuritySummaryTable t = this.df.SecSummary();

                s += "<div class=\"security-grid\">";
                s += SecurityGridItem("Immutability", t.ImmutabilityEnabled);
                s += SecurityGridItem("Traffic Encryption", t.TrafficEncrptionEnabled);
                s += SecurityGridItem("Backup File Encryption", t.BackupFileEncrptionEnabled);
                s += SecurityGridItem("Config Backup Encryption", t.ConfigBackupEncrptionEnabled);
                s += SecurityGridItem("MFA Enabled", t.MFAEnabled);
                s += "</div>";
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

            s += this.form.SectionEndNoTable(summary);

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
            string s = this.form.SectionStartWithButtonNoTable("secsummary", VbrLocalizationHelper.SSTitle, string.Empty);
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
            s += this.form.SectionEndNoTable(summary);

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
                            this.form.TableHeader(VbrLocalizationHelper.MssHdr2, VbrLocalizationHelper.MssHdr2TT);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                Dictionary<string, int> list = this.df.ServerSummaryToXml();

                foreach (var d in list)
                {
                    s += "<tr>";
                    s += $"<td style=\"font-weight:600;text-align:left\">{d.Key}</td>";
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
            // Delegated to CJobSummaryInfoTable (extracted to use CSectionTable<T>)
            var jobSummaryTable = new CJobSummaryInfoTable();
            return jobSummaryTable.Render(scrub);
        }

        public string AddMissingJobsTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("missingjobs", VbrLocalizationHelper.NpTitle, VbrLocalizationHelper.NpButton);

            string summary = this.sum.MissingJobsSUmmary();

            s += this.form.TableHeaderLeftAligned(VbrLocalizationHelper.JobSum0, string.Empty);

                // _form.TableHeader("Count", "Total detected of this type")
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
            string s = this.form.SectionStartWithButtonNoTable("protectedworkloads", VbrLocalizationHelper.PlTitle, VbrLocalizationHelper.PlButton);
            string summary = this.sum.ProtectedWorkloads();
            try
            {
                this.df.ProtectedWorkloadsToXml();

                // vi table
                s += "<h3>VMware Backups</h3>";
                s += this.form.Table();
                s += "<thead><tr>" +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr0, VbrLocalizationHelper.PlHdrTT0) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr1, VbrLocalizationHelper.PlHdrTT1) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr2, VbrLocalizationHelper.PlHdrTT2) +
                this.form.TableHeader(VbrLocalizationHelper.PlHdr3, VbrLocalizationHelper.PlHdrTT3);
                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();
                s += "<tr>";
                s += this.form.TableData((this.df.viProtectedNames.Distinct().Count() + this.df.viNotProtectedNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df.viProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df.viNotProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df.viDupes.ToString(), string.Empty);
                s += "</tr>";
                s += "</table>";

                // hv
                s += "<h3>HV Backups</h3>";
                s += this.form.Table();

                // hv table
                s += "<thead><tr>";
                s += this.form.TableHeader("HV Total", "Total HV VMs found in environment");
                s += this.form.TableHeader("HV Protected", "Total HV VMs found with existing backup");
                s += this.form.TableHeader("HV Unprotected", "Total HV VMs found without backup");
                s += this.form.TableHeader("HV Duplicates", "Total HV VMs potentially found in multiple backups");
                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();
                s += "<tr>";
                s += this.form.TableData((this.df.hvProtectedNames.Distinct().Count() + this.df.hvNotProtectedNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df.hvProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df.hvNotProtectedNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df.hvDupes.ToString(), string.Empty);
                s += "</tr></table>";

                // phys
                s += "<h3>Physical Backups</h3>";
                s += this.form.Table();
                s += "<thead><tr>";
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr4, VbrLocalizationHelper.PlHdrTT4);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr5, VbrLocalizationHelper.PlHdrTT5);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr6, VbrLocalizationHelper.PlHdrTT6);
                s += this.form.TableHeader(VbrLocalizationHelper.PlHdr7, VbrLocalizationHelper.PlHdrTT7);

                s += this.form.TableHeaderEnd();
                s += this.form.TableBodyStart();

                // CDataFormer cd = new(true);
                s += "<tr>";
                s += this.form.TableData(this.df.vmProtectedByPhys.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData((this.df.physNotProtNames.Distinct().Count() + this.df.physProtNames.Distinct().Count()).ToString(), string.Empty);
                s += this.form.TableData(this.df.physProtNames.Distinct().Count().ToString(), string.Empty);
                s += this.form.TableData(this.df.physNotProtNames.Distinct().Count().ToString(), string.Empty);
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
                        // VMC log data not available - check if NAS backup jobs exist as fallback
                        try
                        {
                            CCsvParser nasJobParser = new();
                            var nasJobs = nasJobParser.GetDynamicNasBackup()?.ToList();
                            if (nasJobs != null && nasJobs.Count > 0)
                            {
                                this.log.Info($"VMC log data unavailable but {nasJobs.Count} NAS backup jobs found - showing job summary", false);
                                double totalSourceGB = 0;
                                foreach (var job in nasJobs)
                                {
                                    try
                                    {
                                        var dict = (IDictionary<string, object>)job;
                                        object srcObj = null;
                                        foreach (var key in dict.Keys)
                                        {
                                            if (key.Equals("SourceGB", StringComparison.OrdinalIgnoreCase))
                                            { srcObj = dict[key]; break; }
                                            if (key.Equals("OnDiskGB", StringComparison.OrdinalIgnoreCase))
                                            { srcObj = dict[key]; break; }
                                        }

                                        if (srcObj != null && double.TryParse(srcObj.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                                            totalSourceGB += val;
                                    }
                                    catch { }
                                }

                                s += "<div id=\"nasTable\" border=\"1\" class=\"content-table\"></div>";
                                s += this.form.Table();
                                s += "<thead><tr>";
                                s += this.form.TableHeader("NAS Backup Jobs", "Number of NAS backup jobs detected");
                                s += this.form.TableHeader("Total Source Size", "Total source size across all NAS backup jobs");
                                s += this.form.TableHeaderEnd();
                                s += this.form.TableBodyStart();
                                s += "<tr>";
                                s += this.form.TableData(nasJobs.Count.ToString(), string.Empty);
                                s += this.form.TableData($"{totalSourceGB:0.00} GB", string.Empty);
                                s += "</tr>";
                                s += this.form.EndTable();
                                s += "<p class=\"subtext\">Detailed file share statistics require local execution (VMC log access)</p>";
                            }
                            else
                            {
                                this.log.Info("No NAS Workloads detected.", false);
                                s += "<p>No NAS Workloads detected</p>";
                            }
                        }
                        catch (Exception)
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

                    if (cProtectedWorkloads.entraWorkloads.Count == 0)
                    {
                        this.log.Info("No Entra tenants detected.", false);
                        s += "<p>No Entra tenants detected</p>";
                    }
                    else
                    {
                        // Small table for Entra Tenant Count:
                        s += "<div id=\"entraTenantCount\" border=\"1\" class=\"content-table\"></div>";
                        s += this.form.Table();
                        s += "<thead><tr>";
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
                        s += "<thead><tr>";
                        s += this.form.TableHeader("Tenant Name", "Name of the Entra ID Tenant being backed up.");
                        s += this.form.TableHeader("Cache Repo", "Cache Repo selected for the tenant");
                        s += this.form.TableHeaderEnd();
                        s += this.form.TableBodyStart();

                        foreach (var load in cProtectedWorkloads.entraWorkloads)
                        {
                            s += "<tr>";
                            s += this.form.TableData(load.TenantName, string.Empty);
                            s += this.form.TableData(load.CacheRepoName, string.Empty);
                            s += "</tr>";
                        }

                        s += this.form.EndTable();
                    }
                }
                catch (Exception)
                {
                }

                s += this.form.endDiv;
            }
            catch (Exception e)
            {
                this.log.Error("Protected Servers Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEndNoTable(summary);

            // JSON protected workloads
            try
            {
                // VMware stats
                int viTotal = this.df.viProtectedNames.Distinct().Count() + this.df.viNotProtectedNames.Distinct().Count();
                int viProtected = this.df.viProtectedNames.Distinct().Count();
                int viUnprotected = this.df.viNotProtectedNames.Distinct().Count();
                int viDupes = this.df.viDupes;

                // HV stats
                int hvTotal = this.df.hvProtectedNames.Distinct().Count() + this.df.hvNotProtectedNames.Distinct().Count();
                int hvProtected = this.df.hvProtectedNames.Distinct().Count();
                int hvUnprotected = this.df.hvNotProtectedNames.Distinct().Count();
                int hvDupes = this.df.hvDupes;

                // Physical stats
                int physVmProtectedByPhys = this.df.vmProtectedByPhys.Distinct().Count();
                int physTotal = this.df.physNotProtNames.Distinct().Count() + this.df.physProtNames.Distinct().Count();
                int physProtected = this.df.physProtNames.Distinct().Count();
                int physUnprotected = this.df.physNotProtNames.Distinct().Count();

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
            var table = new CManagedServerTable();
            return table.Render(scrub);
        }

        public static string SerializeToJson(object obj)
        {
            return JsonSerializer.Serialize(obj);
        }

        public string AddRegKeysTable(bool scrub)
        {
            var table = new CRegKeysTable();
            return table.Render(scrub);
        }

        public string AddProxyTable(bool scrub)
        {
            var table = new CProxyTable();
            return table.Render(scrub);
        }

        public string AddMultiRoleTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = this.sum.Proxies();
            s += this.form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT);
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
            var table = new CSobrTable();
            return table.Render(scrub);
        }

        public string AddSobrExtTable(bool scrub)
        {
            string s = this.form.SectionStartWithButton("extents", VbrLocalizationHelper.SbrExtTitle, VbrLocalizationHelper.SbrExtBtn);
            string summary = this.sum.Extents();
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt0, VbrLocalizationHelper.SbrExt0TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt1, VbrLocalizationHelper.SbrExt1TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt2, VbrLocalizationHelper.SbrExt2TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt3, VbrLocalizationHelper.SbrExt3TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt4, VbrLocalizationHelper.SbrExt4TT);
            s += this.form.TableHeader("Auto Gateway / Direct Connection", VbrLocalizationHelper.SbrExt5TT);
            s += this.form.TableHeader("Specified Gateway(s)", VbrLocalizationHelper.SbrExt6TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt7, VbrLocalizationHelper.SbrExt7TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt8, VbrLocalizationHelper.SbrExt8TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt9, VbrLocalizationHelper.SbrExt9TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt10, VbrLocalizationHelper.SbrExt10TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt11, VbrLocalizationHelper.SbrExt11TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt12, VbrLocalizationHelper.SbrExt12TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt13, VbrLocalizationHelper.SbrExt13TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt14, VbrLocalizationHelper.SbrExt14TT);
            s += this.form.TableHeader(VbrLocalizationHelper.SbrExt15, VbrLocalizationHelper.SbrExt15TT);
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
                    s += RenderStorageProgressBar(d.FreeSpacePercent);

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
            var table = new CRepoTable();
            return table.Render(scrub);
        }

        public string AddCapacityTierExtTable(bool scrub)
        {
            var table = new CCapacityTierExtTable();
            return table.Render(scrub);
        }

        public string AddArchiveTierExtTable(bool scrub)
        {
            var table = new CArchiveTierExtTable();
            return table.Render(scrub);
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
            var table = new CJobSessionSummaryTable();
            return table.RenderFlat(scrub);
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
            var table = new CJobSessionSummaryTable();
            return table.RenderByJob(scrub);
        }


        public string AddJobInfoTable(bool scrub)
        {
            var table = new CJobInfoTable();
            return table.Render(scrub);
        }
        public void AddSessionsFiles(bool scrub)
        {
            this.df.JobSessionInfoToXml(scrub);
        }

        // --- Requirements tables ---
        public string AddServersRequirementsTable(bool scrub)
        {
            return this.AddRequirementsTable(
                sectionId: "serversrequirements",
                title: "Server Sizing",
                buttonText: "Server Sizing",
                rows: this.df.ServersRequirementsToXml(scrub),
                jsonKey: "serversRequirements",
                doScrub: scrub);
        }

        private string AddRequirementsTable(
            string sectionId,
            string title,
            string buttonText,
            List<string[]> rows,
            string jsonKey,
            bool doScrub)
        {
            string s = this.form.SectionStartWithButton(sectionId, title, buttonText);
            string summary = string.Empty;

            // headers
            s += this.form.TableHeader("Server", "Server name");
            s += this.form.TableHeader("Type", "Role/type");
            s += this.form.TableHeader("Required Cores", "Required CPU cores");
            s += this.form.TableHeader("Available Cores", "Available CPU cores");
            s += this.form.TableHeader("Required RAM (GB)", "Required RAM in GB");
            s += this.form.TableHeader("Available RAM (GB)", "Available RAM in GB");
            s += this.form.TableHeader("Concurrent Tasks", "Current concurrent tasks");
            s += this.form.TableHeader("Suggested Tasks", "Suggested tasks based on sizing");
            s += this.form.TableHeader("Proxy/Repository Names", "Proxy and repository names on this server");
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                if (rows == null || rows.Count == 0)
                {
                    s += "<tr><td colspan='9' style='text-align:center; padding:20px; color:#666;'>" +
                        "<em>No data available. The CSV file may be missing or empty.</em>" +
                        "</td></tr>";
                }
                else
                {
                    foreach (var r in rows)
                    {
                        string server = r[0];
                        string names = r[8];

                        if (doScrub)
                        {
                            server = this.scrub.ScrubItem(server, ScrubItemType.Server);
                            names = this.scrub.ScrubItem(names, ScrubItemType.Server);
                        }

                        double reqCoresVal = 0, availCoresVal = 0, reqRamVal = 0, availRamVal = 0, concurrentTasksVal = 0, suggestedTasksVal = 0;
                        bool parsedReqCores = r.Length > 2 && double.TryParse(r[2], NumberStyles.Any, CultureInfo.InvariantCulture, out reqCoresVal);
                        bool parsedAvailCores = r.Length > 3 && double.TryParse(r[3], NumberStyles.Any, CultureInfo.InvariantCulture, out availCoresVal);
                        bool parsedReqRam = r.Length > 4 && double.TryParse(r[4], NumberStyles.Any, CultureInfo.InvariantCulture, out reqRamVal);
                        bool parsedAvailRam = r.Length > 5 && double.TryParse(r[5], NumberStyles.Any, CultureInfo.InvariantCulture, out availRamVal);
                        bool parsedConTasks = r.Length > 6 && double.TryParse(r[6], NumberStyles.Any, CultureInfo.InvariantCulture, out concurrentTasksVal);
                        bool parsedSugTasks = r.Length > 7 && double.TryParse(r[7], NumberStyles.Any, CultureInfo.InvariantCulture, out suggestedTasksVal);

                        bool reqCoresWarn = parsedReqCores && parsedAvailCores && reqCoresVal > availCoresVal;
                        bool reqRamWarn = parsedReqRam && parsedAvailRam && reqRamVal > availRamVal;
                        bool conTasksWarn = parsedConTasks && parsedSugTasks && concurrentTasksVal > suggestedTasksVal;

                        s += "<tr>";
                        s += this.form.TableData(server, string.Empty);
                        s += this.form.TableData(r[1], string.Empty);

                        if (reqCoresWarn)
                        {
                            s += this.form.TableData(r[2], string.Empty, 1);
                        }
                        else
                        {
                            s += this.form.TableData(r[2], string.Empty);
                        }

                        s += this.form.TableData(r[3], string.Empty);

                        if (reqRamWarn)
                        {
                            s += this.form.TableData(r[4], string.Empty, 1);
                        }
                        else
                        {
                            s += this.form.TableData(r[4], string.Empty);
                        }

                        s += this.form.TableData(r[5], string.Empty);

                        if (conTasksWarn)
                        {
                            s += this.form.TableData(r[6], string.Empty, 1);
                        }
                        else
                        {
                            s += this.form.TableData(r[6], string.Empty);
                        }

                        s += this.form.TableData(r[7], string.Empty);

                        string formattedNames = names?.Replace("/ ", "<br>").Replace("/", "<br>") ?? "";
                        s += this.form.TableData(formattedNames, string.Empty);
                        s += "</tr>";
                    }
                }
            }
            catch (Exception ex)
            {
                this.log.Error($"Requirements table '{title}' import failed. ERROR:");
                this.log.Error("\t" + ex.Message);
                s += "<tr><td colspan='9' style='text-align:center; padding:20px; color:#d9534f;'>" +
                    "<em>Error loading data. See logs for details.</em>" +
                    "</td></tr>";
            }

            s += this.form.SectionEnd(summary);

            // JSON capture
            try
            {
                List<string> headers = new()
                {
                    "Server","Type","RequiredCores","AvailableCores","RequiredRamGb","AvailableRamGb","ConcurrentTasks","SuggestedTasks","Names"
                };

                List<List<string>> jsonRows = (rows ?? new List<string[]>())
                    .Select(r => r.ToList())
                    .ToList();

                SetSection(jsonKey, headers, jsonRows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error($"Failed to capture {jsonKey} JSON section: " + ex.Message);
            }

            return s;
        }

        public string AddConfigurationTablesHeader()
        {
            return this.form.header1("Configuration Tables");
        }

        public string AddConfigurationTablesFooter()
        {
            return string.Empty;
        }

        public string AddProxyInfoHeader()
        {
            return this.form.header1("Proxy Info");
        }

        public string AddProxyInfoFooter()
        {
            return string.Empty;
        }

        public string AddRepositoryInfoHeader()
        {
            return this.form.header1("Repository Info");
        }

        public string AddRepositoryInfoFooter()
        {
            return string.Empty;
        }

        public string AddJobTablesHeader()
        {
            return this.form.header1("Job Tables");
        }

        public string AddJobTablesFooter()
        {
            return string.Empty;
        }

        // Replication section
        public string AddReplicationHeader()
        {
            return this.form.header1("Replication");
        }

        public string AddReplicationFooter()
        {
            return string.Empty;
        }

        public string AddReplicaJobsTable(bool scrub)
        {
            var table = new CReplicaJobsTable();
            return table.Render(scrub);
        }

        public string AddReplicasTable(bool scrub)
        {
            var table = new CReplicasTable();
            return table.Render(scrub);
        }

        public string AddFailoverPlansTable(bool scrub)
        {
            var table = new CFailoverPlansTable();
            return table.Render(scrub);
        }

        // Tape Infrastructure section
        public string AddTapeInfrastructureHeader()
        {
            return this.form.header1("Tape Infrastructure");
        }

        public string AddTapeInfrastructureFooter()
        {
            return string.Empty;
        }

        public string AddTapeServersTable(bool scrub)
        {
            var table = new CTapeServersTable();
            return table.Render(scrub);
        }

        public string AddTapeLibrariesTable(bool scrub)
        {
            var table = new CTapeLibrariesTable();
            return table.Render(scrub);
        }

        public string AddTapeMediaPoolsTable(bool scrub)
        {
            var table = new CTapeMediaPoolsTable();
            return table.Render(scrub);
        }

        public string AddTapeVaultsTable(bool scrub)
        {
            var table = new CTapeVaultsTable();
            return table.Render(scrub);
        }

        // Cloud Connect section
        public string AddCloudConnectHeader()
        {
            return this.form.header1("Cloud Connect");
        }

        public string AddCloudConnectFooter()
        {
            return string.Empty;
        }

        public string AddCloudGatewaysTable(bool scrub)
        {
            var table = new CCloudGatewaysTable();
            return table.Render(scrub);
        }

        public string AddCloudTenantsTable(bool scrub)
        {
            var table = new CCloudTenantsTable();
            return table.Render(scrub);
        }

        // SureBackup Details
        public string AddSureBackupAppGroupsTable(bool scrub)
        {
            var table = new CSureBackupAppGroupsTable();
            return table.Render(scrub);
        }

        public string AddSureBackupVirtualLabsTable(bool scrub)
        {
            var table = new CSureBackupVirtualLabsTable();
            return table.Render(scrub);
        }

        // General Settings section
        public string AddGeneralSettingsHeader()
        {
            return this.form.header1("General Settings");
        }

        public string AddGeneralSettingsFooter()
        {
            return string.Empty;
        }

        public string AddEmailNotificationTable(bool scrub)
        {
            var table = new CEmailNotificationTable();
            return table.Render(scrub);
        }

        /// <summary>
        /// Renders a storage utilization progress bar as a table cell.
        /// freePercent is the percentage of free space (0-100).
        /// </summary>
        private static string RenderStorageProgressBar(decimal freePercent)
        {
            decimal usedPercent = Math.Max(0, Math.Min(100, 100 - freePercent));
            string colorClass = usedPercent >= 80 ? "progress-danger"
                              : usedPercent >= 60 ? "progress-warning"
                              : "progress-ok";
            return $@"<td><div class=""progress-bar""><div class=""progress-track""><div class=""progress-fill {colorClass}"" style=""width:{usedPercent:F0}%""></div></div><div class=""progress-label"">{freePercent:F0}% free</div></div></td>";
        }

        /// <summary>
        /// Generates the KPI summary cards row for the executive dashboard header area.
        /// </summary>
        public string AddKpiRow(bool scrub)
        {
            string version = scrub ? "x.x.x.x" : (CGlobals.VBRFULLVERSION ?? "Unknown");
            string reportDays = CGlobals.ReportDays.ToString();
            string reportDate = DateTime.Now.ToString("yyyy-MM-dd");

            return $@"<div class=""kpi-row"">
  <div class=""kpi-card"">
    <div class=""kpi-label"">VBR Version</div>
    <div class=""kpi-value"">{version}</div>
  </div>
  <div class=""kpi-card"">
    <div class=""kpi-label"">Report Period</div>
    <div class=""kpi-value"">{reportDays} days</div>
  </div>
  <div class=""kpi-card"">
    <div class=""kpi-label"">Report Date</div>
    <div class=""kpi-value"">{reportDate}</div>
  </div>
  <div class=""kpi-card"">
    <div class=""kpi-label"">Report Type</div>
    <div class=""kpi-value"">{(CGlobals.RunSecReport ? "Security" : "Full Health Check")}</div>
  </div>
</div>";
        }

        /// <summary>
        /// Generates the expand/collapse toolbar.
        /// </summary>
        public string AddToolbar()
        {
            return this.form.Toolbar();
        }

    }
}
