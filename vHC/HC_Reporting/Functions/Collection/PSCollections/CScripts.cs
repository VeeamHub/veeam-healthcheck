using System;
using System.Management.Automation;

namespace VeeamHealthCheck.Functions.Collection.PSCollections
{
    internal class CScripts
    {
        public CScripts()
        {
            //RunPsScript(GetJobInfo());
            //RunPsScript("Get-VBRJob");
            //RunPsScript(File.ReadAllText(Environment.CurrentDirectory + @"\Tools\Scripts\Get-VBRConfig.ps1"));
        }
        //script to get vbr-job info
        public string GetJobInfo()
        {
            string script = @"
            Connect-VBRServer -Server localhost
            $jobs = Get-VBRJob
            $piJob = Get-VBRPluginJob
            $jobInfo = @()
            foreach ($job in $jobs)
            {
                $jobInfo += $job
            }

            $jobInfo
            ";
            return script;
        }

        //script to get job information from Veeam PowerShell
        //write a powershell script to save job information in a custom object containing members: jobName, jobType, sourceSizeUsedGB, sourceSizeProvisionedGb, Repository, daysOrPoints, retentionValue, GfsEnabled, GfsWeeklyCount, GfsMonthlyCount, GfsYearlyCount, SyntheticEnabled, ActiveFullEnabled, SyntheticDays, ActiveFullDays, compressionLevel, BlockSize, Encrypted, IndexingEnabled, schedule

        //execute powershell script returned from GetJobInfo() method

        private void RunPsScript(string script)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(script);
                ps.AddParameter("VBRServer", "localhost");//.AddParameter("localhost");
                ps.AddParameter("VBRVersion", "12");//.AddParameter("12");
                var results = ps.Invoke();
                foreach (var result in results)
                {
                    //do something with the results
                    Console.WriteLine(result);
                }
            }
        }
    }
}
