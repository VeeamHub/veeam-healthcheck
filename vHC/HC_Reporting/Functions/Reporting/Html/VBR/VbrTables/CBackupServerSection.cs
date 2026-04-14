// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Renders the Backup Server section including config backup and database tables
    /// (formerly AddBkpSrvTable + private helpers in CHtmlTables).
    /// </summary>
    internal class CBackupServerSection
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CLogger log = CGlobals.Logger;

        public CBackupServerSection() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButtonNoTable("vbrserver", VbrLocalizationHelper.BkpSrvTblHead, string.Empty);
            string summary = this.sum.SetVbrSummary();

            BackupServer b = this.df.BackupServerInfoToXml(scrub);
            if (String.IsNullOrEmpty(b.Version))
            {
                b.Version = CGlobals.VBRFULLVERSION;
            }

            s += AddBackupServerDetails(b);

            s += this.form.Subsection("Config Backup");
            s += "<table class=\"content-table\">";
            s += "<thead><tr>";
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEnabled, VbrLocalizationHelper.BstCfgEnabledTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgLastRes, VbrLocalizationHelper.BstCfgLastResTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCfgEncrypt, VbrLocalizationHelper.BstCfgEncTT);
            s += this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblTarget, VbrLocalizationHelper.BstCfgTarTT);
            s += this.form.TableHeaderEnd();
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

            s += this.form.TableData(CHtmlTables.Badge(b.ConfigBackupLastResult), string.Empty);
            if (b.ConfigBackupEncryption)
            {
                s += this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                s += this.form.TableData(this.form.False, string.Empty);
            }

            s += this.form.TableData(b.ConfigBackupTarget, string.Empty);
            s += "</tr></tbody></table>";

            s += this.ConfigDbTable(b);

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
                CHtmlTables.SetSectionPublic("backupServer", headers, rows, summary);
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to capture backupServer JSON section: " + ex.Message);
            }

            return s;
        }

        private static string AddBackupServerDetails(BackupServer b)
        {
            CVbrServerTable t = new(b);
            List<Tuple<string, string>> vbrServerData = new();

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

            s += "<table class=\"content-table bold-first-col\"><thead><tr>";
            s += headers;
            s += "</tr></thead><tbody><tr>";
            s += data;
            s += "</tr></tbody></table>";

            return s;
        }

        private string ConfigDbTable(BackupServer b)
        {
            string s = string.Empty;
            s += this.form.Subsection("Config Database");
            s += "<table class=\"content-table\">";
            s += "<thead><tr>";

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

                s += "</tr></tbody></table>";
            }
            catch (Exception e)
            {
                this.log.Error("Failed to add backup server table. Error:");
                this.log.Error("\t" + e.Message);
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
                s += this.form.TableData("<span class=\"text-muted\">&mdash;</span>", dbCoresToolTip);
            }
            else
            {
                s += this.form.TableData(b.DbCores.ToString(), dbCoresToolTip);
            }

            if (b.DbRAM == 0)
            {
                s += this.form.TableData("<span class=\"text-muted\">&mdash;</span>", dbRamToolTip);
            }
            else
            {
                s += this.form.TableData(b.DbRAM.ToString(), dbRamToolTip);
            }

            return s;
        }
    }
}
