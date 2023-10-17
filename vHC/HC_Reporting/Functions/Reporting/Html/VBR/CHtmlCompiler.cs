// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Net;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CHtmlCompiler
    {
        private string _htmldocOriginal = string.Empty;
        private string _htmldocScrubbed = string.Empty;


        private CLogger log = CGlobals.Logger;

        CHtmlFormatting _form = new();
        CHtmlTables _tables = new();

        private string logStart = "[VbrHtmlCompiler]\t";

        // section links



        public CHtmlCompiler()
        {

        }
        public void RunFullVbrReport()
        {
            log.Info(logStart + "Init full report");
            FormHeader();
            FormVbrFullBody();
            ExportHtml();
            log.Info(logStart + "Init full report...done!");
        }
        public void RunSecurityReport()
        {
            log.Info(logStart + "Init Security Report");
            FormHeader();
            FormSecurityBody();
            ExportSecurityHtml();
            log.Info(logStart + "Init Security Report");
        }
        private void ExportHtml()
        {
            CHtmlExporter exporter = new(GetServerName());
            exporter.ExportVbrHtml(_htmldocOriginal, false);
            exporter.ExportVbrHtml(_htmldocScrubbed, true);
            if (CGlobals.OpenExplorer)
                exporter.OpenExplorer();
        }
        private void ExportSecurityHtml()
        {
            CHtmlExporter exporter = new(GetServerName());
            exporter.ExportVbrSecurityHtml(_htmldocOriginal, false);
            //exporter.ExportVbrHtml(_htmldocScrubbed, true);
            if (CGlobals.OpenExplorer)
                exporter.OpenExplorer();
        }
        private string GetServerName()
        {
            log.Info("Checking for server name...");
            return Dns.GetHostName();
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


        private void SetUniversalNavStart()
        {
            log.Info("[HTML] setting HTML navigation");
            AddToHtml(DivId("navigation"));
            AddToHtml(string.Format("<h4>{0}</h4>", VbrLocalizationHelper.NavHeader));
            AddToHtml(string.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", VbrLocalizationHelper.NavColapse));
        }
        private void SetUniversalNavEnd()
        {
            AddToHtml(_form._endDiv);
            log.Info("[HTML] setting HTML navigation...done!");
        }
        private void SetNavigation()
        {
            SetUniversalNavStart();
            NavTable();
            SetUniversalNavEnd();


        }
        private void SetSecurityNavigations()
        {
            SetUniversalNavStart();
            SecurityNavTable();
            SetUniversalNavEnd();
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
        private string FormBodyStart(string htmlString, bool scrub)
        {
            htmlString += _form.body;
            if (scrub)
            {
                htmlString += _form.SetHeaderAndLogo(" ");
                htmlString += _form.SetBannerAndIntro(true);
            }
            else
            {
                htmlString += _form.SetHeaderAndLogo(SetLicHolder());
                htmlString += _form.SetBannerAndIntro(false);
            }

            return htmlString;
        }
        private string SetVbrSecurityHeader()
        {
            return _form.SetHeaderAndLogo(SetLicHolder());
        }
        private string FormSecurityBodyStart(string htmlString, bool scrub)
        {
            htmlString += _form.body;
            htmlString += SetVbrSecurityHeader();
            htmlString += _form.SetSecurityBannerAndIntro(scrub);
            return htmlString;
        }
        private void FormSecurityBody()
        {

            log.Info("[HTML] forming HTML body");
            _htmldocOriginal += FormSecurityBodyStart(_htmldocOriginal, false);

            //nav
            SetSecurityNavigations(); // change for security


            //tables
            CHtmlBodyHelper helper = new();
            _htmldocOriginal = helper.FormSecurityReport(_htmldocOriginal);

            _htmldocOriginal += FormFooter();


            log.Info("[HTML] forming HTML body...done!");
        }
        private void FormVbrFullBody()
        {
            log.Info("[HTML] forming HTML body");
            _htmldocOriginal += FormBodyStart(_htmldocOriginal, false);
            _htmldocScrubbed += FormBodyStart(_htmldocScrubbed, true);

            //nav
            SetNavigation();

            CHtmlBodyHelper helper = new();
            _htmldocScrubbed = helper.FormVbrFullReport(_htmldocScrubbed, true);
            _htmldocOriginal = helper.FormVbrFullReport(_htmldocOriginal, false);

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {
                helper.IndividualJobHtmlBuilder();

                //IndividualJobHtmlBuilder();

            }


            _htmldocOriginal += FormFooter();
            _htmldocScrubbed += FormFooter();

            log.Info("[HTML] forming HTML body...done!");
        }

        #region TableFormation

        private string NavTableStarter()
        {
            return "<table border=\"0\" style=\"background: \"><tbody>";
        }
        private string NavtableEnd()
        {
            return "</tbody>" +
                "</table>" +
                //BackToTop() +
                "</div>";
        }
        private void NavTable()
        {
            string tableString = NavTableStarter();
            tableString += _tables.MakeNavTable();
            tableString += NavtableEnd();
            AddToHtml(tableString);
        }
        private void SecurityNavTable()
        {
            string tableString = NavTableStarter();
            tableString += _tables.MakeSecurityNavTable();
            tableString += NavtableEnd();
            AddToHtml(tableString);
        }

        private string FormFooter()
        {
            string s = "";
            s += "<a>vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";
            s += "<script type=\"text/javascript\">";
            s += CssStyler.JavaScriptBlock();
            s += "</script>";
            return s;
        }


        #endregion

        #region HtmlFunctions
        private string DivId(string id)
        {
            return string.Format("<div id={0}>", id);
        }
        private string h2UnderLine(string text)
        {
            return string.Format("<h2><u>{0}</u></h2>", text);
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
