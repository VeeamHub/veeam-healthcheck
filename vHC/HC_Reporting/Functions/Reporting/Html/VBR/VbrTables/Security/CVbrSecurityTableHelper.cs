// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrSecurityTableHelper
    {
        private CHtmlFormatting _form = new();
        private CDataFormer _df = new();
        private List<int> _secSummaryParts;
        public CVbrSecurityTableHelper()
        {
            _secSummaryParts = _df.SecSummary();
        }

        public Tuple<string, string> ColumnExample()
        {
            string header = _form.TableHeader("", "");
            string data = _form.TableData("", "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> ConsoleInstalled()
        {
            string header = _form.TableHeader("Is VBR Console Installed", "");
            string data = _form.TableData(CSecurityGlobalValues.IsConsoleInstalled, "");

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RdpEnabled()
        {
            string header = _form.TableHeader("Is RDP Enabled", "");
            string data = _form.TableData(CSecurityGlobalValues.IsRdpEnabled, "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> DomainJoined()
        {
            string header = _form.TableHeader("Is VBR Domain Joined", "");
            string data = _form.TableData(CSecurityGlobalValues.IsDomainJoined, "");

            return Tuple.Create(header, data);
        }



        public Tuple<string, string> IsImmutabilityEnabledOnce()
        {
            string header = _form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0);
            string data = _form.TableData("False", "", 1);
            if (_secSummaryParts[0] == 1)
                data = _form.TableData("True", "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> GeneralTrafficEncryptionEnabled()
        {
            string header = _form.TableHeader("General Traffic Encryption", "");
            string data = _form.TableData("False", "", 1);
            if (_secSummaryParts[1] == 1)
                data = _form.TableData("True", "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> IsBackupFileEncryptionInUse()
        {
            string header = _form.TableHeader("Backup File Encryption", "");
            string data = _form.TableData("False", "", 1);
            if (_secSummaryParts[2] == 1)
                data = _form.TableData("True", "");


            return Tuple.Create(header, data);
        }
        public Tuple<string, string> IsConfigBackupEncrypted()
        {
            string header = _form.TableHeader("Config Backup Encryption", "");
            string data = _form.TableData("False", "", 1);
            if (_secSummaryParts[3] == 1)
                data = _form.TableData("True", "");


            return Tuple.Create(header, data);
        }
    }
}
