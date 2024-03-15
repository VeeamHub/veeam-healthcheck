// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.Reporting.Html;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CConfigBackupTable
    {
        CHtmlFormatting _form = new();
        public CConfigBackupTable()
        {
            ParseBackupServer();
        }
        public Tuple<string, string> ConfigBackupEnabled()
        {
            string header = _form.TableHeader("Config Backup Enabled", "");
            string data = _form.TableData(CSecurityGlobalValues.ConfigBackupEnabled.ToString(), "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> ConfigBackupSuccessful()
        {
            string header = _form.TableHeader("Config Backup Last Run Successful", "");
            string data = _form.TableData(CSecurityGlobalValues.ConfigBackupSuccess.ToString(), "");

            return Tuple.Create(header, data);
        }
        public Tuple<string, string> ConfigBackupEncrypted()
        {
            string header = _form.TableHeader("Config Backup Encrypted", "");
            string data = _form.TableData(CSecurityGlobalValues.ConfigBackupEncrypted.ToString(), "");

            return Tuple.Create(header, data);
        }
        private void ParseBackupServer()
        {
            CDataFormer df = new();
            Functions.Analysis.DataModels.BackupServer b = df.BackupServerInfoToXml(false);
            CSecurityGlobalValues.ConfigBackupEnabled = b.ConfigBackupEnabled;
            CSecurityGlobalValues.ConfigBackupEncrypted = b.ConfigBackupEncryption;
            CSecurityGlobalValues.ConfigBackupSuccess = b.ConfigBackupLastResult;

        }
    }
}
