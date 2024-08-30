// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Functions.Reporting.Pdf;

namespace VeeamHealthCheck.Functions.Reporting.Html
{
    public class CHtmlExporter
    {
        private CLogger log = CGlobals.Logger;
        //private readonly string _testFile;
        private readonly string _htmlName = "Veeam Health Check Report";
        //private readonly string _htmlSecurityReportName = "Veeam Healch Check Security Report";


        // path settings
        private string _basePath = CGlobals._desiredPath;
        private string _anonPath = CGlobals._desiredPath + CVariables._safeSuffix;
        private string _origPath = CGlobals._desiredPath + CVariables._unsafeSuffix;


        private string _backupServerName;
        private string _latestReport;
        private CScrubHandler _scrubber;
        private string _outPath = CVariables.unsafeDir;

        public CHtmlExporter(string serverName)
        {
            CheckOutputDirsExist();
            //_testFile = xmlFileName;
            _backupServerName = serverName;
            if (CGlobals.Scrub)
                _scrubber = CGlobals.Scrubber;
        }
        private void CheckOutputDirsExist()
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
            if (!Directory.Exists(_anonPath))
                Directory.CreateDirectory(_anonPath);
            if (!Directory.Exists(_origPath))
                Directory.CreateDirectory(_origPath);
        }

        public int ExportVb365Html(string htmlString)
        {
            log.Info("writing HTML to file...");


            DateTime dateTime = DateTime.Now;
            string n = CGlobals._desiredPath;
            string installID = "";
            try
            {
                if (!string.IsNullOrEmpty(CLogOptions.INSTALLID))
                    installID = CLogOptions.INSTALLID.Substring(0, 7);
            }
            catch (Exception e) { installID = "anon"; }
            if (!Directory.Exists(n))
                Directory.CreateDirectory(n);
            string htmlCore = "";
            if (CGlobals.Scrub)
                htmlCore = "\\" + _htmlName + "_VB365" + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if (!CGlobals.Scrub)
                htmlCore = "\\" + _htmlName + "_VB365" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            string name = n + htmlCore;
            _latestReport = name;//SetReportNameAndPath(CGlobals.Scrub, "VB365");

            WriteHtmlToFile(htmlString);
            log.Info("writing HTML to file..done!");
            //OpenHtml();
            OpenExplorer();
            OpenHtmlIfEnabled(CGlobals.OpenHtml);
            return 0;




        }
        public int ExportVbrHtml(string htmlString, bool scrub)
        {
            

            try
            {
                log.Info("exporting xml to html");
                _latestReport = SetReportNameAndPath(scrub, "VBR");
                WriteHtmlToFile(htmlString);
                log.Info("exporting xml to html..done!");

                //test export to PDF:
                if (!scrub && CGlobals.EXPORTPDF)
                {
                    ExportHtmlStringToPDF(htmlString);
                }

                OpenHtmlIfEnabled(CGlobals.OpenHtml);
                return 0;
            }
            catch(Exception e)
            {
                log.Error("Failed at HTML Export:");
                log.Error("\t" +e.Message); return 1;
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

            pdf.ConvertHtmlToPdf(htmlShowAll, _latestReport.Replace(".html", ".pdf"));
            pdf.Dispose();
        }

        public int ExportVbrSecurityHtml(string htmlString, bool scrub)
        {
            log.Info("exporting xml to html");
            _latestReport = SetReportNameAndPath(scrub, "VBR_Security");

            WriteHtmlToFile(htmlString);
            log.Info("exporting xml to html..done!");

            OpenHtmlIfEnabled(CGlobals.OpenHtml);

            return 0;


        }
        private void WriteHtmlToFile(string htmlString)
        {
            using (StreamWriter sw = new StreamWriter(_latestReport))
            {
                sw.Write(htmlString);
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
                    installID = TrySetInstallId(CLogOptions.INSTALLID);

                    htmlCore = _anonPath + "\\" + _htmlName + "_" + vbrOrVb365 + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";

                }
                else if (!scrub)
                {
                    htmlCore = _origPath + "\\" + _htmlName + "_" + vbrOrVb365 + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
                    //log.Warning("htmlcore = " + htmlCore, false);
                }
                return htmlCore;
            }
            catch(Exception ex)
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
            log.Debug("InstallID String = " + id);
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
            catch(Exception ex)
            {
                log.Error("Failed to get install ID.");
                log.Error(ex.Message);
                return "anon";
            }
        }
        public void OpenExplorer()
        {
            if (CGlobals.OpenExplorer)
                Process.Start("explorer.exe", CVariables.desiredDir);
        }
        public int OpenHtmlIfEnabled(bool open)
        {
            if (open)
            {
                log.Info("opening html");
                ExecBrowser();

                log.Info("opening html..done!");
                return 0;
            }
            else
            {
                log.Warning("HTML not opened. Option not enabled");
                return 1;

            }
        }
        private void ExecBrowser()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                WebBrowser w1 = new();

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(_latestReport)
                {
                    UseShellExecute = true
                };
                p.Start();
            });
        }
    }
}
