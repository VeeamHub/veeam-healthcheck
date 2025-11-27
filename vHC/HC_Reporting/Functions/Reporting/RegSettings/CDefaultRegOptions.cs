// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.RegSettings
{
    class CDefaultRegOptions
    {
        public Dictionary<string, string> defaultKeys = new();

        public CDefaultRegOptions()
        {
            this.FillDict();
        }

        private void FillDict()
        {
            this.defaultKeys.Add("AgentLogging", "1");
            this.defaultKeys.Add("AgentLogOptions", "flush");
            this.defaultKeys.Add("LoggingLevel", "4");
            this.defaultKeys.Add("VNXBlockNaviSECCliPath", @"C:\Program Files\Veeam\Backup and Replication\Backup\EMC Navisphere CLI\NaviSECCli.exe");
            this.defaultKeys.Add("VNXeUemcliPath", @"C:\Program Files\Veeam\Backup and Replication\Backup\EMC Unisphere CLI\3.0.1\uemcli.exe");
            this.defaultKeys.Add("SqlLockInfo", string.Empty);
            this.defaultKeys.Add("CloudServerPort", "10003");
            this.defaultKeys.Add("SqlDatabaseName", "VeeamBackup");
            this.defaultKeys.Add("SqlInstanceName", "VEEAMSQL2016");
            this.defaultKeys.Add("SqlServerName", "WIN-QAVAM1GSL88");
            this.defaultKeys.Add("SqlLogin", string.Empty);
            this.defaultKeys.Add("CorePath", @"C:\Program Files\Veeam\Backup and Replication\Backup\");
            this.defaultKeys.Add("BackupServerPort", "9392");
            this.defaultKeys.Add("SecureConnectionsPort", "9401");
            this.defaultKeys.Add("VddkReadBufferSize", "0");
            this.defaultKeys.Add("EndPointServerPort", "10001");
            this.defaultKeys.Add("SqlSecuredPassword", string.Empty);
            this.defaultKeys.Add("IsComponentsUpdateRequired", "0");
            this.defaultKeys.Add("LicenseAutoUpdate", "1");
            this.defaultKeys.Add("CloudSvcPort", "6169");
            this.defaultKeys.Add("VBRServiceRestartNeeded", "0");
            this.defaultKeys.Add("ImportServers", "0");
            this.defaultKeys.Add("MaxLogCount", "10");
            this.defaultKeys.Add("MaxLogSize", "10240");
        }
    }
}
