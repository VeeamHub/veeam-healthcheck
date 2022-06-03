using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.Shared;

namespace VeeamHealthCheck.Reporting.Html.VBR
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
        public string LicSum()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                _form.AddA("i3", "") +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "") +
                _form.AddA("i2", "")
                ;
            s += "</div>";
            s += "</div>";

            return null;
        }
        public string SetVbrSummary()
        {
            string s = _form.CollapsibleButton(ResourceHandler.BkpSrvButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvSummary1) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary2) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary3) +
                _form.AddA("i3", ResourceHandler.BkpSrvSummary4) +
                // _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes1) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes2) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes3) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes4) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes5) +
                _form.AddA("i2", ResourceHandler.BkpSrvNotes6)
                ;
            s += //_form.LineBreak() +
               _form.BackToTop();
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string SecSum()
        {
            string s = _form.CollapsibleButton(ResourceHandler.SSButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +
                _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.SSSum1) +
                //  _form.LineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SSSubHdr1) +
                _form.AddA("i2", ResourceHandler.SSNote1) +
                //  _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SSSubHdr2) +
                _form.AddA("i2", ResourceHandler.SSNote2) +
                //   _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SSSubHdr3) +
                _form.AddA("i2", ResourceHandler.SSNote3) +
                //  _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SSSubHdr4) +
                _form.AddA("i2", ResourceHandler.SSNote4)
            //   _form.LineBreak();
            ;
            s += _form.BackToTop();
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string SrvSum()
        {
            string s = _form.CollapsibleButton(ResourceHandler.MssButton);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.MssSum1) +
                //_form.LineBreak() +
                _form.BackToTop();

            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobSummary()
        {
            string s = _form.CollapsibleButton(ResourceHandler.JobSumBtn);

            s += "<div class=\"content\" style=\"display: none\">";
            s += _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +
                _form.AddA("i2", ResourceHandler.JobSumSum0) +
                //_form.LineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +
                _form.AddA("i2", ResourceHandler.JobSumNote0) +
                _form.AddA("i2", ResourceHandler.JobSumNote1) +
                //_form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string MissingJobsSUmmary()
        {
            string s = _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.NpSum1) +
                //  _form.LineBreak() +
                _form.BackToTop();
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ProtectedWorkloads()
        {

            string s = _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.PlSum1) +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.PlNote1) +
                //     _form.LineBreak();
                _form.BackToTop();
            ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string ManagedServers()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.ManSrvSum0) +
                //     _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.ManSrvNote0) +
                _form.AddA("i2", ResourceHandler.ManSrvNote1) +
                //  _form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string RegKeys()
        {
            string s =_form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.RegSum0) +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.RegNote0) +
                _form.AddA("i2", ResourceHandler.RegNote1) +
                //    _form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string Proxies()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.PrxSum0) +
                _form.AddA("i3", ResourceHandler.PrxSum1) +
                _form.AddA("i4", ResourceHandler.PrxSum2) +
                _form.AddA("i4", ResourceHandler.PrxSum3) +
                _form.AddA("i4", ResourceHandler.PrxSum4) +
                _form.AddA("i3", ResourceHandler.PrxSum5) +
                _form.AddA("i4", ResourceHandler.PrxSum6) +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.PrxNote0) +
                _form.AddA("i3", ResourceHandler.PrxNote1) +
                _form.AddA("i4", ResourceHandler.PrxNote2) +
                _form.AddA("i4", ResourceHandler.PrxNote3) +
                _form.AddA("i4", ResourceHandler.PrxNote4) +
                _form.AddA("i2", ResourceHandler.PrxNote5) +
                _form.AddA("i3", ResourceHandler.PrxNote6) +
                _form.AddA("i3", ResourceHandler.PrxNote7) +
                _form.AddA("i2", ResourceHandler.PrxNote8) +
                _form.AddA("i2", ResourceHandler.PrxNote9) +
                _form.AddA("i2", ResourceHandler.PrxNote10) +
                _form.AddA("i2", ResourceHandler.PrxNote11) +
                _form.AddA("i2", ResourceHandler.PrxNote12) +
            //    _form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string Sobr()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.SbrSum0) +
                _form.AddA("i3", ResourceHandler.SbrSum0) +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.SbrNote0) +
                _form.AddA("i2", ResourceHandler.SbrNote1) +
                _form.AddA("i2", ResourceHandler.SbrNote2) +
                _form.AddA("i2", ResourceHandler.SbrNote3) +
                _form.AddA("i2", ResourceHandler.SbrNote4) +
                _form.AddA("i2", ResourceHandler.SbrNote5) +
                _form.AddA("i2", ResourceHandler.SbrNote6) +
                //  _form.LineBreak()+
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string Extents()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.SbrExtSum0) +
                _form.AddA("i3", ResourceHandler.SbrExtSum1) +
                //   _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote0subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote1) +
                _form.AddA("i2", ResourceHandler.SbrExtNote2) +
                _form.AddA("i2", ResourceHandler.SbrExtNote3) +
                _form.AddA("i2", ResourceHandler.SbrExtNote4) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote5subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote6) +
                _form.AddA("i2", ResourceHandler.SbrExtNote7) +
                _form.AddA("i2", ResourceHandler.SbrExtNote8) +
                _form.AddA("i2", ResourceHandler.SbrExtNote9) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote10subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote11) +
                _form.AddA("i2", ResourceHandler.SbrExtNote12) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote13subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote14) +
                _form.AddA("i2", ResourceHandler.SbrExtNote15) +
                _form.AddA("i2", ResourceHandler.SbrExtNote16) +
                //  _form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string Repos()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.RepoSum0) +
                _form.AddA("i2", ResourceHandler.RepoSum1) +
            //    _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote0subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote1) +
                _form.AddA("i2", ResourceHandler.SbrExtNote2) +
                _form.AddA("i2", ResourceHandler.SbrExtNote3) +
                _form.AddA("i2", ResourceHandler.SbrExtNote4) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote5subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote6) +
                _form.AddA("i2", ResourceHandler.SbrExtNote7) +
                _form.AddA("i2", ResourceHandler.SbrExtNote8) +
                _form.AddA("i2", ResourceHandler.SbrExtNote9) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote10subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote11) +
                _form.AddA("i2", ResourceHandler.SbrExtNote12) +
                _form.AddA("subhdr", ResourceHandler.SbrExtNote13subhdr) +
                _form.AddA("i2", ResourceHandler.SbrExtNote14) +
                _form.AddA("i2", ResourceHandler.SbrExtNote15) +
                _form.AddA("i2", ResourceHandler.SbrExtNote16) +
            //    _form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobCon()
        {
            string s = _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.JobConSum0) +
            //    _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) + _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.JobConNote0subhdr) +
                _form.AddA("i2", ResourceHandler.JobConNote1) +
                _form.AddA("i2", ResourceHandler.JobConNote2) +
                _form.AddA("i2", ResourceHandler.JobConNote3) +
                _form.AddA("subhdr", ResourceHandler.JobConNote4subhdr) +
                _form.AddA("i2", _form.AddA("bld", ResourceHandler.JobConNote5bold)) +
                "<table border=\"1\"><tr>" +
                "<th>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow1Col1) + "</th>" +
                "<th>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow1Col2) + "</th>" +
                "<th>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow1Col3) + "</th>" +
                "</tr><tr>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow2Col1) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow2Col2) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow2Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRowRow3Col1) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow3Col2) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow3Col3) + "</td>" +
                "</tr><tr>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow4Col1) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow4Col2) + "</td>" +
                "<td>" + _form.AddA("i2", ResourceHandler.JobConNoteSqlTableRow4Col3) + "</td>" +
                "</tr>" +
                "</table>" +
                //     _form.LineBreak()+
                _form.AddA("i2", ResourceHandler.JobConNoteSqlTableNote0) +
                _form.AddA("i2", ResourceHandler.JobConNoteSqlTableNote1) +
                _form.AddA("i2", ResourceHandler.JobConNoteSqlTableNote2) +
                _form.AddA("i2", ResourceHandler.JobConNoteSqlTableNote3) +
                _form.AddA("i2", ResourceHandler.JobConNoteSqlTableNote4) +
                //  _form.LineBreak() + 
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string TaskCon()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.TaskConSum0) +
                //  _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.TaskConNote0) +
              //  _form.LineBreak() +
              _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobSessSummary()
        {
            string s = _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.JssSum0) +
               // _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("subhdr", ResourceHandler.JssNote0subhdr) +
                _form.AddA("i2", ResourceHandler.JssNote1) +
                _form.AddA("i2", ResourceHandler.JssNote2) +
                _form.AddA("i2", ResourceHandler.JssNote3) +
                _form.AddA("i2", ResourceHandler.JssNote4) +
                _form.AddA("i2", ResourceHandler.JssNote5) +
                _form.AddA("i2", ResourceHandler.JssNote6) +
                _form.AddA("i2", ResourceHandler.JssNote7) +
                _form.AddA("i2", ResourceHandler.JssNote8) +
                _form.AddA("i2", ResourceHandler.JssNote9) +
                _form.AddA("i2", ResourceHandler.JssNote10) +
                //_form.LineBreak() +
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }
        public string JobInfo()
        {
            string s =  _form.AddA("hdr", ResourceHandler.GeneralSummaryHeader) + //_form.LineBreak() +
                _form.AddA("i2", ResourceHandler.JobInfoSum0) +
              //  _form.DoubleLineBreak() +
                _form.AddA("hdr", ResourceHandler.GeneralNotesHeader) +// _form.LineBreak() +
                _form.AddA("i2", ResourceHandler.JobInfoNote0) +
               // _form.LineBreak() + 
                _form.BackToTop()
                ;
            s += "</div>";
            s += "</div>";

            return s;
        }

    }
}
