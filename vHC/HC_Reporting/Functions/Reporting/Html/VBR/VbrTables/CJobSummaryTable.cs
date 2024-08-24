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
                var backupJobs = csv.JobCsvParser().ToList();
                var pluginJobs = csv.GetDynamicPluginJobs();
                var agentBackups = csv.GetDynamicAgentBackupJob();
                var catalystJobs = csv.GetDynamicCatalystJob();
                var cdpJobs = csv.GetDynamicCdpJobs();
                var endpointJobs = csv.GetDynamicEndpointJob();
                var nasBackupJobs = csv.GetDynamicNasBackup();
                var nasBcj = csv.GetDynamicNasBCJ();
                var sureBackup = csv.GetDynamicSureBackupJob();
                var tapeJobs = csv.GetTapeJobInfoFromCsv();
                var types = backupJobs.Select(x => x.JobType).Distinct().ToList();

                typeAndCount.Add("Plugin", pluginJobs.Count());
                typeAndCount.Add("Agent Backup", agentBackups.Count());
                typeAndCount.Add("Catalyst Copy", catalystJobs.Count());
                typeAndCount.Add("CDP", cdpJobs.Count());
                typeAndCount.Add("Unmanaged Agent", endpointJobs.Count());
                typeAndCount.Add("File Backup", nasBackupJobs.Count());
                typeAndCount.Add("File Backup - Copy", nasBcj.Count());
                typeAndCount.Add("SureBackup", sureBackup.Count());
                typeAndCount.Add("Tape", tapeJobs.Count());
                try
                {
                    foreach (var bType in types)
                    {
                        if (bType == "NasBackup" || bType == "NasBackupCopy")
                            continue;
                        var realType = CJobTypesParser.GetJobType(bType);
                        if (!typeAndCount.ContainsKey(realType))
                        {
                            try
                            {
                                typeAndCount.Add(realType, backupJobs.Count(x => x.JobType == bType));

                            }
                            catch (Exception ex) { CGlobals.Logger.Error(ex.Message); }
                        }
                    }
                }
                catch (Exception ex) { CGlobals.Logger.Error(ex.Message); }


                foreach (string dbType in Enum.GetNames(typeof(EDbJobType)))
                {
                    string humanReadable = CJobTypesParser.GetJobType(dbType);
                    if(!typeAndCount.ContainsKey(humanReadable))
                    {
                        typeAndCount.Add(humanReadable, 0);
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
