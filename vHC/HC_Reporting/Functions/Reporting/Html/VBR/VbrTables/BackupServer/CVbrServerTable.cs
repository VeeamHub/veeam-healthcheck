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
        private readonly CVbrServerTableHelper _tables;

        public CVbrServerTable(Functions.Analysis.DataModels.BackupServer backupServer)
        {
            _tables = new CVbrServerTableHelper(backupServer);
        }



        public void DebugLogTuple()
        {
            string headers = "";
            string data = "";
            foreach (var h in VbrFullTables())
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
                _tables.ServerName(),
                _tables.ServerVersion(),
                _tables.Cores(),
                _tables.RAM(),
                _tables.ProxyRole(),
                _tables.RepoGatewayRole(),
                _tables.WanRole(),
                //_tables.ConsoleStatus(),
                //_tables.RdpStatus(),
                //_tables.DomainStatus(),
            };

            return headers;
        }
        public List<Tuple<string, string>> VbrSecurityTables()
        {
            List<Tuple<string, string>> headers = new List<Tuple<string, string>>
            {
                _tables.ServerName(),
                _tables.ServerVersion(),
                //_tables.Cores(),
                //_tables.RAM(),
                //_tables.ProxyRole(),
                //_tables.RepoGatewayRole(),
                //_tables.WanRole(),
                _tables.ConsoleStatus(),
                _tables.RdpStatus(),
                _tables.DomainStatus(),
            };

            return headers;
        }


        public void Dispose() { }
    }

}
