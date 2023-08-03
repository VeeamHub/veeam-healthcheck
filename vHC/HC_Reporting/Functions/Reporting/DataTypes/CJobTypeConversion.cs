using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    internal class CJobTypeConversion
    {

        public static string ReturnJobType(string  jobType)
        {
            switch(jobType)
            {
                case "EVmware":
                    return "VMware Backup";
                case "ENasBackup":
                    return "NAS Backup";
                case "EHyperV":
                    return "Hyper-V Backup";
                case "template":
                    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                //case "template":
                //    return "template";
                default:
                    return jobType;
            }
        }
    }
}
