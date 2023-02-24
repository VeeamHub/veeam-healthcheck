﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Security
{
    internal class CSecurityGlobalValues
    {

        // backup server values
        public static string IsConsoleInstalled = "Undetermined";
        public static string IsRdpEnabled = "Undetermined";
        public static string IsDomainJoined;

        // config backup
        public static bool ConfigBackupEnabled;
        public static string ConfigBackupSuccess;
        public static bool ConfigBackupEncrypted;

        // Server info
        public static List<string> OperatingSystemsInUse = new();


        //SOBR INFO
        public static bool CapacityTierUsed;
        public static bool ArchiveTierUsed;
        public static bool CapTierImmutable;
        public static bool ArchiveTierImmutable;
        public static bool AreAllCaptierImmutable;
        public static bool AreAllArchiveTierImmutable;

        // extent info
        public static bool AreAllExtentsImmutable;



        // standardRepoInfo
        public static bool AreAllReposImmutable;

        //job info
        public static bool AreAllJobsEncrypted;

        //public CSecurityGlobalValues()
        //{
        //    IsConsoleInstalled = CGlobals.isConsoleLocal;
        //}
    }
}
