﻿// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Shared;


namespace VeeamHealthCheck.Functions.Reporting.Html.VBR
{
    internal class CHtmlBodyHelper
    {
        private string HTMLSTRING;
        private readonly CHtmlTables _tables;
        private bool SCRUB;
        public CHtmlBodyHelper()
        {
            _tables = new CHtmlTables();
            PopulateCsvToMemory();
        }
        public string FormVbrFullReport(string htmlString, bool scrub)
        {


            SCRUB = scrub;
            HTMLSTRING = htmlString;
            LicenseTable();
            BackupServerTable();
            SecuritySummaryTable();
            ServerSummaryTable();
            JobSummaryTable();
            MissingJobsTable();
            ProtectedWorkloadsTable();
            ManagedServersTable();
            RegistryKeyTable();
            ProxyTable();
            SobrTable();
            ExtentTable();
            RepoTable();
            JobConcurrencyTable();
            TaskConcurrencyTable();
            JobSessionSummaryTable();
            JobInfoTable();

            if (CGlobals.EXPORTINDIVIDUALJOBHTMLS)
            {

                //IndividualJobHtmlBuilder();

            }

            return HTMLSTRING;
        }
        private void PopulateCsvToMemory(){
            CGlobals.DtParser = new();
            CGlobals.ServerInfo = CGlobals.DtParser.ServerInfos;
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
