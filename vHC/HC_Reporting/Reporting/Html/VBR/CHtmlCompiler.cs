using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html
{
    internal class CHtmlCompiler
    {
        private string xslFileName = "StyleSheets\\myHtml.xsl"; // maybe just do memory instead of disk??
        private string _htmldoc = String.Empty;
        private bool _vbrmode = false;
        private bool _vb365mode = false;

        private bool _scrub = false;

        private CLogger log = VhcGui.log;

        CHtmlFormatting _form = new();
        CHtmlTables _tables = new();

        // section links



        public CHtmlCompiler()
        {
            log.Info("Init VBR Compiler");
            FormHeader();
            FormBody();
            ExportHtml();
            log.Info("Init VBR Compiler...done!");
        }
        private void ExportHtml()
        {
            CHtmlExporter exporter = new("", GetServerName(), "", VhcGui._scrub);
            exporter.ExportVbrHtml(_htmldoc);
        }
        private string GetServerName()
        {
            log.Info("Checking for server name...");
            return Dns.GetHostName();
            log.Info("Checking for server name...done!");
        }
        public void Dispose()
        {

        }

        private void FormHeader()
        {
            log.Info("[HTML] Forming Header...");
            _htmldoc = "<html>";
            _htmldoc += "<head>";
            _htmldoc += "<style>";
            _htmldoc += CssStyler.StyleString();
            _htmldoc += "</style></head>";

            log.Info("[HTML] Forming Header...done!");
            //FormBody();
        }

        #region HtmlHeaders



        private void SetNavigation()
        {
            log.Info("[HTML] setting HTML navigation");
            AddToHtml(DivId("navigation"));
            AddToHtml(String.Format("<h4>{0}</h4>", ResourceHandler.NavHeader));
            AddToHtml(String.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", ResourceHandler.NavColapse));
            NavTable();


            AddToHtml(_form._endDiv);
            log.Info("[HTML] setting HTML navigation...done!");
        }



        private string SetLicHolder()
        {
            log.Info("Setting license holder name...");
            CCsvParser csv = new();
            var lic = csv.GetDynamicLicenseCsv();
            log.Info("Setting license holder name...done!");
            foreach (var l in lic)
                return l.licensedto;
            return "";
        }

        #endregion
        private void FormBody()
        {
            log.Info("[HTML] forming HTML body");
            _htmldoc += _form.body;

            if(!VhcGui._scrub)
                _htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
            if(VhcGui._scrub)
                _htmldoc += _form.SetHeaderAndLogo(" ");
            _htmldoc += _form.SetBannerAndIntro();

            //nav
            SetNavigation();

            //tables
            _htmldoc += _tables.LicTable();
            _htmldoc += _tables.AddBkpSrvTable();
            _htmldoc += _tables.AddSecSummaryTable();
            _htmldoc += _tables.AddSrvSummaryTable();
            _htmldoc += _tables.AddJobSummaryTable();
            _htmldoc += _tables.AddMissingJobsTable();
            _htmldoc += _tables.AddProtectedWorkLoadsTable();
            _htmldoc += _tables.AddManagedServersTable();
            _htmldoc += _tables.AddRegKeysTable();
            _htmldoc += _tables.AddProxyTable();
            _htmldoc += _tables.AddSobrTable();
            _htmldoc += _tables.AddSobrExtTable();
            _htmldoc += _tables.AddRepoTable();
            _htmldoc += _tables.AddJobConTable();
            _htmldoc += _tables.AddTaskConTable();
            _htmldoc += _tables.AddJobSessSummTable();
            _htmldoc += _tables.AddJobInfoTable();

            _tables.AddSessionsFiles();

            _htmldoc += "<a>vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";

            _htmldoc += "<script type=\"text/javascript\">";
            _htmldoc += CssStyler.JavaScriptBlock();
            _htmldoc += "</script>";

            log.Info("[HTML] forming HTML body...done!");
        }

        #region TableFormation

        private void NavTable()
        {
            string tableString =
    "<table border=\"0\" style=\"background: \">" +
    "<tbody>";
            tableString += _tables.MakeNavTable();




            tableString +=
                "</tbody>" +
                "</table>" +
                //BackToTop() +
                "</div>";
            AddToHtml(tableString);
        }
        private void Table(string header, string type)
        {
            string logString = string.Format("[HTML] creating table {0} of type {1}", header, type);
            log.Info(logString);
            //CHtmlTables tables = new();
            string tableString = DivId(type) +
                h2UnderLine(header) +
                "<table border=\"1\" style=\"background: lightgray\">" +
                "<tbody>";
            switch (type)
            {
                case "license":
                    //tableString += (tables.AddLicHeaderToTable());
                    tableString += (_tables.LicTable());
                    break;
                case "navigation":
                    tableString += _tables.MakeNavTable();
                    break;
                case "vbrInfo":
                    tableString += _tables.AddBkpSrvTable();
                    break;
                case "secSum":
                    tableString += _tables.AddSecSummaryTable();
                    break;
                case "srvSummary":
                    tableString += _tables.AddSrvSummaryTable();
                    break;
                case "jobSummary":
                    tableString += _tables.AddJobSummaryTable();
                    break;
                case "missingJobs":
                    tableString += _tables.AddMissingJobsTable();
                    break;
                case "protectedWklds":
                    tableString += _tables.AddProtectedWorkLoadsTable();
                    break;
                case "serverInfo":
                    tableString += _tables.AddManagedServersTable();
                    break;
                case "regKeys":
                    tableString += _tables.AddRegKeysTable();
                    break;
                case "proxyInfo":
                    tableString += _tables.AddProxyTable();
                    break;
                case "sobr":
                    tableString += _tables.AddSobrTable();
                    break;
                case "extents":
                    tableString += _tables.AddSobrExtTable();
                    break;
                case "repos":
                    tableString += _tables.AddRepoTable();
                    break;
                case "jobConcurrency":
                    tableString += _tables.AddJobConTable();
                    break;
                case "taskConcurrency":
                    tableString += _tables.AddTaskConTable();
                    break;
                case "jobSessSummary":
                    tableString += _tables.AddJobSessSummTable();
                    break;
                case "jobs":
                    tableString += _tables.AddJobInfoTable();
                    break;
                default:
                    break;
            }




            tableString +=
                "</tbody>" +
                "</table>";
            AddToHtml(tableString);
        }
        private void EndSection()
        {
            _htmldoc += BackToTop() +
                "</div>"; ;
        }


        /*
 Server Summary
Job summary
missing jobs types
protected workloads
managed server info
regkeys
Proxy info
SOBR 
Extents
Repos
Job con
Task COn
Job Session Sum
Job Info
 */

        private string BackToTop()
        {
            return String.Format("<a href=\"#top\">Back To Top</a>");
        }

        #endregion

        #region HtmlFunctions
        private string DivId(string id)
        {
            return String.Format("<div id={0}>", id);
        }
        private string h2UnderLine(string text)
        {
            return String.Format("<h2><u>{0}</u></h2>", text);
        }

        private void AddToHtml(string infoString)
        {
            _htmldoc += infoString;
        }

        #endregion


    }


}
