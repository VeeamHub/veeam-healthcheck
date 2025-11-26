// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CVbrSummaries
    {
        private CHtmlFormatting _form = new();

        public CVbrSummaries()
        {

        }

        public string SummaryTemplate()
        {
            string s = _form.CollapsibleButton("Show Summary");

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i3", string.Empty) +
                _form.AddA("i3", string.Empty) +
                _form.AddA("i3", string.Empty) +
                _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty)
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string LicSum()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i3", string.Empty) +
                _form.AddA("i3", string.Empty) +
                _form.AddA("i3", string.Empty) +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty) +
                _form.AddA("i2", string.Empty)
                ;
            s += "</div>";
            s += "</div>";

            return null;
        }

        public string SetVbrSummary()
        {
            string s = _form.CollapsibleButton(VbrLocalizationHelper.BkpSrvButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvSummary1) +
                _form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary2) +
                _form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary3) +
                _form.AddA("i3", VbrLocalizationHelper.BkpSrvSummary4) + _form.LineBreak() +
                // _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes1) +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes2) +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes3) +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes4) +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes5) +
                _form.AddA("i2", VbrLocalizationHelper.BkpSrvNotes6)
                ;
            s += _form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string SecSum()
        {
            string s = _form.CollapsibleButton(VbrLocalizationHelper.SSButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +
                _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SSSum1) +
                //  _form.LineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr1) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SSNote1) + _form.LineBreak() +
                //  _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr2) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SSNote2) + _form.LineBreak() +
                //   _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr3) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SSNote3) + _form.LineBreak() +
                //  _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SSSubHdr4) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SSNote4) +
               _form.LineBreak();
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string SrvSum()
        {
            string s = _form.CollapsibleButton(VbrLocalizationHelper.MssButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.MssSum1) +
                _form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobSummary()
        {
            string s = _form.CollapsibleButton(VbrLocalizationHelper.JobSumBtn);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobSumSum0) + _form.LineBreak() +
                //_form.LineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobSumNote0) +
                _form.AddA("i2", VbrLocalizationHelper.JobSumNote1) +
                _form.LineBreak();
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string MissingJobsSUmmary()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.NpSum1) +
                  _form.LineBreak() 
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ProtectedWorkloads()
        {

            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.PlSum1) + _form.LineBreak() +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.PlNote1) +
                     _form.LineBreak();
            
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string ManagedServers()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.ManSrvSum0) + _form.LineBreak() +
                //     _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.ManSrvNote0) +
                _form.AddA("i2", VbrLocalizationHelper.ManSrvNote1) +
                  _form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string RegKeys()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.RegSum0) + _form.LineBreak() +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.RegNote0) +
                _form.AddA("i2", VbrLocalizationHelper.RegNote1) +
                    _form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Proxies()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.PrxSum0) +
                _form.AddA("i3", VbrLocalizationHelper.PrxSum1) +
                _form.AddA("i4", VbrLocalizationHelper.PrxSum2) +
                _form.AddA("i4", VbrLocalizationHelper.PrxSum3) +
                _form.AddA("i4", VbrLocalizationHelper.PrxSum4) +
                _form.AddA("i3", VbrLocalizationHelper.PrxSum5) +
                _form.AddA("i4", VbrLocalizationHelper.PrxSum6) + _form.LineBreak() +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote0) +
                _form.AddA("i3", VbrLocalizationHelper.PrxNote1) +
                _form.AddA("i4", VbrLocalizationHelper.PrxNote2) +
                _form.AddA("i4", VbrLocalizationHelper.PrxNote3) +
                _form.AddA("i4", VbrLocalizationHelper.PrxNote4) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote5) +
                _form.AddA("i3", VbrLocalizationHelper.PrxNote6) +
                _form.AddA("i3", VbrLocalizationHelper.PrxNote7) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote8) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote9) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote10) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote11) +
                _form.AddA("i2", VbrLocalizationHelper.PrxNote12) +
                _form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Sobr()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrSum0) +
                _form.AddA("i3", VbrLocalizationHelper.SbrSum0) + _form.LineBreak() +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote0) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote1) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote2) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote3) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote4) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote5) +
                _form.AddA("i2", VbrLocalizationHelper.SbrNote6) +
                  _form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Extents()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtSum0) +
                _form.AddA("i3", VbrLocalizationHelper.SbrExtSum1) + _form.LineBreak() +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote0subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote1) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote2) +
                _form.AddA("i3", VbrLocalizationHelper.SbrExtNote3) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote4) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote5subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote6) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote7) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote8) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote9) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote10subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote11) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote12) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote13subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote14) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote15) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote16) +
                  _form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string Repos()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.RepoSum0) +
                _form.AddA("i2", VbrLocalizationHelper.RepoSum1) + _form.LineBreak() +
            //    _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote0subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote1) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote2) +
                _form.AddA("i3", VbrLocalizationHelper.SbrExtNote3) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote4) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote5subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote6) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote7) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote8) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote9) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote10subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote11) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote12) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.SbrExtNote13subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote14) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote15) +
                _form.AddA("i2", VbrLocalizationHelper.SbrExtNote16) +
                _form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobCon()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobConSum0) + _form.LineBreak() +
            //    _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.JobConNote0subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobConNote1) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobConNote2) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobConNote3) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.JobConNote4subhdr) + _form.LineBreak() +
                _form.AddA("i2", _form.AddA("bld", VbrLocalizationHelper.JobConNote5bold)) + _form.LineBreak() +
                _form.Table() +
                "<thead>" +
                "<th>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col1) + "</th>" +
                "<th>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col2) + "</th>" +
                "<th>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow1Col3) + "</th>" +
                "</tr></thead><tbody><tr>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col1) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col2) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow2Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRowRow3Col1) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow3Col2) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow3Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col1) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col2) + "</td>" +
                "<td>" + _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableRow4Col3) + "</td>" +
                "</tr></tbody>" +
                "</table>" +
                     _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobConNoteSqlTableNote0) + _form.LineBreak() +
                _form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote1) +
                _form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote2) +
                _form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote3) +
                _form.AddA("i3", VbrLocalizationHelper.JobConNoteSqlTableNote4) +
                  _form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string TaskCon()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.TaskConSum0) + _form.LineBreak() +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.TaskConNote0) +
                _form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobSessSummary()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JssSum0) + _form.LineBreak() +
                // _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", VbrLocalizationHelper.JssNote0subhdr) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JssNote1) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote2) +
                _form.AddA("i3", VbrLocalizationHelper.JssNote3) +
                _form.AddA("i3", VbrLocalizationHelper.JssNote4) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote5) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote6) +
                _form.AddA("i3", VbrLocalizationHelper.JssNote7) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote8) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote9) +
                _form.AddA("i2", VbrLocalizationHelper.JssNote10) +
                _form.LineBreak() 
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

        public string JobInfo()
        {
            string s = _form.AddA("hdr", VbrLocalizationHelper.GeneralSummaryHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobInfoSum0) + _form.LineBreak() +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", VbrLocalizationHelper.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("i2", VbrLocalizationHelper.JobInfoNote0) +
                _form.LineBreak()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

    }
}
