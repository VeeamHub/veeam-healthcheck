using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VeeamHealthCheck.Resources;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck
{
    public class EntryPoint
    {
        private static CLogger logger = CGlobals.Logger;
        private static CClientFunctions _functions = new();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        [STAThread]
        public static void Main(string[] args)
        {
            _functions.LogVersionAndArgs(args);
            

            var handle = GetConsoleWindow();

            if (args.Length == 0)
            {
                var pos = Console.GetCursorPosition();
                if (pos == (0, 0))
                {
                    logger.Info("0s");
                    //LaunchUi(handle, true);
                    _functions.LaunchUi(handle, true);
                }
                if (pos != (0, 0))
                {
                    logger.Info("not 0");
                    Console.WriteLine(CMessages.helpMenu);
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
                            Console.WriteLine(CMessages.helpMenu);
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
                            CGlobals.ReportDays = 7;
                            break;
                        case "days:30":
                            logger.Info("Days set to 30");
                            CGlobals.ReportDays = 30;
                            break;
                        case "days:90":
                            logger.Info("Days set to 90");
                            CGlobals.ReportDays = 90;
                            break;
                        case "days:12":
                            logger.Info("Days set to 12");
                            CGlobals.ReportDays = 12;
                            break;
                        case "gui":
                            ui = true;
                            break;
                        case var match when new Regex("outdir:.*").IsMatch(a):
                            string[] outputDir = a.Split(":");
                            targetDir = outputDir[1];
                            logger.Info("Output directory: " + targetDir);
                            break;
                        case "security":
                            // do sec report
                            break;
                    }
                }

                if(args.Any(x => x == "security"))
                {
                    Console.WriteLine(true);
                    CGlobals.RunSecReport = true;
                    CGlobals.RunFullReport = false;
                }

                if (ui)
                    LaunchUi(handle, false);
                else if (run)
                {
                    logger.Info("Starting RUN...", false);
                    //VhcGui app = new VhcGui();
                    //app.CliRun(targetDir);

                    CClientFunctions functions = new();
                    functions.CliRun(targetDir);

                    logger.Info("Starting RUN...complete!", false);
                    logger.Info("Output is stored in " + targetDir);
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
