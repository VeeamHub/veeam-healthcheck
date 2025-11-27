// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.IO;
using VeeamHealthCheck.Functions.Reporting.Html.VB365;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CReportModeSelector
    {
        private readonly CLogger LOG = CGlobals.Logger;

        public CReportModeSelector()
        {
        }

        public int Run()
        {
            return this.FileChecker();
        }

        public void Dispose()
        {
        }

        private int FileChecker()
        {
            this.LOG.Info("Checking output directories..", false);
            int res = 0;
            if (CGlobals.RunSecReport)
            {
                this.StartSecurityReport();
            }


            if (!CGlobals.RunSecReport)
            {
                if (Directory.Exists(CVariables.vb365dir) && CGlobals.RunFullReport)
                {
                    this.StartM365Report();
                }

                if (Directory.Exists(CVariables.vbrDir) && CGlobals.RunFullReport)
                {
                    res = this.StartVbrReport();
                }
            }

            return res;
        }

        private int StartVbrReport()
        {
            this.LOG.Info("Starting B&R report generation", false);
            this.LOG.Info("About to instantiate CHtmlCompiler...", false);
            CHtmlCompiler html = new();
            this.LOG.Info("CHtmlCompiler instantiated successfully", false);
            var res = html.RunFullVbrReport();
            html.Dispose();
            return res;
        }

        private void StartM365Report()
        {
            this.LOG.Info("Starting VB365 Report genration", false);
            CVb365HtmlCompiler compiler = new();
            compiler.Dispose();
        }

        private void StartSecurityReport()
        {
            this.LOG.Info("Starting Security Report generation", false);
            CHtmlCompiler html = new();
            html.RunSecurityReport();
        }
    }
}
