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
            "\t/security\tRuns a modified Health Check reporting only security-related items" +
            "\n"+
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
    }
}
