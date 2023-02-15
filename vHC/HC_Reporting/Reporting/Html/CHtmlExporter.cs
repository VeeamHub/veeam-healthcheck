using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Xsl;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html
{
    internal class CHtmlExporter
    {
        private CLogger log = CGlobals.Logger;
        private readonly string _testFile;
        private readonly string _htmlName = "Veeam Health Check Report";
        //private readonly string _htmlSecurityReportName = "Veeam Healch Check Security Report";


        // path settings
        private string _basePath = CGlobals._desiredPath;
        private string _anonPath = CGlobals._desiredPath + CVariables._safeSuffix;
        private string _origPath = CGlobals._desiredPath + CVariables._unsafeSuffix;


        private string _backupServerName;
        private string _latestReport;
        private bool _scrub;
        private CScrubHandler _scrubber;
        private string _outPath = CVariables.unsafeDir;
        private string _styleSheet;

        public CHtmlExporter(string xmlFileName, string serverName, string styleSheet, bool scrub)
        {
            _testFile = xmlFileName;
            _backupServerName = serverName;
            _styleSheet = styleSheet;
            _scrub = scrub;
            if (scrub)
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

        public void ExportVb365Html(string htmlString)
        {
            log.Info("writing HTML to file...");

            DateTime dateTime = DateTime.Now;
            string n = CGlobals._desiredPath;
            string installID = "";
            try
            {
                if (!String.IsNullOrEmpty(Collection.LogParser.CLogOptions.INSTALLID)) ;
                installID = Collection.LogParser.CLogOptions.INSTALLID.Substring(0, 7);
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
            if (CGlobals.OpenExplorer)
                OpenExplorer();
            OpenHtmlIfEnabled();
        }
        public void ExportVbrHtml(string htmlString, bool scrub)
        {
            log.Info("exporting xml to html");

            _latestReport = SetReportNameAndPath(scrub, "VBR");

            WriteHtmlToFile(htmlString);
            log.Info("exporting xml to html..done!");

            OpenHtmlIfEnabled();
        }

        public void ExportVbrSecurityHtml(string htmlString, bool scrub)
        {
            log.Info("exporting xml to html");


            _latestReport = SetReportNameAndPath(scrub, "VBR_Security");

            WriteHtmlToFile(htmlString);
            log.Info("exporting xml to html..done!");

            if (CGlobals.OpenHtml)
                OpenHtmlIfEnabled();
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
            DateTime dateTime = DateTime.Now;
            string installID = TrySetInstallId();

            string htmlCore = "";
            if (scrub)
                htmlCore = _anonPath + "\\"+ _htmlName + "_" + vbrOrVb365 + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if (!scrub)
            {
                htmlCore = _origPath + "\\" + _htmlName + "_" + vbrOrVb365 + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
                //log.Warning("htmlcore = " + htmlCore, false);
            }
            return htmlCore;
        }
        private string TrySetInstallId()
        {
            string id = "";
            try
            {
                if (!String.IsNullOrEmpty(Collection.LogParser.CLogOptions.INSTALLID))
                {
                    id = Collection.LogParser.CLogOptions.INSTALLID.Substring(0, 7);

                }
            }
            catch (Exception e)
            {
                id = "anon";
            }

            return id;
        }
        public void OpenExplorer()
        {
            Process.Start("explorer.exe", CVariables.desiredDir);
        }
        public void OpenHtmlIfEnabled()
        {
            if (CGlobals.OpenHtml)
            {
                log.Info("opening html");
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    WebBrowser w1 = new();

                    var p = new Process();
                    p.StartInfo = new ProcessStartInfo(_latestReport)
                    {
                        UseShellExecute = true
                    };
                    p.Start();
                });

                log.Info("opening html..done!");
            }

        }
    }
}
