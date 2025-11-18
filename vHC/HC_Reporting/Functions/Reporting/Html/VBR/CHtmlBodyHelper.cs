// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;


namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CHtmlBodyHelper
    {
        private string HTMLSTRING;
        private readonly CHtmlTables _tables;
        private bool SCRUB;
        private CLogger log = CGlobals.Logger;
        private string logStart = "[VbrHtmlBodyHelper]\t";

        public CHtmlBodyHelper()
        {
            log.Info(logStart + ">>> ENTERING CHtmlBodyHelper constructor <<<");
            log.Info(logStart + "Creating CHtmlTables instance...");
            _tables = new CHtmlTables();
            log.Info(logStart + "About to call PopulateCsvToMemory()...");
            PopulateCsvToMemory();
            log.Info(logStart + "CHtmlBodyHelper constructor completed.");
        }
        public string FormVbrFullReport(string htmlString, bool scrub)
        {
            log.Info(logStart + ">>> ENTERING FormVbrFullReport() method <<<");
            log.Info(logStart + "Scrub mode: " + scrub);

            SCRUB = scrub;
            HTMLSTRING = htmlString;
            
            log.Info(logStart + "Generating LicenseTable...");
            LicenseTable();
            log.Info(logStart + "LicenseTable completed.");
            
            log.Info(logStart + "Generating BackupServerTable...");
            BackupServerTable();
            log.Info(logStart + "BackupServerTable completed.");
            
            log.Info(logStart + "Generating SecuritySummaryTable...");
            SecuritySummaryTable();
            log.Info(logStart + "SecuritySummaryTable completed.");
            
            log.Info(logStart + "Generating ServerSummaryTable...");
            ServerSummaryTable();
            log.Info(logStart + "ServerSummaryTable completed.");
            
            log.Info(logStart + "Generating JobSummaryTable...");
            JobSummaryTable();
            log.Info(logStart + "JobSummaryTable completed.");
            
            log.Info(logStart + "Generating MissingJobsTable...");
            MissingJobsTable();
            log.Info(logStart + "MissingJobsTable completed.");
            
            log.Info(logStart + "Generating ProtectedWorkloadsTable...");
            ProtectedWorkloadsTable();
            log.Info(logStart + "ProtectedWorkloadsTable completed.");
            
            log.Info(logStart + "Generating ManagedServersTable...");
            ManagedServersTable();
            log.Info(logStart + "ManagedServersTable completed.");
            
            log.Info(logStart + "Generating RegistryKeyTable...");
            RegistryKeyTable();
            log.Info(logStart + "RegistryKeyTable completed.");
            
            log.Info(logStart + "Generating ProxyTable...");
            ProxyTable();
            log.Info(logStart + "ProxyTable completed.");
            
            log.Info(logStart + "Generating SobrTable...");
            SobrTable();
            log.Info(logStart + "SobrTable completed.");
            
            log.Info(logStart + "Generating ExtentTable...");
            ExtentTable();
            log.Info(logStart + "ExtentTable completed.");
            
            log.Info(logStart + "Generating RepoTable...");
            RepoTable();
            log.Info(logStart + "RepoTable completed.");
            
            log.Info(logStart + "Generating JobConcurrencyTable...");
            JobConcurrencyTable();
            log.Info(logStart + "JobConcurrencyTable completed.");
            
            log.Info(logStart + "Generating TaskConcurrencyTable...");
            TaskConcurrencyTable();
            log.Info(logStart + "TaskConcurrencyTable completed.");
            
            log.Info(logStart + "Generating JobSessionSummaryTable...");
            JobSessionSummaryTable();
            log.Info(logStart + "JobSessionSummaryTable completed.");
            
            log.Info(logStart + "Generating JobInfoTable...");
            JobInfoTable();
            log.Info(logStart + "JobInfoTable completed.");

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {
                log.Info(logStart + "EXPORTINDIVIDUALJOBHTMLS is enabled (skipping IndividualJobHtmlBuilder for now).");
                //IndividualJobHtmlBuilder();

            }

            log.Info(logStart + "FormVbrFullReport() completed successfully.");
            return HTMLSTRING;
        }
        private void PopulateCsvToMemory(){
            log.Info(logStart + "Creating CDataTypesParser instance...");
            CGlobals.DtParser = new();
            log.Info(logStart + "CDataTypesParser instance created successfully.");
            log.Info(logStart + "Accessing ServerInfos property...");
            CGlobals.ServerInfo = CGlobals.DtParser.ServerInfos;
            log.Info(logStart + "PopulateCsvToMemory completed.");
        }
        public string FormSecurityReport(string htmlString)
        {
            HTMLSTRING = htmlString;
            //SecuritySummaryTable();
            FullSecurityTable();
            BackupServerTable();
            JobSummaryTable();
            ManagedServersTable();
            RegistryKeyTable();
            ProxyTable();
            SobrTable();
            ExtentTable();
            RepoTable();
            JobInfoTable();

            return HTMLSTRING;
        }
        private void LicenseTable()
        {
            HTMLSTRING += _tables.LicTable(SCRUB);
        }
        private void BackupServerTable()
        {
            HTMLSTRING += _tables.AddBkpSrvTable(SCRUB);
        }
        private void SecuritySummaryTable()
        {
            HTMLSTRING += _tables.AddSecSummaryTable(SCRUB);
        }
        private void FullSecurityTable()
        {
            HTMLSTRING += _tables.AddSecurityReportSecuritySummaryTable();
        }
        private void ServerSummaryTable()
        {
            HTMLSTRING += _tables.AddSrvSummaryTable(SCRUB);
        }
        private void JobSummaryTable()
        {
            HTMLSTRING += _tables.AddJobSummaryTable(SCRUB);
        }
        private void MissingJobsTable()
        {
            HTMLSTRING += _tables.AddMissingJobsTable(SCRUB);
        }
        private void ProtectedWorkloadsTable()
        {
            HTMLSTRING += _tables.AddProtectedWorkLoadsTable(SCRUB);

        }
        private void ManagedServersTable()
        {
            HTMLSTRING += _tables.AddManagedServersTable(SCRUB);

        }
        private void RegistryKeyTable()
        {
            HTMLSTRING += _tables.AddRegKeysTable(SCRUB);

        }
        private void ProxyTable()
        {
            HTMLSTRING += _tables.AddProxyTable(SCRUB);

        }
        private void SobrTable()
        {
            HTMLSTRING += _tables.AddSobrTable(SCRUB);

        }
        private void ExtentTable()
        {
            HTMLSTRING += _tables.AddSobrExtTable(SCRUB);

        }
        private void RepoTable()
        {
            HTMLSTRING += _tables.AddRepoTable(SCRUB);

        }
        private void JobConcurrencyTable()
        {
            HTMLSTRING += _tables.AddJobConTable(SCRUB);

        }
        private void TaskConcurrencyTable()
        {
            HTMLSTRING += _tables.AddTaskConTable(SCRUB);

        }
        private void JobSessionSummaryTable()
        {
            //HTMLSTRING += _tables.AddJobSessSummTable(SCRUB);
            HTMLSTRING += _tables.AddJobSessSummTableByJob(SCRUB);

        }
        private void JobInfoTable()
        {
            HTMLSTRING += _tables.AddJobInfoTable(SCRUB);

        }
        public void IndividualJobHtmlBuilder()
        {
            _tables.AddSessionsFiles(SCRUB);
        }
    }
}
