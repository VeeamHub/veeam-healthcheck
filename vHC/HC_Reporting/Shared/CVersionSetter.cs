using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace VeeamHealthCheck.Shared
{
    class CVersionSetter
    {
        public CVersionSetter()
        {

        }
        public static string GetFileVersion()
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo("VeeamHealthCheck.exe");
            return fvi.FileVersion;
            
        }
    }
}
