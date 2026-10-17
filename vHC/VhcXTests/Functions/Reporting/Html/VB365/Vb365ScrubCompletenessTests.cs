// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CsvHelper;
using Xunit;
using Xunit.Abstractions;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    /// <summary>
    /// Scrub-completeness sweep for the VB365 report: every identifier-bearing cell in every
    /// fixture CSV must be gone from the scrubbed report. ControllerDrives.csv is the one
    /// exemption: it is hardware inventory (vendor, model, disk friendly name), not tenant data.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class Vb365ScrubCompletenessTests
    {
        private static readonly Regex IdentifierHeader = new(
            @"name|contact|licensed to|user|operators|items$|path|bucket|container|bound|repo|job$|organization|settings|cert$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly ITestOutputHelper output;

        public Vb365ScrubCompletenessTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void CompileReport_ScrubMode_RemovesEveryIdentifierColumnValue()
        {
            string html;
            using (var h = new Vb365ReportHarness(scrub: true))
            {
                html = WebUtility.HtmlDecode(h.CompileReport());
            }

            int columns = 0;
            var leaks = new List<string>();
            foreach (string path in Directory.GetFiles(Vb365ReportHarness.FixtureDir, "*.csv").OrderBy(p => p, StringComparer.Ordinal))
            {
                if (Path.GetFileName(path) == "ControllerDrives.csv")
                {
                    continue;
                }

                using var reader = new StreamReader(path);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                Assert.True(csv.Read());
                csv.ReadHeader();
                string[] headers = csv.HeaderRecord!;
                int[] idColumns = Enumerable.Range(0, headers.Length).Where(i => IdentifierHeader.IsMatch(headers[i])).ToArray();
                columns += idColumns.Length;
                while (csv.Read())
                {
                    foreach (int i in idColumns)
                    {
                        string value = (csv.GetField(i) ?? string.Empty).Trim();
                        foreach (string item in new[] { value }.Concat(value.Split(new[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
                        {
                            if (item.Length >= 4 && !bool.TryParse(item, out _) && !double.TryParse(item, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                                && html.Contains(item, StringComparison.OrdinalIgnoreCase))
                            {
                                leaks.Add($"{Path.GetFileName(path)}:{headers[i]} = {item}");
                            }
                        }
                    }
                }
            }

            this.output.WriteLine($"Checked {columns} identifier columns.");
            Assert.True(leaks.Count == 0, "VB365 identifier leaks:\n" + string.Join("\n", leaks.Distinct()));
        }
    }
}
