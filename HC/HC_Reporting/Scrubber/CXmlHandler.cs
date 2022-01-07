// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace VeeamHealthCheck.Scrubber
{
    class CXmlHandler
    {
        private readonly string _matchListPath = @"C:\temp\vHC\vHC_KeyFile.xml";
        private Dictionary<string,string> _matchDictionary;
        private XDocument _doc;
        public CXmlHandler()
        {
            _matchDictionary = new();
            _doc = new XDocument(new XElement("root"));
        }
        private void AddItemToList(string type, string original, string obfuscated)
        {
            

            //XElement serverRoot = new XElement(type);
            //_doc.Root.Add(serverRoot);

            XElement xml = new XElement("fauxname", obfuscated,
                new XElement("originalname",original));
            //serverRoot.Add(xml);
            _doc.Root.Add(xml);
            _doc.Save(_matchListPath);
        }
        private void AddItemToList(string type, List<string> item)
        {
            XDocument doc = new XDocument(new XElement("root"));

            XElement serverRoot = new XElement(type);
            doc.Root.Add(serverRoot);

            foreach (var i in item)
            {
                XElement xml = new XElement(i);
                serverRoot.Add(xml);

            }

            doc.Save(_matchListPath);
        }
        public string ScrubItem(string item)
        {
            return ScrubItem(item, "");
        }
        public string ScrubItem(string item, string type)
        {
            if (String.IsNullOrEmpty(item))
                return "";
            //item = RemoveLeadingSlashes(item);
            if (!_matchDictionary.ContainsKey(item))
            {
                int counter = _matchDictionary.Count;
                string newName = "Item_" + counter.ToString();
                _matchDictionary.Add(item, newName);
                AddItemToList(type, item, newName);
                return newName;
            }
            else
            {
                _matchDictionary.TryGetValue(item, out string newName);
                return newName;
            }
        }
        private string RemoveLeadingSlashes(string name)
        {
            while (name.StartsWith("\\"))
            {
                name = name.TrimStart('\\');
            }
            return name;
        }
    }
}
