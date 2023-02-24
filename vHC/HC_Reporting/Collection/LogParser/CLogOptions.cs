using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Collection.LogParser
{
    class CLogOptions
    {
        public static readonly string VMCLOG = "\\Utils\\VMC.log";
        public static readonly string installIdLine = "InstallationId:";

        private static string _installId;

        public CLogOptions(string mode)
        {
            CVmcReader vReader = new(mode);
            vReader.PopulateVmc();
            _installId = vReader.INSTALLID;
        }

        public static string INSTALLID
        {
            get
            {
                if (!String.IsNullOrEmpty(_installId))
                    return _installId;
                else
                    return "";
            }
        }

    }
}
