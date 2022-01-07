// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.RegSettings
{
    class CDefaultRegOptions
    {
        public Dictionary<string, string> _defaultKeys = new();

        public CDefaultRegOptions()
        {
            FillDict();
        }

        private void FillDict()
        {
            _defaultKeys.Add("AgentLogging", "1");
            _defaultKeys.Add("AgentLogOptions", "flush");
            _defaultKeys.Add("LoggingLevel", "4");
            _defaultKeys.Add("VNXBlockNaviSECCliPath", @"C:\Program Files\Veeam\Backup and Replication\Backup\EMC Navisphere CLI\NaviSECCli.exe");
            _defaultKeys.Add("VNXeUemcliPath", @"C:\Program Files\Veeam\Backup and Replication\Backup\EMC Unisphere CLI\3.0.1\uemcli.exe");
            //edafaultKeys.Add("SqlLockInfo", @"<CLockInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Id="25b33aab-cdc1-41c7-9865-20844b37ed7c" Time="2021-12-03T11:12:52"><Computer>WIN-QAVAM1GSL88</Computer><UserName>WIN-QAVAM1GSL88\adam</UserName></CLockInfo>");
            _defaultKeys.Add("CloudServerPort", "10003");
            //_defaultKeys.Add("SqlDatabaseName", "VeeamBackup");
            //_defaultKeys.Add("SqlInstanceName", "VEEAMSQL2016");
            //_defaultKeys.Add("SqlServerName", "WIN-QAVAM1GSL88");
            //_defaultKeys.Add("SqlLogin", "");
            _defaultKeys.Add("CorePath", @"C:\Program Files\Veeam\Backup and Replication\Backup\");
            _defaultKeys.Add("BackupServerPort", "9392");
            _defaultKeys.Add("SecureConnectionsPort", "9401");
            _defaultKeys.Add("VddkReadBufferSize", "0");
            _defaultKeys.Add("EndPointServerPort", "10001");
            //_defaultKeys.Add("SqlSecuredPassword", "");
            _defaultKeys.Add("IsComponentsUpdateRequired", "0");
            _defaultKeys.Add("LicenseAutoUpdate", "1");
            _defaultKeys.Add("CloudSvcPort", "6169");
            _defaultKeys.Add("VBRServiceRestartNeeded", "0");
            _defaultKeys.Add("ImportServers", "0");
            _defaultKeys.Add("MaxLogCount", "10");
            _defaultKeys.Add("MaxLogSize", "10240");

        }
    }
}
