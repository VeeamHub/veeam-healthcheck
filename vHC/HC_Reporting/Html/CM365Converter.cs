using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VeeamHealthCheck.CsvHandlers;

namespace VeeamHealthCheck.Html
{
    internal class CM365Converter
    {
        /*  todo:
         *  1. read csv
         *  2. convert to xml
         *  3. export to HTML
         */
        private CCsvParser csv = new CCsvParser();
        private CXmlFunctions XML = new("m365");
        private string _xmlFile = "xml\\m365.xml";
        private CHtmlExporter _exporter;
        private string _ServerName = "M365-Server";
        private string _styleSheet = "StyleSheets\\m365-Report.xsl";

        public CM365Converter()
        {
            XML.HeaderInfoToXml();
            Run();
            _exporter = new(_xmlFile, _ServerName, _styleSheet);
            _exporter.ExportHtml();
            _exporter.OpenHtml();
        }
        private void Run()
        {
            m365Global();
        }
        private void m365Global()
        {
            var global = csv.GetDynamicVboGlobal();

            XDocument doc = XDocument.Load(_xmlFile);
            XElement section = new XElement("Global");
            doc.Root.Add(section);

            var infos = global.ToList();
            foreach (var info in infos)
            {
                // License Status,"License Expiry","License Type",
                // "Licensed To","License Contact","License Usage","Licensed Users",
                // "Support Expiry","Global Folder Exclusions","Global Ret. Exclusions",
                // "Log Retention","Notification Enabled","Notifify On"

                section.Add(XML.AddXelement(info.licensestatus, "Lic Status"));
                section.Add(XML.AddXelement(info.licenseexpiry, "Lic Expire"));
                section.Add(XML.AddXelement(info.licensetype, "Lic Type"));
                section.Add(XML.AddXelement(info.licensedto, "Lic To"));
                section.Add(XML.AddXelement(info.licensecontact, "Lic Contact"));
                section.Add(XML.AddXelement(info.licenseusage, "Lic Usage"));
                section.Add(XML.AddXelement(info.licensedusers, "Lic Users"));
                section.Add(XML.AddXelement(info.supportexpiry, "Sup Expire"));
                section.Add(XML.AddXelement(info.globalfolderexclusions, "Global Folder Excl."));
                section.Add(XML.AddXelement(info.globalretexclusions, "Global Ret Excl"));
                section.Add(XML.AddXelement(info.logretention, "Log Retention"));
                section.Add(XML.AddXelement(info.notificationenabled, "Notifications Enabled"));
                section.Add(XML.AddXelement(info.notififyon, "Notify On", "When will the notification be sent"));

                
            }
            doc.Save(_xmlFile);

        }
    }
}
