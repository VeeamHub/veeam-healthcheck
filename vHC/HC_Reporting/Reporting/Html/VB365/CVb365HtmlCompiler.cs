using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.Shared;

namespace VeeamHealthCheck.Reporting.Html.VB365
{
    internal class CVb365HtmlCompiler
    {
        CHtmlFormatting _form = new();
        private string _htmldoc = String.Empty;
        public CVb365HtmlCompiler()
        {
            RunCompiler();
        }
        private void RunCompiler()
        {
            _htmldoc += _form.FormHeader();
            FormVb365Body();
        }
        private void FormVb365Body()
        {
            _htmldoc += _form.body;

            _htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
            _htmldoc += _form.SetBannerAndIntro();
            // add sections here

            // Navigation!

            CHtmlTables tables = new();
            _htmldoc += _form.header1("Overview");
            _htmldoc += tables.Globals();
            _htmldoc += tables.Vb365ProtStat();
            // other workloads prompt??
            _htmldoc += _form.header1("Backup Infrastructure");
            _htmldoc += tables.Vb365Controllers();
            _htmldoc += tables.Vb365ControllerDrivers();
            _htmldoc += tables.Vb365Proxies();
            _htmldoc += tables.Vb365Repos();
            _htmldoc += tables.Vb365ObjectRepos();

            _htmldoc += _form.header1("Security");
            _htmldoc += tables.Vb365Security();
            _htmldoc += tables.Vb365Rbac();
            _htmldoc += tables.Vb365Permissions();

            _htmldoc += _form.header1("M365 Backups");
            _htmldoc += tables.Vb365Orgs();
            _htmldoc += tables.Vb365JobSessions();
            _htmldoc += tables.Vb365JobStats();



            _htmldoc += "<script type=\"text/javascript\">";
            _htmldoc += CssStyler.JavaScriptBlock();
            _htmldoc += "</script>";

            //File.WriteAllText("myHtml2.html", _htmldoc);
            ExportHtml();
        }
        private void ExportHtml()
        {
            CHtmlExporter exporter = new("", GetServerName(), "", MainWindow._scrub);
            exporter.ExportVb365Html(_htmldoc);
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
                return l.licensedto;
            return "";
        }

    }
}
