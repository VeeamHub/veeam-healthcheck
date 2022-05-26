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
        public string SetVbrSummary()
        {
            string s = _form.CollapsibleButton(ResourceHandler.BkpSrvButton);

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

    }
}
