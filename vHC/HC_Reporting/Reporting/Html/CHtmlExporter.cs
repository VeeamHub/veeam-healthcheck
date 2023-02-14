using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
            else if(!CGlobals.Scrub)
                htmlCore = "\\" + _htmlName + "_VB365" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            string name = n + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(htmlString);
            }
            log.Info("writing HTML to file..done!");
            //OpenHtml();
            if (CGlobals.OpenExplorer)
                OpenExplorer();
            if(CGlobals.OpenHtml)
                OpenHtml();
        }
        public void ExportVbrHtml(string htmlString, bool scrub)
        {
            log.Info("exporting xml to html");

            DateTime dateTime = DateTime.Now;
            string n = CGlobals._desiredPath;
            string installID = "";
            try
            {
                if (!String.IsNullOrEmpty(Collection.LogParser.CLogOptions.INSTALLID)) 
                    installID = Collection.LogParser.CLogOptions.INSTALLID.Substring(0, 7);
            }
            catch(Exception e) { installID = "anon"; }
            if (!Directory.Exists(n))
                Directory.CreateDirectory(n);
            if (!Directory.Exists(n + "\\Anon"))
                Directory.CreateDirectory(n + "\\Anon");
            if (!Directory.Exists(n + "\\Original"))
                Directory.CreateDirectory(n + "\\Original");
            string htmlCore = "";
            if (scrub)
                htmlCore = "\\Anon\\" + _htmlName + "_VBR" + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if (!scrub)
            {
                htmlCore = "\\Original\\" + _htmlName + "_VBR" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";

            }
            string name = n + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(htmlString);
            }
            log.Info("exporting xml to html..done!");
            //if (CGlobals.OpenExplorer)
            //    OpenExplorer();
            if (CGlobals.OpenHtml)
                OpenHtml();
        }
        public void ExportVbrSecurityHtml(string htmlString, bool scrub)
        {
            log.Info("exporting xml to html");

            DateTime dateTime = DateTime.Now;
            string n = CGlobals._desiredPath;
            string installID = "";
            try
            {
                if (!String.IsNullOrEmpty(Collection.LogParser.CLogOptions.INSTALLID))
                    installID = Collection.LogParser.CLogOptions.INSTALLID.Substring(0, 7);
            }
            catch (Exception e) { installID = "anon"; }
            if (!Directory.Exists(n))
                Directory.CreateDirectory(n);
            if (!Directory.Exists(n + "\\Anon"))
                Directory.CreateDirectory(n + "\\Anon");
            if (!Directory.Exists(n + "\\Original"))
                Directory.CreateDirectory(n + "\\Original");
            string htmlCore = "";
            if (scrub)
                htmlCore = "\\Anon\\" + _htmlName + "_VBR_Security" + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if (!scrub)
            {
                htmlCore = "\\Original\\" + _htmlName + "_VBR_Security" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";

            }
            string name = n + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(htmlString);
            }
            log.Info("exporting xml to html..done!");
            //if (CGlobals.OpenExplorer)
            //    OpenExplorer();
            if (CGlobals.OpenHtml)
                OpenHtml();
        }

        public void OpenExplorer()
        {
            Process.Start("explorer.exe", CVariables.desiredDir);
        }
        public void OpenHtml()
        {
            log.Info("opening html");
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                WebBrowser w1 = new();
                //w1.Navigate(("C:\\temp\\HC_Report.html", null,null,null));
                //string report = SaveSessionReportToTemp(_htmlOut);
                //string s = String.Format("cmd", $"/c start {0}", report);
                //Process.Start(new ProcessStartInfo(s));

                var p = new Process();
                p.StartInfo = new ProcessStartInfo(_latestReport)
                {
                    UseShellExecute = true
                };
                p.Start();
            });

            log.Info("opening html..done!");

            //CDataExport ex = new();
            //ex.OpenFolder();
        }
    }
}
