// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.Html.VB365;
using VeeamHealthCheck.Shared;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    /// <summary>
    /// Stages the synthetic VB365 fixture CSVs in a temp dir, points the import statics at it,
    /// and restores every mutated static on Dispose. Callers must be in the "GlobalState" collection.
    /// </summary>
    public sealed class Vb365ReportHarness : IDisposable
    {
        public static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "TestData", "VB365");

        private readonly bool origImport = CGlobals.IMPORT;
        private readonly string origImportPath = CGlobals.IMPORT_PATH;
        private readonly string origResolved = CVariables.ResolvedImportPath;
        private readonly string origDesired = CGlobals.desiredPath;
        private readonly bool origScrub = CGlobals.Scrub;
        private readonly bool origOpenHtml = CGlobals.OpenHtml;
        private readonly bool origPdf = CGlobals.EXPORTPDF;
        private readonly bool origPptx = CGlobals.EXPORTPPTX;
        private readonly bool origRemote = CGlobals.REMOTEEXEC;

        public string Root { get; }

        public string CsvDir { get; }

        public Vb365ReportHarness(bool scrub = false)
        {
            this.Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "VhcVb365_" + Guid.NewGuid().ToString("N")));
            this.CsvDir = Path.Combine(this.Root, "csv");
            Directory.CreateDirectory(this.CsvDir);

            // The scrubber saves its key file under desiredPath/Original; without it the save throws
            // inside a swallowed catch and tables silently render empty.
            Directory.CreateDirectory(Path.Combine(this.Root, "Original"));

            foreach (string f in Directory.GetFiles(FixtureDir, "*.csv"))
            {
                File.Copy(f, Path.Combine(this.CsvDir, Path.GetFileName(f)));
            }

            CGlobals.desiredPath = this.Root;
            CGlobals.IMPORT = true;
            CGlobals.IMPORT_PATH = this.CsvDir;
            CVariables.ResolvedImportPath = this.CsvDir;
            CGlobals.Scrub = scrub;
            CGlobals.OpenHtml = false;
            CGlobals.EXPORTPDF = false;
            CGlobals.EXPORTPPTX = false;
            CGlobals.REMOTEEXEC = false;
        }

        /// <summary>Rewrites one staged fixture CSV, e.g. to inject an edge case.</summary>
        public void Edit(string csvName, Func<string, string> edit)
        {
            string fileName = Path.GetFileName(csvName);
            if (fileName != csvName)
            {
                throw new ArgumentException($"Expected a bare fixture file name, got '{csvName}'.", nameof(csvName));
            }

            string p = Path.Join(this.CsvDir, fileName);
            File.WriteAllText(p, edit(File.ReadAllText(p)));
        }

        /// <summary>Runs the full VB365 compiler and returns the generated report HTML.</summary>
        public string CompileReport()
        {
            _ = new CVb365HtmlCompiler();
            return this.ReadReport();
        }

        public string ReadReport()
        {
            string dir = Path.Combine(this.Root, CGlobals.Scrub ? "vHC-AnonymousReport" : "vHC-Report");
            string file = Directory.GetFiles(dir, "*VB365*.html").OrderBy(f => f).Last();
            return File.ReadAllText(file);
        }

        public static string StripScriptAndStyle(string html) =>
            Regex.Replace(
                Regex.Replace(html, "<(script|style)\\b.*?</\\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase),
                "data:[^\"')]*",
                string.Empty);

        /// <summary>Returns the markup of the div with the given id, up to the next section div.</summary>
        public static string Section(string html, string id)
        {
            int start = html.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }

            int next = html.IndexOf("<div class=", start + 1, StringComparison.Ordinal);
            return next < 0 ? html[start..] : html[start..next];
        }

        /// <summary>Data rows (rows that contain at least one td) as lists of raw td elements.</summary>
        public static List<List<string>> Rows(string sectionHtml) =>
            Regex.Matches(sectionHtml, "<tr>(.*?)</tr>", RegexOptions.Singleline)
                .Select(m => Regex.Matches(m.Groups[1].Value, "<td\\b.*?</td>", RegexOptions.Singleline).Select(c => c.Value).ToList())
                .Where(cells => cells.Count > 0)
                .ToList();

        public static int HeaderCount(string sectionHtml) => Regex.Matches(sectionHtml, "<th\\b").Count;

        public static string CellText(string td) => Regex.Replace(td, "<[^>]+>", string.Empty);

        public static int RowCount(string sectionHtml) => Rows(sectionHtml).Count;

        public void Dispose()
        {
            CGlobals.IMPORT = this.origImport;
            CGlobals.IMPORT_PATH = this.origImportPath;
            CVariables.ResolvedImportPath = this.origResolved;
            CGlobals.desiredPath = this.origDesired;
            CGlobals.Scrub = this.origScrub;
            CGlobals.OpenHtml = this.origOpenHtml;
            CGlobals.EXPORTPDF = this.origPdf;
            CGlobals.EXPORTPPTX = this.origPptx;
            CGlobals.REMOTEEXEC = this.origRemote;
            try { Directory.Delete(this.Root, recursive: true); } catch { /* best effort */ }
        }
    }
}
