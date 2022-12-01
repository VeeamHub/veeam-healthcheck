using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Shared
{
    internal class CMessages
    {
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

    }
}
