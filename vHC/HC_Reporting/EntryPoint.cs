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
using System.Text.RegularExpressions;

namespace VeeamHealthCheck
{
    public class EntryPoint
    {
        private static string _helpMenu = "\nHELP MENU:\nrun\tExecutes the program via CLI" +
            "\noutdir:\tSpecifies desired location for HTML reports. Usage: \"outdir:D:\\example\". Default = C:\\temp\\vHC" +
            "\n\n" +
            "EXAMPLES:\n" +
            "1. Run to default location:\t .\\VeeamHealthCheck.exe run\n" +
            "2. Run to custom location:\t .\\VeeamHealthCheck.exe run outdir:\\\\myshare\\folder" +
            "\n";
        private static CLogger _log = VhcGui.log;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        [STAThread]
        public static void Main(string[] args)
        {
            var handle = GetConsoleWindow();
            if (args != null && args.Length > 0)
            {
                bool run = false;
                string targetDir = @"C:\temp\vHC";
                foreach(var a in args)
                {
                    switch (a)
                    {
                        case "help":
                            Console.WriteLine(_helpMenu);
                            break;
                        case "run":
                            run = true;
                            
                            break;
                        case "show:files":
                            break;
                        case "show:report":
                            break;
                        case "show:all":
                            break;
                        case var match when new Regex("outdir:.*").IsMatch(a):
                            string[] outputDir = a.Split(":");
                            targetDir = outputDir[1];
                            break;
                    }
                }
                if (run)
                {
                    _log.Info("Starting RUN...", false);
                    VhcGui app = new VhcGui();
                    app.CliRun(targetDir);
                    _log.Info("Starting RUN...complete!", false);
                    _log.Info("Output is stored in " + targetDir);
                }

            }
            else
            {
                ShowWindow(handle, SW_HIDE);
                var app = new System.Windows.Application();
                app.Run(new VhcGui());
            }
        }

    }
}
