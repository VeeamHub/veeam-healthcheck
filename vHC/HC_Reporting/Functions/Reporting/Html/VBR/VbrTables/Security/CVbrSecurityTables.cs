// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
//using VeeamHealthCheck.Reporting.Html.VBR;
//using VeeamHealthCheck.Reporting.Html.VBR.VbrTables;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrSecurityTables
    {
        private CVbrSecurityTableHelper _tables;
        private readonly VBR.CConfigBackupTable _cfgTable = new();
        public CVbrSecurityTables()
        {
            _tables = new();
        }

        public List<Tuple<string, string>> ConfigBackupInfo()
        {
            List<Tuple<string, string>> tables = new()
            {
                _cfgTable.ConfigBackupEnabled(),
                _cfgTable.ConfigBackupSuccessful(),
                _cfgTable.ConfigBackupEncrypted()

            };

            return tables;
        }

        public List<Tuple<string, string>> SecuritySummaryTables()
        {
            List<Tuple<string, string>> headers = new()
            {
                _tables.IsImmutabilityEnabledOnce(),
                _tables.GeneralTrafficEncryptionEnabled(),
                _tables.IsBackupFileEncryptionInUse(),
                _tables.IsConfigBackupEncrypted(),
                _tables.IsMFAEnabled(),
        };
            return headers;
        }
    }
}
