using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.Shared;

namespace VeeamHealthCheck.Reporting.Html.VB365
{
    internal class CM365Summaries
    {
        private CHtmlFormatting _form = new();
        public CM365Summaries()
        {

        }
        public string GlobalSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvSummary1) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary2) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary3) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary4) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes1) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes2) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes3) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes4) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes5) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ProxySummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string RepoSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string RbacSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string SecSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ControllerSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ControllerDrivesSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobSessSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobStatSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ObjRepoSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string OrgSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string PermissionSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ProtStatSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        
    }
}
