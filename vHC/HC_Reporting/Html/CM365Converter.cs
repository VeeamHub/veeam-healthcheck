using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Scrubber;

namespace VeeamHealthCheck.Html
{
    internal class CM365Converter
    {
        /*  todo:
         *  1. read csv
         *  2. convert to xml
         *  3. export to HTML
         */
        private CCsvParser csv = new CCsvParser(CVariables.vb365dir);
        private CXmlFunctions XML = new("m365");
        private string _xmlFile = "xml\\m365.xml";
        private CHtmlExporter _exporter;
        private string _ServerName = "M365-Server";
        private string _styleSheet = "StyleSheets\\m365-Report.xsl";
        private XDocument _doc;
        private bool _scrub;
        private CXmlHandler _scrubber;

        public CM365Converter(bool scrub)
        {
            _scrub = scrub;
            if (scrub)
                _scrubber = new();
            XML.HeaderInfoToXml();
            Run();
            _exporter = new(_xmlFile, _ServerName, _styleSheet, scrub);
            _exporter.ExportHtml();
            _exporter.OpenHtml();
        }
        private void Run()
        {
            LoadDoc();

            m365Global();
            m365Proxies();
            m365Repos();
            m365RbacRoles();
            m365Security();


            SaveDoc();
        }
        private void m365Global()
        {
            var global = csv.GetDynamicVboGlobal();

            XElement section = Element("section");
            section.Add(new XAttribute("title", "Global Info"));
            _doc.Root.Add(section);

            var infos = global.ToList();
            foreach (var info in infos)
            {
                // License Status,"License Expiry","License Type",
                // "Licensed To","License Contact","License Usage","Licensed Users",
                // "Support Expiry","Global Folder Exclusions","Global Ret. Exclusions",
                // "Log Retention","Notification Enabled","Notifify On"
                XElement node = Element("node");
                section.Add(node);

                node.Add(XML.AddXelement(info.licensestatus, "Lic Status"));
                node.Add(XML.AddXelement(info.licenseexpiry, "Lic Expire"));
                node.Add(XML.AddXelement(info.licensetype, "Lic Type"));
                node.Add(XML.AddXelement(info.licensedto, "Lic To"));
                node.Add(XML.AddXelement(info.licensecontact, "Lic Contact"));
                node.Add(XML.AddXelement(info.licenseusage, "Lic Usage"));
                node.Add(XML.AddXelement(info.licensedusers, "Lic Users"));
                node.Add(XML.AddXelement(info.supportexpiry, "Sup Expire"));
                node.Add(XML.AddXelement(info.globalfolderexclusions, "Global Folder Excl."));
                node.Add(XML.AddXelement(info.globalretexclusions, "Global Ret Excl"));
                node.Add(XML.AddXelement(info.logretention, "Log Retention"));
                node.Add(XML.AddXelement(info.notificationenabled, "Notifications Enabled"));
                node.Add(XML.AddXelement(info.notififyon, "Notify On", "When will the notification be sent"));

                
            }
            XElement summary = Element("summary");
            section.Add(summary);

            summary.Add(XML.AddSummaryText("Summary", "hdr"));
            summary.Add(XML.AddSummaryText("This is where the summary will be displayed", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
            summary.Add(XML.AddSummaryText("Notes", "hdr"));
            summary.Add(XML.AddSummaryText("General notes about the section will go here", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));


        }
        
        private void m365Proxies()
        {
            var data = csv.GetDynamicVboProxies();
            XElement section = Element("section");
            section.Add(new XAttribute("title", "Proxy Info"));
            _doc.Root.Add(section);


            var infos = data.ToList();
            foreach(var info in infos)
            {
                XElement subSection = Element("node");
                section.Add(subSection);
                subSection.Add(XML.AddXelement(info.name, "Proxy Name"));
                subSection.Add(XML.AddXelement(info.description, "Description"));
                subSection.Add(XML.AddXelement(info.threads, "Threads"));
                subSection.Add(XML.AddXelement(info.throttling, "Throttling"));
                subSection.Add(XML.AddXelement(info.state, "State"));
                subSection.Add(XML.AddXelement(info.type, "Type"));
                subSection.Add(XML.AddXelement(info.outdated, "Outdated", "Is proxy components outdated?"));
                subSection.Add(XML.AddXelement(info.internetproxy, "Internet Proxy"));
            }
            XElement summary = Element("summary");
            section.Add(summary);

            summary.Add(XML.AddSummaryText("Summary", "hdr"));
            summary.Add(XML.AddSummaryText("This is where the summary will be displayed", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
            summary.Add(XML.AddSummaryText("Notes", "hdr"));
            summary.Add(XML.AddSummaryText("General notes about the section will go here", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
        }


        private void m365Repos()
        {
            var data = csv.GetDynamicVboRepo();
            XElement section = Element("section");
            section.Add(new XAttribute("title", "Repo Info"));
            _doc.Root.Add(section);

            var infos = data.ToList();
            foreach (var info in infos)
            {
                XElement node = Element("node");
                section.Add(node);

                node.Add(XML.AddXelement(info.boundproxy, "Bound Proxy"));
                node.Add(XML.AddXelement(info.name, "Name"));
                node.Add(XML.AddXelement(info.description, "Description"));
                node.Add(XML.AddXelement(info.type, "Type"));
                node.Add(XML.AddXelement(info.path, "Path"));
                node.Add(XML.AddXelement(info.objectrepo, "Object Repo"));
                node.Add(XML.AddXelement(info.encryption, "Is encrypted?"));
                node.Add(XML.AddXelement(info.outofsync, "Is out of sync?"));
                node.Add(XML.AddXelement(info.outdated, "Is outdated?"));
                node.Add(XML.AddXelement(info.capacity, "Capacity"));
                node.Add(XML.AddXelement(info.free, "Free Space"));
                node.Add(XML.AddXelement(info.retention, "Retention"));
            }
            XElement summary = Element("summary");
            section.Add(summary);

            summary.Add(XML.AddSummaryText("Summary", "hdr"));
            summary.Add(XML.AddSummaryText("This is where the summary will be displayed", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
            summary.Add(XML.AddSummaryText("Notes", "hdr"));
            summary.Add(XML.AddSummaryText("General notes about the section will go here", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
        }
        private void m365RbacRoles()
        {
            var data = csv.GetDynamicVboRbac();
            XElement section = Element("section");
            section.Add(new XAttribute("title", "RBAC Roles"));
            _doc.Root.Add(section);

            var infos = data.ToList();
            foreach (var info in infos)
            {
                XElement node = Element("node");
                section.Add(node);

                node.Add(XML.AddXelement(info.name, "Name"));
                node.Add(XML.AddXelement(info.description, "Description"));
                node.Add(XML.AddXelement(info.roletype, "Role Type"));
                node.Add(XML.AddXelement(info.operators, "Operators"));
                node.Add(XML.AddXelement(info.selecteditems, "Selected Items"));
                node.Add(XML.AddXelement(info.excludeditems, "Excluded Items"));
            }
            XElement summary = Element("summary");
            section.Add(summary);

            summary.Add(XML.AddSummaryText("Summary", "hdr"));
            summary.Add(XML.AddSummaryText("This is where the summary will be displayed", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
            summary.Add(XML.AddSummaryText("Notes", "hdr"));
            summary.Add(XML.AddSummaryText("General notes about the section will go here", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
        }
        private void m365Security()
        {
            var data = csv.GetDynamicVboSec();
            XElement section = Element("section");
            section.Add(new XAttribute("title", "Security Info"));
            _doc.Root.Add(section);

            var infos = data.ToList();
            foreach (var info in infos)
            {
                XElement node = Element("node");
                section.Add(node);

                node.Add(XML.AddXelement(info.winfirewallenabled, "Win Firewall Enabled"));
                node.Add(XML.AddXelement(info.internetproxy, "Internet Proxy"));
                //node.Add(XML.AddXelement(info.sensservercert, ""));
                node.Add(XML.AddXelement(info.servercertpkexportable, "Server Cert PK Exportable"));
                node.Add(XML.AddXelement(info.servercertexpires, "Server Cert Expires"));
                node.Add(XML.AddXelement(info.servercertselfsigned, "Server Cert Self-Signed"));
                node.Add(XML.AddXelement(info.apienabled, "API Enabled"));
                node.Add(XML.AddXelement(info.apiport, "API Port"));
                //node.Add(XML.AddXelement(info.senseapicert, ""));
                node.Add(XML.AddXelement(info.apicertpkexportable, "API Cert PK Exportable"));
                node.Add(XML.AddXelement(info.apicertexpires, "API Cert Expires"));
                node.Add(XML.AddXelement(info.apicertselfsigned, "API Cert Self-Signed"));
                node.Add(XML.AddXelement(info.tenantauthenabled, "Tenant Auth Enabled?"));
                //node.Add(XML.AddXelement(info.senstenantauthcert, ""));
                node.Add(XML.AddXelement(info.tenantauthpkexportable, "T. Auth PK Exportable"));
                node.Add(XML.AddXelement(info.tenantauthcertexpires, "T. Auth Cert Expires?"));
                node.Add(XML.AddXelement(info.tenantauthcertselfsigned, "T. Auth Cert Self-Signed"));
                node.Add(XML.AddXelement(info.restoreportalenabled, "Restore Portal Enabled?"));
                //node.Add(XML.AddXelement(info.sensrestoreportalcert, ""));
                node.Add(XML.AddXelement(info.restoreportalcertpkexportable, "RP Cert PK Exportable"));
                node.Add(XML.AddXelement(info.restoreportalcertexpires, "RP Cert Expires"));
                node.Add(XML.AddXelement(info.restoreportalcertselfsigned, "RP Cert Self-Signed"));
                node.Add(XML.AddXelement(info.operatorauthenabled, "Operator Auth Enabled"));
                //node.Add(XML.AddXelement(info.sensoperatorauthcert, "Operator Auth Cert"));
                node.Add(XML.AddXelement(info.operatorauthcertpkexportable, "Op. Auth Cert PK Exportable"));
                node.Add(XML.AddXelement(info.operatorauthcertexpires, "Op. Auth Cert Expires"));
                node.Add(XML.AddXelement(info.operatorauthcertselfsigned, "Op. Auth Cert Self-Signed"));
            }
            XElement summary = Element("summary");
            section.Add(summary);

            summary.Add(XML.AddSummaryText("Summary", "hdr"));
            summary.Add(XML.AddSummaryText("This is where the summary will be displayed", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
            summary.Add(XML.AddSummaryText("Notes", "hdr"));
            summary.Add(XML.AddSummaryText("General notes about the section will go here", ""));
            summary.Add(XML.AddSummaryText("This is 1 indent", "i2"));
            summary.Add(XML.AddSummaryText("This is 2 indent", "i3"));
            summary.Add(XML.AddSummaryText("This is 3 indent", "i4"));
        }
        private void SaveDoc()
        {
            _doc.Save(_xmlFile);
        }
        private void LoadDoc()
        {
            _doc = XDocument.Load(_xmlFile);
        }
        private XElement Element(string type)
        {
            XElement e = new XElement(type);
            return e;
        }
    }
}
