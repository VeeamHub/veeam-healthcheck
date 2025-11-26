// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Diagnostics;
using System.Reflection;

namespace VeeamHealthCheck.Shared
{
    class CVersionSetter
    {
        public CVersionSetter()
        {

        }

        public static string GetFileVersion()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule.FileName;
            }
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(exePath);
            CGlobals.VHCVERSION = fvi.FileVersion;
            return fvi.FileVersion;

        }
    }
}
