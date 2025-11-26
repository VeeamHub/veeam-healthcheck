// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrSecurityTableHelper
    {
        private CHtmlFormatting _form = new();
        private CDataFormer _df = new();
        private CSecuritySummaryTable _secSummaryParts;

        public CVbrSecurityTableHelper()
        {
            _secSummaryParts = _df.SecSummary();
        }

        public Tuple<string, string> ColumnExample()
        {
            string header = _form.TableHeader(string.Empty, string.Empty);
            string data = _form.TableData(string.Empty, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> ConsoleInstalled()
        {
            string header = _form.TableHeader("Is VBR Console Installed", string.Empty);
            string data = _form.TableData(CSecurityGlobalValues.IsConsoleInstalled, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RdpEnabled()
        {
            string header = _form.TableHeader("Is RDP Enabled", string.Empty);
            string data = _form.TableData(CSecurityGlobalValues.IsRdpEnabled, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> DomainJoined()
        {
            string header = _form.TableHeader("Is VBR Domain Joined", string.Empty);
            string data = _form.TableData(CSecurityGlobalValues.IsDomainJoined, string.Empty);

            return Tuple.Create(header, data);
        }



        public Tuple<string, string> IsImmutabilityEnabledOnce()
        {
            string header = _form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0);
            string data = _form.TableData("False", string.Empty, 1);
            if (_secSummaryParts.ImmutabilityEnabled == true)
                data = _form.TableData(_form.True, string.Empty);
            else
                data = _form.TableData(_form.False, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> GeneralTrafficEncryptionEnabled()
        {
            string header = _form.TableHeader("General Traffic Encryption", string.Empty);
            string data = _form.TableData("False", string.Empty, 1);
            if (_secSummaryParts.TrafficEncrptionEnabled == true)
                data = _form.TableData(_form.True, string.Empty);
            else
                data = _form.TableData(_form.False, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsBackupFileEncryptionInUse()
        {
            string header = _form.TableHeader("Backup File Encryption", string.Empty);
            string data = _form.TableData("False", string.Empty, 1);
            if (_secSummaryParts.BackupFileEncrptionEnabled == true)
                data = _form.TableData(_form.True, string.Empty);
            else
                data = _form.TableData(_form.False, string.Empty);


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsConfigBackupEncrypted()
        {
            string header = _form.TableHeader("Config Backup Encryption", string.Empty);
            string data = _form.TableData(_form.False, string.Empty, 1);
            if (_secSummaryParts.ConfigBackupEncrptionEnabled == true)
                data = _form.TableData(_form.True, string.Empty); 
            else
                data = _form.TableData(_form.False, string.Empty);


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsMFAEnabled()
        {
            string header = _form.TableHeader("MFA Enabled", string.Empty);
            string data = _form.TableData(_form.False, string.Empty, 1);
            if (_secSummaryParts.MFAEnabled == true)
                data = _form.TableData(_form.True, string.Empty);
            else
                data = _form.TableData(_form.False, string.Empty);

            return Tuple.Create(header, data);
        }
    }
}
