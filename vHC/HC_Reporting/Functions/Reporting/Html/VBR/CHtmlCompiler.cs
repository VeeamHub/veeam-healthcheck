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
        private string htmldocOriginal = string.Empty;
        private string htmldocScrubbed = string.Empty;

        private readonly CLogger log = CGlobals.Logger;

        readonly CHtmlFormatting form = new();
        readonly CHtmlTables tables = new();

        private readonly string logStart = "[VbrHtmlCompiler]\t";

        // section links

        public CHtmlCompiler()
        {
            this.log.Info(this.logStart + "CHtmlCompiler constructor entered");
            this.log.Info(this.logStart + "CHtmlCompiler constructor completed");
        }

        public int RunFullVbrReport()
        {
            this.log.Info(this.logStart + ">>> ENTERING RunFullVbrReport() method <<<");
            this.log.Info(this.logStart + "Init full report");
            this.FormHeader();
            this.FormVbrFullBody();
            int res = this.ExportHtml();
            if (res == 0)
            {
                this.log.Info(this.logStart + "Init full report...success!");
            }

            if (res != 0)
            {
                this.log.Error(this.logStart + "Init full report...failed!");
            }


            return res;

            // log.Info(logStart + "Init full report...done!");
        }

        public void RunSecurityReport()
        {
            this.log.Info(this.logStart + "Init Security Report");
            this.FormHeader();
            this.FormSecurityBody();
            this.ExportSecurityHtml();
            this.log.Info(this.logStart + "Init Security Report");
        }

        private int ExportHtml()
        {
            int res = 0;
            CHtmlExporter exporter = new(this.GetServerName());
            if (CGlobals.Scrub)
            {
                res = exporter.ExportVbrHtml(this.htmldocScrubbed, true);
            }
            else
            {
                res = exporter.ExportVbrHtml(this.htmldocOriginal, false);
            }

            if (CGlobals.OpenExplorer)
            {
                exporter.OpenExplorer();
            }


            if (res == 0)
            {
                return 0;
            }
            else return 1;
        }

        // write a method to export _htmldocOriginal as a PDF
        private void ExportToPdf()
        {
            // HtmlToPdfConverter converter = new();
            // var htmlContent = _htmldocOriginal;
            // var outputPath = "output.pdf";

            // var pdfBytes = converter.ConvertHtmlToPdf(htmlContent, outputPath);

            //// If you need to save the PDF to a file
            // File.WriteAllBytes(outputPath, pdfBytes);
        }

        private void ExportSecurityHtml()
        {
            CHtmlExporter exporter = new(this.GetServerName());
            exporter.ExportVbrSecurityHtml(this.htmldocOriginal, false);

            // exporter.ExportVbrHtml(_htmldocScrubbed, true);
            if (CGlobals.OpenExplorer)
            {
                exporter.OpenExplorer();
            }
        }

        private string GetServerName()
        {
            this.log.Info(this.logStart + ">>> ENTERING GetServerName() method <<<");
            this.log.Info("Checking for server name...");

            // Priority 1: Use remote host if executing remotely
            if (CGlobals.REMOTEEXEC && !string.IsNullOrEmpty(CGlobals.REMOTEHOST))
            {
                this.log.Info(this.logStart + "Using REMOTEHOST: " + CGlobals.REMOTEHOST);
                return CGlobals.REMOTEHOST;
            }

            // Priority 2: Extract VBR server name from vbrinfo CSV (works for import and local)
            try
            {
                this.log.Info(this.logStart + "Attempting to read VBR server name from vbrinfo.csv...");
                CCsvParser parser = new();
                var vbrInfo = parser.BnrCsvParser()?.FirstOrDefault();

                if (vbrInfo != null)
                {
                    // Determine server name based on database type
                    string serverName = null;

                    if (!string.IsNullOrEmpty(vbrInfo.DbType))
                    {
                        if (vbrInfo.DbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                            vbrInfo.DbType.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
                        {
                            serverName = vbrInfo.PgHost;
                            this.log.Info(this.logStart + "Using PostgreSQL host: " + serverName);
                        }
                        else if (vbrInfo.DbType.Contains("Sql", StringComparison.OrdinalIgnoreCase))
                        {
                            serverName = vbrInfo.MsHost;
                            this.log.Info(this.logStart + "Using SQL Server host: " + serverName);
                        }
                    }

                    // If we got a valid server name, use it
                    if (!string.IsNullOrEmpty(serverName))
                    {
                        this.log.Info(this.logStart + "VBR server name from CSV: " + serverName);
                        return serverName;
                    }
                }
                else
                {
                    this.log.Warning(this.logStart + "vbrinfo.csv not found or empty");
                }
            }
            catch (Exception ex)
            {
                this.log.Warning(this.logStart + "Failed to read VBR server name from CSV: " + ex.Message);
            }

            // Priority 3: Fallback to local hostname
            this.log.Info(this.logStart + "Falling back to Dns.GetHostName()...");
            string hostname = Dns.GetHostName();
            this.log.Info(this.logStart + "Using local hostname: " + hostname);
            return hostname;
        }

        public void Dispose()
        {
        }

        private void FormHeader()
        {
            this.log.Info(this.logStart + ">>> ENTERING FormHeader() method <<<");
            this.log.Info("[HTML] Forming Header...");
            this.log.Info(this.logStart + "About to call _form.Header()...");
            string h = this.form.Header();
            this.log.Info(this.logStart + "_form.Header() completed successfully");

            this.htmldocOriginal = h;
            this.htmldocScrubbed += h;

            this.log.Info("[HTML] Forming Header...done!");
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
            this.log.Info("[HTML] setting HTML navigation");
            this.AddToHtml(this.DivId("navigation"));
            this.AddToHtml(string.Format("<h2>{0}</h2>", VbrLocalizationHelper.NavHeader));

            // AddToHtml(string.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", VbrLocalizationHelper.NavColapse));
        }

        private void SetNavigation()
        {
            // SetUniversalNavStart();
            this.NavTable();

            this.AddToHtml(this.SetVbrHcIntro(false));

            // add end div for card holding nav & summary
            this.AddToHtml("</div>");

            // add end div for end of section
            this.AddToHtml("</div>");

            // add line break for spacing
            this.AddToHtml("<br>");
        }

        private string SetVbrHcIntro(bool scrub)
        {
            string s = string.Empty;
            if (!CGlobals.RunSecReport)
            {
                if (scrub)
                {
                    s += "<div class=\"card2\">" +
                        "<h2>About</h2>"
                        + VbrLocalizationHelper.HtmlIntroLine1 + "</a>\n";

                    // s += LineBreak();
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

                    // s += LineBreak();
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
            // SetUniversalNavStart();
            this.SecurityNavTable();

            // SetUniversalNavEnd();
        }

        private string SetLicHolder()
        {
            this.log.Info("Setting license holder name...");
            CCsvParser csv = new();
            var lic = csv.GetDynamicLicenseCsv();
            this.log.Info("Setting license holder name...done!");
            foreach (var l in lic)
            {

                return l.licensedto;
            }


            return string.Empty;
        }

        #endregion

        private string FormBodyStart(string htmlString, bool scrub)
        {
            CGlobals.Scrub = scrub;
            string h = this.form.body;
            h += this.form.FormHtmlButtonGoToTop();
            if (scrub)
            {
                h += this.form.SetHeaderAndLogo(" ");

                // h += _form.SetBannerAndIntro(true);
            }
            else
            {
                h += this.form.SetHeaderAndLogo(this.SetLicHolder());

                // h += _form.SetBannerAndIntro(false);
            }

            return h;
        }

        private string SetVbrSecurityHeader()
        {
            return this.form.SetHeaderAndLogo(this.SetLicHolder());
        }

        private string FormSecurityBodyStart(string htmlString, bool scrub)
        {
            htmlString += this.form.body;
            htmlString += this.SetVbrSecurityHeader();
            htmlString += this.form.SetSecurityBannerAndIntro(scrub);
            return htmlString;
        }

        private void FormSecurityBody()
        {
            this.log.Info("[HTML] forming HTML body");
            this.htmldocOriginal += this.FormSecurityBodyStart(this.htmldocOriginal, false);

            // nav
            this.SetSecurityNavigations(); // change for security

            // tables
            CHtmlBodyHelper helper = new();
            this.htmldocOriginal = helper.FormSecurityReport(this.htmldocOriginal);

            this.htmldocOriginal += this.FormFooter();

            this.log.Info("[HTML] forming HTML body...done!");
        }

        private void LoadCsvToMemory()
        {
            this.log.Info(this.logStart + ">>> ENTERING LoadCsvToMemory() method <<<");
            this.log.Info(this.logStart + "Building CSV file path...");
            string serverName = string.IsNullOrEmpty(CGlobals.REMOTEHOST) ? "localhost" : CGlobals.REMOTEHOST;
            string file = Path.Combine(CVariables.vbrDir, $"{serverName}_vbrinfo.csv");
            this.log.Info("looking for VBR CSV at: " + file);
            this.log.Info(this.logStart + "About to call CCsvsInMemory.GetCsvData()...");
            var res = CCsvsInMemory.GetCsvData(file);
            this.log.Info(this.logStart + "CCsvsInMemory.GetCsvData() completed. Result is " + (res == null ? "null" : "not null"));
            if (res != null && res.Count > 0)
            {
                this.log.Info("VBR CSV data loaded successfully. Number of rows: " + res.Count);
                for (int i = 0; i < Math.Min(5, res.Count); i++)
                {
                    this.log.Debug($"Row {i + 1}: {string.Join(", ", res[i].Select(kv => $"{kv.Key}: {kv.Value}"))}");
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
                    {
                        versionStr = dict[versionKey];
                    }
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
                        this.log.Info($"Set VBRMAJORVERSION to {majorVersion} from Version '{versionStr}'");
                    }
                    else
                    {
                        this.log.Error($"Failed to parse major version from Version '{versionStr}'");
                    }
                }
                else
                {
                    this.log.Error("Version field not found in CSV data.");
                }
            }
            else
            {
                this.log.Error("Failed to load VBR CSV data or no data found.");
            }
        }

        private void FormVbrFullBody()
        {
            this.log.Info(this.logStart + ">>> ENTERING FormVbrFullBody() method <<<");
            this.log.Info("[HTML] forming HTML body");

            // maybe load all CSV to memory here?
            this.log.Info(this.logStart + "About to call LoadCsvToMemory()...");
            this.LoadCsvToMemory();
            this.log.Info(this.logStart + "LoadCsvToMemory() completed.");

           // LoadCsvToMemory();
            this.log.Info(this.logStart + "Creating CHtmlBodyHelper instance...");
            CHtmlBodyHelper helper = new();
            this.log.Info(this.logStart + "CHtmlBodyHelper instance created.");
            if (CGlobals.Scrub)
            {
                this.log.Info(this.logStart + "Scrub mode enabled. Starting scrubbed report generation...");
                this.htmldocScrubbed += this.FormBodyStart(this.htmldocScrubbed, true);
                this.SetNavigation();

                this.AddToHtml(string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

                this.log.Info(this.logStart + "About to call helper.FormVbrFullReport() [SCRUBBED]...");
                this.htmldocScrubbed = helper.FormVbrFullReport(this.htmldocScrubbed, true);
                this.log.Info(this.logStart + "helper.FormVbrFullReport() [SCRUBBED] completed.");
                this.htmldocScrubbed += this.FormFooter();
            }
            else
            {
                this.log.Info(this.logStart + "Scrub mode disabled. Starting original report generation...");
                this.htmldocOriginal += this.FormBodyStart(this.htmldocOriginal, false);
                this.SetNavigation();

                this.AddToHtml(string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

                this.log.Info(this.logStart + "About to call helper.FormVbrFullReport() [ORIGINAL]...");
                this.htmldocOriginal = helper.FormVbrFullReport(this.htmldocOriginal, false);
                this.log.Info(this.logStart + "helper.FormVbrFullReport() [ORIGINAL] completed.");
                this.htmldocOriginal += this.FormFooter();
            }

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {
                helper.IndividualJobHtmlBuilder();

                // IndividualJobHtmlBuilder();
            }

            this.log.Info("[HTML] forming HTML body...done!");
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

                // BackToTop() +
                "</div>";
        }

        private void NavTable()
        {
            this.log.Info("[HTML] setting HTML navigation");

            // string tableString = NavTableStarter();
            string tableString = this.form.SetNavTables("vbr");
            this.AddToHtml(tableString);
        }

        private void SecurityNavTable()
        {
            string tableString = this.NavTableStarter();
            tableString += this.tables.MakeSecurityNavTable();
            tableString += this.NavtableEnd();
            this.AddToHtml(tableString);
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
            this.htmldocOriginal += infoString;
            this.htmldocScrubbed += infoString;
        }

        private void AddToHtml(string infoString, bool scrub)
        {
        }

        #endregion

    }
}
