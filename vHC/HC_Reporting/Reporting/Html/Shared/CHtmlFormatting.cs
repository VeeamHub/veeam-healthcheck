using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Resources.Localization.VB365;

namespace VeeamHealthCheck.Reporting.Html.Shared
{
    internal class CHtmlFormatting
    {
        public string html = "<html>";
        public string htmlend = "</html>";
        public string body = "<body>";
        public string bodyend = "</body>";
        public string _endDiv = "</div>";
        public string _collapsible = "collapsible";
        public CHtmlFormatting()
        {

        }
        public string DivId(string id)
        {
            return String.Format("<div id={0}>", id);
        }
        public string h2UnderLine(string text)
        {
            return String.Format("<h2><u>{0}</u></h2>", text);
        }

        public string Button(string displayText)
        {
            return String.Format("<button type=\"button\" class=\"collapsible\">{0}</button>", displayText);
        }


        public string HyperLink(string link, string displayText)
        {
            string s = String.Format("<a href=\"{0}\" target=\"_blank\">{1}</a>", link, displayText);
            return s;
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
            return String.Format("<button type=\"button\" class=\"{0}\">{1}</button>", classType, displayText);
        }
        public string AddA(string classInfo, string displaytext)
        {
            return String.Format("<div class=\"{0}\">{1}</div>", classInfo, displaytext);
        }

        public string BackToTop()
        {
            return String.Format("<a href=\"#top\">Back To Top</a>");
        }
        public string CollapsibleButton(string buttonText)
        {
            return SectionButton(_collapsible, buttonText);
        }
        public string FormNavRows(string linkText, string sectionLink, string info)
        {
            return "<tr>" +
                "<td>" +
                "<li>" +
                String.Format("<a class=\"smoothscroll\" data-link=\"{0}\" href=\"#{0}\">{1}</a>", sectionLink, linkText) +
                "</li>" +
                "</td>" +
                String.Format("<td>{0}</td>", info) +
                "</tr>";
        }
        public string SectionEnd(string summary)
        {
            string s = "</tr>";
            s += "</table>";
            s += LineBreak();
            s += summary;
            s += _endDiv;

            return s;
        }
        public string SectionStart(string id, string header)
        {
            string s = SectionId(id);
            s += header2(header);
            s += Table();
            s += "<tr>";
            return s;
        }
        public string SectionStartWithButton(string id, string header, string buttonName)
        {
            string s = SectionId(id);
            s += header2(header);
            s += CollapsibleButton( buttonName);
            s += "<div class=\"content\" style=\"display: none\">";
            s += Table();
            s += "<tr>";

            return s;
        }
        public string SectionStartWithButton(string id, string header, string buttonName, int reportInterval)
        {
            string s = SectionId(id);
            s += header2(header + " (" + reportInterval + " Days)") ;
            s += CollapsibleButton(buttonName);
            s += "<div class=\"content\" style=\"display: none\">";
            s += Table();
            s += "<tr>";

            return s;
        }
        public string TableHeader(string header, string tooltip)
        {
            return String.Format("<th title=\"{0}\">{1}</th>", tooltip, header);
        }
        public string TableData(string data, string toolTip)
        {
            return String.Format("<td title=\"{0}\">{1}</td>", toolTip, data);
        }
        public string TableData(string data, string toolTip, int shading)
        {
            Type shade = typeof(CellShade);
            var color = shade.GetEnumName(shading);
            
            return String.Format("<td title=\"{0}\" bgcolor=\"{2}\">{1}</td>", toolTip, data, color);
        }
        public string Table()
        {
            return "<table border=\"1\">";
        }
        public string TableCollapsible()
        {
            return "<table border =\"1\" style=\"display: none;\">";
        }
        public string header1(string text)
        {
            return String.Format("<h1><u>{0}</u></h1>", text);
        }
        public string header2(string text)
        {
            return String.Format("<h2><u>{0}:</u></h2>", text);
        }
        public string FormHeader()
        {
            string s = "<html>";
            s += "<head>";
            s += "<style>";
            s += CssStyler.StyleString();
            s += "</style></head>";

            return s;
        }
        public string SetBannerAndIntro()
        {
            string s = "<h2 style=\"color: green; font-style: italic; background: lightgray; text-align:center\">" + ResourceHandler.HtmlHeader + "</h2>";


            s += "<div class=\"i2\">" + ResourceHandler.HtmlIntroLine1 + "</a>\n";
            s += LineBreak();
            s += "<a class=\"\">" + ResourceHandler.HtmlIntroLine2 + "</a>\n";
            s += LineBreak();
            if(VhcGui._scrub)
                s += "<a class=\"\">" + ResourceHandler.HtmlIntroLine3Anon + "</a>\n";
            else
                s += "<a class=\"\">" + ResourceHandler.HtmlIntroLine3Original + "</a>\n";
            s += LineBreak();
            s += "<a class=\"i2\">" + ResourceHandler.HtmlIntroLine4 + "</a>\n";
            s += LineBreak();
            s += "<a class=\"i2\">" + ResourceHandler.HtmlIntroLine5 + "</a>\n";
            s += "</div>";
            s += LineBreak();

            return s;

        }
        public string SetBannerAndIntroVb365()
        {
            string s = "<h2 style=\"color: green; font-style: italic; background: lightgray; text-align:center\">" + ResourceHandler.Vb365Banner + "</h2>";


            s += "<div class=\"i2\">" + Vb365ResourceHandler.HtmlIntroLine1 + "</a>\n";
            s += LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine2 + "</a>\n";
            s += LineBreak();
            //s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine3Original + "</a>\n";
            //s += LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine4 + "</a>\n";
            s += LineBreak();
            s += "<a class=\"\">" + Vb365ResourceHandler.HtmlIntroLine5vb365 + "</a>\n";
            s += "</div>";
            s += LineBreak();

            return s;

        }
        public string SetHeaderAndLogo(string licenseHolder)
        {

            string s = "<h1 style=\"background: #005f4b; color: white;text-align:center\">";
            s += "<a class=\"logo\">";
            s += "<img src=\"data:image/png;base64,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\"></a>";
            s += licenseHolder;
            s += LineBreak();
            s += "<a style=\"font-size:16px\">" + String.Format(DateTime.Now.ToString("dd-MM-yyyy") + LineBreak() +  VhcGui._reportDays.ToString() + " day report") + "</a>";
            s += "</h1>";

            return s;

        }
        public string SectionId(string identifier)
        {
            return String.Format("<div class=\"{0}\" id=\"{1}\">", identifier,identifier);
        }

    }
    enum CellShade
    {
        orangered = 1,
        greenyellow = 2,
        yellow = 3
        
    };
}
