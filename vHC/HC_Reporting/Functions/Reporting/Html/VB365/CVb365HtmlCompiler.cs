// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using System.Net;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VB365
{
    internal class CVb365HtmlCompiler
    {
        readonly CHtmlFormatting form = new();
        readonly CM365Tables tables = new CM365Tables();

        private string htmldoc = string.Empty;
        private readonly CLogger log = CGlobals.Logger;

        public CVb365HtmlCompiler()
        {
            this.log.Info("[VB365] HTML complier init...");
            this.RunCompiler();
            this.log.Info("[VB365] HTML complier complete!");
        }

        public void Dispose() { }

        private void RunCompiler()
        {
            this.log.Info("[VB365][HTML] forming header...");
            this.htmldoc += this.form.Header();
            this.log.Info("[VB365][HTML] forming header...done!");

            this.log.Info("[VB365][HTML] forming body...");
            this.FormVb365Body();
            this.log.Info("[VB365][HTML] forming body...done!");
        }

        private void FormVb365Body()
        {
            try
            {
                this.htmldoc += this.form.body;

                this.htmldoc += this.FormBodyStartVb365(this.htmldoc);

                // add sections here
                this.htmldoc += this.SetNavigation();

                // _htmldoc += _form.SetVb365Intro();
                // Navigation!

                // expand all button:
                this.htmldoc += (string.Format("<button id='expandBtn' type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", "Expand All Sections"));

                CM365Tables tables = new();
                this.htmldoc += this.form.header1("Overview");

                this.htmldoc += tables.Globals();

                this.htmldoc += tables.Vb365ProtStat();

                // other workloads prompt??
                this.htmldoc += this.form.header1("Backup Infrastructure");
                this.htmldoc += tables.Vb365Controllers();
                this.htmldoc += tables.Vb365ControllerDrives();
                this.htmldoc += tables.Vb365Proxies();
                this.htmldoc += tables.Vb365Repos();
                this.htmldoc += tables.Vb365ObjectRepos();

                this.htmldoc += this.form.header1("Security");
                this.htmldoc += tables.Vb365Security();

                // _htmldoc += tables.Vb365Rbac();
                // _htmldoc += tables.Vb365Permissions();
                this.htmldoc += this.form.header1("M365 Backups");
                this.htmldoc += tables.Vb365Orgs();
                this.htmldoc += tables.Jobs();

                this.htmldoc += tables.Vb365JobStats();
                this.htmldoc += tables.Vb365ProcStats();
                this.htmldoc += tables.Vb365JobSessions();
                this.htmldoc += this.form.LineBreak();
                this.htmldoc += "<a align=\"center\">vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";

                this.htmldoc += "<script type=\"text/javascript\">";
                this.htmldoc += CHtmlCompiler.GetEmbeddedCssContent("ReportScript.js");
                this.htmldoc += "</script>";

                this.ExportHtml();
            }
            catch (System.Exception e)
            {
                this.log.Error("[VB365][HTML] Error: " + e.Message);
            }
        }

        private string FormBodyStartVb365(string htmlString)
        {
            string h = this.form.body;
            h += this.form.FormHtmlButtonGoToTop();
            if (CGlobals.Scrub)
            {
                h += this.form.SetHeaderAndLogoVB365(" ");

                // h += _form.SetBannerAndIntro(true);
            }
            else
            {
                h += this.form.SetHeaderAndLogoVB365(this.SetLicHolder());

                // h += _form.SetBannerAndIntro(false);
            }

            return h;
        }

        private string SetNavigation()
        {
            this.log.Info("[VB365][HTML] forming navigation...");
            string s = string.Empty;
            s += this.form.SetNavTables("vb365");
            s += this.form.SetVb365Intro();
            s += "</div>";
            s += "</div>";
            s += "<br>";
            this.log.Info("[VB365][HTML] forming navigation...done!");
            return s;
        }

        private void ExportHtml()
        {
            this.log.Info("[VB365][HTML] exporting HTML file...");
            CHtmlExporter exporter = new(this.GetServerName());

            // exporter.ExportVb365Html(_htmldoc);
            // exporter.ExportVb365Html(_htmldocScrubbed);
            if (CGlobals.Scrub)
            {
                exporter.ExportHtmlVb365(this.htmldoc, true);
            }
            else
            {
                exporter.ExportHtmlVb365(this.htmldoc, false);
            }


            this.log.Info("[VB365][HTML] exporting HTML...done!");
        }

        private string GetServerName()
        {
            this.log.Info("[VB365][HTML] >>> ENTERING GetServerName() method <<<");
            this.log.Info("[VB365][HTML] Checking for server name...");

            // Priority 1: Use remote host if executing remotely
            if (CGlobals.REMOTEEXEC && !string.IsNullOrEmpty(CGlobals.REMOTEHOST))
            {
                this.log.Info("[VB365][HTML] Using REMOTEHOST: " + CGlobals.REMOTEHOST);
                return CGlobals.REMOTEHOST;
            }

            // Priority 2: Extract VB365 server name from Proxies CSV (works for import and local)
            try
            {
                this.log.Info("[VB365][HTML] Attempting to read VB365 server name from Proxies.csv...");
                CCsvParser parser = new(CVariables.vb365dir);
                var proxies = parser.GetDynamicVboProxies()?.ToList();

                if (proxies != null && proxies.Count > 0)
                {
                    // Get first proxy's hostname - typically the VB365 server itself or primary proxy
                    string serverName = proxies[0].Name?.ToString();

                    if (!string.IsNullOrEmpty(serverName))
                    {
                        this.log.Info("[VB365][HTML] VB365 server name from Proxies CSV: " + serverName);
                        return serverName;
                    }
                }
                else
                {
                    this.log.Warning("[VB365][HTML] Proxies.csv not found or empty");
                }
            }
            catch (Exception ex)
            {
                this.log.Warning("[VB365][HTML] Failed to read VB365 server name from CSV: " + ex.Message);
            }

            // Priority 3: Fallback to local hostname
            this.log.Info("[VB365][HTML] Falling back to Dns.GetHostName()...");
            string hostname = Dns.GetHostName();
            this.log.Info("[VB365][HTML] Using local hostname: " + hostname);
            return hostname;
        }

        private string SetLicHolder()
        {
            CCsvParser csv = new(CVariables.vb365dir);
            var lic = csv.GetDynamicVboGlobal().ToList();
            foreach (var l in lic)
            {

                return l.LicensedTo;
            }


            return string.Empty;
        }
    }
}
