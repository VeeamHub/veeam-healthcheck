using System.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Resources.Localization.VB365
{
    class Vb365ResourceHandler
    {
        private static ResourceManager vb365res = new("VeeamHealthCheck.Resources.Localization.VB365.vb365_vhcres", typeof(ResourceHandler).Assembly);

        public static string GlobalHeader = vb365res.GetString("GlobalHeader");
        public static string GlobalColHeadLicStatus = vb365res.GetString("GlobalColHeadLicStatus");
        public static string GlobalColHeadLicExp = vb365res.GetString("GlobalColHeadLicExp");
        public static string GlobalColHeadLicType = vb365res.GetString("GlobalColHeadLicType");
        public static string GlobalColHeadLicTo = vb365res.GetString("GlobalColHeadLicTo");
        public static string GlobalColHeadLicContact = vb365res.GetString("GlobalColHeadLicContact");
        public static string GlobalColHeadLicFor = vb365res.GetString("GlobalColHeadLicFor");
        public static string GlobalColHeadLicUsed = vb365res.GetString("GlobalColHeadLicUsed");
        public static string GlobalColHeadSupExp = vb365res.GetString("GlobalColHeadSupExp");
        public static string GlobalColHeadGFolderExcl = vb365res.GetString("GlobalColHeadGFolderExcl");
        public static string GlobalColHeadGRetExcl = vb365res.GetString("GlobalColHeadGRetExcl");
        public static string GlobalColHeadSessHisRet = vb365res.GetString("GlobalColHeadSessHisRet");
        public static string GlobalColHeadNotifyEnabled = vb365res.GetString("GlobalColHeadNotifyEnabled");
        public static string GlobalColHeadNotifyOn = vb365res.GetString("GlobalColHeadNotifyOn");
        public static string GlobalColHeadAutoUpdate = vb365res.GetString("GlobalColHeadAutoUpdate");
        public static string GlobalSummaryHeader = vb365res.GetString("GlobalSummaryHeader");
        public static string GlobalSummary1 = vb365res.GetString("GlobalSummary1");
        public static string GlobalNotesHeader = vb365res.GetString("GlobalNotesHeader");
        public static string GlobalNotes1 = vb365res.GetString("GlobalNotes1");
        public static string GlobalNotes2 = vb365res.GetString("GlobalNotes2");
        public static string GlobalNotes3 = vb365res.GetString("GlobalNotes3");
        public static string GlobalNotes4 = vb365res.GetString("GlobalNotes4");
        public static string GlobalNotes5 = vb365res.GetString("GlobalNotes5");
        public static string GlobalNotes6 = vb365res.GetString("GlobalNotes6");
        public static string GlobalFolderExclTT = vb365res.GetString("GlobalFolderExclTT");
        public static string GlobalRetExclTT = vb365res.GetString("GlobalRetExclTT");
        public static string GlobalLogRetTT = vb365res.GetString("GlobalLogRetTT");
        public static string GlobalNotificationEnabledTT = vb365res.GetString("GlobalNotificationEnabledTT");
        public static string GlobalNotifyOnTT = vb365res.GetString("GlobalNotifyOnTT");
        public static string BkpSrvDisksHeader = vb365res.GetString("BkpSrvDisksHeader");
        public static string BkpSrvDisksSummaryHeader = vb365res.GetString("BkpSrvDisksSummaryHeader");
        public static string BkpSrvDisksSummary1 = vb365res.GetString("BkpSrvDisksSummary1");
        public static string BkpSrvDisksSummary2 = vb365res.GetString("BkpSrvDisksSummary2");
        public static string BkpSrvDisksNotesHeader = vb365res.GetString("BkpSrvDisksNotesHeader");
        public static string BkpSrvDisksNotes1 = vb365res.GetString("BkpSrvDisksNotes1");
        public static string BkpSrvDisksNotes2 = vb365res.GetString("BkpSrvDisksNotes2");
        public static string BkpSrvDisksDeviceIdTT = vb365res.GetString("BkpSrvDisksDeviceIdTT");
        public static string BkpSrvDisksAllocatedSize = vb365res.GetString("BkpSrvDisksAllocatedSize");
        public static string BkpSrvDisksSizePhysical = vb365res.GetString("BkpSrvDisksSizePhysical");
        public static string ObjStgHeader = vb365res.GetString("ObjStgHeader");
        public static string ObjStgSummaryHeader = vb365res.GetString("ObjStgSummaryHeader");
        public static string ObjStgSummary1 = vb365res.GetString("ObjStgSummary1");
        public static string ObjStgNotesHeader = vb365res.GetString("ObjStgNotesHeader");
        public static string ObjStgNotes1 = vb365res.GetString("ObjStgNotes1");
        public static string ObjStgNotes2 = vb365res.GetString("ObjStgNotes2");
        public static string ObjStgNotes3 = vb365res.GetString("ObjStgNotes3");
        public static string ObjStgNotes4 = vb365res.GetString("ObjStgNotes4");
        public static string ObjStgNotes5 = vb365res.GetString("ObjStgNotes5");
        public static string ObjStgNotes6 = vb365res.GetString("ObjStgNotes6");
        public static string ObjStgType = vb365res.GetString("ObjStgType");
        public static string ObjStgPath = vb365res.GetString("ObjStgPath");
        public static string ObjStgSizeLimit = vb365res.GetString("ObjStgSizeLimit");
        public static string ObjStgFreeSpace = vb365res.GetString("ObjStgFreeSpace");
        public static string JobStatHeader = vb365res.GetString("JobStatHeader");
        public static string JobStatSummaryHeader = vb365res.GetString("JobStatSummaryHeader");
        public static string JobStatSummary1 = vb365res.GetString("JobStatSummary1");
        public static string JobStatSummary2 = vb365res.GetString("JobStatSummary2");
        public static string JobStatSummary3 = vb365res.GetString("JobStatSummary3");
        public static string JobStatSummary4 = vb365res.GetString("JobStatSummary4");
        public static string JobStatSummary5 = vb365res.GetString("JobStatSummary5");
        public static string JobStatSummary6 = vb365res.GetString("JobStatSummary6");
        public static string JobStatSummary7 = vb365res.GetString("JobStatSummary7");
        public static string JobStatSummary8 = vb365res.GetString("JobStatSummary8");
        public static string JobStatNoteHeader = vb365res.GetString("JobStatNoteHeader");
        public static string JobStatNote1 = vb365res.GetString("JobStatNote1");
        public static string JobStatNote2 = vb365res.GetString("JobStatNote2");
        public static string JobStatNote3 = vb365res.GetString("JobStatNote3");
        public static string JobStatNote4 = vb365res.GetString("JobStatNote4");
        public static string JobStatNote5 = vb365res.GetString("JobStatNote5");
        public static string JobStatNote6 = vb365res.GetString("JobStatNote6");


    }
}
