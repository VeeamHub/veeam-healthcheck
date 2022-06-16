using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.CsvHandlers;
using VeeamHealthCheck.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization.VB365;

namespace VeeamHealthCheck.Reporting.Html.VB365
{
    internal class CM365Tables
    {
        private CHtmlFormatting _form = new();
        private CCsvParser _csv = new(CVariables.vb365dir);
        private CM365Summaries _summary = new CM365Summaries();
        private Scrubber.CXmlHandler _scrubber = new();
        public CM365Tables()
        {

        }
        public string Globals()
        {
            string s = "<div class=\"global\" id=\"global\">";
            s += _form.header2("Global Configuration");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicStatus, "License Status");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicExp, "License Expiry");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicType, "License Type");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicTo, "Licensed To");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicContact, "License Contact");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicFor, "Licensed For");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadLicUsed, "Licenses Used");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadSupExp, "Support Expiry");
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadGFolderExcl, Vb365ResourceHandler.GlobalFolderExclTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadGRetExcl, Vb365ResourceHandler.GlobalRetExclTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadSessHisRet, Vb365ResourceHandler.GlobalLogRetTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyEnabled, Vb365ResourceHandler.GlobalNotificationEnabledTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadNotifyOn, Vb365ResourceHandler.GlobalNotifyOnTT);
            s += _form.TableHeader(Vb365ResourceHandler.GlobalColHeadAutoUpdate, "Automatic Updates?");
            s += "</tr>";

            var global = _csv.GetDynamicVboGlobal().ToList();
            s += "<tr>";
            foreach (var gl in global)
            {
                int counter = 0;
               // List<string> g2 = gl;
                
                //s += _form.TableData("","");
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 4)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }
            }
            s += "</tr></table>";

            // summary
            s += _summary.GlobalSummary();

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
            s += _form.TableHeader("Throttling", "");
            s += _form.TableHeader("State", "");
            s += _form.TableHeader("Type", "");
            s += _form.TableHeader("Outdated", "");
            s += _form.TableHeader("Internet Proxy", "");
            s += _form.TableHeader("Objects Managed", "");
            s += _form.TableHeader("OS Version", "");
            s += _form.TableHeader("RAM", "");
            s += _form.TableHeader("CPUs", "");

            s += "</tr>";

            var global = _csv.GetDynamicVboProxies().ToList();
            foreach (var gl in global)
            {
                int counter = 0;

                s += "<tr>";

                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 || counter == 1)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;

                }

                s += "</tr>";
            }
            s += "</table>";
            s += _summary.ProxySummary();

            s += "</div>";
            return s;
        }
        public string Vb365Repos()
        {
            string s = "<div class=\"repos\" id=\"repos\">";
            s += _form.header2("Repositories");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Bound Proxy", "Bound Proxy");
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Description", "Description");
            s += _form.TableHeader("Type", "Type");
            s += _form.TableHeader("Path", "Path");
            s += _form.TableHeader("Object Repo", "Object Repo");
            s += _form.TableHeader("Encryption?", "Encryption?");
            s += _form.TableHeader("Out of Sync?", "Out of Sync?");
            s += _form.TableHeader("Outdated?", "Outdated?");
            s += _form.TableHeader("Capacity", "Capacity");
            s += _form.TableHeader("Free", "");
            s += _form.TableHeader("Data Stored", "");
            s += _form.TableHeader("Cache Space Used", "Cache Space Used");
            s += _form.TableHeader("Local Space Used", "Local Space Used");
            s += _form.TableHeader("Object Space Used", "Object Space Used");
            s += _form.TableHeader("Daily Change", "Daily Change");
            s += _form.TableHeader("Retention", "Retention");

            s += "</tr>";

            var global = _csv.GetDynamicVboRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";
                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 || counter == 1 || counter == 2
                            || counter == 4
                            || counter == 5)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.RepoSummary();
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

            var global = _csv.GetDynamicVboRbac().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.RbacSummary();
            s += "</div>";
            return s;
        }
        public string Vb365Security()
        {
            string s = "<div class=\"sec\" id=\"sec\">";
            s += _form.header2("Security Info");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Win. Firewall Enabled?", "Win. Firewall Enabled?");
            s += _form.TableHeader("Internet proxy?", "Internet proxy?");


            s += "</tr>";

            var global = _csv.GetDynamicVboSec().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                int certcounter = 0;
                int nameIterator = 0;
                foreach (var g in gl)
                {
                    if (counter == 2)
                    {
                        s += "</table><table border =\"1\"><tr><br/>";
                        s += _form.TableHeader("Service", "Enabled");
                        s += _form.TableHeader("Enabled", "Enabled");
                        s += _form.TableHeader("Port", "Server Cert");
                        s += _form.TableHeader("Cert", "Server Cert PK Exportable?");
                        // s += _form.TableHeader("Exportable", "Server Cert Expires");
                        s += _form.TableHeader("Expires", "Server Cert Self-Signed?");
                        s += _form.TableHeader("Self-Signed", "API Enabled?");
                        //s += _form.TableHeader("API Port", "API Port");
                        //s += _form.TableHeader("API Cert", "API Cert");
                        //s += _form.TableHeader("API Cert PK Exportable?", "API Cert PK Exportable?");
                        //s += _form.TableHeader("API Cert Expires", "API Cert Expires");
                        //s += _form.TableHeader("API Cert Self-Signed?", "API Cert Self-Signed?");
                        //s += _form.TableHeader("Tenant Auth Enabled?", "Tenant Auth Enabled?");
                        //s += _form.TableHeader("Tenant Auth Cert", "Tenant Auth Cert");
                        //s += _form.TableHeader("Tenant Auth PK Exportable?", "Tenant Auth PK Exportable?");
                        //s += _form.TableHeader("Tenant Auth Cert Expires", "Tenant Auth Cert Expires");
                        //s += _form.TableHeader("Tenant Auth Cert Self-Signed?", "Tenant Auth Cert Self-Signed?");
                        //s += _form.TableHeader("Restore Portal Enabled?", "Restore Portal Enabled?");
                        //s += _form.TableHeader("Restore Portal Cert", "Restore Portal Cert");
                        //s += _form.TableHeader("Restore Portal Cert PK Exportable?", "Restore Portal Cert PK Exportable?");
                        //s += _form.TableHeader("Restore Portal Cert Expires", "Restore Portal Cert Expires");
                        //s += _form.TableHeader("Restore Portal Cert Self-Signed?", "Restore Portal Cert Self-Signed?");
                        //s += _form.TableHeader("Operator Auth Enabled?", "Operator Auth Enabled?");
                        //s += _form.TableHeader("Operator Auth Cert", "Operator Auth Cert");
                        //s += _form.TableHeader("Operator Auth Cert PK Exportable?", "Operator Auth Cert PK Exportable?");
                        //s += _form.TableHeader("Operator Auth Cert Expires", "Operator Auth Cert Expires");
                        //s += _form.TableHeader("Operator Auth Cert Self-Signed?", "Operator Auth Cert Self-Signed?");
                        s += "</tr><tr>";
                        s += _form.TableData("Server", "");
                        s += _form.TableData("", "");
                        s += _form.TableData("", "");
                        certcounter++;
                    }
                    if (certcounter == 7 ||
                        certcounter == 13 ||
                        certcounter == 18 ||
                        certcounter == 23)
                    {
                        s += "</tr><tr>";
                        if (nameIterator == 0)
                            s += _form.TableData("API", "");
                        if (nameIterator == 1)
                            s += _form.TableData("Tenant Auth", "");
                        if (nameIterator == 2)
                            s += _form.TableData("Restore Portal", "");
                        if (nameIterator == 3)
                            s += _form.TableData("Operator Auth", "");

                        nameIterator++;
                    }
                    if (certcounter == 15)
                        s += _form.TableData("", "");
                    if (certcounter == 4)
                    {
                        counter++;
                        certcounter++;
                        continue;
                    }
                    if (certcounter == 10)
                    {
                        counter++;
                        certcounter++;
                        continue;
                    }
                    //skipped on purpose
                    if (certcounter == 16) { counter++; certcounter++; continue; }
                    //skipped on purpose
                    if (certcounter == 19)
                        s += _form.TableData("", "");
                    if (certcounter == 20) { counter++; certcounter++; continue; }
                    //skipped on purpose
                    if (certcounter == 24)
                        s += _form.TableData("", "");
                    if (certcounter == 25) { counter++; certcounter++; continue; }
                    //skipped on purpose


                    string output = g.Value;

                    // 6 columns: Enabled? Port Cert Exportable? Expires Self-Signed
                    if (MainWindow._scrub)
                    {
                        if (counter == 2 ||
                            counter == 8 ||
                            counter == 13 ||
                            counter == 18 ||
                            counter == 23)
                            output = _scrubber.ScrubItem(output);
                    }
                    else
                    {
                        s += _form.TableData(output, "");

                    }
                    //s += _form.TableData(output, "");
                    counter++;
                    certcounter++;
                }

                s += "</tr>";
            }
            s += "</table>";
            s += _summary.SecSummary();

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

            var global = _csv.GetDynVboController().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 12 || counter == 13)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";
            s += _summary.ControllerSummary();

            s += "</div>";
            return s;
        }
        public string Vb365ControllerDrivers()
        {
            string s = "<div class=\"controllerdrives\" id=\"controllerdrives\">";
            s += _form.header2("Backup Server Disks");
            s += "<br>";
            s += "<table border=\"1\"><tr>";
            s += _form.TableHeader("Friendly Name", "Friendly Name");
            s += _form.TableHeader("DeviceId", "DeviceId");
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

            var global = _csv.GetDynVboControllerDriver().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                foreach (var g in gl)
                    s += _form.TableData(g.Value, "");

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.ControllerDrivesSummary();
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

            var global = _csv.GetDynVboJobSess().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 || counter == 5)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.JobSessSummary();

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

            var global = _csv.GetDynVboJobStats().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.JobStatSummary();
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

            var global = _csv.GetDynVboObjRepo().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 ||
                            counter == 1 ||
                            counter == 4 ||
                            counter == 5)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.ObjRepoSummary();

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
            s += _form.TableHeader("EXO Settings", "EXO Settings");
            s += _form.TableHeader("EXO App Cert", "EXO App Cert");
            s += _form.TableHeader("SPO Settings", "SPO Settings");
            s += _form.TableHeader("SPO App Cert", "SPO App Cert");
            s += _form.TableHeader("On-Prem Exch Settings", "On-Prem Exch Settings");
            s += _form.TableHeader("On-Prem SP Settings", "On-Prem SP Settings");
            s += _form.TableHeader("Licensed Users", "Licensed Users");
            s += _form.TableHeader("Grant SC Admin", "Grant SC Admin");
            s += _form.TableHeader("Aux Accounts/Apps", "Aux Accounts/Apps");

            s += "</tr>";

            var global = _csv.GetDynVboOrg().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 ||
                            counter == 1 ||
                            counter == 4 ||
                            counter == 5 ||
                            counter == 6 ||
                            counter == 8 ||
                            counter == 9)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";

            s += _summary.OrgSummary();
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

            var global = _csv.GetDynVboPerms().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 1)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            s += "</table>";
            s += _summary.PermissionSummary();

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
            var global = _csv.GetDynVboProtStat().ToList();
            var lic = _csv.GetDynamicVboGlobal().ToList();
            foreach (var li in lic)
            {
                string s2 = li.licensesused;
                double.TryParse(s2, out protectedUsers);
            }
            //int.TryParse(p, out int protectedUsers);
            foreach (var gl in global)
            {
                //s += "<tr>";
                if (gl.protectionstatus == "Unprotected")
                    notProtectedUsers++;
                if (gl.protectionstatus == "Stale Backup")
                    stale++;
            }

            double percent = notProtectedUsers / (notProtectedUsers + protectedUsers) * 100;
            double targetPercent = 20;
            int shade = 0;
            if (percent > targetPercent)
            {
                shade = 1;
            }

            s += "<tr>";
            s += _form.TableData((protectedUsers + notProtectedUsers).ToString(), "");
            s += _form.TableData(protectedUsers.ToString(), "");
            s += _form.TableData(notProtectedUsers.ToString(), "", shade);
            s += _form.TableData(stale.ToString(), "");
            s += "</tr>";




            s += "</table>";

            s += _summary.ProtStatSummary();

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
            s += _form.TableHeader("Organization", "Organization");
            s += _form.TableHeader("Name", "Name");
            s += _form.TableHeader("Description", "Description");
            s += _form.TableHeader("Job Type", "Job Type");
            s += _form.TableHeader("Scope Type", "Scope Type");
            s += _form.TableHeader("Processing Options", "");
            s += _form.TableHeader("Selected Items", "Selected Items");
            s += _form.TableHeader("Excluded Items", "Excluded Items");
            s += _form.TableHeader("Repository", "Repository");
            s += _form.TableHeader("Bound Proxy", "Bound Proxy");
            s += _form.TableHeader("Enabled?", "Enabled?");
            s += _form.TableHeader("Schedule", "Schedule");
            s += _form.TableHeader("Related Job", "Related Job");

            s += "</tr>";

            var global = _csv.GetDynamicVboJobs().ToList();
            foreach (var gl in global)
            {
                s += "<tr>";

                int counter = 0;
                foreach (var g in gl)
                {
                    string output = g.Value;
                    if (MainWindow._scrub)
                    {
                        if (counter == 0 ||
                            counter == 1 ||
                            counter == 2 ||
                            counter == 7 ||
                            counter == 8 ||
                            counter == 11)
                            output = _scrubber.ScrubItem(output);
                    }
                    s += _form.TableData(output, "");
                    counter++;
                }

                s += "</tr>";
            }
            //s += "<tr>";
            //s += _form.TableData(counter.ToString(), "");
            //s += "</tr>";



            s += "</table>";

            s += _summary.ProtStatSummary();

            s += "</div>";
            return s;
        }
        public string MakeVb365NavTable()
        {
            return _form.FormNavRows(ResourceHandler.v365NavTitle0, "global", ResourceHandler.v365NavValue1) +
                _form.FormNavRows(ResourceHandler.v365NavTitle1, "protstat", ResourceHandler.v365NavValue1) +
                _form.FormNavRows(ResourceHandler.v365NavTitle2, "controller", ResourceHandler.v365NavValue2) +
                _form.FormNavRows(ResourceHandler.v365NavTitle3, "controllerdrives", ResourceHandler.v365NavValue3) +
                _form.FormNavRows(ResourceHandler.v365NavTitle4, "proxies", ResourceHandler.v365NavValue4) +
                _form.FormNavRows(ResourceHandler.v365NavTitle5, "repos", ResourceHandler.v365NavValue5) +
                _form.FormNavRows(ResourceHandler.v365NavTitle6, "objrepo", ResourceHandler.v365NavValue6) +
                _form.FormNavRows(ResourceHandler.v365NavTitle7, "sec", ResourceHandler.v365NavValue7) +
                _form.FormNavRows(ResourceHandler.v365NavTitle8, "rbac", ResourceHandler.v365NavValue8) +
                //_form.FormNavRows(ResourceHandler.v365NavTitle9, "perms", ResourceHandler.v365NavValue9) +
                _form.FormNavRows(ResourceHandler.v365NavTitle10, "orgs", ResourceHandler.v365NavValue10) +
                _form.FormNavRows(ResourceHandler.v365NavTitle11, "jobs", ResourceHandler.v365NavValue11) +
                _form.FormNavRows(ResourceHandler.v365NavTitle12, "jobstats", ResourceHandler.v365NavValue12) +
                _form.FormNavRows(ResourceHandler.v365NavTitle13, "jobsessions", ResourceHandler.v365NavValue13);
        }

    }
}
