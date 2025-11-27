// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace VeeamHealthCheck.Scrubber
{
    public class CScrubHandler
    {
        private readonly string matchListPath = CVariables.unsafeDir + @"\vHC_KeyFile.xml";
        private Dictionary<string, string> matchDictionary;
        private readonly XDocument doc;

        public CScrubHandler()
        {
            this.matchDictionary = new();
            this.doc = new XDocument(new XElement("root"));
        }

        private void AddItemToList(string type, string original, string obfuscated)
        {
            XElement xml = new XElement("fauxname", obfuscated,
                new XElement("originalname", original));
            this.doc.Root.Add(xml);
            this.doc.Save(this.matchListPath);

            this.WriteToText();
        }

        private void WriteToText()
        {
            // sort _matchDictionary alphabetically
            this.matchDictionary = new Dictionary<string, string>(this.matchDictionary);

            var newDict = this.matchDictionary.OrderBy(kvp => kvp.Value)
                  .ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            this.WriteDictionaryToJsonFile(newDict, CVariables.unsafeDir + @"\vHC_KeyFile.json");
        }

        private void WriteDictionaryToJsonFile(Dictionary<string, string> dictionary, string filePath)
        {
            try
            {
                // Serialize the dictionary to a JSON string
                string jsonString = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = true });

                // Write the JSON string to a file
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while writing to the JSON file: {ex.Message}");
            }
        }

        public string ScrubItem(string item, string type, ScrubItemType itemType)
        {
            if (String.IsNullOrEmpty(item))
            {
                return string.Empty;
            }


            if (item.StartsWith(type + "_"))
            {
                return item;
            }

            // item = RemoveLeadingSlashes(item);

            switch (itemType)
            {
                case ScrubItemType.Item:
                    break;

                // case for all ScrubItemType objects:
                case ScrubItemType.Job:
                    break;
                case ScrubItemType.MediaPool:
                    break;
                case ScrubItemType.Repository:
                    break;
                case ScrubItemType.Server:
                    break;
                case ScrubItemType.Path:
                    break;
                case ScrubItemType.VM:
                    break;
                case ScrubItemType.SOBR:
                    break;
            }

            if (!this.matchDictionary.ContainsKey(item))
            {
                int counter = this.matchDictionary.Count;
                string newName = type + "_" + counter.ToString();
                this.matchDictionary.Add(item, newName);
                this.AddItemToList(type, item, newName);
                return newName;
            }
            else
            {
                this.matchDictionary.TryGetValue(item, out string newName);
                return newName;
            }
        }

        public string ScrubItem(string item, ScrubItemType type)
        {
            return this.ScrubItem(item, type.ToString(), type);
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
