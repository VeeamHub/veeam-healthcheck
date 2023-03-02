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
        private CLogger LOG = CGlobals.Logger;
        public CReportModeSelector()
        {
        }
        public void Run()
        {
            FileChecker();
        }
        public void Dispose()
        {

        }
        private void FileChecker()
        {
            LOG.Info("Checking output directories..", false);
            if (CGlobals.RunSecReport)
                StartSecurityReport();
            if (!CGlobals.RunSecReport)
            {

                if (Directory.Exists(CVariables.vb365dir) && CGlobals.RunFullReport)
                    StartM365Report();
                if (Directory.Exists(CVariables.vbrDir) && CGlobals.RunFullReport)
                    StartVbrReport();
            }
        }
        private void StartVbrReport()
        {
            LOG.Info("Starting B&R report generation", false);
            CHtmlCompiler html = new();
            html.RunFullVbrReport();
            html.Dispose();
        }

        private void StartM365Report()
        {
            LOG.Info("Starting VB365 Report genration", false);
            CVb365HtmlCompiler compiler = new();
            compiler.Dispose();
        }
        private void StartSecurityReport()
        {
            LOG.Info("Starting Security Report generation", false);
            CHtmlCompiler html = new();
            html.RunSecurityReport();
        }
    }
}
