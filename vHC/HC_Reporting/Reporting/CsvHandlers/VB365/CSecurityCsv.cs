using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Reporting.CsvHandlers.VB365
{
    internal class CSecurityCsv
    {
        [Index(0)]
        public string WinFirewallEnabled { get; set; }
        [Index(1)]
        public string Internetproxy { get; set; }
        [Index(2)]
        public string ServerCert { get; set; }
        [Index(3)]
        public string ServerCertExpires { get; set; }
        [Index(4)]
        public string ServerCertSelfSigned { get; set; }
        [Index(5)]
        public string APIEnabled { get; set; }
        [Index(6)]
        public string APIPort { get; set; }
        [Index(7)]
        public string APICert { get; set; }
        [Index(8)]
        public string APICertExpires { get; set; }
        [Index(9)]
        public string APICertSelfSigned { get; set; }
        [Index(10)]
        public string TenantAuthEnabled { get; set; }
        [Index(11)]
        public string TenantAuthCert { get; set; }
        [Index(12)]
        public string TenantAuthCertExpires { get; set; }
        [Index(13)]
        public string TenantAuthCertSelfSigned { get; set; }
        [Index(14)]
        public string RestorePortalEnabled { get; set; }
        [Index(15)]
        public string RestorePortalCert { get; set; }
        [Index(16)]
        public string RestorePortalCertExpires { get; set; }
        [Index(17)]
        public string RestorePortalCertSelfSigned { get; set; }
        [Index(18)]
        public string OperatorAuthEnabled { get; set; }
        [Index(19)]
        public string OperatorAuthCert { get; set; }
        [Index(20)]
        public string OperatorAuthCertExpires { get; set; }
        [Index(21)]
        public string OperatorAuthCertSelfSigned { get; set; }
    }
}
