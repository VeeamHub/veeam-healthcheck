// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Linq;
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
            log.Info(logStart + "CHtmlCompiler constructor entered");
            log.Info(logStart + "CHtmlCompiler constructor completed");
        }
        public int RunFullVbrReport()
        {
            log.Info(logStart + ">>> ENTERING RunFullVbrReport() method <<<");
            log.Info(logStart + "Init full report");
            FormHeader();
            FormVbrFullBody();
            int res = ExportHtml();
            if (res == 0)
                log.Info(logStart + "Init full report...success!");
            if (res != 0)
                log.Error(logStart + "Init full report...failed!");

            return res;
            //log.Info(logStart + "Init full report...done!");
        }
        public void RunSecurityReport()
        {
            log.Info(logStart + "Init Security Report");
            FormHeader();
            FormSecurityBody();
            ExportSecurityHtml();
            log.Info(logStart + "Init Security Report");
        }
        private int ExportHtml()
        {
            int res = 0;
            CHtmlExporter exporter = new(GetServerName());
            if (CGlobals.Scrub)
            {
                res = exporter.ExportVbrHtml(_htmldocScrubbed, true);

            }
            else
            {
                res = exporter.ExportVbrHtml(_htmldocOriginal, false);

            }
            if (CGlobals.OpenExplorer)
                exporter.OpenExplorer();

            if (res == 0)
            {
                return 0;
            }
            else return 1;
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
            log.Info(logStart + ">>> ENTERING GetServerName() method <<<");
            log.Info("Checking for server name...");
            log.Info(logStart + "About to call Dns.GetHostName()...");
            string hostname = Dns.GetHostName();
            log.Info(logStart + "Dns.GetHostName() completed successfully. Hostname: " + hostname);
            return hostname;
        }
        public void Dispose()
        {

        }

        private void FormHeader()
        {
            log.Info(logStart + ">>> ENTERING FormHeader() method <<<");
            log.Info("[HTML] Forming Header...");
            log.Info(logStart + "About to call _form.Header()...");
            string h = _form.Header();
            log.Info(logStart + "_form.Header() completed successfully");

            _htmldocOriginal = h;
            _htmldocScrubbed += h;

            log.Info("[HTML] Forming Header...done!");
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

        private void SetNavigation()
        {
            //SetUniversalNavStart();
            NavTable();

            AddToHtml(SetVbrHcIntro(false));

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
                <dt>NOTE</dt>
                <dd>{2}</dd>
                </br>
                <dt>NOTE</dt>
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
            //SetUniversalNavEnd();
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
            CGlobals.Scrub = scrub;
            string h = _form.body;
            h += _form.FormHtmlButtonGoToTop();
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
        private void LoadCsvToMemory()
        {
            log.Info(logStart + ">>> ENTERING LoadCsvToMemory() method <<<");
            log.Info(logStart + "Building CSV file path...");
            string file = Path.Combine(CVariables.vbrDir, "localhost_vbrinfo.csv");
            log.Info("looking for VBR CSV at: " + file);
            log.Info(logStart + "About to call CCsvsInMemory.GetCsvData()...");
            var res = CCsvsInMemory.GetCsvData(file);
            log.Info(logStart + "CCsvsInMemory.GetCsvData() completed. Result is " + (res == null ? "null" : "not null"));
            if (res != null && res.Count > 0)
            {
                log.Info("VBR CSV data loaded successfully. Number of rows: " + res.Count);
                for (int i = 0; i < Math.Min(5, res.Count); i++)
                {
                    log.Debug($"Row {i + 1}: {string.Join(", ", res[i].Select(kv => $"{kv.Key}: {kv.Value}"))}");
                }

                // Try to find the Version key, with or without quotes
                var dict = res[0];
                string versionStr = null;
                if (dict.TryGetValue("Version", out versionStr) && !string.IsNullOrWhiteSpace(versionStr))
                {
                    // found as Version
                }
                else if (dict.TryGetValue("\"Version\"", out versionStr) && !string.IsNullOrWhiteSpace(versionStr))
                {
                    // found as "Version"
                }
                else
                {
                    // Try to find any key that matches Version ignoring quotes and case
                    var versionKey = dict.Keys.FirstOrDefault(k => k.Trim('\"').Equals("Version", StringComparison.OrdinalIgnoreCase));
                    if (versionKey != null)
                        versionStr = dict[versionKey];
                }

                if (!string.IsNullOrWhiteSpace(versionStr))
                {
                    // Remove any leading/trailing quotes
                    versionStr = versionStr.Trim('\"');
                    CGlobals.VBRFULLVERSION = versionStr;
                    var majorVersionPart = versionStr.Split('.')[0];
                    if (int.TryParse(majorVersionPart, out int majorVersion))
                    {
                        CGlobals.VBRMAJORVERSION = majorVersion;
                        log.Info($"Set VBRMAJORVERSION to {majorVersion} from Version '{versionStr}'");
                    }
                    else
                    {
                        log.Error($"Failed to parse major version from Version '{versionStr}'");
                    }
                }
                else
                {
                    log.Error("Version field not found in CSV data.");
                }
            }
            else
            {
                log.Error("Failed to load VBR CSV data or no data found.");
            }
        }
        private void FormVbrFullBody()
        {
            log.Info(logStart + ">>> ENTERING FormVbrFullBody() method <<<");
            log.Info("[HTML] forming HTML body");

            // maybe load all CSV to memory here?
            log.Info(logStart + "About to call LoadCsvToMemory()...");
            LoadCsvToMemory();
            log.Info(logStart + "LoadCsvToMemory() completed.");
           // LoadCsvToMemory();

            log.Info(logStart + "Creating CHtmlBodyHelper instance...");
            CHtmlBodyHelper helper = new();
            log.Info(logStart + "CHtmlBodyHelper instance created.");
            if (CGlobals.Scrub)
            {
                log.Info(logStart + "Scrub mode enabled. Starting scrubbed report generation...");
                _htmldocScrubbed += FormBodyStart(_htmldocScrubbed, true);
                SetNavigation();

                AddToHtml(string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

                log.Info(logStart + "About to call helper.FormVbrFullReport() [SCRUBBED]...");
                _htmldocScrubbed = helper.FormVbrFullReport(_htmldocScrubbed, true);
                log.Info(logStart + "helper.FormVbrFullReport() [SCRUBBED] completed.");
                _htmldocScrubbed += FormFooter();

            }
            else
            {
                log.Info(logStart + "Scrub mode disabled. Starting original report generation...");
                _htmldocOriginal += FormBodyStart(_htmldocOriginal, false);
                SetNavigation();

                AddToHtml(string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

                log.Info(logStart + "About to call helper.FormVbrFullReport() [ORIGINAL]...");
                _htmldocOriginal = helper.FormVbrFullReport(_htmldocOriginal, false);
                log.Info(logStart + "helper.FormVbrFullReport() [ORIGINAL] completed.");
                _htmldocOriginal += FormFooter();

            }

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {
                helper.IndividualJobHtmlBuilder();

                //IndividualJobHtmlBuilder();

            }



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
            string tableString = _form.SetNavTables("vbr");
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
