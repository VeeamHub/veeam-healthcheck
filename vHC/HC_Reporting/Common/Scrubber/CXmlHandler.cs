// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VeeamHealthCheck.Scrubber
{
    public class CScrubHandler
    {
        // Matches RFC1918 private, loopback, and link-local IPv4 only. Deliberately
        // NOT public IPv4: a 4-octet public-range pattern also matches product build
        // versions (e.g. 13.0.2.29), so a blanket pass would corrupt the report.
        // Public/registered addresses are caught by the registered-value pass instead.
        private static readonly Regex PrivateIPv4Regex = BuildPrivateIPv4Regex();

        private static Regex BuildPrivateIPv4Regex()
        {
            const string o = @"(?:25[0-5]|2[0-4]\d|1?\d?\d)";
            string pattern =
                @"\b(?:" +
                $@"10\.{o}\.{o}\.{o}" + "|" +
                $@"172\.(?:1[6-9]|2\d|3[01])\.{o}\.{o}" + "|" +
                $@"192\.168\.{o}\.{o}" + "|" +
                $@"169\.254\.{o}\.{o}" + "|" +
                $@"127\.{o}\.{o}\.{o}" +
                @")\b";
            return new Regex(pattern, RegexOptions.Compiled);
        }

        private string matchListPath => CVariables.unsafeDir + @"\vHC_KeyFile.xml";
        private Dictionary<string, string> matchDictionary;
        private readonly Dictionary<string, int> typeCounters = new();
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
            RestrictFileToOwner(this.matchListPath);

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
                RestrictFileToOwner(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while writing to the JSON file: {ex.Message}");
            }
        }

        /// <summary>
        /// Tightens NTFS permissions on the de-anonymization key file so it is no
        /// longer world-readable in the shared temp tree. Inheritance is broken and
        /// access is restricted to the current user, Administrators, and SYSTEM.
        /// The file stays human-readable to the operator who generated it (its
        /// purpose), but other local users can no longer read the obfuscated->real
        /// mapping. Best-effort: never fails report generation.
        /// NOTE: the sibling Original\ directory still holds the raw (unscrubbed)
        /// CSVs and should be secured/deleted after the report is shared.
        /// </summary>
        internal static void RestrictFileToOwner(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    return;
                }

                var security = new FileSecurity();

                // Break inheritance and drop any inherited (potentially broad) ACEs.
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                var currentUser = WindowsIdentity.GetCurrent().User;
                if (currentUser != null)
                {
                    security.SetOwner(currentUser);
                    security.AddAccessRule(new FileSystemAccessRule(
                        currentUser, FileSystemRights.FullControl, AccessControlType.Allow));
                }

                var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));

                fileInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                // Best-effort hardening only; do not abort report generation.
                Console.WriteLine($"[Scrub] Failed to restrict key-file ACLs on '{path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Final safety pass over a fully-assembled scrubbed report. Catches values
        /// that the opt-in per-field scrub missed: (1) any registered original value
        /// appearing verbatim (e.g. a hostname embedded in a path or log line), and
        /// (2) raw private/loopback/link-local IPv4 addresses. Runs ONLY on the
        /// scrubbed output path; the unscrubbed report is never touched.
        /// </summary>
        public string FinalizeScrubbedText(string text)
        {
            text = ReplaceRegisteredValuesInHtml(text, this.matchDictionary);
            text = ScrubRawPrivateIPv4(text);
            return text;
        }

        // Splits an HTML document into the parts the registered-value pass must leave alone
        // (<style> and <script> blocks) and the tags whose attribute values it may rewrite.
        // Everything that matches neither is text content.
        //
        // Cell data is written into the report unencoded, so a value like "Name <jsmith@corp.local>"
        // puts a literal '<' in the file. Only real markup may be treated as a tag, or a registered
        // value inside such text would be skipped and leak: a tag must be a known HTML element, with
        // attributes that are name=value pairs (quoted or bare) or one of a few boolean attributes.
        // Anything else - "<jsmith@corp.local>", "<y then host01", "<a few files from host01>" -
        // is text. A generated tag that does not fit also degrades to text, which is the old
        // whole-string behaviour for that one tag, so it fails safe.
        private const string TagNames =
            "html|head|body|title|meta|link|style|script|div|span|p|br|hr|a|b|i|u|em|strong|small|sub|sup|code|pre"
            + "|h[1-6]|ul|ol|li|dl|dt|dd|table|thead|tbody|tfoot|tr|td|th|caption|colgroup|col|button|input|label"
            + "|select|option|textarea|form|img|svg|path|g|circle|rect|line|polyline|text|details|summary|section"
            + "|header|footer|nav|main|article|aside|center|font|canvas";

        private const string BooleanAttributes =
            "disabled|checked|selected|hidden|open|readonly|required|multiple|async|defer|autofocus|download";

        private const string TagAttribute =
            @"(?:[^\s""'<>=/]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'<>=`]+)|(?:" + BooleanAttributes + @")(?![^\s/>]))";

        private static readonly Regex HtmlSegmentRegex = new Regex(
            @"(?<skip><(?<block>style|script)\b[^>]*>.*?</\k<block>\s*>)" +
            @"|(?<tag><!DOCTYPE\s+html\s*>|</?(?:" + TagNames + @")(?:\s+" + TagAttribute + @")*\s*/?>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex QuotedAttributeRegex = new Regex(
            @"(?<name>[^\s=/>""']+)(?<eq>\s*=\s*)(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)')",
            RegexOptions.Compiled);

        /// <summary>
        /// Replaces each registered original value with its obfuscated token wherever
        /// it appears as a whole word. Longest originals first so a value that is a
        /// substring of another is handled first. Values shorter than 4 chars are
        /// skipped to avoid colliding with the report's own HTML/CSS tokens.
        /// Plain-text replacement: it does not know about markup. Use
        /// <see cref="ReplaceRegisteredValuesInHtml"/> for a whole HTML document.
        /// </summary>
        internal static string ReplaceRegisteredValues(string text, IReadOnlyDictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            Regex regex = BuildRegisteredValueRegex(map);
            return regex == null ? text : ReplaceWith(regex, text, map);
        }

        /// <summary>
        /// The final-pass leak net for a whole HTML document. Replaces registered values in
        /// text content and in attribute values only. <c>&lt;style&gt;</c> and
        /// <c>&lt;script&gt;</c> content, tag names, attribute names and the attributes that
        /// carry CSS/JS identifiers (class, id, style, for, on*) are left alone: a registered
        /// value that is also a markup word (a user named "root" against <c>:root</c>, "body"
        /// against <c>&lt;body&gt;</c>) would otherwise corrupt the report's own theme and
        /// structure (#263).
        /// </summary>
        internal static string ReplaceRegisteredValuesInHtml(string html, IReadOnlyDictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(html))
            {
                return html;
            }

            Regex regex = BuildRegisteredValueRegex(map);
            if (regex == null)
            {
                return html;
            }

            var sb = new System.Text.StringBuilder(html.Length);
            int pos = 0;
            foreach (Match m in HtmlSegmentRegex.Matches(html))
            {
                sb.Append(ReplaceWith(regex, html.Substring(pos, m.Index - pos), map));
                if (m.Groups["skip"].Success)
                {
                    sb.Append(m.Value);
                }
                else
                {
                    sb.Append(QuotedAttributeRegex.Replace(m.Value, a => ReplaceAttributeValue(a, regex, map)));
                }

                pos = m.Index + m.Length;
            }

            sb.Append(ReplaceWith(regex, html.Substring(pos), map));
            return sb.ToString();
        }

        private static string ReplaceAttributeValue(Match attr, Regex regex, IReadOnlyDictionary<string, string> map)
        {
            string name = attr.Groups["name"].Value;
            bool isDouble = attr.Groups["dq"].Success;
            string value = isDouble ? attr.Groups["dq"].Value : attr.Groups["sq"].Value;

            // CSS/JS identifiers, in-page anchors and embedded data URIs are not report data.
            if (name.Equals("class", StringComparison.OrdinalIgnoreCase)
                || name.Equals("id", StringComparison.OrdinalIgnoreCase)
                || name.Equals("style", StringComparison.OrdinalIgnoreCase)
                || name.Equals("for", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("#", StringComparison.Ordinal)
                || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }

            string quote = isDouble ? "\"" : "'";
            return name + attr.Groups["eq"].Value + quote + ReplaceWith(regex, value, map) + quote;
        }

        // One alternation instead of one Regex.Replace per registered value: the document is
        // now processed per segment, and a pattern per value would thrash the regex cache.
        // Alternatives are ordered longest first, so the leftmost match is the longest one.
        private static Regex BuildRegisteredValueRegex(IReadOnlyDictionary<string, string> map)
        {
            if (map == null || map.Count == 0)
            {
                return null;
            }

            var alternatives = map.Keys
                .Where(k => !string.IsNullOrEmpty(k) && k.Length >= 4)
                .OrderByDescending(k => k.Length)
                .Select(k => @"\b" + Regex.Escape(k) + @"\b")
                .ToList();

            return alternatives.Count == 0 ? null : new Regex("(?:" + string.Join("|", alternatives) + ")");
        }

        private static string ReplaceWith(Regex regex, string text, IReadOnlyDictionary<string, string> map)
        {
            if (text.Length == 0)
            {
                return text;
            }

            return regex.Replace(text, m => map.TryGetValue(m.Value, out string token) ? token ?? string.Empty : m.Value);
        }

        /// <summary>
        /// Replaces raw RFC1918 private, loopback (127/8), and link-local (169.254/16)
        /// IPv4 addresses with a placeholder. Public IPv4 is intentionally excluded to
        /// avoid corrupting product build versions that share the dotted-quad shape.
        /// </summary>
        internal static string ScrubRawPrivateIPv4(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return PrivateIPv4Regex.Replace(text, "PRIVATE_IP");
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
                if (!this.typeCounters.ContainsKey(type))
                    this.typeCounters[type] = 0;
                int counter = this.typeCounters[type]++;
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
