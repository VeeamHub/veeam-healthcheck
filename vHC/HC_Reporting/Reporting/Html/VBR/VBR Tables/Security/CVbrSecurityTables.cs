using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Backup_Server;

namespace VeeamHealthCheck.Reporting.Html.VBR.VBR_Tables.Security
{
    internal class CVbrSecurityTables
    {
        private CVbrSecurityTableHelper _tables;
        private CConfigBackupTable _cfgTable = new();
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
                _tables.IsConfigBackupEncrypted()
        };
            return headers;
        }
    }
}
