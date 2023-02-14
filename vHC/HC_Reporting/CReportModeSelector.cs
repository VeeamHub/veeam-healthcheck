using System.IO;
using System.Management.Automation;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.Reporting.Html.VB365;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    internal class CReportModeSelector
    {
        private readonly string _path;
        private readonly bool _scrub;
        private readonly bool _openHtml;
        private readonly bool _import;
        private CLogger _log = CGlobals.Logger;
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
            _log.Info("Checking output directories..");
            if (CGlobals.RunSecReport)
                StartSecurityReport();
            if (Directory.Exists(CVariables.vb365dir) && CGlobals.RunFullReport)
                StartM365Report();
            if (Directory.Exists(CVariables.vbrDir) && CGlobals.RunFullReport)
                StartVbrReport();
        }
        private void StartVbrReport()
        {
            _log.Info("Starting B&R report generation");
            CHtmlCompiler html = new();
            html.RunFullVbrReport();
            html.Dispose();
        }

        private void StartM365Report()
        {
            _log.Info("Starting VB365 Report genration");
            CVb365HtmlCompiler compiler = new();
            compiler.Dispose();
        }
        private void StartSecurityReport()
        {
            CHtmlCompiler html = new();
            html.RunSecurityReport();
        }
    }
}
