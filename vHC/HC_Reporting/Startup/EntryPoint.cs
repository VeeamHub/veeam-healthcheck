﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Runtime;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup
{
    public class EntryPoint
    {
        private static CClientFunctions _functions = new();
        [STAThread]
        public static int Main(string[] args)
        {
            CGlobals.Logger.Debug("Starting the application");
            CGlobals.Logger.Debug("The arguments are: " + string.Join(" ", args));

            try
            {
                CArgsParser ap = new(args);
                var res =  ap.ParseArgs();
                CGlobals.Logger.Info("The result is: " + res, true);
                return 0;
            }
            catch (Exception ex) {
                CGlobals.Logger.Error(ex.Message);
                CGlobals.Logger.Error("The result is: " + 1, true);
                return 1;
            }
            
        }

    }
}
