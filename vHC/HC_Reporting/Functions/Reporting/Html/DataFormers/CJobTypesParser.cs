using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.DataFormers
{
    public class CJobTypesParser
    {
        public static string GetJobType(string jobType)
        {
            switch (jobType)
            {
                case "Copy":
                    return "File Copy";
                case "SimpleBackupCopyPolicy":
                    return "Backup Copy";
                case "NasBackup":
                    return "File Backup";
                case "ENasBackup":
                    return "File Backup";
                case "Backup":
                    return "Backup";
                case "Replica":
                    return "Replica";
                case "NasBackupCopy":
                    return "File Backup - Copy";
                case "MSSQLPlugin":
                    return "MS SQL Plugin";
                case "SureBackup":
                    return "SureBackup";
                case "FileTapeBackup":
                    return "Tape";
                case "VmTapeBackup":
                    return "Tape";
                case "BackupSync":
                    return "Backup Copy";
                case "SqlLogBackup":
                    return "SQL Log Backup";
                case "OracleLogBackup":
                    return "Oracle Log Backup";
                case "SimpleBackupCopyWorker":
                    return "Backup Copy";
                case "ConfBackup":
                    return "Configuration Backup";
                case "Cloud":
                    return "Cloud Backup";
                case "OrchestratedTask":
                    return "Orchestrated Task";
                case "OracleRMANBackup":
                    return "Enterprise Database Plugin";
                case "SapBackintBackup":
                    return "Enterprise Database Plugin";
                case "EpAgentManagement":
                    return "Agent Backup";
                case "ELinuxPhysical":
                    return "Agent Backup";
                case "EEndPoint":
                    return "Endpoint Backup";
                case "EHyperV":
                    return "Hyper-V Backup";
                case "EVmware":
                    return "VMware Backup";
                case "":
                    return "Other";
                default:
                    return jobType;
            }

        }
    }
}
