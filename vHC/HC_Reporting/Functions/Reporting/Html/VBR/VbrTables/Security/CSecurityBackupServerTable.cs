// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Reporting.Html.VBR;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CSecurityBackupServerTable
    {
        readonly CVbrSecurityTableHelper tables = new();

        public CSecurityBackupServerTable() { }

        public List<Tuple<string, string>> ServerSpecificInfo()
        {
            CVbrServerTableHelper helper = new();

            List<Tuple<string, string>> tables = new()
            {
                helper.ConsoleStatus(),
                helper.RdpStatus(),
                helper.DomainStatus()

                // _tables.ConsoleInstalled(),
                // _tables.RdpEnabled(),
                // _tables.DomainJoined()
            };

            return tables;
        }
    }
}
