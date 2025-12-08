// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Shared
{
    public class CGlobals
    {
        // static globals:
        public static CLogger mainlog = new("HealthCheck");
        private static bool scrub;
        private static readonly CScrubHandler scrubberMain = new();
        public static readonly string backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";
        public static bool IMPORT = false;
        public static int VBRMAJORVERSION;
        public static string VBRFULLVERSION;
        public static int PowerShellVersion;
        public static DateTime TOOLSTART;
        public static bool REMOTEEXEC = false;
        public static string REMOTEHOST = string.Empty;
        public static bool GUIEXEC = false;
        public static string VHCVERSION = string.Empty;
        public static bool DEBUG = false;

        // Remote Exec variables
        public static string VBRServerName = "localhost";

        public static string RawReport = string.Empty;
        public static string ScrubbedReport = string.Empty;

        // GUI & CLI Options:
        private static int reportDays = 7;
        private static string _desiredPath = CVariables.unsafeDir;
        private static bool openHtml;
        private static bool openExplorer;
        private static bool isVbr;
        private static bool isVb365;
        private static bool runFullReport;
        private static bool runSecReport;
        public static bool EXPORTINDIVIDUALJOBHTMLS = true;
        public static bool CHECKFIXES = false;
        public static bool EXPORTPDF = false;
        public static bool ClearStoredCreds = false;
        public static bool RunningWithoutAdmin = false;

        // Security Values
        public static bool IsMfaEnabled = false;

        // B&R Server global values
        // public static string isConsoleLocal = "Undetermined";
        // public static string _isRdpEnabled = "Undetermined";
        // public static string _isDomainJoined = "";

        // config db values
        public static BackupServer BACKUPSERVER;
        public static string ISDBLOCAL;
        public static string DBTYPE;
        public static readonly string SqlTypeName = "MS SQL";
        public static readonly string PgTypeName = "PG SQL";
        public static string DBNAME;
        public static string DBVERSION;
        public static string DBHOSTNAME;
        public static string DBEdition;
        public static string DBINSTANCE;
        public static int DBCORES;
        public static int DBRAM;

        // collections
        public static Dictionary<string, Object> DEFAULTREGISTRYKEYS = new Dictionary<string, Object>();
        public static List<CServerTypeInfos> ServerInfo = new();
        public static CDataTypesParser DtParser;

        public static bool IsReconDetected = false;
        public static DateTime LastReconRun = DateTime.MinValue;

        // JSON aggregation object for full report output
        internal static CFullReportJson FullReportJson = new();

        public CGlobals()
        {
        }

        public static int ReportDays
        {
            get { return reportDays; }
            set { reportDays = value; }
        }

        public static bool Scrub
        {
            get { return scrub; }
            set { scrub = value; }
        }

        public static DateTime GetToolStart
        {
            get { if(TOOLSTART == DateTime.MinValue)
                {
                    TOOLSTART = DateTime.Now;
                }


                return TOOLSTART;
            }

            set { TOOLSTART = value; }
        }

        public static CLogger Logger { get { return mainlog; } }

        public static CScrubHandler Scrubber { get { return scrubberMain; } }

        public static bool OpenHtml { get { return openHtml; } set { openHtml = value; } }

        public static bool OpenExplorer { get { return openExplorer; } set { openExplorer = value; } }

        // public bool Import { get { return _import; } set { _import = value; } }
        public static bool IsVbr { get { return isVbr; } set { isVbr = value; } }

        public static bool IsVb365 { get { return isVb365; } set { isVb365 = value; } }

        public static bool RunFullReport { get { return runFullReport; } set { runFullReport = value; } }

        public static bool RunSecReport { get { return runSecReport; } set { runSecReport = value; } }

        // Add a public property to match callers using 'desiredPath'
        public static string desiredPath
        {
            get => _desiredPath;
            set => _desiredPath = value;
        }
    }
}
