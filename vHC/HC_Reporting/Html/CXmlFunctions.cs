using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Html
{
    internal class CXmlFunctions
    {
        private CLogger log = MainWindow.log;
        private string _xmlOut;
        private XDocument _doc;
        public CXmlFunctions(string mode)
        {
            CheckXmlFolder();

            switch (mode)
            {
                case "vbr":
                    _xmlOut = "xml\\vbr.xml";
                    break;
                case "m365":
                    _xmlOut = "xml\\m365.xml";
                    break;
                default:
                    throw new ArgumentException("No mode selected.");
            }
        }
        public XDocument Doc()
        {
            _doc = XDocument.Load(_xmlOut);
            //XElement extElement = new XElement(sectionName);
            //_doc.Root.Add(extElement);
            return _doc;
        }
        public void SaveDoc()
        {
            _doc.Save(_xmlOut);
        }
        private void CheckXmlFolder()
        {
            if (!Directory.Exists("xml"))
                Directory.CreateDirectory("xml");
        }
        public XElement AddXelement(string data, string headerName, string tooltip)
        {
            return AddXelement(data, headerName, tooltip, "");
        }
        public XElement AddXelement(string data, string headerName)
        {
            return AddXelement(data, headerName, "", "");
        }
        public XElement AddXelement(string data, string headerName, string tooltip, string provisioning)
        {

            var xml = new XElement("td", data,
                new XAttribute("headerName", headerName),
                new XAttribute("tooltip", tooltip),
                new XAttribute("color", provisioning)
                );

            return xml;
        }
        public XElement AddSummaryHeader(string summaryOrNotes)
        {
            var xml = new XElement("summaryheader", summaryOrNotes);

            return xml;
        }
        public XElement AddSummaryText(string summaryOrNotes, string indent)
        {
            var xml = new XElement("text", summaryOrNotes,
                new XAttribute("indent", indent));

            return xml;
        }
        //public XElement AddIndent1()
        //{

        //}
        //public XElement AddIndent2()
        //{

        //}
        //public XElement AddIndent3()
        //{
            
        //}
        //public XElement AddIndent4()
        //{

        //}
        private string GetLicNameForHeader()
        {
            var parser = new CCsvParser();

            //check for VBR key
            try
            {
                var rec = parser.GetDynamicLicenseCsv();
                foreach (var r in rec)
                {
                    return r.licensedto;
                }

            }
            catch (Exception ex) { }
            //Check VBO Key
            try
            {
                var m365 = parser.GetDynamicVboGlobal().ToList();
                foreach(var m in m365)
                {
                    return m.licensedto;
                }
                
            }
            catch(Exception ex) { } 
            

            return "";
        }
        public void HeaderInfoToXml()
        {
            log.Info("converting header info to xml");
            

            string cxName = GetLicNameForHeader();
            

            XDocument doc = new XDocument(new XElement("root"));

            XElement serverRoot = new XElement("header");
            doc.Root.Add(serverRoot);
            doc.AddFirst(new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"SessionReport.xsl\""));
            string summary = "This report provides data and insight into your Veeam Backup and Replication (VBR) deployment. The information provided here is intended to be used in collaboration with your Veeam representative.";

            var xml = new XElement("h1",
                new XElement("name", cxName),
                new XElement("hc", "Health Check Report"),
                new XElement("summary", summary)
                );

            serverRoot.Add(xml);



            doc.Save(_xmlOut);
            log.Info("converting header info to xml");
        }
    }

}
