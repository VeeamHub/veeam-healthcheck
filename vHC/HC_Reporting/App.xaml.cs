// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VeeamHealthCheck
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow mw = new();
            if (e.Args.Length < 1)
            {
                //mw.Show();
            }
            if (e.Args.Length == 1)
            {
                switch (e.Args[0])
                {
                    case "run":
                        mw.RunAction();
                        break;

                    case "help":
                        Console.WriteLine("test");
                        break;
                    default:
                        Console.WriteLine("default");
                        break;
                }
                Environment.Exit(0);

            }
        }
    }
}
