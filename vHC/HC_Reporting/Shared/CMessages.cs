using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Shared
{
    internal class CMessages
    {
        private static string ProcEnd = "DONE!";

        public static string helpMenu = "\nHELP MENU:\n" +
            "\t/run\t\tExecutes the program via CLI" +
            "\n" +
            "\t/outdir:\tSpecifies desired location for HTML reports. Usage: \"outdir:D:\\example\". Default = C:\\temp\\vHC" +
            "\n" +
            "\t/days:\t\tSpecifies reporting interval. Choose 7 or 30. 7 is default. USAGE: 'days:30'\n" +
            "\t/gui\t\tStarts GUI. GUI overrides other commands." +
            "\n"+
            //"\t/security\tRuns a modified Health Check reporting only security-related items" +
            //"\n"+
            "\t/lite\t\t" + "Skips output of individual jobs to HTML files. Default is ON and adds extra processing time." +
            "\n"+
            "\t/scrub:\t\t" + "/scrub:true | /scrub:false; determines if sensitive data is removed. Default option creates both options"+
            "\n\n" +
            "EXAMPLES:\n" +
            "1. Run to default location:\t .\\VeeamHealthCheck.exe /run\n" +
            "2. Run to custom location:\t .\\VeeamHealthCheck.exe /run outdir:\\\\myshare\\folder\n" +
            "3. Run with 30 day report:\t .\\VeeamHealthCheck.exe /run days:30\n" +
            "4. Run GUI from CLI:\t .\\VeeamHealthCheck.exe /gui" +
            "5. Run without extra job details:\t .\\VeeamHealthCheck.exe /run /lite"+
            "6. Run report on existing data without starting new collection:\t VeeamHealthCheck.exe /import /run (also works with /security)"+
            "\n";

        public static string PsVbrConfigStart = "[PS] Enter Config Collection Invoker...";
        public static string PsVbrConfigDone = PsVbrConfigStart + ProcEnd;

        public static string PsVbrFunctionStart = "[PS] Enter Function Setter...";
        public static string PsVbrFunctionDone = PsVbrConfigStart + ProcEnd;

        public static string PsVbrConfigStartProc = "[PS][VBR Config] Starting PowerShell Process...";
        public static string PsVbrConfigStartProcDone = PsVbrConfigStartProc+ ProcEnd;

        public static string PsVbrConfigProcId = "[PS][VBR Config] PowerShell Process started with ID: ";
        public static string PsVbrConfigProcIdDone = PsVbrConfigProcId+ ProcEnd;


        public static string FoundHotfixesMessage(List<string> fixes)
        {
            string output = String.Format("\n\nThank you for running the Veeam Hotfix Detector." +
                "\n - HFD version: {0}" +
                "\n - Detected B&R Version: {1}" +
                "\n\n" +
                "The scan has found {2} hotfixes:\n", CVersionSetter.GetFileVersion(), CGlobals.VBRFULLVERSION, fixes.Count);
            foreach(var fix in fixes)
            {
                output += "\n - " + fix;
            }
            output += "\r\n \r\nPlease delay your upgrade until verification has been completed." +
                " To verify your system, please do the following:" +
                "\n\t1. Open a support case" +
                "\n\t2. Set the subject to: " +
                "\n\ta. Hotfix Detector Results" +
                "\n\t3. Paste the following as the body and await a reply from a Support Representative:" +
                "\n\nHello," +
                "\nI have used the Veeam Hotfix Detector on my Veeam Server. " +
                "I would like to verify if the following fixes are included in the " +
                "latest release of Veeam Backup & Replication.";

            foreach (var fix in fixes)
            {
                output += "\n - " + fix;
            }
            output += String.Format("\r\n \r\nPlease check these fixes and let me know if I " +
                "may safely upgrade my system." +
                "\r\n \r\nInstalled B&R Version: {0}" +
                "\r\nHotfix Detector Version: {1}" +
                "\r\n \r\nThank you,", CGlobals.VBRFULLVERSION, CVersionSetter.GetFileVersion());
            //"\r\n4.\tAllow the support representative to return an answer.\r\n"
            return output;
        } 
    }
}
