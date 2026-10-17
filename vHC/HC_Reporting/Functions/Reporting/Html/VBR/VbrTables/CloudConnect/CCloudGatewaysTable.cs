using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.CloudConnect
{
    internal class CCloudGatewaysTable
    {
        private readonly CHtmlFormatting form = new();

        public CCloudGatewaysTable() { }

        public string Render(bool scrub)
        {
            string s = this.form.SectionStartWithButton("cloudgateways", "Cloud Gateways", "Cloud Gateways");

            s += this.form.TableHeaderLeftAligned("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("IP Address", string.Empty);
            s += this.form.TableHeader("Network Mode", string.Empty);
            s += this.form.TableHeader("Incoming Port", string.Empty);
            s += this.form.TableHeader("NAT Port", string.Empty);
            s += this.form.TableHeader("Host", string.Empty);
            s += this.form.TableHeader("CPU Cores", string.Empty);
            s += this.form.TableHeader("RAM (GB)", string.Empty);
            s += this.form.TableHeader("Enabled", string.Empty);

            s += this.form.TableHeaderEnd();
            s += this.form.TableBodyStart();

            try
            {
                CCsvParser c = new();
                var data = c.GetDynamicCloudGateways().ToList();

                var serverLookup = new System.Collections.Generic.Dictionary<string, (string Cores, string Ram)>(System.StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var srv in c.ServerCsvParser())
                    {
                        if (!string.IsNullOrEmpty(srv.Name))
                            serverLookup.TryAdd(srv.Name, (srv.Cores ?? "", srv.Ram ?? ""));
                    }
                }
                catch { }

                if (!data.Any())
                {
                    s += "<tr><td colspan='10' style='text-align: center; padding: 20px; color: #666;'><em>No cloud gateways detected.</em></td></tr>";
                }
                else
                {
                    foreach (var item in data)
                    {
                        s += "<tr>";

                        string name = (string)(item.name ?? "");
                        string ipAddress = (string)(item.ipaddress ?? "");
                        string description = (string)(item.description ?? "");
                        string hostName = (string)(item.hostname ?? "");
                        if (scrub)
                        {
                            name = CGlobals.Scrubber.ScrubItem(name, ScrubItemType.Server);
                            ipAddress = CGlobals.Scrubber.ScrubItem(ipAddress, ScrubItemType.Server);
                            description = CGlobals.Scrubber.ScrubItem(description, ScrubItemType.Item);
                            hostName = CGlobals.Scrubber.ScrubItem(hostName, ScrubItemType.Server);
                        }

                        s += this.form.TableDataLeftAligned(name, string.Empty);
                        s += this.form.TableData(description, string.Empty);
                        s += this.form.TableData(ipAddress, string.Empty);
                        s += this.form.TableData((string)(item.networkmode ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.incomingport ?? ""), string.Empty);
                        s += this.form.TableData((string)(item.natport ?? ""), string.Empty);
                        s += this.form.TableData(hostName, string.Empty);
                        string cores = "", ram = "";
                        if (!string.IsNullOrEmpty(hostName) && serverLookup.TryGetValue(hostName, out var srvInfo))
                        {
                            cores = srvInfo.Cores;
                            ram = FormatRamGb(srvInfo.Ram);
                        }
                        s += this.form.TableData(cores, string.Empty);
                        s += this.form.TableData(ram, string.Empty);
                        s += this.form.TableData((string)(item.enabled ?? ""), string.Empty);

                        s += "</tr>";
                    }
                }
            }
            catch (Exception e)
            {
                CGlobals.Logger.Error("Failed to render Cloud Gateways table: " + e.Message);
            }

            s += this.form.SectionEndWithEmptyState(s, string.Empty);

            return s;
        }

        private static string FormatRamGb(string rawBytes)
        {
            if (string.IsNullOrWhiteSpace(rawBytes)) return string.Empty;
            if (!long.TryParse(rawBytes.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long bytes)) return rawBytes;
            long gb = (long)System.Math.Round(bytes / (1024d * 1024d * 1024d));
            return $"{gb} GB";
        }
    }
}
