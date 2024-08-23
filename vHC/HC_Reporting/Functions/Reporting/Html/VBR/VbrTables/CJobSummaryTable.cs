using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.Html.DataFormers;
using VeeamHealthCheck.Shared;
using static VeeamHealthCheck.Functions.Collection.DB.CModel;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    internal class CJobSummaryTable
    {
        public CJobSummaryTable() { }
        public Dictionary<string,int> JobSummaryTable()
        {
            Dictionary<string, int> typeAndCount = new();

            try
            {
                CCsvParser csv = new();
                var backupJobs = csv.JobCsvParser();
                var pluginJobs = csv.GetDynamicPluginJobs();
                var agentBackups = csv.GetDynamicAgentBackupJob();
                var catalystJobs = csv.GetDynamicCatalystJob();
                var cdpJobs = csv.GetDynamicCdpJobs();
                var endpointJobs = csv.GetDynamicEndpointJob();
                var nasBackupJobs = csv.GetDynamicNasBackup();
                var nasBcj = csv.GetDynamicNasBCJ();
                var sureBackup = csv.GetDynamicSureBackupJob();
                var tapeJobs = csv.GetTapeJobInfoFromCsv();

                foreach(var bType in backupJobs.Select(x => x.JobType).Distinct())
                {

                   typeAndCount.Add(bType, backupJobs.Count(x => x.JobType == bType));
                }
                typeAndCount.Add("Plugin", pluginJobs.Count());
                typeAndCount.Add("Agent Backup", agentBackups.Count());
                typeAndCount.Add("Catalyst Copy", catalystJobs.Count());
                typeAndCount.Add("CDP", cdpJobs.Count());
                typeAndCount.Add("Unmanaged Agent", endpointJobs.Count());
                typeAndCount.Add("NasBackup", nasBackupJobs.Count());
                typeAndCount.Add("NasBCJ", nasBcj.Count());
                typeAndCount.Add("SureBackup", sureBackup.Count());
                typeAndCount.Add("Tape", tapeJobs.Count());

                foreach(string dbType in Enum.GetNames(typeof(EDbJobType)))
                {
                    string humanReadable = CJobTypesParser.GetJobType(dbType);
                    if(!typeAndCount.ContainsKey(dbType))
                    {
                        typeAndCount.Add(dbType, 0);
                    }
                }

                //sort the dictionary
                typeAndCount = typeAndCount.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            }

            catch (Exception ex) { CGlobals.Logger.Error(ex.Message); }


            return typeAndCount;
        }
    }
}
