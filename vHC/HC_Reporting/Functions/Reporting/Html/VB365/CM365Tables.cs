// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization.VB365;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VB365
{
    internal class CM365Tables
    {
        private readonly CHtmlFormatting form = new();
        private readonly CCsvParser csv = new(CVariables.vb365dir);
        private readonly CM365Summaries summary = new CM365Summaries();
        private readonly Scrubber.CScrubHandler scrubber = CGlobals.Scrubber;

        public CM365Tables()
        {
        }

        public string Globals()
        {
            string s = "<div class=\"global\" id=\"global\">";
            s += this.form.CollapsibleButton("Global Configuration");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicStatus, "License Status");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicExp, "License Expiry");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadSupExp, "Support Expiry");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicType, "License Type");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicTo, "Licensed To");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicContact, "License Contact");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicFor, "Licensed For");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicUsed, "Licenses Used");
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadGFolderExcl, Vb365ResourceHandler.GlobalFolderExclTT);
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadGRetExcl, Vb365ResourceHandler.GlobalRetExclTT);
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadSessHisRet, Vb365ResourceHandler.GlobalLogRetTT);
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyEnabled, Vb365ResourceHandler.GlobalNotificationEnabledTT);
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyOn, Vb365ResourceHandler.GlobalNotifyOnTT);
            s += this.form.TableHeader(Vb365ResourceHandler.GlobalColHeadAutoUpdate, "Automatic Updates?");
            s += "</tr>";
            try
            {
                var global = this.csv.GetDynamicVboGlobal();
                s += "<tr>";
                foreach (var gl in global)
                {
                    // parse lic to int:
                    decimal.TryParse(gl.LicensedFor, out decimal licFor);
                    decimal.TryParse(gl.LicensesUsed, out decimal licUsed);
                    decimal percentUsed = licUsed / licFor * 100;

                    DateTime.TryParse(gl.LicenseExpiry, out DateTime expireDate);

                    s += this.form.TableData(gl.LicenseStatus, string.Empty);

                    if (expireDate < DateTime.Now)
                    {
                        s += this.form.TableData(gl.LicenseExpiry, string.Empty, 1);
                    }
                    else if (expireDate < DateTime.Now.AddDays(60))
                    {
                        s += this.form.TableData(gl.LicenseExpiry, string.Empty, 3);
                    }
                    else
                    {
                        s += this.form.TableData(gl.LicenseExpiry, string.Empty);
                    }


                    s += this.form.TableData(gl.SupportExpiry, string.Empty);
                    s += this.form.TableData(gl.LicenseType, string.Empty);
                    if (CGlobals.Scrub)
                    {
                        var licName = CGlobals.Scrubber.ScrubItem(gl.LicensedTo, Scrubber.ScrubItemType.Item);
                        s += this.form.TableData(licName, string.Empty);
                    }
                    else
                    {
                        s += this.form.TableData(gl.LicensedTo, string.Empty);
                    }

                    s += this.form.TableData(gl.LicenseContact, string.Empty);
                    s += this.form.TableData(gl.LicensedFor, string.Empty);

                    if (percentUsed > 95)
                    {
                        s += this.form.TableData(gl.LicensesUsed, string.Empty, 1);
                    }
                    else if (percentUsed > 90)
                    {
                        s += this.form.TableData(gl.LicensesUsed, string.Empty, 3);
                    }
                    else
                    {
                        s += this.form.TableData(gl.LicensesUsed, string.Empty);
                    }


                    s += this.form.TableData(gl.GlobalFolderExclusions, string.Empty);
                    s += this.form.TableData(gl.GlobalRetExclusions, string.Empty);
                    s += this.form.TableData(gl.LogRetention, string.Empty);
                    if (gl.NotificationEnabled == "False")
                    {
                        s += this.form.TableData(gl.NotificationEnabled, string.Empty, 3);
                    }
                    else
                    {
                        s += this.form.TableData(gl.NotificationEnabled, string.Empty);
                    }


                    s += this.form.TableData(gl.NotifyOn, string.Empty);

                    if (gl.AutomaticUpdates == "False")
                    {
                        s += this.form.TableData(gl.AutomaticUpdates, string.Empty, 3);
                    }
                    else
                    {
                        s += this.form.TableData(gl.AutomaticUpdates, string.Empty);
                    }
                }
            }
            catch (Exception e)
            {
            }

            s += "</tr></table>";

            // summary
            // s += _summary.GlobalSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Proxies()
        {
            string s = "<div class=\"proxies\" id=\"proxies\">";
            s += this.form.header2("Proxies");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Proxy Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Threads", string.Empty);
            s += this.form.TableHeader("Throttling", Vb365ResourceHandler.proxyTTThrottling);
            s += this.form.TableHeader("State", Vb365ResourceHandler.proxyTTState);

            // s += _form.TableHeader("Type", "");
            s += this.form.TableHeader("Outdated", Vb365ResourceHandler.proxyTTOutdated);
            s += this.form.TableHeader("Internet Proxy", Vb365ResourceHandler.proxyTTInternet);
            s += this.form.TableHeader("Objects Managed", Vb365ResourceHandler.proxyTTObjects);
            s += this.form.TableHeader("OS Version", string.Empty);
            s += this.form.TableHeader("RAM", string.Empty);
            s += this.form.TableHeader("CPUs", Vb365ResourceHandler.proxyTTCPUs);
            s += this.form.TableHeader("Extended Logging?", string.Empty);

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynamicVboProxies().ToList();
                foreach (var gl in global)
                {
                    string proxyname = string.Empty;
                    string description = string.Empty;
                    string threads = string.Empty;
                    string throttling = string.Empty;
                    string state = string.Empty;
                    string outdated = string.Empty;
                    string internetproxy = string.Empty;
                    string objectsmanaged = string.Empty;
                    string osversion = string.Empty;
                    string ram = string.Empty;
                    string cpus = string.Empty;
                    string extendedlogging = string.Empty;

                    s += "<tr>";

                    foreach (var g in gl)
                    {
                        switch (g.Key)
                        {
                            case "name":
                                proxyname = g.Value;
                                break;
                            case "description":
                                description = g.Value;
                                break;
                            case "threads":
                                threads = g.Value;
                                break;
                            case "throttling":
                                throttling = g.Value;
                                break;
                            case "state":
                                state = g.Value;
                                break;
                            case "outdated":
                                outdated = g.Value;
                                break;
                            case "internetproxy":
                                internetproxy = g.Value;
                                break;
                            case "objectsmanaged":
                                objectsmanaged = g.Value;
                                break;
                            case "osversion":
                                osversion = g.Value;
                                break;
                            case "ram":
                                ram = g.Value;
                                break;
                            case "cpus":
                                cpus = g.Value;
                                break;
                            case "extendedlogging":
                                extendedlogging = g.Value;
                                break;
                        }

                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            proxyname = this.scrubber.ScrubItem(proxyname, Scrubber.ScrubItemType.Server);
                            description = this.scrubber.ScrubItem(description, Scrubber.ScrubItemType.Item);
                        }
                    }

                    int threadShade = 0;
                    int throttleShade = 0;
                    int stateShade = 0;
                    int outdatedShade = 0;
                    int objManagedShade = 0;
                    int osVersionShade = 0;
                    int ramShade = 0;
                    int cpuShade = 0;
                    int proxyShade = 0;

                    if (internetproxy == "True")
                    {
                        proxyShade = 3;
                    }


                    int.TryParse(threads, out int threadCount);
                    if (threadCount != 64)
                    {
                        threadShade = 3;
                    }


                    if (throttling != "disabled")
                    {
                        throttleShade = 3;
                    }

                    if (state != "Online")
                    {
                        stateShade = 1;
                    }

                    if (outdated == "True")
                    {
                        outdatedShade = 1;
                    }


                    try
                    {
                        string[] osVersionString = osversion.Split();
                        string[] osVersionNumbers = osVersionString[3].Split(".");
                        int.TryParse(osVersionNumbers[0], out int osversionNumber);
                        int.TryParse(osVersionNumbers[1], out int osSubVersion);
                        if (osversionNumber < 10)
                        {
                            osVersionShade = 3;
                        }


                        if (osversionNumber == 6 && osSubVersion < 2)
                        {
                            osVersionShade = 1;
                        }
                    }
                    catch (Exception e)
                    {
                    }

                    string[] ramInt = ram.Split();
                    int.TryParse(ramInt[0], out int ramNumber);
                    int.TryParse(cpus, out int cpuNumber);

                    if (cpuNumber < 4)
                    {
                        cpuShade = 1;
                    }

                    if (cpuNumber > 8)
                    {
                        cpuShade = 3;
                    }

                    if (ramNumber > 32)
                    {
                        ramShade = 3;
                    }


                    if (ramNumber < 8)
                    {
                        ramShade = 1;
                    }

                    // objectsmanaged RAM scales at 1GB X 250 X 2.5 = max objects managed.
                    // cores scale at 1 * 500 * 5 = max objects managed

                    s += this.form.TableData(proxyname, string.Empty);
                    s += this.form.TableData(description, string.Empty);
                    s += this.form.TableData(threads, string.Empty, threadShade);
                    s += this.form.TableData(throttling, string.Empty, throttleShade);
                    s += this.form.TableData(state, string.Empty, stateShade);
                    s += this.form.TableData(outdated, string.Empty, outdatedShade);
                    s += this.form.TableData(internetproxy, string.Empty, proxyShade);
                    s += this.form.TableData(objectsmanaged, string.Empty, objManagedShade);
                    s += this.form.TableData(osversion, string.Empty, osVersionShade);
                    s += this.form.TableData(ram, string.Empty, ramShade);
                    s += this.form.TableData(cpus, string.Empty, cpuShade);
                    s += this.form.TableData(extendedlogging, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.ProxySummary();
            s += "</div>";
            return s;
        }

        public string Vb365Repos()
        {
            string s = "<div class=\"repos\" id=\"repos\">";
            s += this.form.header2("Repositories");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn1, Vb365ResourceHandler.RepoColumnTT1);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn2, Vb365ResourceHandler.RepoColumnTT2);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn3, Vb365ResourceHandler.RepoColumnTT3);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn4, Vb365ResourceHandler.RepoColumnTT4);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn5, Vb365ResourceHandler.RepoColumnTT5);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn6, Vb365ResourceHandler.RepoColumnTT6);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn7, Vb365ResourceHandler.RepoColumnTT7);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn8, Vb365ResourceHandler.RepoColumnTT8);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn9, Vb365ResourceHandler.RepoColumnTT9);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn10, Vb365ResourceHandler.RepoColumnTT10);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn11, Vb365ResourceHandler.RepoColumnTT11);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn12, Vb365ResourceHandler.RepoColumnTT12);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn13, Vb365ResourceHandler.RepoColumnTT13);
            s += this.form.TableHeader(Vb365ResourceHandler.RepoColumn14, Vb365ResourceHandler.RepoColumnTT14);

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynamicVboRepo().ToList();

                foreach (var g in global)
                {
                    s += "<tr>";

                    // int counter = 0;
                    // string output = g.ToString();
                    string boundProxy = g.BoundProxy;
                    string name = g.Name;
                    string desc = g.Description;
                    string path = g.Path;
                    string objRepo = g.ObjectRepo;

                    int pathShade = 0;
                    int objencshade = 0;
                    int stateShade = 0;
                    int freeShade = 0;

                    if (path.StartsWith("C:"))
                    {
                        pathShade = 1;
                    }


                    if (!string.IsNullOrEmpty(objRepo) && g.Encryption == "False")
                    {
                        objencshade = 3;
                    }


                    double.TryParse(g.Free, out double freeSpace);
                    double.TryParse(g.Capacity, out double capacity);

                    if (freeSpace / capacity * 100 < 10)
                    {
                        freeShade = 3;
                    }


                    if (freeSpace / capacity * 100 < 5)
                    {
                        freeSpace = 1;
                    }


                    if (g.State.Contains("Out of Date"))
                    {
                        stateShade = 2;
                    }


                    if (g.State.Contains("Out of Sync") || g.State.Contains("Invalid"))
                    {
                        stateShade = 1;
                    }


                    if (CGlobals.Scrub)
                    {
                        boundProxy = this.scrubber.ScrubItem(g.BoundProxy, Scrubber.ScrubItemType.Server);
                        name = this.scrubber.ScrubItem(g.Name, Scrubber.ScrubItemType.Repository);
                        desc = this.scrubber.ScrubItem(g.Description, Scrubber.ScrubItemType.Item);
                        path = this.scrubber.ScrubItem(g.Path, Scrubber.ScrubItemType.Path);
                        objRepo = this.scrubber.ScrubItem(g.ObjectRepo, Scrubber.ScrubItemType.Repository);
                    }

                    s += this.form.TableData(boundProxy, string.Empty);
                    s += this.form.TableData(name, string.Empty);
                    s += this.form.TableData(desc, string.Empty);
                    s += this.form.TableData(g.Type, string.Empty);
                    s += this.form.TableData(path, string.Empty, pathShade);
                    s += this.form.TableData(objRepo, string.Empty, objencshade);
                    s += this.form.TableData(g.Encryption, string.Empty, objencshade);
                    s += this.form.TableData(g.State, string.Empty, stateShade);
                    s += this.form.TableData(g.Capacity, string.Empty);
                    s += this.form.TableData(g.Free, string.Empty, freeShade);
                    s += this.form.TableData(g.DataStored, string.Empty);
                    s += this.form.TableData(g.CacheSpaceUsed, string.Empty);
                    s += this.form.TableData(g.DailyChangeRate, string.Empty);
                    s += this.form.TableData(g.Retention, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

           // s += _summary.RepoSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Rbac()
        {
            string s = "<div class=\"rbac\" id=\"rbac\">";
            s += this.form.header2("RBAC Roles Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Name", string.Empty);
            s += this.form.TableHeader("Description", string.Empty);
            s += this.form.TableHeader("Role Type", string.Empty);
            s += this.form.TableHeader("Operators", string.Empty);
            s += this.form.TableHeader("Selected Items", string.Empty);
            s += this.form.TableHeader("Excluded Items", string.Empty);

            s += "</tr>";

            try
            {
                var global = this.csv.GetDynamicVboRbac().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        }

                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.RbacSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Security()
        {
            string s = "<div class=\"sec\" id=\"sec\">";
            s += this.form.header2("Security Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader(Vb365ResourceHandler.SecurityTableColumn1, "Win. Firewall Enabled?");
            s += this.form.TableHeader(Vb365ResourceHandler.SecurityTableColumn2, "Internet proxy?");
            s += this.form.TableHeader("RBAC Roles Defined", string.Empty);
            s += "</tr>";

            try
            {
                var global = this.csv.GetDynamicVboSec().ToList();
                var rbacCsv = this.csv.GetDynamicVboRbac().ToList();
                bool rbacRowsCount = false;
                if (rbacCsv.Count > 0)
                {
                    rbacRowsCount = true;
                }


                foreach (var g in global)
                {
                    string apiCert = g.APICert;
                    string serverCert = g.ServerCert;
                    string tenantCert = g.TenantAuthCert;
                    string portalCert = g.RestorePortalCert;
                    string operatorCert = g.OperatorAuthCert;

                    int apiCertSignShade = 0;
                    int portalCertSignShade = 0;
                    int serverCertSignShade = 0;
                    int tenantCertSignShade = 0;
                    int operatorCertSignShade = 0;
                    if (g.APICertSelfSigned == "True")
                    {
                        apiCertSignShade = 1;
                    }


                    if (g.RestorePortalCertSelfSigned == "True")
                    {
                        portalCertSignShade = 1;
                    }


                    if (g.ServerCertSelfSigned == "True")
                    {
                        serverCertSignShade = 3;
                    }


                    if (g.OperatorAuthCertSelfSigned == "True")
                    {
                        operatorCertSignShade = 3;
                    }


                    if (g.TenantAuthCertSelfSigned == "True")
                    {
                        tenantCertSignShade = 3;
                    }


                    int serverDateShade = 0;
                    int apiDateShade = 0;
                    int tenantDateShade = 0;
                    int portalDateShade = 0;
                    int operatorDateShade = 0;

                    DateTime.TryParse(g.ServerCertExpires, out DateTime sCertExpiry);
                    DateTime.TryParse(g.APICertExpires, out DateTime aCertExpiry);
                    DateTime.TryParse(g.TenantAuthCertExpires, out DateTime tCertExpiry);
                    DateTime.TryParse(g.RestorePortalCertExpires, out DateTime pCertExpiry);
                    DateTime.TryParse(g.OperatorAuthCertExpires, out DateTime oCertExpiry);

                    if (sCertExpiry < DateTime.Now)
                    {
                        serverDateShade = 1;
                    }


                    if (sCertExpiry > DateTime.Now && sCertExpiry < DateTime.Now.AddDays(60))
                    {
                        serverDateShade |= 3;
                    }


                    if (aCertExpiry < DateTime.Now)
                    {
                        apiDateShade = 1;
                    }


                    if (aCertExpiry > DateTime.Now && aCertExpiry < DateTime.Now.AddDays(60))
                    {
                        apiDateShade |= 3;
                    }

                    if (tCertExpiry < DateTime.Now)
                    {
                        tenantDateShade = 1;
                    }

                    if (tCertExpiry > DateTime.Now && tCertExpiry < DateTime.Now.AddDays(60))
                    {
                        tenantDateShade |= 3;
                    }

                    if (pCertExpiry < DateTime.Now)
                    {
                        portalDateShade = 1;
                    }

                    if (pCertExpiry > DateTime.Now && pCertExpiry < DateTime.Now.AddDays(60))
                    {
                        portalDateShade |= 3;
                    }

                    if (oCertExpiry < DateTime.Now)
                    {
                        operatorDateShade = 1;
                    }

                    if (oCertExpiry > DateTime.Now && oCertExpiry < DateTime.Now.AddDays(60))
                    {
                        operatorDateShade |= 3;
                    }


                    if (CGlobals.Scrub)
                    {
                        apiCert = this.scrubber.ScrubItem(apiCert, Scrubber.ScrubItemType.Item);
                        serverCert = this.scrubber.ScrubItem(serverCert, Scrubber.ScrubItemType.Item);
                        tenantCert = this.scrubber.ScrubItem(tenantCert, Scrubber.ScrubItemType.Item);
                        portalCert = this.scrubber.ScrubItem(portalCert, Scrubber.ScrubItemType.Item);
                        operatorCert = this.scrubber.ScrubItem(operatorCert, Scrubber.ScrubItemType.Item);
                    }

                    s += "<tr>";
                    s += this.form.TableData(g.WinFirewallEnabled, string.Empty);
                    s += this.form.TableData(g.Internetproxy, string.Empty);
                    s += this.form.TableData(rbacRowsCount.ToString(), string.Empty);
                    s += "</tr>";

                    s += "</table><table border =\"1\"><tr><br/>";
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column1, Vb365ResourceHandler.securityTTServices);
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column2, string.Empty);
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column3, Vb365ResourceHandler.securityTTPort);
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column4, Vb365ResourceHandler.securityTTCert);
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column5, string.Empty);
                    s += this.form.TableHeader(Vb365ResourceHandler.SecurityTable2Column6, string.Empty);
                    s += "</tr><tr>";
                    s += this.form.TableData("Server", string.Empty);
                    s += this.form.TableData(string.Empty, string.Empty);// enabled
                    s += this.form.TableData(string.Empty, string.Empty);// port
                    s += this.form.TableData(serverCert, string.Empty);// cert
                    s += this.form.TableData(g.ServerCertExpires, string.Empty);// expires
                    s += this.form.TableData(g.ServerCertSelfSigned, string.Empty, serverCertSignShade);// self signed
                    s += "</tr><tr>";

                    s += this.form.TableData("API", string.Empty);
                    s += this.form.TableData(g.APIEnabled, string.Empty);
                    s += this.form.TableData(g.APIPort, string.Empty);
                    s += this.form.TableData(apiCert, string.Empty);
                    s += this.form.TableData(g.APICertExpires, string.Empty);
                    s += this.form.TableData(g.APICertSelfSigned, string.Empty, apiCertSignShade);
                    s += "</tr><tr>";
                    s += this.form.TableData("Tenant Auth", string.Empty);
                    s += this.form.TableData(g.TenantAuthEnabled, string.Empty);
                    s += this.form.TableData(string.Empty, string.Empty);
                    s += this.form.TableData(tenantCert, string.Empty);
                    s += this.form.TableData(g.TenantAuthCertExpires, string.Empty);
                    s += this.form.TableData(g.TenantAuthCertSelfSigned, string.Empty, tenantCertSignShade);
                    s += "</tr><tr>";
                    s += this.form.TableData("Restore Portal", string.Empty);
                    s += this.form.TableData(g.RestorePortalEnabled, string.Empty);
                    s += this.form.TableData(string.Empty, string.Empty);
                    s += this.form.TableData(portalCert, string.Empty);
                    s += this.form.TableData(g.RestorePortalCertExpires, string.Empty);
                    s += this.form.TableData(g.RestorePortalCertSelfSigned, string.Empty, portalCertSignShade);
                    s += "</tr>";
                    s += "</tr><tr>";
                    s += this.form.TableData("Operator Auth", string.Empty);
                    s += this.form.TableData(g.OperatorAuthEnabled, string.Empty);
                    s += this.form.TableData(string.Empty, string.Empty);
                    s += this.form.TableData(operatorCert, string.Empty);
                    s += this.form.TableData(g.OperatorAuthCertExpires, string.Empty);
                    s += this.form.TableData(g.OperatorAuthCertSelfSigned, string.Empty, operatorCertSignShade);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.SecSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Controllers()
        {
            string s = "<div class=\"controller\" id=\"controller\">";
            s += this.form.header2("Backup Server");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("VB365 Version", "VB365 Version");
            s += this.form.TableHeader("OS Version", "OS Version");
            s += this.form.TableHeader("RAM", "RAM");
            s += this.form.TableHeader("CPUs", "CPUs");
            s += this.form.TableHeader("Proxies Managed", "Proxies Managed");
            s += this.form.TableHeader("Repos Managed", "Repos Managed");
            s += this.form.TableHeader("Orgs Managed", "Orgs Managed");
            s += this.form.TableHeader("Jobs Managed", "Jobs Managed");
            s += this.form.TableHeader("PowerShell Installed?", "PowerShell Installed?");
            s += this.form.TableHeader("Proxy Installed?", "Proxy Installed?");
            s += this.form.TableHeader("REST Installed?", "REST Installed?");
            s += this.form.TableHeader("Console Installed?", "Console Installed?");
            s += this.form.TableHeader("VM Name", "VM Name");
            s += this.form.TableHeader("VM Location", "VM Location");
            s += this.form.TableHeader("VM SKU", "VM SKU");
            s += this.form.TableHeader("VM Size", "VM Size");

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynVboController().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";
                    string vbVersion = string.Empty;// g.Select(x => x.Key).Where(y=>y == "vb365version");
                    string osVersion = string.Empty;
                    string ram = string.Empty;
                    string cpu = string.Empty;
                    string proxies = string.Empty;
                    string repos = string.Empty;
                    string orgs = string.Empty;
                    string jobs = string.Empty;
                    string psEnabled = string.Empty;
                    string proxyInstalled = string.Empty;
                    string restEnabled = string.Empty;
                    string consoleInstalled = string.Empty;
                    string vmName = string.Empty;
                    string vmLoc = string.Empty;
                    string vmSku = string.Empty;
                    string vmSize = string.Empty;
                    int counter = 0;
                    foreach (var g in gl)
                    {
                        switch (g.Key)
                        {
                            case "vb365version":
                                vbVersion = g.Value;// g.Select(x => x.Key).Where(y=>y == "vb365version");
                                break;
                            case "osversion":
                                osVersion = g.Value;
                                break;
                            case "ram":
                                ram = g.Value;
                                break;
                            case "cpus":
                                cpu = g.Value;
                                break;
                            case "proxiesmanaged":
                                proxies = g.Value;
                                break;
                            case "reposmanaged":
                                repos = g.Value;
                                break;
                            case "orgsmanaged":
                                orgs = g.Value;
                                break;
                            case "jobsmanaged":
                                jobs = g.Value;
                                break;
                            case "powershellinstalled":
                                psEnabled = g.Value;
                                break;
                            case "proxyinstalled":
                                proxyInstalled = g.Value;
                                break;
                            case "restinstalled":
                                restEnabled = g.Value;
                                break;
                            case "consoleinstalled":
                                consoleInstalled = g.Value;
                                break;
                            case "vmname":
                                vmName = g.Value;
                                break;
                            case "vmlocation":
                                vmLoc = g.Value;
                                break;
                            case "vmsku":
                                vmSku = g.Value;
                                break;
                            case "vmsize":
                                vmSize = g.Value;
                                break;
                        }

                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            vmName = this.scrubber.ScrubItem(vmName, Scrubber.ScrubItemType.VM);
                        }
                    }

                    string[] osVersionString = osVersion.Split();
                    string[] osVersionNumbers = osVersionString[3].Split(".");
                    int.TryParse(osVersionNumbers[0], out int osversion);
                    int.TryParse(osVersionNumbers[1], out int osSubVersion);
                    int osShade = 0;
                    if (osversion < 10)
                    {
                        osShade = 3;
                    }


                    if (osversion == 6 && osSubVersion < 2)
                    {
                        osShade = 1;
                    }


                    string[] ramInt = ram.Split();
                    int.TryParse(ramInt[0], out int ramNumber);
                    int.TryParse(cpu, out int cpuNumber);
                    int cpuShade = 0;
                    int ramShade = 0;

                    if (cpuNumber < 4)
                    {
                        cpuShade = 3;
                    }

                    if (ramNumber < 8)
                    {
                        ramShade = 3;
                    }


                    if (restEnabled == "True")
                    {
                        if (ramNumber < 16)
                        {
                            ramShade = 3;
                        }
                    }

                    s += this.form.TableData(vbVersion, string.Empty);
                    s += this.form.TableData(osVersion, string.Empty, osShade);
                    s += this.form.TableData(ram, string.Empty, ramShade);
                    s += this.form.TableData(cpu, string.Empty, cpuShade);
                    s += this.form.TableData(proxies, string.Empty);
                    s += this.form.TableData(repos, string.Empty);
                    s += this.form.TableData(orgs, string.Empty);
                    s += this.form.TableData(jobs, string.Empty);
                    s += this.form.TableData(psEnabled, string.Empty);
                    s += this.form.TableData(proxyInstalled, string.Empty);
                    s += this.form.TableData(restEnabled, string.Empty);
                    s += this.form.TableData(consoleInstalled, string.Empty);
                    s += this.form.TableData(vmName, string.Empty);
                    s += this.form.TableData(vmLoc, string.Empty);
                    s += this.form.TableData(vmSku, string.Empty);
                    s += this.form.TableData(vmSize, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.ControllerSummary();
            s += "</div>";
            return s;
        }

        public string Vb365ControllerDrives()
        {
            string s = "<div class=\"controllerdrives\" id=\"controllerdrives\">";
            s += this.form.header2("Backup Server Disks");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Friendly Name", "Friendly Name");
            s += this.form.TableHeader("DeviceId", Vb365ResourceHandler.BkpSrvDisksDeviceIdTT);
            s += this.form.TableHeader("Bus Type", "Bus Type");
            s += this.form.TableHeader("Media Type", "Media Type");
            s += this.form.TableHeader("Manufacturer", "Manufacturer");
            s += this.form.TableHeader("Model", "Model");
            s += this.form.TableHeader("Size", "Size");
            s += this.form.TableHeader("Allocated Size", "Allocated Size");
            s += this.form.TableHeader("Operational Status", "Operational Status");
            s += this.form.TableHeader("Health Status", "Health Status");
            s += this.form.TableHeader("Boot Drive", "Boot Drive");

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynVboControllerDriver().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";
                    string friendlyname = string.Empty;
                    string deviceid = string.Empty;
                    string bustype = string.Empty;
                    string mediatype = string.Empty;
                    string manufacturer = string.Empty;
                    string model = string.Empty;
                    string size = string.Empty;
                    string allocatedsize = string.Empty;
                    string operationalstatus = string.Empty;
                    string healthstatus = string.Empty;
                    string bootdrive = string.Empty;

                    foreach (var g in gl)
                    {
                        switch (g.Key)
                        {
                            case "friendlyname":
                                friendlyname = g.Value;
                                break;
                            case "deviceid":
                                deviceid = g.Value;
                                break;
                            case "bustype":
                                bustype = g.Value;
                                break;
                            case "mediatype":
                                mediatype = g.Value;
                                break;
                            case "manufacturer":
                                manufacturer = g.Value;
                                break;
                            case "model":
                                model = g.Value;
                                break;
                            case "size":
                                size = g.Value;
                                break;
                            case "allocatedsize":
                                allocatedsize = g.Value;
                                break;
                            case "operationalstatus":
                                operationalstatus = g.Value;
                                break;
                            case "healthstatus":
                                healthstatus = g.Value;
                                break;
                            case "bootdrive":
                                bootdrive = g.Value;
                                break;
                            default:
                                break;
                        }
                    }

                    int mediaTypeShade = 0;
                    int opStatShade = 0;
                    int healthStatShade = 0;

                    if (mediatype != "SSD")
                    {
                        mediaTypeShade = 3;
                    }


                    if (operationalstatus != "OK")
                    {
                        opStatShade = 3;
                    }


                    if (healthstatus != "Healthy")
                    {
                        healthStatShade = 3;
                    }


                    s += this.form.TableData(friendlyname, string.Empty);
                    s += this.form.TableData(deviceid, string.Empty);
                    s += this.form.TableData(bustype, string.Empty);
                    s += this.form.TableData(mediatype, string.Empty, mediaTypeShade);
                    s += this.form.TableData(manufacturer, string.Empty);
                    s += this.form.TableData(model, string.Empty);
                    s += this.form.TableData(size, string.Empty);
                    s += this.form.TableData(allocatedsize, string.Empty);
                    s += this.form.TableData(operationalstatus, string.Empty, opStatShade);
                    s += this.form.TableData(healthstatus, string.Empty, healthStatShade);
                    s += this.form.TableData(bootdrive, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.ControllerDrivesSummary();
            s += "</div>";
            return s;
        }

        public string Vb365JobSessions()
        {
            string s = "<div class=\"jobsessions\" id=\"jobsessions\">";
            s += this.form.header2("Job Sessions");

            // s += "<br>";
            s += this.form.CollapsibleButton("Show Job Sessions");

            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += this.form.TableHeader("Name", "Name");
            s += this.form.TableHeader("Status", "Status");
            s += this.form.TableHeader("Start Time", "Start Time");
            s += this.form.TableHeader("End Time", "End Time");
            s += this.form.TableHeader("Duration", "Duration");
            s += this.form.TableHeader("Log", "Log");

            s += "</tr>";

            try
            {
                var global = this.csv.GetDynVboJobSess().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            if (counter == 0 || counter == 5)
                            {
                                output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Job);
                            }
                        }

                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.JobSessSummary();
            s += "</div>";
            return s;
        }

        public string Vb365ProcStats()
        {
            string s = "<div class=\"procstats\" id=\"procstats\">";
            s += this.form.header2("Processing Statistics");

            // s += "<br>";
            s += this.form.CollapsibleButton("Show Processing Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += this.form.TableHeader("Name", string.Empty);
            s += this.form.TableHeader("Operation", string.Empty);
            s += this.form.TableHeader("Time (latest)", string.Empty);
            s += this.form.TableHeader("Time (Median)", string.Empty);
            s += this.form.TableHeader("Time (Min)", string.Empty);
            s += this.form.TableHeader("Time (Avg)", string.Empty);
            s += this.form.TableHeader("Time (Max)", string.Empty);
            s += this.form.TableHeader("Time (90%)", string.Empty);

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynamicVboProcStat().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";
                    string name;
                    string operation;
                    string tLatest;
                    string tMed;
                    string tMin;
                    string tAvg;
                    string tMax;
                    string tNinety;

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;

                        if (counter == 0 && CGlobals.Scrub)
                        {
                            output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        }


                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.JobStatSummary();
            s += "</div>";
            return s;
        }

        public string Vb365JobStats()
        {
            string s = "<div class=\"jobstats\" id=\"jobstats\">";
            s += this.form.header2("Job Statistics");

            // s += "<br>";
            s += this.form.CollapsibleButton("Show Job Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += this.form.TableHeader("Name", "Name");
            s += this.form.TableHeader("Average Duration (hh:mm:ss)", "Average Duration (hh:mm:ss)");
            s += this.form.TableHeader("Max Duration (hh:mm:ss)", "Max Duration (hh:mm:ss)");
            s += this.form.TableHeader("Average Data Transferred", "Average Data Transferred");
            s += this.form.TableHeader("Max Data Transferred", "Max Data Transferred");
            s += this.form.TableHeader("Average Objects (#)", "Average Objects (#)");
            s += this.form.TableHeader("Max Objects (#)", "Max Objects (#)");
            s += this.form.TableHeader("Average Items (#)", "Average Items (#)");
            s += this.form.TableHeader("Max Items (#)", "Max Items (#)");
            s += this.form.TableHeader("Typical Bottleneck", "Typical Bottleneck");
            s += this.form.TableHeader("Job Avg Throughput", "Job Avg Throughput");
            s += this.form.TableHeader("Job Avg Processing Rate", "Job Avg Processing Rate");

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynVboJobStats().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            if (counter == 0)
                            {
                                output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Job);
                            }
                        }

                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.JobStatSummary();
            s += "</div>";
            return s;
        }

        public string Vb365ObjectRepos()
        {
            string s = "<div class=\"objrepo\" id=\"objrepo\">";
            s += this.form.header2("Object Storage");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Name", "Name");
            s += this.form.TableHeader("Description", "Description");
            s += this.form.TableHeader("Cloud", "Cloud");
            s += this.form.TableHeader("Type", "Type");
            s += this.form.TableHeader("Bucket/Container", "Bucket/Container");
            s += this.form.TableHeader("Path", "Path");
            s += this.form.TableHeader("Size Limit", "Size Limit");
            s += this.form.TableHeader("Used Space", "Used Space");
            s += this.form.TableHeader("Free Space", "Free Space");
            s += this.form.TableHeader("Bound Repo", "Bound Repository");

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynVboObjRepo().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    string name = string.Empty;
                    string description = string.Empty;
                    string cloud = string.Empty;
                    string type = string.Empty;
                    string bucketcontainer = string.Empty;
                    string path = string.Empty;
                    string sizelimit = string.Empty;
                    string usedspace = string.Empty;
                    string freespace = string.Empty;
                    string boundrepo = string.Empty;

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        switch (g.Key)
                        {
                            case "name":
                                name = g.Value;
                                break;
                            case "description":
                                description = g.Value;
                                break;
                            case "cloud":
                                cloud = g.Value;
                                break;
                            case "type":
                                type = g.Value;
                                break;
                            case "bucketcontainer":
                                bucketcontainer = g.Value;
                                break;
                            case "path":
                                path = g.Value;
                                break;
                            case "sizelimit":
                                sizelimit = g.Value;
                                break;
                            case "usedspace":
                                usedspace = g.Value;
                                break;
                            case "freespace":
                                freespace = g.Value;
                                break;
                            case "boundrepo":
                                boundrepo = g.Value;
                                break;
                        }

                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            name = this.scrubber.ScrubItem(name, Scrubber.ScrubItemType.Repository);
                            description = this.scrubber.ScrubItem(description, Scrubber.ScrubItemType.Item);
                            path = this.scrubber.ScrubItem(path, Scrubber.ScrubItemType.Path);
                            boundrepo = this.scrubber.ScrubItem(boundrepo, Scrubber.ScrubItemType.Repository);
                            bucketcontainer = this.scrubber.ScrubItem(bucketcontainer, Scrubber.ScrubItemType.Item);
                        }
                    }

                    int boundRepoShade = 0;
                    if (string.IsNullOrEmpty(boundrepo))
                    {
                        boundRepoShade = 1;
                    }


                    int freeSpaceShade = 0;
                    string[] sizeLimitArray = sizelimit.Split();
                    double.TryParse(sizeLimitArray[0], out double sizeLimitNumber);

                    string[] freeSpaceArray = freespace.Split();
                    double.TryParse(freeSpaceArray[0], out double freeSpaceNumber);
                    if (freeSpaceNumber / sizeLimitNumber * 100 < 10)
                    {
                        freeSpaceShade = 3;
                    }


                    if (freeSpaceNumber / sizeLimitNumber * 100 < 5)
                    {
                        freeSpaceShade = 1;
                    }


                    s += this.form.TableData(name, string.Empty);
                    s += this.form.TableData(description, string.Empty);
                    s += this.form.TableData(cloud, string.Empty);
                    s += this.form.TableData(type, string.Empty);
                    s += this.form.TableData(bucketcontainer, string.Empty);
                    s += this.form.TableData(path, string.Empty);
                    s += this.form.TableData(sizelimit, string.Empty);
                    s += this.form.TableData(usedspace, string.Empty);
                    s += this.form.TableData(freespace, string.Empty);
                    s += this.form.TableData(boundrepo, string.Empty, boundRepoShade);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.ObjRepoSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Orgs()
        {
            string s = "<div class=\"orgs\" id=\"orgs\">";
            s += this.form.header2("Organizations");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Friendly Name", "Friendly Name");
            s += this.form.TableHeader("Real Name", "Real Name");
            s += this.form.TableHeader("Type", "Type");
            s += this.form.TableHeader("Protected Apps", "Protected Apps");
            s += this.form.TableHeader("EXO Settings", Vb365ResourceHandler.orgTTEXOSettings);
            s += this.form.TableHeader("EXO App Cert", Vb365ResourceHandler.orgTTEXOApp);
            s += this.form.TableHeader("SPO Settings", Vb365ResourceHandler.orgTTSPOSettings);
            s += this.form.TableHeader("SPO App Cert", Vb365ResourceHandler.orgTTSPOApp);
            s += this.form.TableHeader("On-Prem Exch Settings", "On-Prem Exch Settings");
            s += this.form.TableHeader("On-Prem SP Settings", "On-Prem SP Settings");
            s += this.form.TableHeader("Licensed Users", "Licensed Users");
            s += this.form.TableHeader("Grant SC Admin", "Grant SC Admin");
            s += this.form.TableHeader("Aux Accounts/Apps", "Aux Accounts/Apps");

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynVboOrg().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            if (counter == 0 ||
                                counter == 1 ||
                                counter == 4 ||
                                counter == 5 ||
                                counter == 6 ||
                                counter == 8 ||
                                counter == 9)
                            {
                                output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                            }
                        }

                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.OrgSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Permissions()
        {
            string s = "<div class=\"perms\" id=\"perms\">";
            s += this.form.header2("Permissions Check");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Type", "Type");
            s += this.form.TableHeader("Organization", "Organization");
            s += this.form.TableHeader("API", "API");
            s += this.form.TableHeader("Permission", "Permission");

            s += "</tr>";

            try
            {
                var global = this.csv.GetDynVboPerms().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            if (counter == 1)
                            {
                                output = this.scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                            }
                        }

                        s += this.form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.PermissionSummary();
            s += "</div>";
            return s;
        }

        public string Vb365ProtStat()
        {
            string s = "<div class=\"protstat\" id=\"protstat\">";
            s += this.form.header2("Unprotected Users");

            // s += CollapsibleButton("Show Protection Statistics");
            // s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";

            // s += _form.TableHeader("User", "User");
            // s += _form.TableHeader("E-mail", "E-mail");
            // s += _form.TableHeader("Organization", "Organization");
            // s += _form.TableHeader("Protection Status", "Protection Status");
            // s += _form.TableHeader("Last Backup Date", "Last Backup Date");

            // s += "</tr>";
            s += "<tr>";
            s += this.form.TableHeader("Total Users", string.Empty);
            s += this.form.TableHeader("Protected Users", string.Empty);
            s += this.form.TableHeader("Unprotected Users", string.Empty);
            s += this.form.TableHeader("Stale Backups", string.Empty);
            s += "</tr>";
            double protectedUsers = 0;
            double notProtectedUsers = 0;
            double stale = 0;

            try
            {
                var global = this.csv.GetDynVboProtStat().ToList();
                var lic = this.csv.GetDynamicVboGlobal().ToList();

                // foreach (var li in lic)
                // {
                //    string s2 = li.licensesused;
                //    double.TryParse(s2, out protectedUsers);
                // }
                // int.TryParse(p, out int protectedUsers);
                foreach (var gl in global)
                {
                    if (gl.hasbackup == "True" && gl.isstale == "False")
                    {
                        protectedUsers++;
                    }


                    if (gl.hasbackup == "False")
                    {
                        notProtectedUsers++;
                    }

                    if (gl.isstale == "True")
                    {
                        stale++;
                    }
                }

                // TODO from ProtectionStatus -> (SUM of HasBackup = False) / total rows - 1 = percentage protected
                // (SUM of HasBackup = False) is unprotected users
                double percent = notProtectedUsers / (notProtectedUsers + protectedUsers) * 100;
                double targetPercent = 20;
                double yellowPercent = 10;
                int shade = 0;
                if (percent > targetPercent)
                {
                    shade = 1;
                }

                if (percent > yellowPercent && percent < targetPercent)
                {
                    shade = 3;
                }

                s += "<tr>";
                s += this.form.TableData((protectedUsers + notProtectedUsers + stale).ToString(), string.Empty);
                s += this.form.TableData(protectedUsers.ToString(), string.Empty);
                s += this.form.TableData(notProtectedUsers.ToString(), string.Empty, shade);
                s += this.form.TableData(stale.ToString(), string.Empty);
                s += "</tr>";
            }
            catch (Exception e)
            {
            }

            s += "</table>";

            // s += _summary.ProtStatSummary();
            s += "</div>";
            return s;
        }

        public string Jobs()
        {
            string s = "<div class=\"jobs\" id=\"jobs\">";
            s += this.form.header2("Jobs");

            // s += CollapsibleButton("Show Protection Statistics");
            // s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";
            s += this.form.TableHeader("Organization", Vb365ResourceHandler.jobsTTOrganization);
            s += this.form.TableHeader("Name", Vb365ResourceHandler.jobsTTName);
            s += this.form.TableHeader("Description", Vb365ResourceHandler.jobsTTDescription);
            s += this.form.TableHeader("Job Type", Vb365ResourceHandler.jobsTTJobType);
            s += this.form.TableHeader("Scope Type", Vb365ResourceHandler.jobsTTScopeType);
            s += this.form.TableHeader("Processing Options", string.Empty);
            s += this.form.TableHeader("Selected Objects", Vb365ResourceHandler.jobsTTSelectedItems);
            s += this.form.TableHeader("Excluded Objects", Vb365ResourceHandler.jobsTTExcludedItems);
            s += this.form.TableHeader("Repository", Vb365ResourceHandler.jobsTTRepository);
            s += this.form.TableHeader("Bound Proxy", Vb365ResourceHandler.jobsTTBoundProxy);
            s += this.form.TableHeader("Enabled?", string.Empty);
            s += this.form.TableHeader("Schedule", string.Empty);
            s += this.form.TableHeader("Related Job", Vb365ResourceHandler.jobsTTRelatedJob);

            s += "</tr>";
            try
            {
                var global = this.csv.GetDynamicVboJobs().ToList();

                foreach (var gl in global)
                {
                    var org = string.Empty;
                    var name = string.Empty;
                    var desc = string.Empty;
                    var jobType = string.Empty;
                    var scopeType = string.Empty;
                    var procOpt = string.Empty;
                    var selItems = string.Empty;
                    var exclItem = string.Empty;
                    var repo = string.Empty;
                    var boundProxy = string.Empty;
                    var enabled = string.Empty;
                    var schedul = string.Empty;
                    var relJob = string.Empty;

                    s += "<tr>";

                    int selectedItemsShade = 0;
                    int excludedItemsShade = 0;
                    int enabledShade = 0;
                    int scheduleShade = 0;

                    foreach (var g in gl)
                    {
                        switch (g.Key)
                        {
                            case "boundproxy":
                                boundProxy = g.Value;
                                break;
                            case "organization":
                                org = g.Value;
                                break;
                            case "name":
                                name = g.Value;
                                break;
                            case "description":
                                desc = g.Value;
                                break;
                            case "jobtype":
                                jobType = g.Value;
                                break;
                            case "scopetype":
                                scopeType = g.Value;
                                break;
                            case "processingoptions":
                                procOpt = g.Value;
                                break;
                            case "selectedobjects":
                                selItems = g.Value;
                                break;
                            case "excludedobjects":
                                exclItem = g.Value;
                                break;
                            case "repository":
                                repo = g.Value;
                                break;
                            case "enabled":
                                enabled = g.Value;
                                break;
                            case "schedule":
                                schedul = g.Value;
                                break;
                            case "relatedjob":
                                relJob = g.Value;
                                break;
                            default:
                                break;
                        }

                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            org = this.scrubber.ScrubItem(org, Scrubber.ScrubItemType.Item);
                            name = this.scrubber.ScrubItem(name, Scrubber.ScrubItemType.Job);
                            desc = this.scrubber.ScrubItem(desc, Scrubber.ScrubItemType.Item);
                            repo = this.scrubber.ScrubItem(repo, Scrubber.ScrubItemType.Repository);
                            boundProxy = this.scrubber.ScrubItem(boundProxy, Scrubber.ScrubItemType.Server);
                            relJob = this.scrubber.ScrubItem(relJob, Scrubber.ScrubItemType.Job);
                        }
                    }

                    if (string.IsNullOrEmpty(org))
                    {
                        break;
                    }


                    int.TryParse(selItems, out int selectedItemsCount);
                    if (selectedItemsCount > 5000)
                    {
                        selectedItemsShade = 3;
                    }


                    int.TryParse(exclItem, out int excludedCount);
                    if (excludedCount > 50)
                    {
                        excludedItemsShade = 3;
                    }

                    if (enabled == "False")
                    {
                        enabledShade = 3;
                    }


                    if (schedul == "Not Scheduled")
                    {
                        scheduleShade = 3;
                    }


                    s += this.form.TableData(org, string.Empty);
                    s += this.form.TableData(name, string.Empty);
                    s += this.form.TableData(desc, string.Empty);
                    s += this.form.TableData(jobType, string.Empty);
                    s += this.form.TableData(scopeType, string.Empty);
                    s += this.form.TableData(procOpt, string.Empty);
                    s += this.form.TableData(selItems, string.Empty, selectedItemsShade);
                    s += this.form.TableData(exclItem, string.Empty, excludedItemsShade);
                    s += this.form.TableData(repo, string.Empty);
                    s += this.form.TableData(boundProxy, string.Empty);
                    s += this.form.TableData(enabled, string.Empty, enabledShade);
                    s += this.form.TableData(schedul, string.Empty, scheduleShade);
                    s += this.form.TableData(relJob, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {
            }

            // s += "<tr>";
            // s += _form.TableData(counter.ToString(), "");
            // s += "</tr>";

            s += "</table>";

            // s += _summary.JobsSummary();
            s += "</div>";
            return s;
        }

        public string MakeVb365NavTable()
        {
            return this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle0, "global", Vb365ResourceHandler.v365NavValue1) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle1, "protstat", Vb365ResourceHandler.v365NavValue1) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle2, "controller", Vb365ResourceHandler.v365NavValue2) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle3, "controllerdrives", Vb365ResourceHandler.v365NavValue3) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle4, "proxies", Vb365ResourceHandler.v365NavValue4) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle5, "repos", Vb365ResourceHandler.v365NavValue5) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle6, "objrepo", Vb365ResourceHandler.v365NavValue6) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle7, "sec", Vb365ResourceHandler.v365NavValue7) +

                // _form.FormNavRows(ResourceHandler.v365NavTitle8, "rbac", ResourceHandler.v365NavValue8) +
                // _form.FormNavRows(ResourceHandler.v365NavTitle9, "perms", ResourceHandler.v365NavValue9) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle10, "orgs", Vb365ResourceHandler.v365NavValue10) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle11, "jobs", Vb365ResourceHandler.v365NavValue11) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle12, "jobstats", Vb365ResourceHandler.v365NavValue12) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle14, "procstats", Vb365ResourceHandler.v365NavValue14) +
                this.form.FormNavRows(Vb365ResourceHandler.v365NavTitle13, "jobsessions", Vb365ResourceHandler.v365NavValue13);
        }
    }
}
