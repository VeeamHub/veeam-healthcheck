// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Startup
{
    public class EntryPoint
    {
        private static CClientFunctions _functions = new();
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                CArgsParser ap = new(args);
                ap.ParseArgs();
                return 1;
            }
            catch (Exception ex) { return 0; }
            
        }

    }
}
