// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Resources.Localization.VB365;

namespace VeeamHealthCheck.Functions.Reporting.Html.VB365
{
    internal class CM365Summaries
    {
        private readonly CHtmlFormatting form = new();

        public CM365Summaries()
        {
        }

        public string GlobalSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", Vb365ResourceHandler.GlobalSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalSummary1) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", Vb365ResourceHandler.GlobalNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalNotes1) +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalNotes2) +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalNotes3) +
                this.form.AddA("i3", Vb365ResourceHandler.GlobalNotes4) +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalNotes5) +
                this.form.AddA("i2", Vb365ResourceHandler.GlobalNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ProxySummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.proxySummary1) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.proxyNote1) +
                this.form.AddA("i2", Vb365ResourceHandler.proxyNote2) +
                this.form.AddA("i2", Vb365ResourceHandler.proxyNote3)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string RepoSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.reposSummary) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.reposNote1) +
                this.form.AddA("i2", Vb365ResourceHandler.reposNote2) +

                // _form.AddA("i2", Vb365ResourceHandler.reposNote3) +
                this.form.AddA("i2", Vb365ResourceHandler.reposNote4) +
                this.form.AddA("i2", Vb365ResourceHandler.reposNote5)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string RbacSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string SecSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.securitySummary) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.securityNote1) +
                this.form.AddA("i2", Vb365ResourceHandler.securityNote2) +
                this.form.AddA("i2", Vb365ResourceHandler.securityNote3)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ControllerSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.backupServerSummary1) +
                this.form.AddA("i3", Vb365ResourceHandler.backupServerSummary2) +
                this.form.AddA("i3", Vb365ResourceHandler.backupServerSummary3) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.backupServerNotes1) +
                this.form.AddA("i2", Vb365ResourceHandler.backupServerNotes2) +
                this.form.AddA("i2", Vb365ResourceHandler.backupServerNotes3)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ControllerDrivesSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", Vb365ResourceHandler.BkpSrvDisksSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksSummary1) +
                this.form.AddA("i3", Vb365ResourceHandler.BkpSrvDisksSummary2) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", Vb365ResourceHandler.BkpSrvDisksNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksNotes1) +
                this.form.AddA("i2", Vb365ResourceHandler.BkpSrvDisksNotes2)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobSessSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.jobSessSummary) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.jobSessNote1)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobStatSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", Vb365ResourceHandler.JobStatSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatSummary1) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary2) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary3) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary4) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary5) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary6) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary7) +
                this.form.AddA("i3", Vb365ResourceHandler.JobStatSummary8) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", Vb365ResourceHandler.JobStatNoteHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatNote1) +

                this.form.AddA("i3", Vb365ResourceHandler.JobStatNote2) +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatNote3) +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatNote4) +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatNote5) +
                this.form.AddA("i2", Vb365ResourceHandler.JobStatNote6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobsSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", Vb365ResourceHandler.JobStatSummaryHeader) +
                this.form.AddA("i2", Vb365ResourceHandler.jobsSummary) + // _form.LineBreak() +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", Vb365ResourceHandler.JobStatNoteHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.jobsNote1) +

                this.form.AddA("i3", Vb365ResourceHandler.jobsNote2) +
                this.form.AddA("i2", Vb365ResourceHandler.jobsNote3)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ObjRepoSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", Vb365ResourceHandler.ObjStgSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgSummary1) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", Vb365ResourceHandler.ObjStgNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes1) +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes2) +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes3) +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes4) +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes5) +
                this.form.AddA("i2", Vb365ResourceHandler.ObjStgNotes6)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string OrgSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.orgSummary) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.orgNote1) +
                this.form.AddA("i2", Vb365ResourceHandler.orgNote2) +
                this.form.AddA("i2", Vb365ResourceHandler.orgNote3)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string PermissionSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ProtStatSummary()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.protectedUsersSummary1) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + // _form.LineBreak() +
                this.form.AddA("i2", Vb365ResourceHandler.protectedUsersNote1) +
                this.form.AddA("i2", Vb365ResourceHandler.protectedUsersNote2)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
    }
}
