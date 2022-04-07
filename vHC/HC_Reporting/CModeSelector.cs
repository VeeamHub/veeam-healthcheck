using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Html;

namespace VeeamHealthCheck
{
    internal class CModeSelector
    {
        private readonly string _path;
        private readonly bool _scrub;
        private readonly bool _openHtml;
        public CModeSelector(string path, bool scrub, bool openHtml)
        {
            _path = path;
            _scrub = scrub;
            _openHtml = openHtml;
        }
        public void Run()
        {
            FileChecker();
        }
        private void FileChecker()
        {
            string[] fileList = Directory.GetFiles(_path + "\\Raw_Data");
            if (fileList.Any(x => Path.GetFileName(x).StartsWith("m365")))
                StartM365Report();
            if(fileList.Any(y => Path.GetFileName(y).StartsWith("vbr")))
                StartVbrReportImport(_scrub, _openHtml);
        }
        private void StartVbrReport()
        {

        }
        private void StartVbrReportImport(bool scrub, bool openHtml)
        {
            CCsvToXml c = new();

            //choose VBO or VBR
            c.ConvertToXml(scrub, false, openHtml, true);
        }
        private void StartM365Report()
        {
            CCsvToXml m = new CCsvToXml();
        }
    }
}
