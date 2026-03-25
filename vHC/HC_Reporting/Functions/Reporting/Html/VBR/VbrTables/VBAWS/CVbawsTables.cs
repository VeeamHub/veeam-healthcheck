// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.VBAWS
{
    internal class CVbawsTables
    {
        private readonly CHtmlFormatting form = new();

        public CVbawsTables() { }

        public string RenderAll(bool scrub)
        {
            string s = string.Empty;

            // Only render if VBAWS data exists
            string vbawsBaseDir = CVariables.GetVbawsBaseDir();
            if (!Directory.Exists(vbawsBaseDir) && !CGlobals.IsVbaws)
            {
                return s;
            }

            s += this.form.SectionStartWithButton("vbaws", "Veeam Backup for AWS", "Veeam Backup for AWS");

            s += this.RenderPoliciesTable(scrub);
            s += this.RenderSessionsTable(scrub);
            s += this.RenderRestorePointsTable(scrub);
            s += this.RenderRepositoriesTable(scrub);

            s += this.form.SectionEnd();

            return s;
        }

        private string RenderPoliciesTable(bool scrub)
        {
            string s = "<h3>Backup Policies</h3>";

            s += this.form.TableHeaderLeftAligned("Appliance", string.Empty);
            s += this.form.TableHeader("Policy Name", string.Empty);
            s += this.form.TableHeader("Status", string.Empty);
            s += this.form.TableHeader("Region", string.Empty);
            s += this.form.TableHeader("Schedule", string.Empty);
            s += this.form.TableHeader("Retention", string.Empty);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                var data = this.ReadVbawsCsv("_VbawsPolicies.csv");

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No VBAWS policies detected.</em></td></tr>";
                }
                else
                {
                    foreach (var row in data)
                    {
                        s += "<tr>";

                        string appliance = GetField(row, "ApplianceId");
                        string name = GetField(row, "name");
                        if (scrub)
                        {
                            appliance = CGlobals.Scrubber.ScrubItem(appliance, ScrubItemType.Server);
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Job);
                        }

                        s += this.form.TableDataLeftAligned(appliance, string.Empty);
                        s += this.form.TableData(name, string.Empty);
                        s += this.form.TableData(GetField(row, "status"), string.Empty);
                        s += this.form.TableData(GetField(row, "regionId"), string.Empty);
                        s += this.form.TableData(GetField(row, "schedulePolicy"), string.Empty);
                        s += this.form.TableData(GetField(row, "retentionPolicy"), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render VBAWS Policies table: " + e.Message);
            }

            s += "</tbody></table>";
            return s;
        }

        private string RenderSessionsTable(bool scrub)
        {
            string s = "<h3>Sessions</h3>";

            s += this.form.TableHeaderLeftAligned("Appliance", string.Empty);
            s += this.form.TableHeader("Policy Name", string.Empty);
            s += this.form.TableHeader("Status", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Start Time", string.Empty);
            s += this.form.TableHeader("End Time", string.Empty);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                var data = this.ReadVbawsCsv("_VbawsSessions.csv");

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='6' style='text-align: center; padding: 20px; color: #666;'><em>No VBAWS sessions detected.</em></td></tr>";
                }
                else
                {
                    foreach (var row in data)
                    {
                        s += "<tr>";

                        string appliance = GetField(row, "ApplianceId");
                        string policyName = GetField(row, "policyName");
                        if (scrub)
                        {
                            appliance = CGlobals.Scrubber.ScrubItem(appliance, ScrubItemType.Server);
                            policyName = CGlobals.Scrubber.ScrubItem(policyName, ScrubItemType.Job);
                        }

                        s += this.form.TableDataLeftAligned(appliance, string.Empty);
                        s += this.form.TableData(policyName, string.Empty);
                        s += this.form.TableData(GetField(row, "status"), string.Empty);
                        s += this.form.TableData(GetField(row, "type"), string.Empty);
                        s += this.form.TableData(GetField(row, "startTime"), string.Empty);
                        s += this.form.TableData(GetField(row, "endTime"), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render VBAWS Sessions table: " + e.Message);
            }

            s += "</tbody></table>";
            return s;
        }

        private string RenderRestorePointsTable(bool scrub)
        {
            string s = "<h3>Restore Points</h3>";

            s += this.form.TableHeaderLeftAligned("Appliance", string.Empty);
            s += this.form.TableHeader("VM Name", string.Empty);
            s += this.form.TableHeader("Region", string.Empty);
            s += this.form.TableHeader("Created", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                var data = this.ReadVbawsCsv("_VbawsRestorePoints.csv");

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No VBAWS restore points detected.</em></td></tr>";
                }
                else
                {
                    foreach (var row in data)
                    {
                        s += "<tr>";

                        string appliance = GetField(row, "ApplianceId");
                        string vmName = GetField(row, "vmName");
                        if (scrub)
                        {
                            appliance = CGlobals.Scrubber.ScrubItem(appliance, ScrubItemType.Server);
                            vmName = CGlobals.Scrubber.ScrubItem(vmName, ScrubItemType.VM);
                        }

                        s += this.form.TableDataLeftAligned(appliance, string.Empty);
                        s += this.form.TableData(vmName, string.Empty);
                        s += this.form.TableData(GetField(row, "regionId"), string.Empty);
                        s += this.form.TableData(GetField(row, "createdDate"), string.Empty);
                        s += this.form.TableData(GetField(row, "type"), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render VBAWS Restore Points table: " + e.Message);
            }

            s += "</tbody></table>";
            return s;
        }

        private string RenderRepositoriesTable(bool scrub)
        {
            string s = "<h3>Repositories</h3>";

            s += this.form.TableHeaderLeftAligned("Appliance", string.Empty);
            s += this.form.TableHeader("Name", string.Empty);
            s += this.form.TableHeader("Type", string.Empty);
            s += this.form.TableHeader("Region", string.Empty);
            s += this.form.TableHeader("Bucket", string.Empty);
            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                var data = this.ReadVbawsCsv("_VbawsRepositories.csv");

                if (data == null || data.Count == 0)
                {
                    s += "<tr><td colspan='5' style='text-align: center; padding: 20px; color: #666;'><em>No VBAWS repositories detected.</em></td></tr>";
                }
                else
                {
                    foreach (var row in data)
                    {
                        s += "<tr>";

                        string appliance = GetField(row, "ApplianceId");
                        string name = GetField(row, "name");
                        string bucket = GetField(row, "bucketName");
                        if (scrub)
                        {
                            appliance = CGlobals.Scrubber.ScrubItem(appliance, ScrubItemType.Server);
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Repository);
                            bucket = CGlobals.Scrubber.ScrubItem(bucket, "S3Bucket", ScrubItemType.Item);
                        }

                        s += this.form.TableDataLeftAligned(appliance, string.Empty);
                        s += this.form.TableData(name, string.Empty);
                        s += this.form.TableData(GetField(row, "type"), string.Empty);
                        s += this.form.TableData(GetField(row, "regionId"), string.Empty);
                        s += this.form.TableData(bucket, string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render VBAWS Repositories table: " + e.Message);
            }

            s += "</tbody></table>";
            return s;
        }

        /// <summary>
        /// Reads a VBAWS CSV file from disk. Searches all subdirectories under the VBAWS base dir.
        /// Returns a list of dictionaries (header -> value) for each row.
        /// </summary>
        private List<Dictionary<string, string>> ReadVbawsCsv(string fileName)
        {
            string baseDir = CVariables.GetVbawsBaseDir();
            if (!Directory.Exists(baseDir))
            {
                return null;
            }

            // Search for the file in all subdirectories
            var files = Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                return null;
            }

            var result = new List<Dictionary<string, string>>();

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                if (lines.Length < 2) continue;

                var headers = ParseCsvLine(lines[0]);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    var values = ParseCsvLine(lines[i]);
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int j = 0; j < headers.Count && j < values.Count; j++)
                    {
                        row[headers[j]] = values[j];
                    }
                    result.Add(row);
                }
            }

            return result;
        }

        /// <summary>
        /// Simple CSV line parser that handles quoted fields.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result;
        }

        private static string GetField(Dictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out string value) ? value : "";
        }
    }
}
