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
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html
{
    internal class CHtmlExporter
    {
        private CLogger log = VhcGui.log;
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
            if(scrub)
                _scrubber = new CScrubHandler();
        }
        public void ExportHtml()
        {
            log.Info("exporting xml to html");
            
            string s = TransformXMLToHTML(_testFile, _styleSheet);
            DateTime dateTime = DateTime.Now;
            string n = VhcGui._desiredPath;
            string htmlCore = "\\" + _htmlName + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd_HHmmss") + ".html";
            //string name = _outPath + htmlCore;
            string name = n + htmlCore;
            //if (_scrub)
            //    name = CVariables.safeDir + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(s);
            }
            log.Info("exporting xml to html..done!");
            //OpenHtml();
            if (VhcGui._openExplorer)
                OpenExplorer();
        }
        public void ExportVb365Html(string htmlString)
        {
            log.Info("writing HTML to file...");

            DateTime dateTime = DateTime.Now;
            string n = VhcGui._desiredPath;
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
            if (VhcGui._scrub)
                htmlCore = "\\" + _htmlName + "_VB365" + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if(!VhcGui._scrub)
                htmlCore = "\\" + _htmlName + "_VB365" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            string name = n + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(htmlString);
            }
            log.Info("writing HTML to file..done!");
            //OpenHtml();
            if (VhcGui._openExplorer)
                OpenExplorer();
            if(VhcGui._openHtml)
                OpenHtml();
        }
        public void ExportVbrHtml(string htmlString)
        {
            log.Info("exporting xml to html");

            DateTime dateTime = DateTime.Now;
            string n = VhcGui._desiredPath;
            string installID = "";
            try
            {
                if (!String.IsNullOrEmpty(Collection.LogParser.CLogOptions.INSTALLID)) ;
                installID = Collection.LogParser.CLogOptions.INSTALLID.Substring(0, 7);
            }
            catch(Exception e) { installID = "anon"; }
            if (!Directory.Exists(n))
                Directory.CreateDirectory(n);
            string htmlCore = "";
            if (VhcGui._scrub)
                htmlCore = "\\" + _htmlName + "_VBR" + "_" + installID + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            else if (!VhcGui._scrub)
                htmlCore = "\\" + _htmlName + "_VBR" + "_" + _backupServerName + dateTime.ToString("_yyyy.MM.dd.HHmmss") + ".html";
            string name = n + htmlCore;
            _latestReport = name;

            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(htmlString);
            }
            log.Info("exporting xml to html..done!");
            if (VhcGui._openExplorer)
                OpenExplorer();
            if (VhcGui._openHtml)
                OpenHtml();
        }
        public void ExportHtml(string xmlFile)
        {
            log.Info("exporting xml to html");
            string s = TransformXMLToHTML(xmlFile, _styleSheet);
            string reportsFolder = "\\JobSessionReports\\";

            string jname = Path.GetFileNameWithoutExtension(xmlFile);
            if (_scrub)
                jname = _scrubber.ScrubItem(jname, "job");
            DateTime dateTime = DateTime.Now;

            string n = VhcGui._desiredPath;
            string outFolder = _outPath + reportsFolder;
            outFolder = n + reportsFolder;
            //if (_scrub)
            //    outFolder = CVariables.safeDir + reportsFolder;
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);
            string name = outFolder + jname + dateTime.ToString("_yyyy.MM.dd_HHmmss") + ".html";
            _latestReport = name;
            using (StreamWriter sw = new StreamWriter(name))
            {
                sw.Write(s);
            }
            log.Info("exporting xml to html..done!");
            //OpenHtml();
        }
        public static string TransformXMLToHTML(string xmlFile, string xsltFile)
        {
            //log.Info("transforming XML to HTML");
            var transform = new XslCompiledTransform();
            XsltSettings settings = new XsltSettings(true, true);
            XsltArgumentList xList = new();
            using (var reader = XmlReader.Create(File.OpenRead(xsltFile)))
            {
                transform.Load(reader, settings, new XmlUrlResolver());
            }

            var results = new StringWriter();
            using (var reader = XmlReader.Create(File.OpenRead(xmlFile)))
            {
                transform.Transform(reader, null, results);
            }
            //log.Info("transforming XML to HTML..done!");
            return results.ToString();
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
