using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Functions.Reporting.Html;

namespace VhcXTests.Functions.Reporting.Html.VBR.VbrTables
{
    public class CJobSessSummaryTEST
    {
        private string _originalPath = CVariables._vbrDir;
        [Fact]
        public void CDataFormer_JobSessionSummary_Success()
        {
            CDataFormer df = new();
            var result = df.ConvertJobSessSummaryToXml(false);

            Assert.NotNull(result);
        }
        [Fact]
        public void CDataFormer_IndividualSessions_Scrub_Success()
        {
            CDataFormer df = new();
            var res = df.JobSessionInfoToXml(true);
            Assert.Equal(0, res);
        }
        [Fact]
        public void CDataFormer_IndividualSessions_NoScrub_Success()
        {
            CDataFormer df = new();
            var res = df.JobSessionInfoToXml(false);
            Assert.Equal(0, res);
        }
        [Fact]
        public void CDataFormer_IndividualSessions_NoScrub_NoFile_Success()
        {
            CCsvParser parser = new();
            string originalPath = parser._sessionPath;
            parser._sessionPath = "/Fart";
            CDataFormer df = new();
            var res = df.JobSessionInfoToXml(false);
            Assert.Equal(0, res);

            parser._sessionPath = originalPath;
        }
        [Fact]
        public void CDataFormer_ParserNonProtectedTypes_Default()
        {
            CDataFormer df = new();
            var res = df.ParseNonProtectedTypes();

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_SecSummary_Default()
        {
            CDataFormer df = new();
            var res = df.SecSummary();

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ServerSummaryToXml_Default()
        {
            CDataFormer df = new();
            var res = df.ServerSummaryToXml();

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ProtectedWorkloadsToXml_FilePresent_Default()
        {
            CDataFormer df = new();
            var res = df.ProtectedWorkloadsToXml();

            Assert.Equal(0, res);
        }
        [Fact]
        public void CDataFormer_ProtectedWorkloadsToXml_NoFilePresent_Fail()
        {
            string originalDir = CVariables.vbrDir;
            CVariables._vbrDir = @"C:\temp\vHC\nothingness";

            CDataFormer df = new();
            var res = df.ProtectedWorkloadsToXml();

            Assert.Equal(1, res);
            CVariables._vbrDir = originalDir;
        }
        [Fact]
        public void CDataFormer_BackupServerInfoToXml_Scrub_File_Default()
        {
            CDataFormer df = new();
            var res = df.BackupServerInfoToXml(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_BackupServerInfoToXml_NoScrub_File_Default()
        {
            CDataFormer df = new();
            var res = df.BackupServerInfoToXml(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_BackupServerInfoToXml_Scrub_NoFile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.BackupServerInfoToXml(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_BackupServerInfoToXml_NoScrub_NoFile_Default()
        {
            CDataFormer df = new();
            var res = df.BackupServerInfoToXml(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_SobrInfoToXml_NoScrub_Default()
        {
            CDataFormer df = new();
            var res = df.SobrInfoToXml(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_SobrInfoToXml_Scrub_Default()
        {
            CDataFormer df = new();
            var res = df.SobrInfoToXml(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_SobrInfoToXml_NoScrub_NoFile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.SobrInfoToXml(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_SobrInfoToXml_Scrub_NoFile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.SobrInfoToXml(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_ExtentXmlFromCsv_Scrub_Default()
        {
            CDataFormer df = new();
            var res = df.ExtentXmlFromCsv(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ExtentXmlFromCsv_NoScrubDefault()
        {
            CDataFormer df = new();
            var res = df.ExtentXmlFromCsv(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ExtentXmlFromCsv_Scrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ExtentXmlFromCsv(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_ExtentXmlFromCsv_NoScrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ExtentXmlFromCsv(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_RepoInfoToXml_Scrub_Default()
        {
            CDataFormer df = new();
            var res = df.RepoInfoToXml(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_RepoInfoToXml_NoScrubDefault()
        {
            CDataFormer df = new();
            var res = df.RepoInfoToXml(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_RepoInfoToXml_Scrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.RepoInfoToXml(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_RepoInfoToXml_NoScrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.RepoInfoToXml(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }

        [Fact]
        public void CDataFormer_ProxyXmlFromCsv_Scrub_Default()
        {
            CDataFormer df = new();
            var res = df.ProxyXmlFromCsv(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ProxyXmlFromCsv_NoScrubDefault()
        {
            CDataFormer df = new();
            var res = df.ProxyXmlFromCsv(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ProxyXmlFromCsv_Scrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ProxyXmlFromCsv(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_ProxyXmlFromCsv_NoScrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ProxyXmlFromCsv(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_ServerXmlFromCsv_Scrub_Default()
        {
            CDataFormer df = new();
            var res = df.ServerXmlFromCsv(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ServerXmlFromCsv_NoScrubDefault()
        {
            CDataFormer df = new();
            var res = df.ServerXmlFromCsv(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_ServerXmlFromCsv_Scrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ServerXmlFromCsv(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_ServerXmlFromCsv_NoScrub_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.ServerXmlFromCsv(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_JobSummaryInfoToXml_Nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.JobSummaryInfoToXml();

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_JobSummaryInfoToXml_Default()
        {
            CDataFormer df = new();
            var res = df.JobSummaryInfoToXml();

            Assert.NotNull(res);
        }

        [Fact]
        public void CDataFormer_JobConcurrency_Default()
        {
            CDataFormer df = new();
            var res = df.JobConcurrency(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_JobConcurrency_False_Default()
        {
            CDataFormer df = new();
            var res = df.JobConcurrency(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_JobConcurrency_nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.JobConcurrency(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_JobConcurrency_False_nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.JobConcurrency(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }

        [Fact]
        public void CDataFormer_RegOptions_False_Default()
        {
            CDataFormer df = new();
            var res = df.RegOptions();

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_RegOptions_nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.RegOptions();

            Assert.NotNull(res);
            SetPathBackToNormal();
        }

        [Fact]
        public void CDataFormer_JobInfoToXml_Default()
        {
            CDataFormer df = new();
            var res = df.JobInfoToXml(true);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_JobInfoToXml_False_Default()
        {
            CDataFormer df = new();
            var res = df.JobInfoToXml(false);

            Assert.NotNull(res);
        }
        [Fact]
        public void CDataFormer_JobInfoToXml_nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.JobInfoToXml(true);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }
        [Fact]
        public void CDataFormer_JobInfoToXml_False_nofile_Default()
        {
            SetFalsePath();
            CDataFormer df = new();
            var res = df.JobInfoToXml(false);

            Assert.NotNull(res);
            SetPathBackToNormal();
        }

        private void SetFalsePath()
        {
            CVariables._vbrDir = @"C:\temp\vHC\doesntexist";
        }
        private void SetPathBackToNormal()
        {
            CVariables._vbrDir = _originalPath;
        }
    }
}
