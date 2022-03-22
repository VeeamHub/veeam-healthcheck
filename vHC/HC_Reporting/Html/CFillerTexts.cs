// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Html
{
    class CFillerTexts
    {
        public string serverSummary = "Server types visible to VBR, typically through their addition in the VBR console, and thereby added to the VBR database.";//"Quick summary of different workloads added into B&R Console";
        public string LicenseSummary =      "";//"Simple breakdown of the installed license.";
        public string BackupServerSummary = "";//"Details about the Backup Server and configuration DB. Some fields may be blank if the user running the HealthCheck does not have adequate SQL permissions.";
        public string SobrSummary =         "";//"Details about each Scale-Out Backup Repository added into B&R";
        public string SobrExtentSummary =   "";//"More granular details about the extents within each SOBR";
        public string RepoSummary =         "";//"Details about standalone repositories not associated with SOBR";
        public string ProxySummary =        "";//"Detailed info of each proxy type added into B&R";
        public string ServerInfo =          "";//"Detailed breakdown of each server that was summarised in the Server Summary chart";
        public string JobSummary =          "";//"Quick count of all detected job types";
        public string JobSessionSummary =   "";//"Detailed information collected from all session history";
        public string JobInfoSummary =      "";//"Details about each job found in B&R";

        //public string overallSummary = String.Format("Thank you for participating in the Veeam HealthCheck Program! Here is a detailed summary of our findings:" +
        //        "Your Backup Server {0}" +
        //        "Your Backup Server has {1} cores and {2} GB of RAM");
    }
}
