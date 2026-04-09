// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Resources.Localization;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    /// <summary>
    /// Renders the Multi-Role / Proxy table (formerly AddMultiRoleTable in CHtmlTables).
    /// </summary>
    internal class CMultiRoleTable
    {
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CVbrSummaries sum = new();
        private readonly CScrubHandler scrub = CGlobals.Scrubber;
        private readonly CLogger log = CGlobals.Logger;

        public CMultiRoleTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("proxies", VbrLocalizationHelper.PrxTitle, VbrLocalizationHelper.PrxBtn);
            string summary = this.sum.Proxies();
            s += this.form.TableHeader(VbrLocalizationHelper.Prx0, VbrLocalizationHelper.Prx0TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx1, VbrLocalizationHelper.Prx1TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx2, VbrLocalizationHelper.Prx2TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx3, VbrLocalizationHelper.Prx3TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx4, VbrLocalizationHelper.Prx4TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx5, VbrLocalizationHelper.Prx5TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx6, VbrLocalizationHelper.Prx6TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx7, VbrLocalizationHelper.Prx7TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx8, VbrLocalizationHelper.Prx8TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx9, VbrLocalizationHelper.Prx9TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx10, VbrLocalizationHelper.Prx10TT);
            s += this.form.TableHeader(VbrLocalizationHelper.Prx11, VbrLocalizationHelper.Prx11TT);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();
            try
            {
                List<string[]> list = this.df.ProxyXmlFromCsv(scrub);

                foreach (var d in list)
                {
                    s += "<tr>";
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[0], ScrubItemType.Server), string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(d[0], string.Empty);
                    }

                    s += this.form.TableData(d[1], string.Empty);
                    s += this.form.TableData(d[2], string.Empty);
                    s += this.form.TableData(d[3], string.Empty);
                    s += this.form.TableData(d[4], string.Empty);
                    s += this.form.TableData(d[5], string.Empty);
                    s += this.form.TableData(d[6], string.Empty);
                    s += this.form.TableData(d[7], string.Empty);
                    s += this.form.TableData(d[8], string.Empty);
                    s += this.form.TableData(d[9], string.Empty);
                    if (scrub)
                    {
                        s += this.form.TableData(this.scrub.ScrubItem(d[10], ScrubItemType.Server), string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(d[10], string.Empty);
                    }

                    s += this.form.TableData(d[11], string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
                this.log.Error("PROXY Data import failed. ERROR:");
                this.log.Error("\t" + e.Message);
            }

            s += this.form.SectionEnd(summary);
            return s;
        }
    }
}
