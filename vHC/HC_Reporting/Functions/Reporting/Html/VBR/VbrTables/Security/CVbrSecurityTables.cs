// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Shared;

// using VeeamHealthCheck.Reporting.Html.VBR;
// using VeeamHealthCheck.Reporting.Html.VBR.VbrTables;
namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrSecurityTables
    {
        private readonly CVbrSecurityTableHelper tables;
        private readonly VBR.CConfigBackupTable cfgTable = new();

        public CVbrSecurityTables()
        {
            this.tables = new();
            if (CGlobals.DEBUG)
            {
                CGlobals.Logger.Debug("VbrSecurityTables Constructor");
            }

            // CheckRecon();
        }

        public List<Tuple<string, string>> ConfigBackupInfo()
        {
            List<Tuple<string, string>> tables = new()
            {
                this.cfgTable.ConfigBackupEnabled(),
                this.cfgTable.ConfigBackupSuccessful(),
                this.cfgTable.ConfigBackupEncrypted()
            };

            return tables;
        }

        public List<Tuple<string, string>> SecuritySummaryTables()
        {
            List<Tuple<string, string>> headers = new()
            {
                this.tables.IsImmutabilityEnabledOnce(),
                this.tables.GeneralTrafficEncryptionEnabled(),
                this.tables.IsBackupFileEncryptionInUse(),
                this.tables.IsConfigBackupEncrypted(),
                this.tables.IsMFAEnabled(),
            };
            return headers;
        }
    }
}
