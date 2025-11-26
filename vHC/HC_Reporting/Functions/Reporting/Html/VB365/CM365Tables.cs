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
        private CHtmlFormatting _form = new();
        private CCsvParser _csv = new(CVariables.vb365dir);
        private CM365Summaries _summary = new CM365Summaries();
        private Scrubber.CScrubHandler _scrubber = CGlobals.Scrubber;

        public CM365Tables()
        {

        }

        public string Globals()
        {
            string s = "<div class=\"global\" id=\"global\">";
            s += _form.CollapsibleButton("Global Configuration");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicStatus, "License Status");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicExp, "License Expiry");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadSupExp, "Support Expiry");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicType, "License Type");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicTo, "Licensed To");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicContact, "License Contact");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicFor, "Licensed For");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicUsed, "Licenses Used");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadGFolderExcl, Vb365ResourceHandler.GlobalFolderExclTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadGRetExcl, Vb365ResourceHandler.GlobalRetExclTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadSessHisRet, Vb365ResourceHandler.GlobalLogRetTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyEnabled, Vb365ResourceHandler.GlobalNotificationEnabledTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyOn, Vb365ResourceHandler.GlobalNotifyOnTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadAutoUpdate, "Automatic Updates?");
            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboGlobal();
                s += "<tr>";
                foreach (var gl in global)
                {
                    //parse lic to int:
                    decimal.TryParse(gl.LicensedFor, out decimal licFor);
                    decimal.TryParse(gl.LicensesUsed, out decimal licUsed);
                    decimal percentUsed = licUsed / licFor * 100;

                    DateTime.TryParse(gl.LicenseExpiry, out DateTime expireDate);


                    s += _form.TableData(gl.LicenseStatus, string.Empty);

                    if (expireDate < DateTime.Now)
                        s += _form.TableData(gl.LicenseExpiry, string.Empty, 1);
                    else if (expireDate < DateTime.Now.AddDays(60))
                        s += _form.TableData(gl.LicenseExpiry, string.Empty, 3);
                    else
                        s += _form.TableData(gl.LicenseExpiry, string.Empty);

                    s += _form.TableData(gl.SupportExpiry, string.Empty);
                    s += _form.TableData(gl.LicenseType, string.Empty);
                    if (CGlobals.Scrub)
                    {
                        var licName = CGlobals.Scrubber.ScrubItem(gl.LicensedTo, Scrubber.ScrubItemType.Item);
                        s += _form.TableData(licName, string.Empty);

                    }
                    else
                    {
                        s += _form.TableData(gl.LicensedTo, string.Empty);

                    }
                    s += _form.TableData(gl.LicenseContact, string.Empty);
                    s += _form.TableData(gl.LicensedFor, string.Empty);

                    if (percentUsed > 95)
                        s += _form.TableData(gl.LicensesUsed, string.Empty, 1);
                    else if (percentUsed > 90)
                        s += _form.TableData(gl.LicensesUsed, string.Empty, 3);
                    else
                        s += _form.TableData(gl.LicensesUsed, string.Empty);

                    s += _form.TableData(gl.GlobalFolderExclusions, string.Empty);
                    s += _form.TableData(gl.GlobalRetExclusions, string.Empty);
                    s += _form.TableData(gl.LogRetention, string.Empty);
                    if (gl.NotificationEnabled == "False")
                        s += _form.TableData(gl.NotificationEnabled, string.Empty, 3);
                    else
                        s += _form.TableData(gl.NotificationEnabled, string.Empty);

                    s += _form.TableData(gl.NotifyOn, string.Empty);

                    if (gl.AutomaticUpdates == "False")
                        s += _form.TableData(gl.AutomaticUpdates, string.Empty, 3);
                    else
                        s += _form.TableData(gl.AutomaticUpdates, string.Empty);
                }
            }
            catch (Exception e)
            {

            }
            s += "</tr></table>";

            // summary
            //s += _summary.GlobalSummary();

            s += "</div>";
            return s;
        }

        public string Vb365Proxies()
        {
            string s = "<div class=\"proxies\" id=\"proxies\">";
            s += _form.header2("Proxies");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Proxy Name", string.Empty);
            s += _form.TableHeader("Description", string.Empty);
            s += _form.TableHeader("Threads", string.Empty);
            s += _form.TableHeader("Throttling", Vb365ResourceHandler.proxyTTThrottling);
            s += _form.TableHeader("State", Vb365ResourceHandler.proxyTTState);
            //s += _form.TableHeader("Type", "");
            s += _form.TableHeader("Outdated", Vb365ResourceHandler.proxyTTOutdated);
            s += _form.TableHeader("Internet Proxy", Vb365ResourceHandler.proxyTTInternet);
            s += _form.TableHeader("Objects Managed", Vb365ResourceHandler.proxyTTObjects);
            s += _form.TableHeader("OS Version", string.Empty);
            s += _form.TableHeader("RAM", string.Empty);
            s += _form.TableHeader("CPUs", Vb365ResourceHandler.proxyTTCPUs);
            s += _form.TableHeader("Extended Logging?", string.Empty);

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboProxies().ToList();
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
                            proxyname = _scrubber.ScrubItem(proxyname, Scrubber.ScrubItemType.Server);
                            description = _scrubber.ScrubItem(description, Scrubber.ScrubItemType.Item);
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
                        proxyShade = 3;


                    int.TryParse(threads, out int threadCount);
                    if (threadCount != 64)
                        threadShade = 3;

                    if (throttling != "disabled")
                        throttleShade = 3;

                    if (state != "Online")
                        stateShade = 1;
                    if (outdated == "True")
                        outdatedShade = 1;

                    try
                    {
                        string[] osVersionString = osversion.Split();
                        string[] osVersionNumbers = osVersionString[3].Split(".");
                        int.TryParse(osVersionNumbers[0], out int osversionNumber);
                        int.TryParse(osVersionNumbers[1], out int osSubVersion);
                        if (osversionNumber < 10)
                            osVersionShade = 3;
                        if (osversionNumber == 6 && osSubVersion < 2)
                            osVersionShade = 1;
                    }
                    catch (Exception e)
                    {

                    }


                    string[] ramInt = ram.Split();
                    int.TryParse(ramInt[0], out int ramNumber);
                    int.TryParse(cpus, out int cpuNumber);

                    if (cpuNumber < 4)
                        cpuShade = 1;
                    if (cpuNumber > 8)
                        cpuShade = 3;
                    if (ramNumber > 32)
                        ramShade = 3;
                    if (ramNumber < 8)
                        ramShade = 1;

                    // objectsmanaged RAM scales at 1GB X 250 X 2.5 = max objects managed.
                    // cores scale at 1 * 500 * 5 = max objects managed

                    s += _form.TableData(proxyname, string.Empty);
                    s += _form.TableData(description, string.Empty);
                    s += _form.TableData(threads, string.Empty, threadShade);
                    s += _form.TableData(throttling, string.Empty, throttleShade);
                    s += _form.TableData(state, string.Empty, stateShade);
                    s += _form.TableData(outdated, string.Empty, outdatedShade);
                    s += _form.TableData(internetproxy, string.Empty, proxyShade);
                    s += _form.TableData(objectsmanaged, string.Empty, objManagedShade);
                    s += _form.TableData(osversion, string.Empty, osVersionShade);
                    s += _form.TableData(ram, string.Empty, ramShade);
                    s += _form.TableData(cpus, string.Empty, cpuShade);
                    s += _form.TableData(extendedlogging, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";
            //s += _summary.ProxySummary();

            s += "</div>";
            return s;
        }

        public string Vb365Repos()
        {
            string s = "<div class=\"repos\" id=\"repos\">";
            s += _form.header2("Repositories");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn1, Vb365ResourceHandler.RepoColumnTT1);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn2, Vb365ResourceHandler.RepoColumnTT2);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn3, Vb365ResourceHandler.RepoColumnTT3);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn4, Vb365ResourceHandler.RepoColumnTT4);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn5, Vb365ResourceHandler.RepoColumnTT5);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn6, Vb365ResourceHandler.RepoColumnTT6);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn7, Vb365ResourceHandler.RepoColumnTT7);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn8, Vb365ResourceHandler.RepoColumnTT8);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn9, Vb365ResourceHandler.RepoColumnTT9);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn10, Vb365ResourceHandler.RepoColumnTT10);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn11, Vb365ResourceHandler.RepoColumnTT11);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn12, Vb365ResourceHandler.RepoColumnTT12);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn13, Vb365ResourceHandler.RepoColumnTT13);
            s += _form.TableHeader(Vb365ResourceHandler.RepoColumn14, Vb365ResourceHandler.RepoColumnTT14);

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboRepo().ToList();

                foreach (var g in global)
                {
                    s += "<tr>";
                    //int counter = 0;
                    //string output = g.ToString();
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
                        pathShade = 1;
                    if (!string.IsNullOrEmpty(objRepo) && g.Encryption == "False")
                        objencshade = 3;

                    double.TryParse(g.Free, out double freeSpace);
                    double.TryParse(g.Capacity, out double capacity);

                    if (freeSpace / capacity * 100 < 10)
                        freeShade = 3;
                    if (freeSpace / capacity * 100 < 5)
                        freeSpace = 1;

                    if (g.State.Contains("Out of Date"))
                        stateShade = 2;
                    if (g.State.Contains("Out of Sync") || g.State.Contains("Invalid"))
                        stateShade = 1;

                    if (CGlobals.Scrub)
                    {
                        boundProxy = _scrubber.ScrubItem(g.BoundProxy, Scrubber.ScrubItemType.Server);
                        name = _scrubber.ScrubItem(g.Name, Scrubber.ScrubItemType.Repository);
                        desc = _scrubber.ScrubItem(g.Description, Scrubber.ScrubItemType.Item);
                        path = _scrubber.ScrubItem(g.Path, Scrubber.ScrubItemType.Path);
                        objRepo = _scrubber.ScrubItem(g.ObjectRepo, Scrubber.ScrubItemType.Repository);

                    }
                    s += _form.TableData(boundProxy, string.Empty);
                    s += _form.TableData(name, string.Empty);
                    s += _form.TableData(desc, string.Empty);
                    s += _form.TableData(g.Type, string.Empty);
                    s += _form.TableData(path, string.Empty, pathShade);
                    s += _form.TableData(objRepo, string.Empty, objencshade);
                    s += _form.TableData(g.Encryption, string.Empty, objencshade);
                    s += _form.TableData(g.State, string.Empty, stateShade);
                    s += _form.TableData(g.Capacity, string.Empty);
                    s += _form.TableData(g.Free, string.Empty, freeShade);
                    s += _form.TableData(g.DataStored, string.Empty);
                    s += _form.TableData(g.CacheSpaceUsed, string.Empty);
                    s += _form.TableData(g.DailyChangeRate, string.Empty);
                    s += _form.TableData(g.Retention, string.Empty);
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
            s += _form.header2("RBAC Roles Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Name", string.Empty);
            s += _form.TableHeader("Description", string.Empty);
            s += _form.TableHeader("Role Type", string.Empty);
            s += _form.TableHeader("Operators", string.Empty);
            s += _form.TableHeader("Selected Items", string.Empty);
            s += _form.TableHeader("Excluded Items", string.Empty);

            s += "</tr>";

            try
            {

                var global = _csv.GetDynamicVboRbac().ToList();
                foreach (var gl in global)
                {
                    s += "<tr>";

                    int counter = 0;
                    foreach (var g in gl)
                    {
                        string output = g.Value;
                        if (CGlobals.Scrub)
                        {
                            output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        }
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }

            s += "</table>";

            //s += _summary.RbacSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Security()
        {
            string s = "<div class=\"sec\" id=\"sec\">";
            s += _form.header2("Security Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader(Vb365ResourceHandler.SecurityTableColumn1, "Win. Firewall Enabled?");
            s += _form.TableHeader(Vb365ResourceHandler.SecurityTableColumn2, "Internet proxy?");
            s += _form.TableHeader("RBAC Roles Defined", string.Empty);
            s += "</tr>";


            try
            {

                var global = _csv.GetDynamicVboSec().ToList();
                var rbacCsv = _csv.GetDynamicVboRbac().ToList();
                bool rbacRowsCount = false;
                if (rbacCsv.Count > 0)
                    rbacRowsCount = true;

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
                        apiCertSignShade = 1;
                    if (g.RestorePortalCertSelfSigned == "True")
                        portalCertSignShade = 1;
                    if (g.ServerCertSelfSigned == "True")
                        serverCertSignShade = 3;
                    if (g.OperatorAuthCertSelfSigned == "True")
                        operatorCertSignShade = 3;
                    if (g.TenantAuthCertSelfSigned == "True")
                        tenantCertSignShade = 3;

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
                        serverDateShade = 1;
                    if (sCertExpiry > DateTime.Now && sCertExpiry < DateTime.Now.AddDays(60))
                        serverDateShade |= 3;

                    if (aCertExpiry < DateTime.Now)
                        apiDateShade = 1;
                    if (aCertExpiry > DateTime.Now && aCertExpiry < DateTime.Now.AddDays(60))
                        apiDateShade |= 3;

                    if (tCertExpiry < DateTime.Now)
                        tenantDateShade = 1;
                    if (tCertExpiry > DateTime.Now && tCertExpiry < DateTime.Now.AddDays(60))
                        tenantDateShade |= 3;

                    if (pCertExpiry < DateTime.Now)
                        portalDateShade = 1;
                    if (pCertExpiry > DateTime.Now && pCertExpiry < DateTime.Now.AddDays(60))
                        portalDateShade |= 3;

                    if (oCertExpiry < DateTime.Now)
                        operatorDateShade = 1;
                    if (oCertExpiry > DateTime.Now && oCertExpiry < DateTime.Now.AddDays(60))
                        operatorDateShade |= 3;





                    if (CGlobals.Scrub)
                    {
                        apiCert = _scrubber.ScrubItem(apiCert, Scrubber.ScrubItemType.Item);
                        serverCert = _scrubber.ScrubItem(serverCert, Scrubber.ScrubItemType.Item);
                        tenantCert = _scrubber.ScrubItem(tenantCert, Scrubber.ScrubItemType.Item);
                        portalCert = _scrubber.ScrubItem(portalCert, Scrubber.ScrubItemType.Item);
                        operatorCert = _scrubber.ScrubItem(operatorCert, Scrubber.ScrubItemType.Item);
                    }


                    s += "<tr>";
                    s += _form.TableData(g.WinFirewallEnabled, string.Empty);
                    s += _form.TableData(g.Internetproxy, string.Empty);
                    s += _form.TableData(rbacRowsCount.ToString(), string.Empty);
                    s += "</tr>";

                    s += "</table><table border =\"1\"><tr><br/>";
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column1, Vb365ResourceHandler.securityTTServices);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column2, string.Empty);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column3, Vb365ResourceHandler.securityTTPort);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column4, Vb365ResourceHandler.securityTTCert);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column5, string.Empty);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column6, string.Empty);
                    s += "</tr><tr>";
                    s += _form.TableData("Server", string.Empty);
                    s += _form.TableData(string.Empty, string.Empty);// enabled
                    s += _form.TableData(string.Empty, string.Empty);// port
                    s += _form.TableData(serverCert, string.Empty);// cert
                    s += _form.TableData(g.ServerCertExpires, string.Empty);// expires
                    s += _form.TableData(g.ServerCertSelfSigned, string.Empty, serverCertSignShade);// self signed
                    s += "</tr><tr>";

                    s += _form.TableData("API", string.Empty);
                    s += _form.TableData(g.APIEnabled, string.Empty);
                    s += _form.TableData(g.APIPort, string.Empty);
                    s += _form.TableData(apiCert, string.Empty);
                    s += _form.TableData(g.APICertExpires, string.Empty);
                    s += _form.TableData(g.APICertSelfSigned, string.Empty, apiCertSignShade);
                    s += "</tr><tr>";
                    s += _form.TableData("Tenant Auth", string.Empty);
                    s += _form.TableData(g.TenantAuthEnabled, string.Empty);
                    s += _form.TableData(string.Empty, string.Empty);
                    s += _form.TableData(tenantCert, string.Empty);
                    s += _form.TableData(g.TenantAuthCertExpires, string.Empty);
                    s += _form.TableData(g.TenantAuthCertSelfSigned, string.Empty, tenantCertSignShade);
                    s += "</tr><tr>";
                    s += _form.TableData("Restore Portal", string.Empty);
                    s += _form.TableData(g.RestorePortalEnabled, string.Empty);
                    s += _form.TableData(string.Empty, string.Empty);
                    s += _form.TableData(portalCert, string.Empty);
                    s += _form.TableData(g.RestorePortalCertExpires, string.Empty);
                    s += _form.TableData(g.RestorePortalCertSelfSigned, string.Empty, portalCertSignShade);
                    s += "</tr>";
                    s += "</tr><tr>";
                    s += _form.TableData("Operator Auth", string.Empty);
                    s += _form.TableData(g.OperatorAuthEnabled, string.Empty);
                    s += _form.TableData(string.Empty, string.Empty);
                    s += _form.TableData(operatorCert, string.Empty);
                    s += _form.TableData(g.OperatorAuthCertExpires, string.Empty);
                    s += _form.TableData(g.OperatorAuthCertSelfSigned, string.Empty, operatorCertSignShade);
                    s += "</tr>";


                }
            }
            catch (Exception e)
            {

            }


            s += "</table>";
            //s += _summary.SecSummary();

            s += "</div>";
            return s;
        }

        public string Vb365Controllers()
        {
            string s = "<div class=\"controller\" id=\"controller\">";
            s += _form.header2("Backup Server");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("VB365 Version", "VB365 Version");
            s += _form.TableHeader("OS Version", "OS Version");
            s += _form.TableHeader("RAM", "RAM");
            s += _form.TableHeader("CPUs", "CPUs");
            s += _form.TableHeader("Proxies Managed", "Proxies Managed");
            s += _form.TableHeader("Repos Managed", "Repos Managed");
            s += _form.TableHeader("Orgs Managed", "Orgs Managed");
            s += _form.TableHeader("Jobs Managed", "Jobs Managed");
            s += _form.TableHeader("PowerShell Installed?", "PowerShell Installed?");
            s += _form.TableHeader("Proxy Installed?", "Proxy Installed?");
            s += _form.TableHeader("REST Installed?", "REST Installed?");
            s += _form.TableHeader("Console Installed?", "Console Installed?");
            s += _form.TableHeader("VM Name", "VM Name");
            s += _form.TableHeader("VM Location", "VM Location");
            s += _form.TableHeader("VM SKU", "VM SKU");
            s += _form.TableHeader("VM Size", "VM Size");



            s += "</tr>";
            try
            {

                var global = _csv.GetDynVboController().ToList();
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
                            vmName = _scrubber.ScrubItem(vmName, Scrubber.ScrubItemType.VM);
                        }


                    }
                    string[] osVersionString = osVersion.Split();
                    string[] osVersionNumbers = osVersionString[3].Split(".");
                    int.TryParse(osVersionNumbers[0], out int osversion);
                    int.TryParse(osVersionNumbers[1], out int osSubVersion);
                    int osShade = 0;
                    if (osversion < 10)
                        osShade = 3;
                    if (osversion == 6 && osSubVersion < 2)
                        osShade = 1;
                    string[] ramInt = ram.Split();
                    int.TryParse(ramInt[0], out int ramNumber);
                    int.TryParse(cpu, out int cpuNumber);
                    int cpuShade = 0;
                    int ramShade = 0;

                    if (cpuNumber < 4)
                        cpuShade = 3;
                    if (ramNumber < 8)
                        ramShade = 3;
                    if (restEnabled == "True")
                    {
                        if (ramNumber < 16)
                            ramShade = 3;
                    }

                    s += _form.TableData(vbVersion, string.Empty);
                    s += _form.TableData(osVersion, string.Empty, osShade);
                    s += _form.TableData(ram, string.Empty, ramShade);
                    s += _form.TableData(cpu, string.Empty, cpuShade);
                    s += _form.TableData(proxies, string.Empty);
                    s += _form.TableData(repos, string.Empty);
                    s += _form.TableData(orgs, string.Empty);
                    s += _form.TableData(jobs, string.Empty);
                    s += _form.TableData(psEnabled, string.Empty);
                    s += _form.TableData(proxyInstalled, string.Empty);
                    s += _form.TableData(restEnabled, string.Empty);
                    s += _form.TableData(consoleInstalled, string.Empty);
                    s += _form.TableData(vmName, string.Empty);
                    s += _form.TableData(vmLoc, string.Empty);
                    s += _form.TableData(vmSku, string.Empty);
                    s += _form.TableData(vmSize, string.Empty);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";
            //s += _summary.ControllerSummary();

            s += "</div>";
            return s;
        }

        public string Vb365ControllerDrives()
        {
            string s = "<div class=\"controllerdrives\" id=\"controllerdrives\">";
            s += _form.header2("Backup Server Disks");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Friendly Name", "Friendly Name");
            s += _form.TableHeader("DeviceId", Vb365ResourceHandler.BkpSrvDisksDeviceIdTT);
            s += _form.TableHeader("Bus Type", "Bus Type");
            s += _form.TableHeader("Media Type", "Media Type");
            s += _form.TableHeader("Manufacturer", "Manufacturer");
            s += _form.TableHeader("Model", "Model");
            s += _form.TableHeader("Size", "Size");
            s += _form.TableHeader("Allocated Size", "Allocated Size");
            s += _form.TableHeader("Operational Status", "Operational Status");
            s += _form.TableHeader("Health Status", "Health Status");
            s += _form.TableHeader("Boot Drive", "Boot Drive");

            s += "</tr>";
            try
            {

                var global = _csv.GetDynVboControllerDriver().ToList();
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
                        mediaTypeShade = 3;
                    if (operationalstatus != "OK")
                        opStatShade = 3;
                    if (healthstatus != "Healthy")
                        healthStatShade = 3;


                    s += _form.TableData(friendlyname, string.Empty);
                    s += _form.TableData(deviceid, string.Empty);
                    s += _form.TableData(bustype, string.Empty);
                    s += _form.TableData(mediatype, string.Empty, mediaTypeShade);
                    s += _form.TableData(manufacturer, string.Empty);
                    s += _form.TableData(model, string.Empty);
                    s += _form.TableData(size, string.Empty);
                    s += _form.TableData(allocatedsize, string.Empty);
                    s += _form.TableData(operationalstatus, string.Empty, opStatShade);
                    s += _form.TableData(healthstatus, string.Empty, healthStatShade);
                    s += _form.TableData(bootdrive, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.ControllerDrivesSummary();
            s += "</div>";
            return s;
        }

        public string Vb365JobSessions()
        {
            string s = "<div class=\"jobsessions\" id=\"jobsessions\">";
            s += _form.header2("Job Sessions");
            //s += "<br>";
            s += _form.CollapsibleButton("Show Job Sessions");

            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Status", "Status");
            s += _form.TableHeader("Start Time", "Start Time");
            s += _form.TableHeader("End Time", "End Time");
            s += _form.TableHeader("Duration", "Duration");
            s += _form.TableHeader("Log", "Log");

            s += "</tr>";

            try
            {
                var global = _csv.GetDynVboJobSess().ToList();
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
                                output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Job);
                        }
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.JobSessSummary();

            s += "</div>";
            return s;
        }

        public string Vb365ProcStats()
        {
            string s = "<div class=\"procstats\" id=\"procstats\">";
            s += _form.header2("Processing Statistics");
            //s += "<br>";
            s += _form.CollapsibleButton("Show Processing Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += _form.TableHeader("Name", string.Empty);
            s += _form.TableHeader("Operation", string.Empty);
            s += _form.TableHeader("Time (latest)", string.Empty);
            s += _form.TableHeader("Time (Median)", string.Empty);
            s += _form.TableHeader("Time (Min)", string.Empty);
            s += _form.TableHeader("Time (Avg)", string.Empty);
            s += _form.TableHeader("Time (Max)", string.Empty);
            s += _form.TableHeader("Time (90%)", string.Empty);

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboProcStat().ToList();
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
                            output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.JobStatSummary();
            s += "</div>";
            return s;
        }

        public string Vb365JobStats()
        {
            string s = "<div class=\"jobstats\" id=\"jobstats\">";
            s += _form.header2("Job Statistics");
            //s += "<br>";
            s += _form.CollapsibleButton("Show Job Stats");
            s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Average Duration (hh:mm:ss)", "Average Duration (hh:mm:ss)");
            s += _form.TableHeader("Max Duration (hh:mm:ss)", "Max Duration (hh:mm:ss)");
            s += _form.TableHeader("Average Data Transferred", "Average Data Transferred");
            s += _form.TableHeader("Max Data Transferred", "Max Data Transferred");
            s += _form.TableHeader("Average Objects (#)", "Average Objects (#)");
            s += _form.TableHeader("Max Objects (#)", "Max Objects (#)");
            s += _form.TableHeader("Average Items (#)", "Average Items (#)");
            s += _form.TableHeader("Max Items (#)", "Max Items (#)");
            s += _form.TableHeader("Typical Bottleneck", "Typical Bottleneck");
            s += _form.TableHeader("Job Avg Throughput", "Job Avg Throughput");
            s += _form.TableHeader("Job Avg Processing Rate", "Job Avg Processing Rate");

            s += "</tr>";
            try
            {

                var global = _csv.GetDynVboJobStats().ToList();
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
                                output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Job);
                        }
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.JobStatSummary();
            s += "</div>";
            return s;
        }

        public string Vb365ObjectRepos()
        {
            string s = "<div class=\"objrepo\" id=\"objrepo\">";
            s += _form.header2("Object Storage");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Description", "Description");
            s += _form.TableHeader("Cloud", "Cloud");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Bucket/Container", "Bucket/Container");
            s += _form.TableHeader("Path", "Path");
            s += _form.TableHeader("Size Limit", "Size Limit");
            s += _form.TableHeader("Used Space", "Used Space");
            s += _form.TableHeader("Free Space", "Free Space");
            s += _form.TableHeader("Bound Repo", "Bound Repository");

            s += "</tr>";
            try
            {

                var global = _csv.GetDynVboObjRepo().ToList();
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
                            name = _scrubber.ScrubItem(name, Scrubber.ScrubItemType.Repository);
                            description = _scrubber.ScrubItem(description, Scrubber.ScrubItemType.Item);
                            path = _scrubber.ScrubItem(path, Scrubber.ScrubItemType.Path);
                            boundrepo = _scrubber.ScrubItem(boundrepo, Scrubber.ScrubItemType.Repository);
                            bucketcontainer = _scrubber.ScrubItem(bucketcontainer, Scrubber.ScrubItemType.Item);
                        }

                    }
                    int boundRepoShade = 0;
                    if (string.IsNullOrEmpty(boundrepo))
                        boundRepoShade = 1;

                    int freeSpaceShade = 0;
                    string[] sizeLimitArray = sizelimit.Split();
                    double.TryParse(sizeLimitArray[0], out double sizeLimitNumber);

                    string[] freeSpaceArray = freespace.Split();
                    double.TryParse(freeSpaceArray[0], out double freeSpaceNumber);
                    if (freeSpaceNumber / sizeLimitNumber * 100 < 10)
                        freeSpaceShade = 3;
                    if (freeSpaceNumber / sizeLimitNumber * 100 < 5)
                        freeSpaceShade = 1;

                    s += _form.TableData(name, string.Empty);
                    s += _form.TableData(description, string.Empty);
                    s += _form.TableData(cloud, string.Empty);
                    s += _form.TableData(type, string.Empty);
                    s += _form.TableData(bucketcontainer, string.Empty);
                    s += _form.TableData(path, string.Empty);
                    s += _form.TableData(sizelimit, string.Empty);
                    s += _form.TableData(usedspace, string.Empty);
                    s += _form.TableData(freespace, string.Empty);
                    s += _form.TableData(boundrepo, string.Empty, boundRepoShade);
                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.ObjRepoSummary();

            s += "</div>";
            return s;
        }

        public string Vb365Orgs()
        {
            string s = "<div class=\"orgs\" id=\"orgs\">";
            s += _form.header2("Organizations");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Friendly Name", "Friendly Name");
            s += _form.TableHeader("Real Name", "Real Name");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Protected Apps", "Protected Apps");
            s += _form.TableHeader("EXO Settings", Vb365ResourceHandler.orgTTEXOSettings);
            s += _form.TableHeader("EXO App Cert", Vb365ResourceHandler.orgTTEXOApp);
            s += _form.TableHeader("SPO Settings", Vb365ResourceHandler.orgTTSPOSettings);
            s += _form.TableHeader("SPO App Cert", Vb365ResourceHandler.orgTTSPOApp);
            s += _form.TableHeader("On-Prem Exch Settings", "On-Prem Exch Settings");
            s += _form.TableHeader("On-Prem SP Settings", "On-Prem SP Settings");
            s += _form.TableHeader("Licensed Users", "Licensed Users");
            s += _form.TableHeader("Grant SC Admin", "Grant SC Admin");
            s += _form.TableHeader("Aux Accounts/Apps", "Aux Accounts/Apps");

            s += "</tr>";
            try
            {

                var global = _csv.GetDynVboOrg().ToList();
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
                                output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        }
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";

            //s += _summary.OrgSummary();
            s += "</div>";
            return s;
        }

        public string Vb365Permissions()
        {
            string s = "<div class=\"perms\" id=\"perms\">";
            s += _form.header2("Permissions Check");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Organization", "Organization");
            s += _form.TableHeader("API", "API");
            s += _form.TableHeader("Permission", "Permission");

            s += "</tr>";

            try
            {
                var global = _csv.GetDynVboPerms().ToList();
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
                                output = _scrubber.ScrubItem(output, Scrubber.ScrubItemType.Item);
                        }
                        s += _form.TableData(output, string.Empty);
                        counter++;
                    }

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            s += "</table>";
            //s += _summary.PermissionSummary();

            s += "</div>";
            return s;
        }

        public string Vb365ProtStat()
        {
            string s = "<div class=\"protstat\" id=\"protstat\">";
            s += _form.header2("Unprotected Users");
            //s += CollapsibleButton("Show Protection Statistics");
            //s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";
            //s += _form.TableHeader("User", "User");
            //s += _form.TableHeader("E-mail", "E-mail");
            //s += _form.TableHeader("Organization", "Organization");
            //s += _form.TableHeader("Protection Status", "Protection Status");
            //s += _form.TableHeader("Last Backup Date", "Last Backup Date");

            //s += "</tr>";

            s += "<tr>";
            s += _form.TableHeader("Total Users", string.Empty);
            s += _form.TableHeader("Protected Users", string.Empty);
            s += _form.TableHeader("Unprotected Users", string.Empty);
            s += _form.TableHeader("Stale Backups", string.Empty);
            s += "</tr>";
            double protectedUsers = 0;
            double notProtectedUsers = 0;
            double stale = 0;

            try
            {
                var global = _csv.GetDynVboProtStat().ToList();
                var lic = _csv.GetDynamicVboGlobal().ToList();
                //foreach (var li in lic)
                //{
                //    string s2 = li.licensesused;
                //    double.TryParse(s2, out protectedUsers);
                //}
                //int.TryParse(p, out int protectedUsers);
                foreach (var gl in global)
                {
                    if (gl.hasbackup == "True" && gl.isstale == "False")
                        protectedUsers++;
                    if (gl.hasbackup == "False")
                        notProtectedUsers++;
                    if (gl.isstale == "True")
                        stale++;
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
                s += _form.TableData((protectedUsers + notProtectedUsers + stale).ToString(), string.Empty);
                s += _form.TableData(protectedUsers.ToString(), string.Empty);
                s += _form.TableData(notProtectedUsers.ToString(), string.Empty, shade);
                s += _form.TableData(stale.ToString(), string.Empty);
                s += "</tr>";
            }
            catch (Exception e)
            {

            }


            s += "</table>";

            //s += _summary.ProtStatSummary();

            s += "</div>";
            return s;
        }

        public string Jobs()
        {
            string s = "<div class=\"jobs\" id=\"jobs\">";
            s += _form.header2("Jobs");
            //s += CollapsibleButton("Show Protection Statistics");
            //s += "<table border=\"1\" style=\"display: none;\"><tr>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Organization", Vb365ResourceHandler.jobsTTOrganization);
            s += _form.TableHeader("Name", Vb365ResourceHandler.jobsTTName);
            s += _form.TableHeader("Description", Vb365ResourceHandler.jobsTTDescription);
            s += _form.TableHeader("Job Type", Vb365ResourceHandler.jobsTTJobType);
            s += _form.TableHeader("Scope Type", Vb365ResourceHandler.jobsTTScopeType);
            s += _form.TableHeader("Processing Options", string.Empty);
            s += _form.TableHeader("Selected Objects", Vb365ResourceHandler.jobsTTSelectedItems);
            s += _form.TableHeader("Excluded Objects", Vb365ResourceHandler.jobsTTExcludedItems);
            s += _form.TableHeader("Repository", Vb365ResourceHandler.jobsTTRepository);
            s += _form.TableHeader("Bound Proxy", Vb365ResourceHandler.jobsTTBoundProxy);
            s += _form.TableHeader("Enabled?", string.Empty);
            s += _form.TableHeader("Schedule", string.Empty);
            s += _form.TableHeader("Related Job", Vb365ResourceHandler.jobsTTRelatedJob);

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboJobs().ToList();


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
                            org = _scrubber.ScrubItem(org, Scrubber.ScrubItemType.Item);
                            name = _scrubber.ScrubItem(name, Scrubber.ScrubItemType.Job);
                            desc = _scrubber.ScrubItem(desc, Scrubber.ScrubItemType.Item);
                            repo = _scrubber.ScrubItem(repo, Scrubber.ScrubItemType.Repository);
                            boundProxy = _scrubber.ScrubItem(boundProxy, Scrubber.ScrubItemType.Server);
                            relJob = _scrubber.ScrubItem(relJob, Scrubber.ScrubItemType.Job);
                        }
                    }
                    if (string.IsNullOrEmpty(org))
                        break;
                    int.TryParse(selItems, out int selectedItemsCount);
                    if (selectedItemsCount > 5000)
                        selectedItemsShade = 3;

                    int.TryParse(exclItem, out int excludedCount);
                    if (excludedCount > 50)
                        excludedItemsShade = 3;

                    if (enabled == "False")
                        enabledShade = 3;

                    if (schedul == "Not Scheduled")
                        scheduleShade = 3;

                    s += _form.TableData(org, string.Empty);
                    s += _form.TableData(name, string.Empty);
                    s += _form.TableData(desc, string.Empty);
                    s += _form.TableData(jobType, string.Empty);
                    s += _form.TableData(scopeType, string.Empty);
                    s += _form.TableData(procOpt, string.Empty);
                    s += _form.TableData(selItems, string.Empty, selectedItemsShade);
                    s += _form.TableData(exclItem, string.Empty, excludedItemsShade);
                    s += _form.TableData(repo, string.Empty);
                    s += _form.TableData(boundProxy, string.Empty);
                    s += _form.TableData(enabled, string.Empty, enabledShade);
                    s += _form.TableData(schedul, string.Empty, scheduleShade);
                    s += _form.TableData(relJob, string.Empty);

                    s += "</tr>";
                }
            }
            catch (Exception e)
            {

            }
            //s += "<tr>";
            //s += _form.TableData(counter.ToString(), "");
            //s += "</tr>";



            s += "</table>";

            //s += _summary.JobsSummary();

            s += "</div>";
            return s;
        }

        public string MakeVb365NavTable()
        {
            return _form.FormNavRows(Vb365ResourceHandler.v365NavTitle0, "global", Vb365ResourceHandler.v365NavValue1) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle1, "protstat", Vb365ResourceHandler.v365NavValue1) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle2, "controller", Vb365ResourceHandler.v365NavValue2) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle3, "controllerdrives", Vb365ResourceHandler.v365NavValue3) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle4, "proxies", Vb365ResourceHandler.v365NavValue4) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle5, "repos", Vb365ResourceHandler.v365NavValue5) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle6, "objrepo", Vb365ResourceHandler.v365NavValue6) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle7, "sec", Vb365ResourceHandler.v365NavValue7) +
                //_form.FormNavRows(ResourceHandler.v365NavTitle8, "rbac", ResourceHandler.v365NavValue8) +
                //_form.FormNavRows(ResourceHandler.v365NavTitle9, "perms", ResourceHandler.v365NavValue9) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle10, "orgs", Vb365ResourceHandler.v365NavValue10) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle11, "jobs", Vb365ResourceHandler.v365NavValue11) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle12, "jobstats", Vb365ResourceHandler.v365NavValue12) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle14, "procstats", Vb365ResourceHandler.v365NavValue14) +
                _form.FormNavRows(Vb365ResourceHandler.v365NavTitle13, "jobsessions", Vb365ResourceHandler.v365NavValue13);
        }

    }
}
