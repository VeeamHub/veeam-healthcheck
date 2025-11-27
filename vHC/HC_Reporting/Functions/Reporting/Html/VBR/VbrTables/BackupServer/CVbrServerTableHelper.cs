// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Functions.Analysis.DataModels;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Reporting.Html.VBR
{
    internal class CVbrServerTableHelper
    {
        private readonly CHtmlFormatting form = new();
        private readonly Functions.Analysis.DataModels.BackupServer b;

        public CVbrServerTableHelper(Functions.Analysis.DataModels.BackupServer backupServer)
        {
            this.b = backupServer;
        }

        public CVbrServerTableHelper()
        {
        }

        public void Run()
        {
        }

        public Tuple<string, string> ServerName()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblName, VbrLocalizationHelper.BstNameTT);
            string data = this.form.TableData(this.b.Name, string.Empty);

            return this.ReturnColumn(header, data);
        }

        public Tuple<string, string> ServerVersion()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblVersion, VbrLocalizationHelper.BstVerTT);
            string data = this.form.TableData(this.b.Version, string.Empty);

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> Cores()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblCore, VbrLocalizationHelper.BstCpuTT);
            string data = this.form.TableData(this.b.Cores.ToString(), string.Empty);
            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RAM()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblRam, VbrLocalizationHelper.BstRamTT);
            string data = this.form.TableData(this.b.RAM.ToString(), string.Empty);
            return Tuple.Create(header, data);
        }

        public Tuple<string, string> ProxyRole()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblProxyRole, VbrLocalizationHelper.BstPrxTT);
            string data = string.Empty;
            if (this.b.HasProxyRole)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RepoGatewayRole()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblRepoRole, VbrLocalizationHelper.BstRepTT);
            string data = string.Empty;
            if(this.b.HasRepoRole)
            {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }
            
            return Tuple.Create(header, data);
        }

        public Tuple<string, string> WanRole()
        {
            string header = this.form.TableHeader(VbrLocalizationHelper.BkpSrvTblWanRole, VbrLocalizationHelper.BstWaTT);
            string data = string.Empty;
            if(this.b.HasWanAccRole)
                {
                data = this.form.TableData(this.form.True, string.Empty);
            }
            else
            {
                data = this.form.TableData(this.form.False, string.Empty);
            }

            return Tuple.Create(header, data);
        }

        public Tuple<string, string> ConsoleStatus()
        {
            string result = CSecurityGlobalValues.IsConsoleInstalled;
            int shade = this.ParseTrueAsBadShade(result);

            string header = this.form.TableHeader(VbrLocalizationHelper.BackupServerConsoleInstalled, string.Empty);
            string data = this.form.TableData(result, string.Empty, shade);
            return Tuple.Create(header, data);
        }

        public Tuple<string, string> RdpStatus()
        {
            string result = CSecurityGlobalValues.IsRdpEnabled;
            int shade = this.ParseTrueAsBadShade(result);

            string header = this.form.TableHeader(VbrLocalizationHelper.BackupServerRdpEnabled, string.Empty);
            string data = this.form.TableData(result, string.Empty, shade);
            return Tuple.Create(header, data);
        }

        public Tuple<string, string> DomainStatus()
        {
            string result = CSecurityGlobalValues.IsDomainJoined;
            int shade = this.ParseFalseAsBadShade(result);

            string header = this.form.TableHeader(VbrLocalizationHelper.BackupServerDomainJoined, string.Empty);
            string data = this.form.TableData(result, string.Empty);
            return Tuple.Create(header, data);
        }

        private int ParseFalseAsBadShade(string input)
        {
            if (input == "False")
            {
                return 1;
            }


            return 0;
        }

        private int ParseTrueAsBadShade(string input)
        {
            if (input == "True")
            {
                return 1;
            }


            return 0;
        }

        private Tuple<string, string> ReturnColumn(string header, string data)
        {
            return Tuple.Create(header, data);
        }
    }
}
