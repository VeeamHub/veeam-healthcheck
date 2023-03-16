// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Startup
{
    public class EntryPoint
    {
        private static CClientFunctions _functions = new();
        [STAThread]
        public static void Main(string[] args)
        {
            CArgsParser ap = new(args);
            ap.ParseArgs();
        }

    }
}
