using System.IO;
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
            if (Directory.Exists(CVariables.vb365dir))
                StartM365Report();
            if (Directory.Exists(CVariables.vbrDir))
                StartVbrReport();
        }
        private void StartVbrReport()
        {
            _log.Info("Starting B&R report generation");
            CHtmlCompiler html = new();
            html.Dispose();
        }
        
        private void StartM365Report()
        {
            _log.Info("Starting VB365 Report genration");
            CVb365HtmlCompiler compiler = new();
            compiler.Dispose();
        }
    }
}
