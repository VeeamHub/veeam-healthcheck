using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using VeeamHealthCheck.Startup;

namespace VeeamHealthCheck
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
