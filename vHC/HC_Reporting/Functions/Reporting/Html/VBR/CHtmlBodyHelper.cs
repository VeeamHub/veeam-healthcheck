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
        private readonly CHtmlTables tables;
        private bool SCRUB;
        private readonly CLogger log = CGlobals.Logger;
        private readonly string logStart = "[VbrHtmlBodyHelper]\t";

        public CHtmlBodyHelper()
        {
            this.log.Info(this.logStart + ">>> ENTERING CHtmlBodyHelper constructor <<<");
            this.log.Info(this.logStart + "Creating CHtmlTables instance...");
            this.tables = new CHtmlTables();
            this.log.Info(this.logStart + "About to call PopulateCsvToMemory()...");
            this.PopulateCsvToMemory();
            this.log.Info(this.logStart + "CHtmlBodyHelper constructor completed.");
        }

        public string FormVbrFullReport(string htmlString, bool scrub)
        {
            this.log.Info(this.logStart + ">>> ENTERING FormVbrFullReport() method <<<");
            this.log.Info(this.logStart + "Scrub mode: " + scrub);

            this.SCRUB = scrub;
            this.HTMLSTRING = htmlString;
            
            this.log.Info(this.logStart + "Generating LicenseTable...");
            this.LicenseTable();
            this.log.Info(this.logStart + "LicenseTable completed.");
            
            this.log.Info(this.logStart + "Generating DataCollectionSummaryTable...");
            this.DataCollectionSummaryTable();
            this.log.Info(this.logStart + "DataCollectionSummaryTable completed.");
            
            this.log.Info(this.logStart + "Generating BackupServerTable...");
            this.BackupServerTable();
            this.log.Info(this.logStart + "BackupServerTable completed.");
            
            this.log.Info(this.logStart + "Generating SecuritySummaryTable...");
            this.SecuritySummaryTable();
            this.log.Info(this.logStart + "SecuritySummaryTable completed.");
            
            this.log.Info(this.logStart + "Generating ServerSummaryTable...");
            this.ServerSummaryTable();
            this.log.Info(this.logStart + "ServerSummaryTable completed.");
            
            this.log.Info(this.logStart + "Generating Configuration Tables section...");
            this.ConfigurationTablesSection();
            this.log.Info(this.logStart + "Configuration Tables section completed.");
            
            this.log.Info(this.logStart + "Generating RegistryKeyTable...");
            this.RegistryKeyTable();
            this.log.Info(this.logStart + "RegistryKeyTable completed.");
            
            this.log.Info(this.logStart + "Generating ProxyTable...");
            this.ProxyTable();
            this.log.Info(this.logStart + "ProxyTable completed.");
            
            this.log.Info(this.logStart + "Generating SobrTable...");
            this.SobrTable();
            this.log.Info(this.logStart + "SobrTable completed.");
            
            this.log.Info(this.logStart + "Generating ExtentTable...");
            this.ExtentTable();
            this.log.Info(this.logStart + "ExtentTable completed.");
            
            this.log.Info(this.logStart + "Generating RepoTable...");
            this.RepoTable();
            this.log.Info(this.logStart + "RepoTable completed.");
            
            this.log.Info(this.logStart + "Generating JobConcurrencyTable...");
            this.JobConcurrencyTable();
            this.log.Info(this.logStart + "JobConcurrencyTable completed.");
            
            this.log.Info(this.logStart + "Generating TaskConcurrencyTable...");
            this.TaskConcurrencyTable();
            this.log.Info(this.logStart + "TaskConcurrencyTable completed.");
            
            this.log.Info(this.logStart + "Generating JobSessionSummaryTable...");
            this.JobSessionSummaryTable();
            this.log.Info(this.logStart + "JobSessionSummaryTable completed.");
            
            this.log.Info(this.logStart + "Generating JobInfoTable...");
            this.JobInfoTable();
            this.log.Info(this.logStart + "JobInfoTable completed.");

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {
                this.log.Info(this.logStart + "EXPORTINDIVIDUALJOBHTMLS is enabled (skipping IndividualJobHtmlBuilder for now).");

                // IndividualJobHtmlBuilder();
            }

            this.log.Info(this.logStart + "FormVbrFullReport() completed successfully.");
            return this.HTMLSTRING;
        }

        private void PopulateCsvToMemory(){
            this.log.Info(this.logStart + "Creating CDataTypesParser instance...");
            CGlobals.DtParser = new();
            this.log.Info(this.logStart + "CDataTypesParser instance created successfully.");
            this.log.Info(this.logStart + "Accessing ServerInfos property...");
            CGlobals.ServerInfo = CGlobals.DtParser.ServerInfos;
            this.log.Info(this.logStart + "PopulateCsvToMemory completed.");
        }

        public string FormSecurityReport(string htmlString)
        {
            this.HTMLSTRING = htmlString;

            // SecuritySummaryTable();
            this.FullSecurityTable();
            this.BackupServerTable();
            this.JobSummaryTable();
            this.ManagedServersTable();
            this.RegistryKeyTable();
            this.ProxyTable();
            this.SobrTable();
            this.ExtentTable();
            this.RepoTable();
            this.JobInfoTable();

            return this.HTMLSTRING;
        }

        private void LicenseTable()
        {
            this.HTMLSTRING += this.tables.LicTable(this.SCRUB);
        }

        private void DataCollectionSummaryTable()
        {
            this.HTMLSTRING += this.tables.DataCollectionSummaryTable();
        }

        private void BackupServerTable()
        {
            this.HTMLSTRING += this.tables.AddBkpSrvTable(this.SCRUB);
        }

        private void SecuritySummaryTable()
        {
            this.HTMLSTRING += this.tables.AddSecSummaryTable(this.SCRUB);
        }

        private void FullSecurityTable()
        {
            this.HTMLSTRING += this.tables.AddSecurityReportSecuritySummaryTable();
        }

        private void ServerSummaryTable()
        {
            this.HTMLSTRING += this.tables.AddSrvSummaryTable(this.SCRUB);
        }

        private void JobSummaryTable()
        {
            this.HTMLSTRING += this.tables.AddJobSummaryTable(this.SCRUB);
        }

        private void ConfigurationTablesSection()
        {
            // Add parent header for Configuration Tables
            this.HTMLSTRING += this.tables.AddConfigurationTablesHeader();
            
            // Add child configuration tables
            this.JobSummaryTable();
            this.MissingJobsTable();
            this.ProtectedWorkloadsTable();
            this.ManagedServersTable();
            
            // Close the parent section
            this.HTMLSTRING += this.tables.AddConfigurationTablesFooter();
        }

        private void MissingJobsTable()
        {
            this.HTMLSTRING += this.tables.AddMissingJobsTable(this.SCRUB);
        }

        private void ProtectedWorkloadsTable()
        {
            this.HTMLSTRING += this.tables.AddProtectedWorkLoadsTable(this.SCRUB);
        }

        private void ManagedServersTable()
        {
            this.HTMLSTRING += this.tables.AddManagedServersTable(this.SCRUB);
        }

        private void RegistryKeyTable()
        {
            this.HTMLSTRING += this.tables.AddRegKeysTable(this.SCRUB);
        }

        private void ProxyTable()
        {
            this.HTMLSTRING += this.tables.AddProxyTable(this.SCRUB);
        }

        private void SobrTable()
        {
            this.HTMLSTRING += this.tables.AddSobrTable(this.SCRUB);
        }

        private void ExtentTable()
        {
            this.HTMLSTRING += this.tables.AddSobrExtTable(this.SCRUB);
        }

        private void RepoTable()
        {
            this.HTMLSTRING += this.tables.AddRepoTable(this.SCRUB);
        }

        private void JobConcurrencyTable()
        {
            this.HTMLSTRING += this.tables.AddJobConTable(this.SCRUB);
        }

        private void TaskConcurrencyTable()
        {
            this.HTMLSTRING += this.tables.AddTaskConTable(this.SCRUB);
        }

        private void JobSessionSummaryTable()
        {
            // HTMLSTRING += _tables.AddJobSessSummTable(SCRUB);
            this.HTMLSTRING += this.tables.AddJobSessSummTableByJob(this.SCRUB);
        }

        private void JobInfoTable()
        {
            this.HTMLSTRING += this.tables.AddJobInfoTable(this.SCRUB);
        }

        public void IndividualJobHtmlBuilder()
        {
            this.tables.AddSessionsFiles(this.SCRUB);
        }
    }
}
