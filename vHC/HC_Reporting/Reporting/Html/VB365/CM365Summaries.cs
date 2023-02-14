using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization.VB365;
using VeeamHealthCheck.Resources.Localization;

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
            s += _form.AddA("hdr", Vb365ResourceHandler.GlobalSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.GlobalSummary1) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", Vb365ResourceHandler.GlobalNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.GlobalNotes1) +
                _form.AddA("i2", Vb365ResourceHandler.GlobalNotes2) +
                _form.AddA("i2", Vb365ResourceHandler.GlobalNotes3) +
                _form.AddA("i3", Vb365ResourceHandler.GlobalNotes4) +
                _form.AddA("i2", Vb365ResourceHandler.GlobalNotes5) +
                _form.AddA("i2", Vb365ResourceHandler.GlobalNotes6) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ProxySummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.proxySummary1) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.proxyNote1) +
                _form.AddA("i2", Vb365ResourceHandler.proxyNote2) +
                _form.AddA("i2", Vb365ResourceHandler.proxyNote3) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string RepoSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.reposSummary) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.reposNote1) +
                _form.AddA("i2", Vb365ResourceHandler.reposNote2) +
                //_form.AddA("i2", Vb365ResourceHandler.reposNote3) +
                _form.AddA("i2", Vb365ResourceHandler.reposNote4) +
                _form.AddA("i2", Vb365ResourceHandler.reposNote5) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string RbacSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
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
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.securitySummary) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.securityNote1) +
                _form.AddA("i2", Vb365ResourceHandler.securityNote2) +
                _form.AddA("i2", Vb365ResourceHandler.securityNote3) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ControllerSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.backupServerSummary1) +
                _form.AddA("i3", Vb365ResourceHandler.backupServerSummary2) +
                _form.AddA("i3", Vb365ResourceHandler.backupServerSummary3) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.backupServerNotes1) +
                _form.AddA("i2", Vb365ResourceHandler.backupServerNotes2) +
                _form.AddA("i2", Vb365ResourceHandler.backupServerNotes3) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ControllerDrivesSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", Vb365ResourceHandler.BkpSrvDisksSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksSummary1) +
                _form.AddA("i3", Vb365ResourceHandler.BkpSrvDisksSummary2) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", Vb365ResourceHandler.BkpSrvDisksNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksNotes1) +
                _form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksNotes2) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobSessSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.jobSessSummary) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.jobSessNote1) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobStatSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", Vb365ResourceHandler.JobStatSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.JobStatSummary1) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary2) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary3) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary4) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary5) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary6) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary7) +
                _form.AddA("i3", Vb365ResourceHandler.JobStatSummary8) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", Vb365ResourceHandler.JobStatNoteHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.JobStatNote1) +

                _form.AddA("i3", Vb365ResourceHandler.JobStatNote2) +
                _form.AddA("i2", Vb365ResourceHandler.JobStatNote3) +
                _form.AddA("i2", Vb365ResourceHandler.JobStatNote4) +
                _form.AddA("i2", Vb365ResourceHandler.JobStatNote5) +
                _form.AddA("i2", Vb365ResourceHandler.JobStatNote6) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobsSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", Vb365ResourceHandler.JobStatSummaryHeader) + 
                _form.AddA("i2", Vb365ResourceHandler.jobsSummary) + //_form.LineBreak() +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", Vb365ResourceHandler.JobStatNoteHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.jobsNote1) +

                _form.AddA("i3", Vb365ResourceHandler.jobsNote2) +
                _form.AddA("i2", Vb365ResourceHandler.jobsNote3) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ObjRepoSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", Vb365ResourceHandler.ObjStgSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgSummary1) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", Vb365ResourceHandler.ObjStgNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes1) +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes2) +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes3) +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes4) +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes5) +
                _form.AddA("i2", Vb365ResourceHandler.ObjStgNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string OrgSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.orgSummary) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.orgNote1) +
                _form.AddA("i2", Vb365ResourceHandler.orgNote2) +
                _form.AddA("i2", Vb365ResourceHandler.orgNote3) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string PermissionSummary()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
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
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.protectedUsersSummary1) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", Vb365ResourceHandler.protectedUsersNote1) +
                _form.AddA("i2", Vb365ResourceHandler.protectedUsersNote2) 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        
    }
}
