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
                StartVbrReportImport();
        }
        private void StartVbrReport()
        {

        }
        private void StartVbrReportImport()
        {
            CCsvToXml c = new("vbr", _scrub, false, _openHtml, true);

            //choose VBO or VBR
            c.ConvertToXml();
        }
        private void StartM365Report()
        {
            //CCsvToXml m = new CCsvToXml("m365", _scrub, false, _openHtml, true);
            CM365Converter converter = new CM365Converter();
        }
    }
}
