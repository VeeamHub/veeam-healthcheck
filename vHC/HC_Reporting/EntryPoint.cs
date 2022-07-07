using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    public class EntryPoint
    {
        private static string _helpMenu = "HELP MENU:\n\nrun - Executes the program";
        private static CLogger _log = VhcGui.log;
        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {

                switch (args[0])
                {
                    case "help":
                        Console.WriteLine(_helpMenu);
                        break;
                    case "run":
                        _log.Info("Starting RUN...", false);
                        VhcGui app = new VhcGui();
                        app.CliRun();
                        _log.Info("Starting RUN...complete!", false);
                        break;
                }
            }
            else
            {

                var app = new System.Windows.Application();
                app.Run(new VhcGui());
            }
        }

    }
}
