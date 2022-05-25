﻿using System;
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
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html
{
    internal class CHtmlCompiler
    {
        private string xslFileName = "StyleSheets\\myHtml.xsl"; // maybe just do memory instead of disk??
        private string _htmldoc;
        private bool _vbrmode = false;
        private bool _vb365mode = false;

        private bool _scrub = false;


        CHtmlFormatting _form = new();

        // section links
        string _serverSumLink = "serverSummary";

        

        public CHtmlCompiler(string reportType)
        {
            FormHeader();
            if(reportType == "vbr")
            {
                _vbrmode = true;
                FormHeader();
                FormBody();
            }
            if (reportType == "vb365")
            {
                _vb365mode = true;
                //FormVb365Body();
            }

        }
        public void Dispose()
        {

        }

        private void FormHeader()
        {
            _htmldoc = "<html>";
            _htmldoc += "<head>";
            _htmldoc += "<style>";
            _htmldoc += CssStyler.StyleString();
            _htmldoc += "</style></head>";

            //FormBody();
        }
        
        #region HtmlHeaders



        private void SetNavigation()
        {
            AddToHtml(DivId("navigation"));
            AddToHtml(String.Format("<h4>{0}</h4>", ResourceHandler.NavHeader));
            AddToHtml(String.Format("<button type=\"button\" class=\"btn\" onclick=\"test()\">{0}</button>", ResourceHandler.NavColapse));
            NavTable();


            AddToHtml(_form._endDiv);
        }



        private string SetLicHolder()
        {
            CCsvParser csv = new();
            var lic = csv.GetDynamicLicenseCsv();
            foreach (var l in lic)
                return l.licensedto;
            return "";
        }

        #endregion
        private void FormBody()
        {
            _htmldoc += "<body>";


            _htmldoc += _form.SetHeaderAndLogo(SetLicHolder());
            _htmldoc += _form.SetBannerAndIntro();
            SetNavigation();
            SetLicTable();
            SetVbrTable();
            SetSecSumTable();
            SetServerSummaryTable();
            SetJobSummaryTable();
            SetMissingJobTypeTable();
            SetProtectedWkldTable();
            SetMgdSrvInfoTable();
            SetRegKeyTable();
            SetProxyTable();
            SetSobrTable();
            SetExtentTable();
            SetRepoTable();
            SetJobConTable();
            SetTaskConTable();
            SetJobSessSumTable();
            SetJobInfoTable();
            /*
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


            _htmldoc += "<script type=\"text/javascript\">";
            _htmldoc += CssStyler.JavaScriptBlock();
            _htmldoc += "</script>";

            File.WriteAllText("myHtml2.html", _htmldoc);

        }

        #region TableFormation

        private void Section(string sectionType)
        {
            /* div id=nameInLink
             * h2 section name (i.e. Detected Infr Types)
             * break
             * Table
             * div content
                * Summary
                * Summary Text
                * Notes
                * Notes text
                * end div
             * BackToTop
             * end div
             */
            AddSectionHeader(sectionType);
            AddTable(sectionType);

        }
        private void AddTable(string type)
        {
            switch (type)
            {
                case "serverSummary":

                    break;

                default:
                    break;
            }
        }
        private void AddSectionHeader(string sectionType)
        {
            switch (sectionType)
            {
                case "serverSummary":
                    SectionHeader("serverSummary", "");
                    break;
            }

        }
        private void SectionHeader(string sectionLinkName, string headerText)
        {
            _htmldoc += String.Format("<div id=\"{0}\">{1}", sectionLinkName, headerText);
        }





        private void NavTable()
        {
            CHtmlTables tables = new();
            string tableString =
    "<table border=\"0\" style=\"background: \">" +
    "<tbody>";
            tableString += tables.MakeNavTable();




            tableString +=
                "</tbody>" +
                "</table>" +
                BackToTop() +
                "</div>";
            AddToHtml(tableString);
        }
        private void Table(string header, string type)
        {
            CHtmlTables tables = new();
            string tableString = DivId(type) +
                h2UnderLine(header) +
                "<table border=\"1\" style=\"background: lightgray\">" +
                "<tbody>";
            switch (type)
            {
                case "license":
                    tableString += (tables.AddLicHeaderToTable());
                    tableString += (tables.AddLicDataToTable());
                    break;
                case "navigation":
                    tableString += tables.MakeNavTable();
                    break;
                case "vbrInfo":
                    tableString += tables.AddBkpSrvTable();
                    break;
                case "secSum":
                    tableString += tables.AddSecSummaryTable();
                    break;
                case "srvSummary":
                    tableString += tables.AddSrvSummaryTable();
                    break;
                case "jobSummary":
                    tableString += tables.AddJobSummaryTable();
                    break;
                case "missingJobs":
                    tableString += tables.AddMissingJobsTable();
                    break;
                case "protectedWklds":
                    tableString += tables.AddProtectedWorkLoadsTable();
                    break;
                case "serverInfo":
                    tableString += tables.AddManagedServersTable();
                    break;
                case "regKeys":
                    tableString += tables.AddRegKeysTable();
                    break;
                case "proxyInfo":
                    tableString += tables.AddProxyTable();
                    break;
                case "sobr":
                    tableString += tables.AddSobrTable();
                    break;
                case "extents":
                    tableString += tables.AddSobrExtTable();
                    break;
                case "repos":
                    tableString += tables.AddRepoTable();
                    break;
                case "jobConcurrency":
                    tableString += tables.AddJobConTable();
                    break;
                case "taskConcurrency":
                    tableString += tables.AddTaskConTable();
                    break;
                case "jobSessSummary":
                    tableString += tables.AddJobSessSummTable();
                    break;
                case "jobs":
                    tableString += tables.AddJobInfoTable();
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
        private void SetLicTable()
        {
            Table("License Summary", "license");
            EndSection();

        }
        private void SetVbrTable()
        {
            Table("Backup Server & Config DB Info", "vbrInfo");
            SetVbrSummary();
            EndSection();
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
        private void SetServerSummaryTable()
        {
            Table("Detected Infrastructure Types & Counts", "srvSummary");
            //taable
            EndSection();
        }
        private void SetJobSummaryTable()
        {
            Table("Job Summary", "jobSummary");
            //taable
            EndSection();
        }
        private void SetMissingJobTypeTable()
        {
            Table("Missing Job Types", "missingJobs");
            //taable
            EndSection();
        }
        private void SetProtectedWkldTable()
        {
            Table("Protected Workloads", "protectedWklds");
            //taable
            EndSection();
        }
        private void SetMgdSrvInfoTable()
        {
            Table("Managed Server Info", "serverInfo");
            //taable
            EndSection();
        }
        private void SetRegKeyTable()
        {
            Table("Non-Default Registry Keys", "regKeys");
            //taable
            EndSection();
        }
        private void SetProxyTable()
        {
            Table("Proxy Info", "proxyInfo");
            //taable
            EndSection();
        }
        private void SetSobrTable()
        {
            Table("SOBR Details", "sobr");
            //taable
            EndSection();
        }
        private void SetExtentTable()
        {
            Table("SOBR Extent Info", "extents");
            //taable
            EndSection();
        }
        private void SetRepoTable()
        {
            Table("Standalone Repository Details", "repos");
            //taable
            EndSection();
        }
        private void SetJobConTable()
        {
            Table("Job Concurrency (7 days)", "jobConcurrency");
            //taable
            EndSection();
        }
        private void SetTaskConTable()
        {
            Table("Task Concurrency - 7 days", "taskConcurrency");
            //taable
            EndSection();
        }
        private void SetJobSessSumTable()
        {
            Table("Job Session Summary", "jobSessSummary");
            //taable
            EndSection();
        }
        private void SetJobInfoTable()
        {
            Table("Job Info", "jobs");
            //taable
            EndSection();
        }
        private void SetSecSumTable()
        {
            Table("Security Summary", "secSum");

            EndSection();
        }
        private string CollapsibleButton(string buttonText)
        {
            return SectionButton(_form._collapsible, buttonText);
        }
        private void SetVbrSummary()
        {
            _htmldoc += CollapsibleButton(ResourceHandler.BkpSrvButton);

            _htmldoc += "<div class=\"content\">";
            _htmldoc += AddA("hdr", ResourceHandler.GeneralSummaryHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvSummary1) +
                AddA("i3", ResourceHandler.BkpSrvSummary2) +
                AddA("i3", ResourceHandler.BkpSrvSummary3) +
                AddA("i3", ResourceHandler.BkpSrvSummary4) +
                DoubleLineBreak() +
                AddA("hdr", ResourceHandler.GeneralNotesHeader) + LineBreak() +
                AddA("i2", ResourceHandler.BkpSrvNotes1) +
                AddA("i2", ResourceHandler.BkpSrvNotes2) +
                AddA("i2", ResourceHandler.BkpSrvNotes3) +
                AddA("i2", ResourceHandler.BkpSrvNotes4) +
                AddA("i2", ResourceHandler.BkpSrvNotes5) +
                AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            _htmldoc += "</div>";
            _htmldoc += "</div>";
        }
        private string SectionButton(string classType, string displayText)
        {
            return String.Format("<button type=\"button\" class=\"{0}\">{1}</button>", classType, displayText);
        }
        private string AddA(string classInfo, string displaytext)
        {
            return String.Format("<a class=\"{0}\">{1}</a>" + LineBreak(), classInfo, displaytext);
        }

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

        private string Button(string displayText)
        {
            return String.Format("<button type=\"button\" class=\"collapsible\">{0}</button>", displayText);
        }


        private string HyperLink(string link, string displayText)
        {
            string s = String.Format("<a href=\"{0}\" target=\"_blank\">{1}</a>", link, displayText);
            return s;
        }

        private string LineBreak()
        {
            return "<br/>";
        }
        private string DoubleLineBreak()
        {
            return "<br/><br/>";
        }
        private void AddToHtml(string infoString)
        {
            _htmldoc += infoString;
        }

        #endregion


    }

    
}
