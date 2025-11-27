// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CVbrSummaries
    {
        private readonly CHtmlFormatting form = new();

        public CVbrSummaries()
        {
        }

        public string SummaryTemplate()
        {
            string s = this.form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\" style=\"display: none\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
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

        public string LicSum()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +
                this.form.AddA("i3", string.Empty) +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) +// _form.LineBreak() +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty) +
                this.form.AddA("i2", string.Empty)
                ;
            s += "</div>";
            s += "</div>";

            return null;
        }

        public string SetVbrSummary()
        {
            string s = this.form.CollapsibleButton(VbrLocalizationHelper.BkpSrvButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvSummary1) +
                this.form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary2) +
                this.form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary3) +
                this.form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary4) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes1) +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes2) +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes3) +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes4) +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes5) +
                this.form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes6)
                ;
            s += this.form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string SecSum()
        {
            string s = this.form.CollapsibleButton(VbrLocalizationHelper.SSButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +
                this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SSSum1) +

                // _form.LineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr1) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SSNote1) + this.form.LineBreak() +

                // _form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr2) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SSNote2) + this.form.LineBreak() +

                // _form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr3) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SSNote3) + this.form.LineBreak() +

                // _form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr4) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SSNote4) +
               this.form.LineBreak();
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string SrvSum()
        {
            string s = this.form.CollapsibleButton(VbrLocalizationHelper.MssButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.MssSum1) +
                this.form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobSummary()
        {
            string s = this.form.CollapsibleButton(VbrLocalizationHelper.JobSumBtn);

            s += "<div class=\"content\" style=\"display: none\">";
            s += this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobSumSum0) + this.form.LineBreak() +

                // _form.LineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobSumNote0) +
                this.form.AddA("i2", VbrLocalizationHelper.JobSumNote1) +
                this.form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string MissingJobsSUmmary()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.NpSum1) +
                  this.form.LineBreak() 
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ProtectedWorkloads()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.PlSum1) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.PlNote1) +
                     this.form.LineBreak();
            
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ManagedServers()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.ManSrvSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.ManSrvNote0) +
                this.form.AddA("i2", VbrLocalizationHelper.ManSrvNote1) +
                  this.form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string RegKeys()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.RegSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.RegNote0) +
                this.form.AddA("i2", VbrLocalizationHelper.RegNote1) +
                    this.form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Proxies()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.PrxSum0) +
                this.form.AddA("i3", VbrLocalizationHelper.PrxSum1) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxSum2) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxSum3) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxSum4) +
                this.form.AddA("i3", VbrLocalizationHelper.PrxSum5) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxSum6) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote0) +
                this.form.AddA("i3", VbrLocalizationHelper.PrxNote1) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxNote2) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxNote3) +
                this.form.AddA("i4", VbrLocalizationHelper.PrxNote4) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote5) +
                this.form.AddA("i3", VbrLocalizationHelper.PrxNote6) +
                this.form.AddA("i3", VbrLocalizationHelper.PrxNote7) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote8) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote9) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote10) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote11) +
                this.form.AddA("i2", VbrLocalizationHelper.PrxNote12) +
                this.form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Sobr()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrSum0) +
                this.form.AddA("i3", VbrLocalizationHelper.SbrSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote0) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote1) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote2) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote3) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote4) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote5) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrNote6) +
                  this.form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Extents()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtSum0) +
                this.form.AddA("i3", VbrLocalizationHelper.SbrExtSum1) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote0subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote1) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote2) +
                this.form.AddA("i3", VbrLocalizationHelper.SbrExtNote3) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote4) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote5subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote6) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote7) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote8) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote9) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote10subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote11) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote12) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote13subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote14) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote15) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote16) +
                  this.form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Repos()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.RepoSum0) +
                this.form.AddA("i2", VbrLocalizationHelper.RepoSum1) + this.form.LineBreak() +

            // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote0subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote1) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote2) +
                this.form.AddA("i3", VbrLocalizationHelper.SbrExtNote3) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote4) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote5subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote6) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote7) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote8) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote9) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote10subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote11) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote12) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote13subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote14) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote15) +
                this.form.AddA("i2", VbrLocalizationHelper.SbrExtNote16) +
                this.form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobCon()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobConSum0) + this.form.LineBreak() +

            // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.JobConNote0subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobConNote1) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobConNote2) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobConNote3) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.JobConNote4subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", this.form.AddA("bld", VbrLocalizationHelper.JobConNote5bold)) + this.form.LineBreak() +
                this.form.Table() +
                "<thead>" +
                "<th>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col1) + "</th>" +
                "<th>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col2) + "</th>" +
                "<th>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col3) + "</th>" +
                "</tr></thead><tbody><tr>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col1) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col2) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRowRow3Col1) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow3Col2) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow3Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col1) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col2) + "</td>" +
                "<td>" + this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col3) + "</td>" +
                "</tr></tbody>" +
                "</table>" +
                     this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableNote0) + this.form.LineBreak() +
                this.form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote1) +
                this.form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote2) +
                this.form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote3) +
                this.form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote4) +
                  this.form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string TaskCon()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.TaskConSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.TaskConNote0) +
                this.form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobSessSummary()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JssSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("subhdr", VbrLocalizationHelper.JssNote0subhdr) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote1) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote2) +
                this.form.AddA("i3", VbrLocalizationHelper.JssNote3) +
                this.form.AddA("i3", VbrLocalizationHelper.JssNote4) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote5) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote6) +
                this.form.AddA("i3", VbrLocalizationHelper.JssNote7) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote8) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote9) +
                this.form.AddA("i2", VbrLocalizationHelper.JssNote10) +
                this.form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobInfo()
        {
            string s = this.form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobInfoSum0) + this.form.LineBreak() +

                // _form.DoubleLineBreak() +
                this.form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + this.form.LineBreak() +
                this.form.AddA("i2", VbrLocalizationHelper.JobInfoNote0) +
                this.form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
    }
}
