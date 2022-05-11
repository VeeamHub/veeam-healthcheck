using System.IO;
using VeeamHealthCheck.Html;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    internal class CReportModeSelector
    {
        private readonly string _path;
        private readonly bool _scrub;
        private readonly bool _openHtml;
        private readonly bool _import;
        private CLogger _log = MainWindow.log;
        public CReportModeSelector(string path, bool scrub, bool openHtml, bool import)
        {
            _path = path;
            _scrub = scrub;
            _openHtml = openHtml;
            _import = import;
        }
        public void Run()
        {
            FileChecker();
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
            CHtmlCompiler html = new("vbr");
        }
        private void StartVbrReportImport()
        {
            _log.Info("Running report in import mode");
            CCsvToXml c = new("vbr", _scrub, false, _openHtml, true);

            //choose VBO or VBR
            //c.ConvertToXml();
        }
        private void StartM365Report()
        {
            _log.Info("Starting VB365 Report genration");
            //CCsvToXml m = new CCsvToXml("m365", _scrub, false, _openHtml, true);
            CM365Converter converter = new CM365Converter(_scrub);
        }
    }
}
