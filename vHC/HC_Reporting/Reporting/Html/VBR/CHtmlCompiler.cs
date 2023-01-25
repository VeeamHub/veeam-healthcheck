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
        private string _htmldocOriginal = String.Empty;
        private string _htmldocScrubbed = String.Empty;
        private bool _vbrmode = false;
        private bool _vb365mode = false;

        private bool _scrub = false;

        private CLogger log = CGlobals.Logger;

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
            CHtmlExporter exporter = new("", GetServerName(), "", CGlobals.Scrub);
            exporter.ExportVbrHtml(_htmldocOriginal, false);
            exporter.ExportVbrHtml(_htmldocScrubbed, true);
            if (CGlobals.OpenExplorer)
                exporter.OpenExplorer();
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
            _htmldocOriginal = "<html>";
            _htmldocOriginal += "<head>";
            _htmldocOriginal += "<style>";
            _htmldocOriginal += CssStyler.StyleString();
            _htmldocOriginal += "</style></head>";

            _htmldocScrubbed = "<html>";
            _htmldocScrubbed += "<head>";
            _htmldocScrubbed += "<style>";
            _htmldocScrubbed += CssStyler.StyleString();
            _htmldocScrubbed += "</style></head>";
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
            _htmldocOriginal += _form.body;
            _htmldocScrubbed += _form.body;

            // set correct logo for ORIGINAL and set blank logo for Scrubbed
            _htmldocOriginal += _form.SetHeaderAndLogo(SetLicHolder());
            _htmldocScrubbed += _form.SetHeaderAndLogo(" ");

            _htmldocOriginal += _form.SetBannerAndIntro(false);
            _htmldocScrubbed += _form.SetBannerAndIntro(true);

            //nav
            SetNavigation();


            //tables
            if (CGlobals.RunFullReport)
            {
                _htmldocOriginal += _tables.LicTable(false);
                _htmldocOriginal += _tables.AddBkpSrvTable(false);
            }
            
            if(CGlobals.RunSecReport || CGlobals.RunFullReport)
                _htmldocOriginal += _tables.AddSecSummaryTable(false);

            if (CGlobals.RunFullReport)
            {
                _htmldocOriginal += _tables.AddSrvSummaryTable(false);
                _htmldocOriginal += _tables.AddJobSummaryTable(false);
                _htmldocOriginal += _tables.AddMissingJobsTable(false);
                _htmldocOriginal += _tables.AddProtectedWorkLoadsTable(false);
                _htmldocOriginal += _tables.AddManagedServersTable(false);
                _htmldocOriginal += _tables.AddRegKeysTable(false);
                _htmldocOriginal += _tables.AddProxyTable(false);
                _htmldocOriginal += _tables.AddSobrTable(false);
                _htmldocOriginal += _tables.AddSobrExtTable(false);
                _htmldocOriginal += _tables.AddRepoTable(false);
                _htmldocOriginal += _tables.AddJobConTable(false);
                _htmldocOriginal += _tables.AddTaskConTable(false);
                _htmldocOriginal += _tables.AddJobSessSummTable(false);
                _htmldocOriginal += _tables.AddJobInfoTable(false);
            }



            // anon report
            if (CGlobals.RunFullReport)
            {
                _htmldocScrubbed += _tables.LicTable(true);
                _htmldocScrubbed += _tables.AddBkpSrvTable(true);
            }
            
            
            // anon sec report
            if (CGlobals.RunSecReport || CGlobals.RunFullReport)
                _htmldocScrubbed += _tables.AddSecSummaryTable(true);

            if (CGlobals.RunFullReport)
            {


                _htmldocScrubbed += _tables.AddSrvSummaryTable(true);
                _htmldocScrubbed += _tables.AddJobSummaryTable(true);
                _htmldocScrubbed += _tables.AddMissingJobsTable(true);
                _htmldocScrubbed += _tables.AddProtectedWorkLoadsTable(true);
                _htmldocScrubbed += _tables.AddManagedServersTable(true);
                _htmldocScrubbed += _tables.AddRegKeysTable(true);
                _htmldocScrubbed += _tables.AddProxyTable(true);
                _htmldocScrubbed += _tables.AddSobrTable(true);
                _htmldocScrubbed += _tables.AddSobrExtTable(true);
                _htmldocScrubbed += _tables.AddRepoTable(true);
                _htmldocScrubbed += _tables.AddJobConTable(true);
                _htmldocScrubbed += _tables.AddTaskConTable(true);
                _htmldocScrubbed += _tables.AddJobSessSummTable(true);
                _htmldocScrubbed += _tables.AddJobInfoTable(true);
            }
            //_tables.AddSessionsFiles();

            _htmldocOriginal += "<a>vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";
            _htmldocOriginal += "<script type=\"text/javascript\">";
            _htmldocOriginal += CssStyler.JavaScriptBlock();
            _htmldocOriginal += "</script>";

            _htmldocScrubbed += "<a>vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";
            _htmldocScrubbed += "<script type=\"text/javascript\">";
            _htmldocScrubbed += CssStyler.JavaScriptBlock();
            _htmldocScrubbed += "</script>";

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
            _htmldocOriginal += infoString;
            _htmldocScrubbed += infoString;
        }
        private void AddToHtml(string infoString, bool scrub)
        {
            
        }

        #endregion


    }


}
