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
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    public class EntryPoint
    {
        private static CLogger logger = VhcGui.log;
        private static string _helpMenu = "\nHELP MENU:\n" +
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
            logger.Info("Args count = " + args.Count().ToString());
            foreach (var arg in args)
                logger.Info("Input: " + arg);
            var handle = GetConsoleWindow();
            if (args.Length == 0)
            {
                var pos = Console.GetCursorPosition();
                if (pos == (0, 0))
                {
                    logger.Info("0s");
                    LaunchUi(handle, true);
                }
                if (pos != (0, 0))
                {
                    logger.Info("not 0");
                    Console.WriteLine(_helpMenu);
                }
            }
            else if (args != null && args.Length > 0)
            {
                bool run = false;
                bool ui = false;
                string targetDir = @"C:\temp\vHC";
                foreach (var a in args)
                {
                    switch (a)
                    {
                        case "help":
                            logger.Info("entering help menu");
                            Console.WriteLine(_helpMenu);
                            break;
                        case "run":
                            run = true;
                            logger.Info("Run = true");
                            break;
                        case "show:files":
                            break;
                        case "show:report":
                            break;
                        case "show:all":
                            break;
                        case "days:7":
                            logger.Info("Days set to 7");
                            VhcGui._reportDays = 7;
                            break;
                        case "days:30":
                            logger.Info("Days set to 30");
                            VhcGui._reportDays = 30;
                            break;
                        case "gui":
                            ui = true;
                            break;
                        case var match when new Regex("outdir:.*").IsMatch(a):
                            string[] outputDir = a.Split(":");
                            targetDir = outputDir[1];
                            logger.Info("Output directory: " + targetDir);
                            break;
                    }
                }
                if (ui)
                    LaunchUi(handle, false);
                else if (run)
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
                LaunchUi(handle, true);
            }
        }
        private static void LaunchUi(IntPtr handle, bool hide)
        {
            logger.Info("Executing GUI");
            if(hide)
                ShowWindow(handle, SW_HIDE);
            var app = new System.Windows.Application();
            app.Run(new VhcGui());
        }

    }
}
