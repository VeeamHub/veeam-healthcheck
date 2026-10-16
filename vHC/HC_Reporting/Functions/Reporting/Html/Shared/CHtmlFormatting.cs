// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Reflection;
using VeeamHealthCheck.Functions.Reporting.Html.VB365;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Resources.Localization.VB365;
using VeeamHealthCheck.Shared;
using static System.Net.Mime.MediaTypeNames;

namespace VeeamHealthCheck.Functions.Reporting.Html.Shared
{
    internal class CHtmlFormatting
    {
        public string html = "<html>";
        public string htmlend = "</html>";
        public string body = "<body>";
        public string bodyend = "</body>";
        public string endDiv = "</div>";
        public string collapsible = "collapsible";

        public string True = "&#9989;";
        public string False = "&#9744;"; // red x = &#10060
        public string Warn = "⚠️"; // yellow warning = &#9888
        public string info = "&#9432;"; // circle i for info

        public CHtmlFormatting()
        {
        }

        public string LineBreak()
        {
            return "<br/>";
        }

        public string DoubleLineBreak()
        {
            return "<br/><br/>";
        }

        public string SectionButton(string classType, string displayText)
        {
            return string.Format("<button type=\"button\" class=\"{0} classBtn\">{1}</button>", classType, displayText);
        }

        public string AddA(string classInfo, string displaytext)
        {
            return string.Format("<div class=\"{0}\">{1}</div>", classInfo, displaytext);
        }

        public string CollapsibleButton(string buttonText)
        {
            return this.SectionButton(this.collapsible, buttonText);
        }

        public string FormNavRows(string linkText, string sectionLink, string info, bool isActive = false)
        {
            string activeClass = isActive ? " active" : "";
            return "<tr>" +
                   "<td>" +
                "<dt>" +
                string.Format("<a class=\"smoothscroll{2}\" data-link=\"{0}\" href=\"#{0}\">{1}</a>", sectionLink, linkText, activeClass) +
                "</dt>" +
                string.Format("<dd>{0}</dd>", info)
               + "</td>" +

               "</tr>";
            ;
        }

        public string SectionEnd(string summary)
        {
            string s = "</tbody>";
            s += "</table>";

            // s += summary;
            s += this.endDiv;
            s += this.endDiv;

            return s;
        }

        public string SectionEnd()
        {
             return this.SectionEnd(string.Empty);
        }

        public string SectionEndNoTable(string summary)
        {
            return "</div></div>";
        }

        public string SectionEndNoTable()
        {
            return this.SectionEndNoTable(string.Empty);
        }

        private string SectionCardStart(string id, string title)
        {
            string firstLetter = string.IsNullOrEmpty(title) ? "?" : title[0].ToString().ToUpper();
            string s = $"<div class=\"section-card open\" id=\"{id}\">";
            s += $"<div class=\"section-header\" onclick=\"toggleSection(this)\">";
            s += $"<h2><span class=\"icon\" style=\"background:#f0f9ff;color:#0369a1\">{firstLetter}</span> {title}</h2>";
            s += "<span class=\"toggle\">&#8964;</span>";
            s += "</div>";
            s += "<div class=\"section-body\">";
            return s;
        }

        public string SectionStart(string id, string header)
        {
            string s = this.SectionCardStart(id, header);
            s += this.Table();
            s += "<thead><tr>";
            return s;
        }

        public string SectionStartWithButton(string id, string header, string buttonName)
        {
            string s = this.SectionCardStart(id, header);
            s += this.Table();
            s += "<thead><tr>";

            return s;
        }

        public string SectionStartWithButton(string id, string header, string buttonName, int reportInterval)
        {
            string displayTitle = String.Format(header + " (" + reportInterval + " Days)");
            string s = this.SectionCardStart(id, displayTitle);
            s += this.Table();
            s += "<thead><tr>";

            return s;
        }

        public string SectionStartWithButtonNoTable(string id, string header, string buttonName)
        {
            return this.SectionCardStart(id, header);
        }

        public string SectionStartWithButtonNoTable(string id, string header, string buttonName, int reportInterval)
        {
            string displayTitle = String.Format(header + " (" + reportInterval + " Days)");
            return this.SectionCardStart(id, displayTitle);
        }

        public string TableHeaderLeftAligned(string header, string tooltip)
        {
            string titleAttr = string.IsNullOrEmpty(tooltip) ? "" : $" title=\"{tooltip}\"";
            return $"<th{titleAttr} style=\"text-align:left\">{header}</th>";
        }

        public string TableHeaderLeftAligned(string header, string tooltip, int index)
        {
            string titleAttr = string.IsNullOrEmpty(tooltip) ? "" : $" title=\"{tooltip}\"";
            return $"<th onclick=\"sortTable({index})\"{titleAttr} style=\"text-align:left\">{header}</th>";
        }

        public string TableHeader(string header, string tooltip)
        {
            string titleAttr = string.IsNullOrEmpty(tooltip) ? "" : $" title=\"{tooltip}\"";
            return $"<th{titleAttr}>{header}</th>";
        }

        public string TableHeader(string header, string tooltip, int index)
        {
            string titleAttr = string.IsNullOrEmpty(tooltip) ? "" : $" title=\"{tooltip}\"";
            return $"<th onclick=\"sortTable({index})\"{titleAttr}>{header}</th>";
        }

        public string TableHeaderEnd()
        {
            return string.Format("</tr></thead>");
        }

        public string TableBodyStart()
        {
            return string.Format("<tbody>");
        }

        public string TableRowStart()
        {
            return "<tr>";
        }

        public string TableRowEnd()
        {
            return "</tr>";
        }

        public string TableDataLeftAligned(string data, string toolTip)
        {
            string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
            return $"<td{titleAttr} style=\"text-align:left\">{data}</td>";
        }

        public string TableData(string data, string toolTip)
        {
            string titleAttr = string.IsNullOrEmpty(toolTip) ? "" : $" title=\"{toolTip}\"";
            return $"<td{titleAttr}>{data}</td>";
        }

        /// <summary>
        /// Converts a producer's <see cref="CGlobals.MultiValueDelimiter"/>-joined value into
        /// HTML line breaks for table rendering. JSON export keeps the delimiter unchanged.
        /// </summary>
        public string RenderMultiValueHtml(string value)
        {
            return value?.Replace(CGlobals.MultiValueDelimiter, "<br>");
        }

        public string TableData(string data, string toolTip, int shading)
        {
            string cssClass = shading switch
            {
                1 => "cell-danger",
                2 => "cell-success",
                3 => "cell-warning",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(cssClass)
                ? string.Format("<td title=\"{0}\">{1}</td>", toolTip, data)
                : string.Format("<td title=\"{0}\" class=\"{2}\">{1}</td>", toolTip, data, cssClass);
        }

        public string Table()
        {
            return "<table>";
        }

        public string Table(string cssClass) =>
            string.IsNullOrEmpty(cssClass) ? "<table>" : $"<table class=\"{cssClass}\">";

        public string TwoColStart() => "<div class=\"two-col\">";
        public string TwoColEnd() => "</div>";

        /// <summary>Heat-map-colored table cell. Assigns CSS class based on concurrency value.</summary>
        public string TableDataHeat(string data)
        {
            int value = 0;
            if (!string.IsNullOrEmpty(data))
                int.TryParse(data, out value);

            string heatClass = value switch
            {
                0 => "heat-0",
                <= 2 => "heat-1",
                <= 5 => "heat-2",
                <= 10 => "heat-3",
                <= 20 => "heat-4",
                _ => "heat-5"
            };

            string display = value == 0 ? "" : data;
            string title = value == 0 ? "" : $" title=\"{data}\"";
            return $"<td class=\"{heatClass}\"{title}>{display}</td>";
        }

        public string HeatmapLegend()
        {
            return "<div class=\"heatmap-legend\">"
                + "<span>Less</span>"
                + "<div class=\"heatmap-legend-cell heat-0\"></div>"
                + "<div class=\"heatmap-legend-cell heat-1\"></div>"
                + "<div class=\"heatmap-legend-cell heat-2\"></div>"
                + "<div class=\"heatmap-legend-cell heat-3\"></div>"
                + "<div class=\"heatmap-legend-cell heat-4\"></div>"
                + "<div class=\"heatmap-legend-cell heat-5\"></div>"
                + "<span>More</span>"
                + "</div>";
        }

        public string EndTable()
        {
            return "</table>";
        }

        public string TableCollapsible()
        {
            return "<table border =\"1\" style=\"display: none;\">";
        }

        public string header1(string text)
        {
            return string.Format("<h1><u>{0}</u></h1>", text);
        }

        public string header2(string text)
        {
            return string.Format("<h2><u>{0}:</u></h2>", text);
        }

        public string header3(string text)
        {
            return string.Format("<h3><u>{0}:</u></h3>", text);
        }

        public string Subsection(string text)
        {
            return $"<div class=\"subsection\"><h4>{text}</h4></div>";
        }

        public string Header()
        {
            // log.Info("[HTML] Forming Header...");
            string docType = "<!DOCTYPE html>";
            string htmlOpen = "<html>";
            string styleOpen = "<style type=\"text/css\">";
            string viewPort = "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">";
            string title = "<title>Veeam HealthCheck Report</title>";
            string charSet = "<meta charset=\"UTF-8\">";
            string headOpen = "<head>";
            string headClose = "</head>";
            string styleClose = "</style>";

            string css = GetEmbeddedCssContent("css.css");
            string header = docType + htmlOpen + headOpen + charSet + viewPort + title
                + styleOpen + css
                + styleClose
                + headClose;

            return header;
        }

        #region Buttons
        public string FormHtmlButtonGoToTop()
        {
            return @"<button onclick=""topFunction()"" id=""myBtn"" title=""Go to top"">Go To Top</button>";
        }

        #endregion

        #region helper functions
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

        public string SetNavTables(string reportKind)
        {
            // string tableString = NavTableStarter();
            string tableString = @"<div class='card'><div class='card-container'><div class=card2 id=navigation>";
            tableString += string.Format("<h2>{0}</h2>", VbrLocalizationHelper.NavHeader);
            if(reportKind == "vbr")
            {
                CHtmlTables t = new();
                tableString += t.MakeNavTable();
            }
            else if(reportKind == "vb365")
            {
                CM365Tables t = new();
                tableString += t.MakeVb365NavTable();
            }

            tableString += this.endDiv;

            // tableString += NavtableEnd();
            return tableString;
        }

        #endregion

        public string SetSecurityBannerAndIntro(bool scrub)
        {
            string s = this.SetVbrSecurityReportTitle();

            // s += SetVbrHcIntro(scrub);
            return s;
        }

        private string SetVbrSecurityReportTitle()
        {
            return "<h2 style=\"text-align:center\">" +
                VbrLocalizationHelper.HtmlSecurityHeader +
                "</h2>";
        }

        public string SetBannerAndIntroVb365()
        {
            // string s = "<h2 style=\"color: green; font-style: italic; background: lightgray; text-align:center\">" + VbrLocalizationHelper.Vb365Banner + "</h2>";
            string s = "<h2 style=\"text-align:center\">" + VbrLocalizationHelper.Vb365Banner + "</h2>";

            s += "<div class=\"i2\">" + Vb365ResourceHandler.HtmlIntroLine1 + "</a>\n";
            s += this.LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine2 + "</a>\n";
            s += this.LineBreak();

            // s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine3Original + "</a>\n";
            // s += LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine4 + "</a>\n";
            s += this.LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine5vb365 + "</a>\n";
            s += "</div>";
            s += this.LineBreak();

            return s;
        }

        public string SetVb365Intro()
        {
            string s = string.Empty;
            s += "<div class=\"card2\">" +
                        "<h2>About</h2>" +
                       Vb365ResourceHandler.HtmlIntroLine1 + "</a>\n";

            // s += LineBreak();
            s += String.Format(@"<dl>
                <dt>CSV Raw Data Output</dt>
                <dd>{0}</dd>
                </br>
                <dt></dt>
                <dd>{1}</dd>
                </br>
                <dt></dt>
                <dd>{2}</dd>
               
                


</dl>",
Vb365ResourceHandler.HtmlIntroLine2,
Vb365ResourceHandler.HtmlIntroLine4,
Vb365ResourceHandler.HtmlIntroLine5vb365, string.Empty
);
            s += "</div>";
            return s;
        }

        public string SetHeaderAndLogo(string licenseHolder)
        {
            string s = string.Empty;// "<img src=\"https://uploads-eu-west-1.insided.com/veeam-en/attachment/4cd1f911-4a13-4d3f-adcc-a342f853fa15_thumb.png\"  style=\"width:100%; height:auto;\"";

            string bannerString = CHtmlCompiler.GetEmbeddedCssContent("banner_string.txt");

            // s += String.Format("<img src=\"data:image/png;base64,{0}\" style=\"width:100%; height:auto;\">", bannerString);

            // s += "<div class=\"header-background\"></div>";

            s += String.Format(@"
<div class=""image-container"">
        <div class=""text-overlay"">
        <h1 >
                {0}
        </h1>
        <h3>
            Veeam Backup & Recovery 
        </h3>
        <p>Health Check Report</p>
        <p>{1}|{2}{3}</p>
        </div>
        <img src=""data:image/png;base64,{4}"">
</div>
        

", licenseHolder, DateTime.Now.ToString("MMMM dd yyyy"), CGlobals.ReportDays.ToString(), " day report", bannerString);

            return s;
        }
 
        public string SetHeaderAndLogoVB365(string licenseHolder)
        {
            string s = string.Empty;// "<img src=\"https://uploads-eu-west-1.insided.com/veeam-en/attachment/4cd1f911-4a13-4d3f-adcc-a342f853fa15_thumb.png\"  style=\"width:100%; height:auto;\"";

            string bannerString = CHtmlCompiler.GetEmbeddedCssContent("banner_string.txt");

            // s += String.Format("<img src=\"data:image/png;base64,{0}\" style=\"width:100%; height:auto;\">", bannerString);

            // s += "<div class=\"header-background\"></div>";

            s += String.Format(@"
<div class=""image-container"">
        <div class=""text-overlay"">
        <h1 >
                {0}
        </h1>
        <h3>
            Veeam Backup For Microsoft 365
        </h3>
        <p>Health Check Report</p>
        <p>{1}|{2}{3}</p>
        </div>
        <img src=""data:image/png;base64,{4}"">
</div>
        

", licenseHolder, DateTime.Now.ToString("MMMM dd yyyy"), CGlobals.ReportDays.ToString(), " day report", bannerString);

            return s;
        }

        public string SectionId(string identifier)
        {
            return string.Format("<div class=\"{0} table-sortable card\" id=\"{1}\">", identifier, identifier);
        }

        #region Executive Dashboard Layout

        /// <summary>
        /// Generates the sidebar HTML for the executive dashboard.
        /// </summary>
        public string Sidebar(string navContent, string iconImgTag)
        {
            string logoHtml = string.IsNullOrEmpty(iconImgTag)
                ? "<div class=\"logo-mark\">V</div>"
                : iconImgTag;

            return @"<nav class=""sidebar"">
  <div class=""sidebar-brand"">"
                + logoHtml
                + @"<h2>Veeam Health Check</h2>
    <p>Configuration Report</p>
  </div>"
                + navContent
                + "</nav>";
        }

        /// <summary>
        /// Opens the main content wrapper.
        /// </summary>
        public string MainContentStart()
        {
            return @"<main class=""main"">";
        }

        /// <summary>
        /// Closes the main content wrapper.
        /// </summary>
        public string MainContentEnd()
        {
            return "</main>";
        }

        /// <summary>
        /// Generates the page header with company name and meta info.
        /// </summary>
        public string PageHeader(string licenseHolder, int reportDays)
        {
            string reportDate = DateTime.Now.ToString("MMM d, yyyy");
            return string.Format(@"<div class=""page-header"">
    <h1>{0} - Infrastructure Health Report</h1>
    <div class=""meta"">
      <span>Veeam Backup &amp; Replication</span>
      <span>{1} day report</span>
      <span>Generated {2}</span>
    </div>
  </div>", licenseHolder, reportDays, reportDate);
        }

        /// <summary>
        /// Generates the toolbar with expand/collapse all button.
        /// </summary>
        public string Toolbar()
        {
            return @"<div class=""toolbar"">
    <button class=""btn"" onclick=""toggleAll()"">Expand / Collapse All</button>
  </div>";
        }

        /// <summary>
        /// Generates a sidebar navigation section with title and links.
        /// </summary>
        public string NavSection(string title, string linksHtml)
        {
            return string.Format(@"<div class=""nav-section"">
    <div class=""nav-section-title"">{0}</div>
    {1}
  </div>", title, linksHtml);
        }

        /// <summary>
        /// Generates a sidebar navigation link.
        /// </summary>
        public string NavLink(string href, string text)
        {
            return string.Format(@"<a href=""#{0}"" class=""nav-link"">{1}</a>", href, text);
        }

        public string NavLink(string href, string text, bool isActive)
        {
            string cls = isActive ? "nav-link active" : "nav-link";
            return string.Format(@"<a href=""#{0}"" class=""{2}"">{1}</a>", href, text, cls);
        }

        /// <summary>
        /// Starts a section card. Open sections are visible by default.
        /// </summary>
        public string SectionCardStart(string id, string title, string iconLetter, string iconBg, string iconColor, bool defaultOpen)
        {
            string openClass = defaultOpen ? " open" : "";
            return string.Format(@"<div id=""{0}"" class=""section-card{5}"">
    <div class=""section-header"" onclick=""toggleSection(this)"">
      <h2><span class=""icon"" style=""background:{2};color:{3}"">{1}</span> {4}</h2>
      <span class=""toggle"">&#9662;</span>
    </div>
    <div class=""section-body"">",
                id, iconLetter, iconBg, iconColor, title, openClass);
        }

        /// <summary>
        /// Starts a section card that wraps a table. Opens table and thead.
        /// </summary>
        public string SectionCardTableStart(string id, string title, string iconLetter, string iconBg, string iconColor, bool defaultOpen)
        {
            string s = SectionCardStart(id, title, iconLetter, iconBg, iconColor, defaultOpen);
            s += "<table><thead><tr>";
            return s;
        }

        /// <summary>
        /// Overload with report interval in title.
        /// </summary>
        public string SectionCardTableStart(string id, string title, string iconLetter, string iconBg, string iconColor, bool defaultOpen, int reportInterval)
        {
            return SectionCardTableStart(id, string.Format("{0} ({1} Days)", title, reportInterval), iconLetter, iconBg, iconColor, defaultOpen);
        }

        /// <summary>
        /// Ends a section card. Closes section-body and section-card divs.
        /// </summary>
        public string SectionCardEnd()
        {
            return "</div></div>";
        }

        /// <summary>
        /// Ends a section card that contains a table. Closes table, section-body, section-card.
        /// </summary>
        public string SectionCardTableEnd()
        {
            return "</tbody></table>" + SectionCardEnd();
        }

        #endregion
    }

    enum CellShade
    {
        orangered = 1,
        greenyellow = 2,
        yellow = 3
    }
;
}
