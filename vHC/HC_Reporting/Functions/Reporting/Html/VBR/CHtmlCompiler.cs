// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Net;
using System.Reflection;
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
        // write a method to export _htmldocOriginal as a PDF

        private void ExportToPdf()
        {
            //HtmlToPdfConverter converter = new();
            //var htmlContent = _htmldocOriginal;
            //var outputPath = "output.pdf";

            //var pdfBytes = converter.ConvertHtmlToPdf(htmlContent, outputPath);

            //// If you need to save the PDF to a file
            //File.WriteAllBytes(outputPath, pdfBytes);
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
            string docType = "<!DOCTYPE html>";
            string htmlOpen = "<html>";
            string styleOpen = "<style type=\"text/css\">";
            string viewPort = "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">";
            string title = "<title>Veeam HealthCheck Report</title>";
            string charSet = "<meta charset=\"UTF-8\">";
            string headOpen = "<head>";
            string headClose = "</head>";
            string styleClose = "</style>";

            string css  = GetEmbeddedCssContent("css.css");
            string header = docType + htmlOpen + headOpen + charSet + viewPort + title
                + styleOpen + css
                + styleClose
                + headClose;

            _htmldocOriginal = header;
            _htmldocScrubbed += header;

            log.Info("[HTML] Forming Header...done!");
            //FormBody();
        }
        // method to read the text from a resource called css.css
        private string ReadCssResource()
        {
            string css = string.Empty;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VeeamHealthCheck.Resources.css.css"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    css = reader.ReadToEnd();
                }
            }
            return css;
        }
        public static string GetEmbeddedCssContent(string embeddedFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.{embeddedFileName}";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        #region HtmlHeaders


        private void SetUniversalNavStart()
        {
            log.Info("[HTML] setting HTML navigation");
            AddToHtml(DivId("navigation"));
            AddToHtml(string.Format("<h2>{0}</h2>", VbrLocalizationHelper.NavHeader));
           // AddToHtml(string.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", VbrLocalizationHelper.NavColapse));
        }
        private void SetUniversalNavEnd()
        {
           // AddToHtml(_form._endDiv);
            log.Info("[HTML] setting HTML navigation...done!");
        }
        private void SetNavigation()
        {
            //SetUniversalNavStart();
            NavTable();
            SetUniversalNavEnd();

            AddToHtml( SetVbrHcIntro(false));

            //add end div for card holding nav & summary
            AddToHtml("</div>");
            //add end div for end of section
            AddToHtml("</div>");
            // add line break for spacing
            AddToHtml("<br>");

        }
        private string SetVbrHcIntro(bool scrub)
        {
            string s = "";
            if (!CGlobals.RunSecReport)
            {
                if (scrub)
                {
                    s += "<div class=\"card2\">" + 
                        "<h2>About</h2>"
                        + VbrLocalizationHelper.HtmlIntroLine1 + "</a>\n";
                    //s += LineBreak();
                    s += String.Format(@"<dl>
                <dt>CSV Raw Data Output</dt>
                <dd>{0}</dd>
                <dt>Individual Job Session Reports</dt>
                <dd>{1}</dd>
                <dt>NOTE:</dt>
                <dd>{2}</dd>
                <dt>NOTE:</dt>
                <dd>{3}</dd>


</dl>",
        VbrLocalizationHelper.HtmlIntroLine2,
        VbrLocalizationHelper.HtmlIntroLine3Anon,
        VbrLocalizationHelper.HtmlIntroLine4,
        VbrLocalizationHelper.HtmlIntroLine5
        );
                }
                else
                {
                    s += "<div class=\"card2\">" +
                         "<h2>About</h2>" +
                        VbrLocalizationHelper.HtmlIntroLine1 + "</a>\n";
                    //s += LineBreak();
                    s += String.Format(@"<dl>
                <dt>CSV Raw Data Output</dt>
                <dd>{0}</dd>
                </br>
                <dt>Individual Job Session Reports</dt>
                <dd>{1}</dd>
                </br>
                <dt>PDF Report Output</dt>
                <dd>{2}</dd>
                </br>
                <dt>Excel Report Output</dt>
                <dd>{3}</dd>


</dl>",
        VbrLocalizationHelper.HtmlIntroLine2,
        VbrLocalizationHelper.HtmlIntroLine3Original,
        VbrLocalizationHelper.HtmlIntroLine4,
        VbrLocalizationHelper.HtmlIntroLine5
        );
                }
            }

            s += "</div>";

            return s;
        }
        private void SetSecurityNavigations()
        {
            //SetUniversalNavStart();
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
        private string FormHeaderAndLogo(bool scrub)
        {
            string h = _form.body;
            h += FormHtmlButtonGoToTop();
            if (scrub)
            {
                h += _form.SetHeaderAndLogo(" ");
                //h += _form.SetBannerAndIntro(true);
            }
            else
            {
                h += _form.SetHeaderAndLogo(SetLicHolder());
                //h += _form.SetBannerAndIntro(false);
            }

            return h;
        }
        private string FormBodyStart(string htmlString, bool scrub)
        {
            string h  = _form.body;
             h += FormHtmlButtonGoToTop();
            if (scrub)
            {
                h += _form.SetHeaderAndLogo(" ");
                //h += _form.SetBannerAndIntro(true);
            }
            else
            {
                h += _form.SetHeaderAndLogo(SetLicHolder());
                //h += _form.SetBannerAndIntro(false);
            }

            return h;
        }
        private string FormHtmlButtonGoToTop()
        {
            return "<button onclick=\"topFunction()\" id=\"myBtn\" title=\"Go to top\">Go To Top</button>";
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

            // add button
            AddToHtml(string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

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
            return "<table border=\"1\" style=\"background:#efefef \"><tbody>";
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
            log.Info("[HTML] setting HTML navigation");

            // string tableString = NavTableStarter();
            string tableString = @"<div class='card'><div class='card-container'><div class=card2 id=navigation>";
            tableString += string.Format("<h2>{0}</h2>", VbrLocalizationHelper.NavHeader);
              tableString += _tables.MakeNavTable();
            tableString += _form._endDiv;
            //tableString += NavtableEnd();
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
            string jsScript = GetEmbeddedCssContent("ReportScript.js");
            string s = "</body><footer>";
            s += "<a>vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";
            s += "<script type=\"text/javascript\">";
            s += jsScript;
            s += "</script></footer></html>";
            return s;
        }


        #endregion

        #region HtmlFunctions
        private string DivId(string id)
        {
            return string.Format("<div id={0}>", id);
        }
        private string DivIdClass()
        {
            return string.Format("<div id={0} class={1}");
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
