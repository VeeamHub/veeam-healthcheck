// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
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
        CHtmlFormatting _form = new();
        CM365Tables _tables = new CM365Tables();

        private string _htmldoc = string.Empty;
        private CLogger log = CGlobals.Logger;
        public CVb365HtmlCompiler()
        {
            log.Info("[VB365] HTML complier init...");
            RunCompiler();
            log.Info("[VB365] HTML complier complete!");
        }
        public void Dispose() { }
        private void RunCompiler()
        {
            log.Info("[VB365][HTML] forming header...");
            _htmldoc += _form.Header();
            log.Info("[VB365][HTML] forming header...done!");

            log.Info("[VB365][HTML] forming body...");
            FormVb365Body();
            log.Info("[VB365][HTML] forming body...done!");
        }
        private void FormVb365Body()
        {
            try
            {
                _htmldoc += _form.body;


                //_htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
                if (!CGlobals.Scrub)
                    _htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
                if (CGlobals.Scrub)
                    _htmldoc += _form.SetHeaderAndLogo(" ");

                // add sections here
                _htmldoc += SetNavigation();
                _htmldoc += _form.SetVb365Intro();
                // Navigation!

                CM365Tables tables = new();
                _htmldoc += _form.header1("Overview");
                _htmldoc += tables.Globals();
                _htmldoc += tables.Vb365ProtStat();
                // other workloads prompt??
                _htmldoc += _form.header1("Backup Infrastructure");
                _htmldoc += tables.Vb365Controllers();
                _htmldoc += tables.Vb365ControllerDrives();
                _htmldoc += tables.Vb365Proxies();
                _htmldoc += tables.Vb365Repos();
                _htmldoc += tables.Vb365ObjectRepos();

                _htmldoc += _form.header1("Security");
                _htmldoc += tables.Vb365Security();
                //_htmldoc += tables.Vb365Rbac();
                //_htmldoc += tables.Vb365Permissions();

                _htmldoc += _form.header1("M365 Backups");
                _htmldoc += tables.Vb365Orgs();
                _htmldoc += tables.Jobs();

                _htmldoc += tables.Vb365JobStats();
                _htmldoc += tables.Vb365ProcStats();
                _htmldoc += tables.Vb365JobSessions();
                _htmldoc += _form.LineBreak();
                _htmldoc += "<a align=\"center\">vHC Version: " + CVersionSetter.GetFileVersion() + "</a>";



                _htmldoc += "<script type=\"text/javascript\">";
                _htmldoc += CHtmlCompiler.GetEmbeddedCssContent("ReportScript.js");
                _htmldoc += "</script>";

                ExportHtml();
            }
            catch (System.Exception e)
            {
                log.Error("[VB365][HTML] Error: " + e.Message);
            }

        }
        private string SetNavigation()
        {
            log.Info("[VB365][HTML] forming navigation...");
            string s = string.Empty;
            s += _form.SetNavTables("vb365");
            log.Info("[VB365][HTML] forming navigation...done!");
            return s;
        }

        private void ExportHtml()
        {
            log.Info("[VB365][HTML] exporting HTML file...");
            CHtmlExporter exporter = new(GetServerName());
            exporter.ExportVb365Html(_htmldoc);
            log.Info("[VB365][HTML] exporting HTML...done!");
        }
        private string GetServerName()
        {
            return Dns.GetHostName();
        }
        private string SetLicHolder()
        {
            CCsvParser csv = new(CVariables.vb365dir);
            var lic = csv.GetDynamicVboGlobal().ToList();
            foreach (var l in lic)
                return l.LicensedTo;
            return "";
        }

    }
}
