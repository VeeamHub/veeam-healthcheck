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


                    s += _form.TableData(gl.LicenseStatus, "");

                    if (expireDate < DateTime.Now)
                        s += _form.TableData(gl.LicenseExpiry, "", 1);
                    else if (expireDate < DateTime.Now.AddDays(60))
                        s += _form.TableData(gl.LicenseExpiry, "", 3);
                    else
                        s += _form.TableData(gl.LicenseExpiry, "");

                    s += _form.TableData(gl.SupportExpiry, "");
                    s += _form.TableData(gl.LicenseType, "");
                    if (CGlobals.Scrub)
                    {
                        var licName = CGlobals.Scrubber.ScrubItem(gl.LicensedTo, Scrubber.ScrubItemType.Item);
                        s += _form.TableData(licName, "");

                    }
                    else
                    {
                        s += _form.TableData(gl.LicensedTo, "");

                    }
                    s += _form.TableData(gl.LicenseContact, "");
                    s += _form.TableData(gl.LicensedFor, "");

                    if (percentUsed > 95)
                        s += _form.TableData(gl.LicensesUsed, "", 1);
                    else if (percentUsed > 90)
                        s += _form.TableData(gl.LicensesUsed, "", 3);
                    else
                        s += _form.TableData(gl.LicensesUsed, "");

                    s += _form.TableData(gl.GlobalFolderExclusions, "");
                    s += _form.TableData(gl.GlobalRetExclusions, "");
                    s += _form.TableData(gl.LogRetention, "");
                    if (gl.NotificationEnabled == "False")
                        s += _form.TableData(gl.NotificationEnabled, "", 3);
                    else
                        s += _form.TableData(gl.NotificationEnabled, "");

                    s += _form.TableData(gl.NotifyOn, "");

                    if (gl.AutomaticUpdates == "False")
                        s += _form.TableData(gl.AutomaticUpdates, "", 3);
                    else
                        s += _form.TableData(gl.AutomaticUpdates, "");
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
            s += _form.TableHeader("Proxy Name", "");
            s += _form.TableHeader("Description", "");
            s += _form.TableHeader("Threads", "");
            s += _form.TableHeader("Throttling", Vb365ResourceHandler.proxyTTThrottling);
            s += _form.TableHeader("State", Vb365ResourceHandler.proxyTTState);
            //s += _form.TableHeader("Type", "");
            s += _form.TableHeader("Outdated", Vb365ResourceHandler.proxyTTOutdated);
            s += _form.TableHeader("Internet Proxy", Vb365ResourceHandler.proxyTTInternet);
            s += _form.TableHeader("Objects Managed", Vb365ResourceHandler.proxyTTObjects);
            s += _form.TableHeader("OS Version", "");
            s += _form.TableHeader("RAM", "");
            s += _form.TableHeader("CPUs", Vb365ResourceHandler.proxyTTCPUs);
            s += _form.TableHeader("Extended Logging?", "");

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboProxies().ToList();
                foreach (var gl in global)
                {

                    string proxyname = "";
                    string description = "";
                    string threads = "";
                    string throttling = "";
                    string state = "";
                    string outdated = "";
                    string internetproxy = "";
                    string objectsmanaged = "";
                    string osversion = "";
                    string ram = "";
                    string cpus = "";
                    string extendedlogging = "";

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

                    s += _form.TableData(proxyname, "");
                    s += _form.TableData(description, "");
                    s += _form.TableData(threads, "", threadShade);
                    s += _form.TableData(throttling, "", throttleShade);
                    s += _form.TableData(state, "", stateShade);
                    s += _form.TableData(outdated, "", outdatedShade);
                    s += _form.TableData(internetproxy, "", proxyShade);
                    s += _form.TableData(objectsmanaged, "", objManagedShade);
                    s += _form.TableData(osversion, "", osVersionShade);
                    s += _form.TableData(ram, "", ramShade);
                    s += _form.TableData(cpus, "", cpuShade);
                    s += _form.TableData(extendedlogging, "");

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
                    s += _form.TableData(boundProxy, "");
                    s += _form.TableData(name, "");
                    s += _form.TableData(desc, "");
                    s += _form.TableData(g.Type, "");
                    s += _form.TableData(path, "", pathShade);
                    s += _form.TableData(objRepo, "", objencshade);
                    s += _form.TableData(g.Encryption, "", objencshade);
                    s += _form.TableData(g.State, "", stateShade);
                    s += _form.TableData(g.Capacity, "");
                    s += _form.TableData(g.Free, "", freeShade);
                    s += _form.TableData(g.DataStored, "");
                    s += _form.TableData(g.CacheSpaceUsed, "");
                    s += _form.TableData(g.DailyChangeRate, "");
                    s += _form.TableData(g.Retention, "");
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
            s += _form.TableHeader("Name", "");
            s += _form.TableHeader("Description", "");
            s += _form.TableHeader("Role Type", "");
            s += _form.TableHeader("Operators", "");
            s += _form.TableHeader("Selected Items", "");
            s += _form.TableHeader("Excluded Items", "");

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
                        s += _form.TableData(output, "");
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
            s += _form.TableHeader("RBAC Roles Defined", "");
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
                    s += _form.TableData(g.WinFirewallEnabled, "");
                    s += _form.TableData(g.Internetproxy, "");
                    s += _form.TableData(rbacRowsCount.ToString(), "");
                    s += "</tr>";

                    s += "</table><table border =\"1\"><tr><br/>";
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column1, Vb365ResourceHandler.securityTTServices);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column2, "");
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column3, Vb365ResourceHandler.securityTTPort);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column4, Vb365ResourceHandler.securityTTCert);
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column5, "");
                    s += _form.TableHeader(Vb365ResourceHandler.SecurityTable2Column6, "");
                    s += "</tr><tr>";
                    s += _form.TableData("Server", "");
                    s += _form.TableData("", "");// enabled
                    s += _form.TableData("", "");// port
                    s += _form.TableData(serverCert, "");// cert
                    s += _form.TableData(g.ServerCertExpires, "");// expires
                    s += _form.TableData(g.ServerCertSelfSigned, "", serverCertSignShade);// self signed
                    s += "</tr><tr>";

                    s += _form.TableData("API", "");
                    s += _form.TableData(g.APIEnabled, "");
                    s += _form.TableData(g.APIPort, "");
                    s += _form.TableData(apiCert, "");
                    s += _form.TableData(g.APICertExpires, "");
                    s += _form.TableData(g.APICertSelfSigned, "", apiCertSignShade);
                    s += "</tr><tr>";
                    s += _form.TableData("Tenant Auth", "");
                    s += _form.TableData(g.TenantAuthEnabled, "");
                    s += _form.TableData("", "");
                    s += _form.TableData(tenantCert, "");
                    s += _form.TableData(g.TenantAuthCertExpires, "");
                    s += _form.TableData(g.TenantAuthCertSelfSigned, "", tenantCertSignShade);
                    s += "</tr><tr>";
                    s += _form.TableData("Restore Portal", "");
                    s += _form.TableData(g.RestorePortalEnabled, "");
                    s += _form.TableData("", "");
                    s += _form.TableData(portalCert, "");
                    s += _form.TableData(g.RestorePortalCertExpires, "");
                    s += _form.TableData(g.RestorePortalCertSelfSigned, "", portalCertSignShade);
                    s += "</tr>";
                    s += "</tr><tr>";
                    s += _form.TableData("Operator Auth", "");
                    s += _form.TableData(g.OperatorAuthEnabled, "");
                    s += _form.TableData("", "");
                    s += _form.TableData(operatorCert, "");
                    s += _form.TableData(g.OperatorAuthCertExpires, "");
                    s += _form.TableData(g.OperatorAuthCertSelfSigned, "", operatorCertSignShade);
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
                    string vbVersion = "";// g.Select(x => x.Key).Where(y=>y == "vb365version");
                    string osVersion = "";
                    string ram = "";
                    string cpu = "";
                    string proxies = "";
                    string repos = "";
                    string orgs = "";
                    string jobs = "";
                    string psEnabled = "";
                    string proxyInstalled = "";
                    string restEnabled = "";
                    string consoleInstalled = "";
                    string vmName = "";
                    string vmLoc = "";
                    string vmSku = "";
                    string vmSize = "";
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

                    s += _form.TableData(vbVersion, "");
                    s += _form.TableData(osVersion, "", osShade);
                    s += _form.TableData(ram, "", ramShade);
                    s += _form.TableData(cpu, "", cpuShade);
                    s += _form.TableData(proxies, "");
                    s += _form.TableData(repos, "");
                    s += _form.TableData(orgs, "");
                    s += _form.TableData(jobs, "");
                    s += _form.TableData(psEnabled, "");
                    s += _form.TableData(proxyInstalled, "");
                    s += _form.TableData(restEnabled, "");
                    s += _form.TableData(consoleInstalled, "");
                    s += _form.TableData(vmName, "");
                    s += _form.TableData(vmLoc, "");
                    s += _form.TableData(vmSku, "");
                    s += _form.TableData(vmSize, "");
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
                    string friendlyname = "";
                    string deviceid = "";
                    string bustype = "";
                    string mediatype = "";
                    string manufacturer = "";
                    string model = "";
                    string size = "";
                    string allocatedsize = "";
                    string operationalstatus = "";
                    string healthstatus = "";
                    string bootdrive = "";

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


                    s += _form.TableData(friendlyname, "");
                    s += _form.TableData(deviceid, "");
                    s += _form.TableData(bustype, "");
                    s += _form.TableData(mediatype, "", mediaTypeShade);
                    s += _form.TableData(manufacturer, "");
                    s += _form.TableData(model, "");
                    s += _form.TableData(size, "");
                    s += _form.TableData(allocatedsize, "");
                    s += _form.TableData(operationalstatus, "", opStatShade);
                    s += _form.TableData(healthstatus, "", healthStatShade);
                    s += _form.TableData(bootdrive, "");

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
                        s += _form.TableData(output, "");
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
            s += _form.TableHeader("Name", "");
            s += _form.TableHeader("Operation", "");
            s += _form.TableHeader("Time (latest)", "");
            s += _form.TableHeader("Time (Median)", "");
            s += _form.TableHeader("Time (Min)", "");
            s += _form.TableHeader("Time (Avg)", "");
            s += _form.TableHeader("Time (Max)", "");
            s += _form.TableHeader("Time (90%)", "");

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
                        s += _form.TableData(output, "");
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
                        s += _form.TableData(output, "");
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

                    string name = "";
                    string description = "";
                    string cloud = "";
                    string type = "";
                    string bucketcontainer = "";
                    string path = "";
                    string sizelimit = "";
                    string usedspace = "";
                    string freespace = "";
                    string boundrepo = "";


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

                    s += _form.TableData(name, "");
                    s += _form.TableData(description, "");
                    s += _form.TableData(cloud, "");
                    s += _form.TableData(type, "");
                    s += _form.TableData(bucketcontainer, "");
                    s += _form.TableData(path, "");
                    s += _form.TableData(sizelimit, "");
                    s += _form.TableData(usedspace, "");
                    s += _form.TableData(freespace, "");
                    s += _form.TableData(boundrepo, "", boundRepoShade);
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
                        s += _form.TableData(output, "");
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
                        s += _form.TableData(output, "");
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
            s += _form.TableHeader("Total Users", "");
            s += _form.TableHeader("Protected Users", "");
            s += _form.TableHeader("Unprotected Users", "");
            s += _form.TableHeader("Stale Backups", "");
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
                s += _form.TableData((protectedUsers + notProtectedUsers + stale).ToString(), "");
                s += _form.TableData(protectedUsers.ToString(), "");
                s += _form.TableData(notProtectedUsers.ToString(), "", shade);
                s += _form.TableData(stale.ToString(), "");
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
            s += _form.TableHeader("Processing Options", "");
            s += _form.TableHeader("Selected Objects", Vb365ResourceHandler.jobsTTSelectedItems);
            s += _form.TableHeader("Excluded Objects", Vb365ResourceHandler.jobsTTExcludedItems);
            s += _form.TableHeader("Repository", Vb365ResourceHandler.jobsTTRepository);
            s += _form.TableHeader("Bound Proxy", Vb365ResourceHandler.jobsTTBoundProxy);
            s += _form.TableHeader("Enabled?", "");
            s += _form.TableHeader("Schedule", "");
            s += _form.TableHeader("Related Job", Vb365ResourceHandler.jobsTTRelatedJob);

            s += "</tr>";
            try
            {

                var global = _csv.GetDynamicVboJobs().ToList();


                foreach (var gl in global)
                {
                    var org = "";
                    var name = "";
                    var desc = "";
                    var jobType = "";
                    var scopeType = "";
                    var procOpt = "";
                    var selItems = "";
                    var exclItem = "";
                    var repo = "";
                    var boundProxy = "";
                    var enabled = "";
                    var schedul = "";
                    var relJob = "";

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

                    s += _form.TableData(org, "");
                    s += _form.TableData(name, "");
                    s += _form.TableData(desc, "");
                    s += _form.TableData(jobType, "");
                    s += _form.TableData(scopeType, "");
                    s += _form.TableData(procOpt, "");
                    s += _form.TableData(selItems, "", selectedItemsShade);
                    s += _form.TableData(exclItem, "", excludedItemsShade);
                    s += _form.TableData(repo, "");
                    s += _form.TableData(boundProxy, "");
                    s += _form.TableData(enabled, "", enabledShade);
                    s += _form.TableData(schedul, "", scheduleShade);
                    s += _form.TableData(relJob, "");

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
