// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace VeeamHealthCheck.Scrubber
{
    public class CScrubHandler
    {
        private readonly string _matchListPath = CVariables.unsafeDir + @"\vHC_KeyFile.xml";
        private Dictionary<string, string> _matchDictionary;
        private XDocument _doc;

        public CScrubHandler()
        {
            _matchDictionary = new();
            _doc = new XDocument(new XElement("root"));
        }
        private void AddItemToList(string type, string original, string obfuscated)
        {

            XElement xml = new XElement("fauxname", obfuscated,
                new XElement("originalname", original));
            _doc.Root.Add(xml);
            _doc.Save(_matchListPath);
        }

        public string ScrubItem(string item, string type)
        {
            if (String.IsNullOrEmpty(item))
                return "";
            if (item.StartsWith(type + "_"))
                return item;
            //item = RemoveLeadingSlashes(item);
            if (!_matchDictionary.ContainsKey(item))
            {
                int counter = _matchDictionary.Count;
                string newName = type + "_" + counter.ToString();
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
        public string ScrubItem(string item, ScrubItemType type)
        {
            return ScrubItem(item, type.ToString());
        }


    }
    public enum ScrubItemType
    {
        Job = 0,
        MediaPool = 1,
        Repository = 2,
        Server = 3,
        Path = 4,
        VM = 5,
        SOBR = 6,


        Item = 99
    }
}
