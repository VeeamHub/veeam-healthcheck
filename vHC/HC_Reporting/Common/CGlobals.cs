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
        public static CLogger _mainlog = new("HealthCheck");
        private static bool _scrub;
        private static CScrubHandler _scrubberMain = new();
        public static readonly string _backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";
        public static bool IMPORT = false;
        public static int VBRMAJORVERSION;
        public static string VBRFULLVERSION;
        public static DateTime TOOLSTART;
        public static bool REMOTEEXEC = false;
        public static string REMOTEHOST = "";
        public static bool GUIEXEC = false;
        public static string VHCVERSION = "";
        public static bool DEBUG = false;

        // Remote Exec variables
        public static string VBRServerName = "localhost";

        public static string RawReport = "";
        public static string ScrubbedReport = "";
        // GUI & CLI Options:
        private static int _reportDays = 7;
        public static string _desiredPath = CVariables.unsafeDir;
        private static bool _openHtml;
        private static bool _openExplorer;
        private static bool _isVbr;
        private static bool _isVb365;
        private static bool _runFullReport;
        private static bool _runSecReport;
        public static bool EXPORTINDIVIDUALJOBHTMLS = true;
        public static bool CHECKFIXES = false;
        public static bool EXPORTPDF = false;


        // Security Values
        public static bool IsMfaEnabled = false;

        // B&R Server global values
        //public static string isConsoleLocal = "Undetermined";
        //public static string _isRdpEnabled = "Undetermined";
        //public static string _isDomainJoined = "";

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

        public CGlobals()
        {

        }
        public static int ReportDays
        {
            get { return _reportDays; }
            set { _reportDays = value; }
        }
        public static bool Scrub
        {
            get { return _scrub; }
            set { _scrub = value; }
        }
        public static DateTime GetToolStart
        {
            get { if(TOOLSTART == DateTime.MinValue)
                    TOOLSTART = DateTime.Now;
                return TOOLSTART;
            }
            set { TOOLSTART = value; }
        }
        public static CLogger Logger { get { return _mainlog; } }
        public static CScrubHandler Scrubber { get { return _scrubberMain; } }

        public static bool OpenHtml { get { return _openHtml; } set { _openHtml = value; } }

        public static bool OpenExplorer { get { return _openExplorer; } set { _openExplorer = value; } }
        //public bool Import { get { return _import; } set { _import = value; } }

        public static bool IsVbr { get { return _isVbr; } set { _isVbr = value; } }
        public static bool IsVb365 { get { return _isVb365; } set { _isVb365 = value; } }
        public static bool RunFullReport { get { return _runFullReport; } set { _runFullReport = value; } }
        public static bool RunSecReport { get { return _runSecReport; } set { _runSecReport = value; } }
    }
}
