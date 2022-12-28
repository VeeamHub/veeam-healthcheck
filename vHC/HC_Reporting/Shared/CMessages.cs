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
            "run\tExecutes the program via CLI" +
            "\n" +
            "outdir:\tSpecifies desired location for HTML reports. Usage: \"outdir:D:\\example\". Default = C:\\temp\\vHC" +
            "\n" +
            "days:\tSpecifies reporting interval. Choose 7 or 30. 7 is default. USAGE: 'days:30'\n" +
            "gui\tStarts GUI. GUI overrides other commands." +
            "\n\n" +
            "EXAMPLES:\n" +
            "1. Run to default location:\t .\\VeeamHealthCheck.exe run\n" +
            "2. Run to custom location:\t .\\VeeamHealthCheck.exe run outdir:\\\\myshare\\folder\n" +
            "3. Run with 30 day report:\t .\\VeeamHealthCheck.exe run days:30\n" +
            "4. Run GUI from CLI:\t .\\VeeamHealthCheck.exe gui" +
            "\n";

        public static string PsVbrConfigStart = "[PS] Enter Config Collection Invoker...";
        public static string PsVbrConfigDone = PsVbrConfigStart + ProcEnd;

        public static string PsVbrConfigStartProc = "[PS][VBR Config] Starting PowerShell Process...";
        public static string PsVbrConfigStartProcDone = PsVbrConfigStartProc+ ProcEnd;

        public static string PsVbrConfigProcId = "[PS][VBR Config] PowerShell Process started with ID: ";
        public static string PsVbrConfigProcIdDone = PsVbrConfigProcId+ ProcEnd;
    }
}
