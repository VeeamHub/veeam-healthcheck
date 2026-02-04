// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Reporting.Html.VBR;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrServerTable
    {
        private readonly CVbrServerTableHelper tables;

        public CVbrServerTable(Functions.Analysis.DataModels.BackupServer backupServer)
        {
            this.tables = new CVbrServerTableHelper(backupServer);
        }

        public void DebugLogTuple()
        {
            string headers = string.Empty;
            string data = string.Empty;
            foreach (var h in this.VbrFullTables())
            {
                headers += h.Item1;
                data += h.Item2;
            }

            CGlobals.Logger.Warning(headers, false);
            CGlobals.Logger.Warning("\nLINE BREAK\n", false);
            CGlobals.Logger.Warning(data, false);
        }

        public List<Tuple<string, string>> VbrFullTables()
        {
            List<Tuple<string, string>> headers = new List<Tuple<string, string>>
            {
                this.tables.ServerName(),
                this.tables.ServerVersion(),
                this.tables.OSVersion(),
                this.tables.Cores(),
                this.tables.RAM(),
                this.tables.ProxyRole(),
                this.tables.RepoGatewayRole(),
                this.tables.WanRole(),

                // _tables.ConsoleStatus(),
                // _tables.RdpStatus(),
                // _tables.DomainStatus(),
            };

            return headers;
        }

        public List<Tuple<string, string>> VbrSecurityTables()
        {
            List<Tuple<string, string>> headers = new List<Tuple<string, string>>
            {
                this.tables.ServerName(),
                this.tables.ServerVersion(),
                this.tables.OSVersion(),

                // _tables.Cores(),
                // _tables.RAM(),
                // _tables.ProxyRole(),
                // _tables.RepoGatewayRole(),
                // _tables.WanRole(),
                this.tables.ConsoleStatus(),
                this.tables.RdpStatus(),
                this.tables.DomainStatus(),
            };

            return headers;
        }

        public void Dispose() { }
    }
}
