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
        private readonly CHtmlFormatting form = new();
        private readonly CDataFormer df = new();
        private readonly CSecuritySummaryTable secSummaryParts;

        public CVbrSecurityTableHelper()
        {
            this.secSummaryParts = this.df.SecSummary();
        }

        public Tuple<string, string> ColumnExample()
        {
            string header = this.form.TableHeader(string.Empty, string.Empty);
            string data = this.form.TableData(string.Empty, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> ConsoleInstalled()
        {
            string header = this.form.TableHeader("Is VBR Console Installed", string.Empty);
            string data = this.form.TableData(CSecurityGlobalValues.IsConsoleInstalled, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RdpEnabled()
        {
            string header = this.form.TableHeader("Is RDP Enabled", string.Empty);
            string data = this.form.TableData(CSecurityGlobalValues.IsRdpEnabled, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> DomainJoined()
        {
            string header = this.form.TableHeader("Is VBR Domain Joined", string.Empty);
            string data = this.form.TableData(CSecurityGlobalValues.IsDomainJoined, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsImmutabilityEnabledOnce()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.SSHdr0, VbrLocalizationHelper.SSHdrTT0);
            string data = this.form.TableData("False", string.Empty, 1);
            if (this.secSummaryParts.ImmutabilityEnabled == true)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> GeneralTrafficEncryptionEnabled()
        {
            string header = this.form.TableHeader("General Traffic Encryption", string.Empty);
            string data = this.form.TableData("False", string.Empty, 1);
            if (this.secSummaryParts.TrafficEncrptionEnabled == true)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsBackupFileEncryptionInUse()
        {
            string header = this.form.TableHeader("Backup File Encryption", string.Empty);
            string data = this.form.TableData("False", string.Empty, 1);
            if (this.secSummaryParts.BackupFileEncrptionEnabled == true)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsConfigBackupEncrypted()
        {
            string header = this.form.TableHeader("Config Backup Encryption", string.Empty);
            string data = this.form.TableData(this.form.False, string.Empty, 1);
            if (this.secSummaryParts.ConfigBackupEncrptionEnabled == true)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }


            return Tuple.Create(header, data);
        }

        public Tuple<string, string> IsMFAEnabled()
        {
            string header = this.form.TableHeader("MFA Enabled", string.Empty);
            string data = this.form.TableData(this.form.False, string.Empty, 1);
            if (this.secSummaryParts.MFAEnabled == true)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {

                data = this.form.TableData(this.form.False, string.Empty);
            }


            return Tuple.Create(header, data);
        }
    }
}
