﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Reporting.Html.VB365
{
    internal class CVb365HtmlCompiler
    {
        CHtmlFormatting _form = new();
        CM365Tables _tables = new CM365Tables();

        private string _htmldoc = String.Empty;
        private CLogger log = VhcGui.log;
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
            _htmldoc += _form.FormHeader();
            log.Info("[VB365][HTML] forming header...done!");

            log.Info("[VB365][HTML] forming body...");
            FormVb365Body();
            log.Info("[VB365][HTML] forming body...done!");
        }
        private void FormVb365Body()
        {
            _htmldoc += _form.body;

            //_htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
            if (!VhcGui._scrub)
                _htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
            if (VhcGui._scrub)
                _htmldoc += _form.SetHeaderAndLogo(" ");
            _htmldoc += _form.SetBannerAndIntroVb365();
            // add sections here
            _htmldoc += SetNavigation();
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
            _htmldoc += CssStyler.JavaScriptBlock();
            _htmldoc += "</script>";

            ExportHtml();
        }
        private string SetNavigation()
        {
            log.Info("[VB365][HTML] forming navigation...");
            string s = String.Empty;
            s += _form.DivId("navigation");
            
            s +=String.Format("<h4>{0}</h4>", ResourceHandler.NavHeader);
            s +=String.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", ResourceHandler.NavColapse);
            s += NavTable();


            s += _form._endDiv;
            log.Info("[VB365][HTML] forming navigation...done!");
            return s;
        }
        private string NavTable()
        {
            CM365Tables tables = new();
            string tableString =
    "<table border=\"0\" style=\"background: \">" +
    "<tbody>";
            tableString += tables.MakeVb365NavTable();




            tableString +=
                "</tbody>" +
                "</table>" +
                //_form.BackToTop() +
                "</div>";
            return tableString;
        }
        private void ExportHtml()
        {
            log.Info("[VB365][HTML] exporting HTML file...");
            CHtmlExporter exporter = new("", GetServerName(), "", VhcGui._scrub);
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
