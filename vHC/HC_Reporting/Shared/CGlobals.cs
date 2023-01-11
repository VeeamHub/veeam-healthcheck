using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Shared
{
    internal class CGlobals
    {
        // static globals:
        public static CLogger _mainlog = new("HealthCheck");
        private static bool _scrub;
        private static CScrubHandler _scrubberMain = new();
        public static  readonly string _backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8";


        // GUI & CLI Options:
        private static int _reportDays;
        public static string _desiredPath = CVariables.unsafeDir;
        private static bool _openHtml;
        private static bool _openExplorer;
        private static bool _isVbr;
        private static bool _isVb365;
        private static bool _runFullReport;
        private static bool _runSecReport;


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
        public static CLogger Logger { get { return _mainlog; }}
        public static CScrubHandler Scrubber { get { return _scrubberMain; }}

        public static bool OpenHtml { get { return _openHtml; } set { _openHtml = value; } }

        public static bool OpenExplorer { get { return _openExplorer; } set { _openExplorer = value; } }
        //public bool Import { get { return _import; } set { _import = value; } }

        public static bool IsVbr { get { return _isVbr; }set { _isVbr = value; } }
        public static bool IsVb365 { get { return _isVb365;}set { _isVb365 = value;} }
        public static bool RunFullReport { get { return _runFullReport;} set { _runFullReport = value;} }
        public static bool RunSecReport { get { return _runSecReport; } set { _runSecReport = value;} }
    }
}
