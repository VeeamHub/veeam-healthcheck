// <copyright file="CHtmlExporter.cs" company="adamcongdon.net">
// Copyright (c) 2024 adamcongdon.net. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Reporting.Html.Exportables;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    /// <summary>
    /// Provides methods for exporting Veeam Health Check reports in HTML, PDF, and JSON formats.
    /// </summary>
        public class CHtmlExporter
        {
        private readonly string htmlName = "Veeam Health Check Report";
        private CLogger log = CGlobals.Logger;

        // path settings
        private string basePath = CGlobals._desiredPath;
        private string anonPath = CGlobals._desiredPath + CVariables._safeSuffix;
        private string origPath = CGlobals._desiredPath + CVariables._unsafeSuffix;

        private string backupServerName;
        private string latestReport;

        public CHtmlExporter(string serverName)
        {
            this.CheckOutputDirsExist();

            // _testFile = xmlFileName;
            this.backupServerName = serverName;

        }

        private void CheckOutputDirsExist()
        {
            if (!Directory.Exists(this.basePath))
            {
                Directory.CreateDirectory(this.basePath);
            }

            if (!Directory.Exists(this.anonPath))
            {
                Directory.CreateDirectory(this.anonPath);
            }

            if (!Directory.Exists(this.origPath))
            {
                Directory.CreateDirectory(this.origPath);
            }
        }

        public int ExportHtmlVb365(string htmlString, bool scrub)
        {

            try
            {
                this.log.Info("exporting xml to html");
                this.latestReport = this.SetReportNameAndPath(scrub, "VB365");
                this.WriteHtmlToFile(htmlString);
                this.log.Info("exporting xml to html..done!");

                // test export to PDF:
                if (!scrub && CGlobals.EXPORTPDF)
                {
                    this.ExportHtmlStringToPDF(htmlString);

                }

                this.OpenHtmlIfEnabled(CGlobals.OpenHtml);
                return 0;
            }
            catch (Exception e)
            {
                this.log.Error("Failed at HTML Export:");
                this.log.Error("\t" + e.Message); return 1;
            }

        }

        public int ExportVbrHtml(string htmlString, bool scrub)
        {
            try
            {
                this.log.Info("exporting xml to html");
                this.latestReport = this.SetReportNameAndPath(scrub, "VBR");
                this.WriteHtmlToFile(htmlString);
                this.log.Info("exporting xml to html..done!");

                // Export JSON report alongside HTML
                this.ExportJsonReport(scrub);

                // test export to PDF:
                if (!scrub && CGlobals.EXPORTPDF)
                {
                    this.ExportHtmlStringToPDF(htmlString);
                }

                this.OpenHtmlIfEnabled(CGlobals.OpenHtml);
                return 0;
            }
            catch (Exception e)
            {
                this.log.Error("Failed at HTML Export:");
                this.log.Error("\t" + e.Message); return 1;
            }

        }

        private void ExportHtmlStringToPDF(string htmlString)
        {
            HtmlToPdfConverter pdf = new HtmlToPdfConverter();

            // find all instances of "display: none;" and replace with "display: block;" in the htmlString
            string htmlShowAll = htmlString.Replace("style=\"display: none\">", "style=\"display: block\">");

            // find all instances of class="collapsible classBtn" and replace with class="collapsible classBtn active"
            htmlShowAll = htmlShowAll.Replace("collapsible classBtn", "collapsible classBtn active");

            // find all instance of overflow: scroll; and replace with overflow: visible;
            htmlShowAll = htmlShowAll.Replace("overflow: scroll;", "overflow: visible;");

            htmlShowAll = htmlShowAll.Replace("overflow: hidden;", "overflow: visible;");

            // replace font-family: Tahoma with font-family: noto sans symbols
            // htmlShowAll = htmlShowAll.Replace("font-family: Tahoma !important;", "font-family: 'NotoSansSymbols';\r\n            src: url('https://fonts.googleapis.com/css2?family=Noto+Sans+Symbols&display=swap');");

            // replace green checkmark with checkmark:
            htmlShowAll = htmlShowAll.Replace("&#9989;", "✓");
            htmlShowAll = htmlShowAll.Replace("&#9744;", "");
            htmlShowAll = htmlShowAll.Replace("⚠️", "(!)");
            htmlShowAll = htmlShowAll.Replace("&#9432;", "ℹ️");

            pdf.ConvertHtmlToPdf(htmlShowAll, this.latestReport.Replace(".html", ".pdf"));
            pdf.Dispose();

            // var htmlToDocx = new CHtmlToDocx();
            // htmlToDocx.ExportHtmlToDocx(htmlShowAll, _latestReport.Replace(".html", ".docx"));
        }

        public int ExportVbrSecurityHtml(string htmlString, bool scrub)
        {
            this.log.Info("exporting xml to html");
            this.latestReport = this.SetReportNameAndPath(scrub, "VBR_Security");

            this.WriteHtmlToFile(htmlString);
            this.log.Info("exporting xml to html..done!");

            this.OpenHtmlIfEnabled(CGlobals.OpenHtml);

            return 0;


        }
        private void WriteHtmlToFile(string htmlString)
        {
            using (StreamWriter sw = new StreamWriter(this.latestReport))
            {
                sw.Write(htmlString);
            }
        }

        private void ExportJsonReport(bool scrub)
        {
            try
            {
                if (CGlobals.FullReportJson == null)
                {
                    this.log.Warning("JSON report data not available, skipping JSON export");
                    return;
                }

                string jsonPath = this.latestReport.Replace(".html", ".json");
                this.log.Info("Exporting JSON report to: " + jsonPath);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CGlobals.FullReportJson, options);
                File.WriteAllText(jsonPath, json);

                this.log.Info("JSON report exported successfully");
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to export JSON report: " + ex.Message);
            }
        }

        private string SetReportNameAndPath(bool scrub, string vbrOrVb365)
        {
            string installID = null;
            string htmlCore = "";

            try
            {
                DateTime dateTime = DateTime.Now;

                if (scrub)
                {
                    installID = this.TrySetInstallId(CLogOptions.INSTALLID);

                    htmlCore = this.anonPath + "\\" + this.htmlName + "_" + vbrOrVb365 + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";

                }
                else if (!scrub)
                {
                    htmlCore = this.origPath + "\\" + this.htmlName + "_" + vbrOrVb365 + "_" + this.backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
                    //log.Warning("htmlcore = " + htmlCore, false);
                }
                return htmlCore;
            }
            catch (Exception ex)
            {
                //log.Debug("Failed to set report name & path");
                //log.Debug(ex.Message);
                //log.Debug("variables below:");
                //log.Debug("\t" + scrub.ToString());
                //log.Debug("\t" + vbrOrVb365);
                //log.Debug("\t" + htmlCore);
                //log.Debug("\t" + installID);
                //log.Debug("\t" + _anonPath);
                //log.Debug("\t" + _htmlName);
                //log.Debug("\t" + _origPath);
                //log.Debug("\t" + _backupServerName);
                //log.Debug("\t" + _anonPath);
                //log.Debug("\t" + _anonPath);
                //log.Debug("\t" + );
                return null;
            }

        }
        private string TrySetInstallId(string id)
        {
            this.log.Debug("InstallID String = " + id);
            try
            {

                if (!string.IsNullOrEmpty(id))
                {
                    return id.Substring(0, 7);
                }
                else
                {
                    return "anon";
                }
            }
            catch (Exception ex)
            {
                this.log.Error("Failed to get install ID.");
                this.log.Error(ex.Message);
                return "anon";
            }
        }
        public void OpenExplorer()
        {
            if (CGlobals.OpenExplorer)
            {
                Process.Start("explorer.exe", CVariables.desiredDir);
            }
        }
        public int OpenHtmlIfEnabled(bool open)
        {
            if (open)
            {
                this.log.Info("opening html");
                this.ExecBrowser();

                this.log.Info("opening html..done!");
                return 0;
            }
            else
            {
                this.log.Warning("HTML not opened. Option not enabled");
                return 1;

            }
        }
        private void ExecBrowser()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                WebBrowser w1 = new();

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(this.latestReport)
                {
                    UseShellExecute = true
                };
                p.Start();
            });
        }
    }
}
