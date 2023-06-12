using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    public class CCsvParser_TEST
    {
        public CCsvParser_TEST()
        {
            
        }

        [Fact]
        public void CsvParser_NoFile_Throw()
        {
            var parser = new CCsvParser();
            string fakeFile = "fakeFile.txt";

            //Assert.Throws<FileNotFoundException>(() => parser.SobrCsvParser(fakeFile)); 
            //Assert.Throws<FileNotFoundException>(() => parser.ProxyCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.RepoCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(()=> parser.CdpProxCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.NasProxCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.HvProxCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.CapTierCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.ConfigBackupCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.ServerCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.BJobCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.JobCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.WanParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.PluginCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.HvUnProtectedReader(fakeFile));



            Assert.Throws<FileNotFoundException>(() => parser.ProxyCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.SobrCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.RepoCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.CdpProxCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.NasProxCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.HvProxCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.CapTierCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.ConfigBackupCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.ServerCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.BJobCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.JobCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.WanParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.PluginCsvParser(fakeFile));
            Assert.Throws<FileNotFoundException>(() => parser.HvUnProtectedReader(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicLicenseCsv(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVbrInfo(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicConfigBackup(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetPhysProtected(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetPhysNotProtected(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicJobInfo(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicBjobs(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamincConfigBackup(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamincNetRules(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicRepo(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicSobrExt(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicSobr(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicCapTier(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynViProxy(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynHvProxy(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynNasProxy(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynCdpProxy(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.SessionCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.BnrCsvParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.WaitsCsvReader(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.SobrExtParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.ViProtectedReader(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.ViUnProtectedReader(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.HvProtectedReader(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.SobrExtParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.SobrExtParser(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.SobrExtParser(fakeFile));


            Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboGlobal(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboProxies(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboRbac(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboJobs(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboProcStat(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboRepo(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynamicVboSec(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboController(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboControllerDriver(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboJobSess(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboJobStats(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboObjRepo(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboOrg(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboPerms(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboProtStat(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboLicOver(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboMbProtRep(fakeFile));
            //Assert.Throws<FileNotFoundException>(() => parser.GetDynVboMbStgConsumption(fakeFile));
        }
        [Fact]
        public void CsvParser_RealFile_Success()
        {
            var parser = new CCsvParser();

            Assert.NotNull(parser.SobrCsvParser(parser._sobrReportName));
            Assert.NotNull(parser.SobrCsvParser(null));
            Assert.NotNull(parser.SobrCsvParser(""));

            Assert.NotNull(parser.ProxyCsvParser(parser._proxyReportName));
            Assert.NotNull(parser.ProxyCsvParser(null));
            Assert.NotNull(parser.ProxyCsvParser(""));

            Assert.NotNull(parser.RepoCsvParser(parser._repoReportName));
            Assert.NotNull(parser.RepoCsvParser(null));
            Assert.NotNull(parser.RepoCsvParser(""));

            Assert.NotNull(parser.CdpProxCsvParser(parser._cdpProxReportName));
            Assert.NotNull(parser.CdpProxCsvParser(null));
            Assert.NotNull(parser.CdpProxCsvParser(""));

            Assert.NotNull(parser.NasProxCsvParser(parser._nasProxReportName));
            Assert.NotNull(parser.NasProxCsvParser(null));
            Assert.NotNull(parser.NasProxCsvParser(""));

            Assert.NotNull(parser.HvProxCsvParser(parser._hvProxReportName));
            Assert.NotNull(parser.HvProxCsvParser(null));
            Assert.NotNull(parser.HvProxCsvParser(""));

            Assert.NotNull(parser.CapTierCsvParser(parser._capTier));
            Assert.NotNull(parser.CapTierCsvParser(null));
            Assert.NotNull(parser.CapTierCsvParser(""));

            Assert.NotNull(parser.ConfigBackupCsvParser(parser._configBackup));
            Assert.NotNull(parser.ConfigBackupCsvParser(null));
            Assert.NotNull(parser.ConfigBackupCsvParser(""));

            Assert.NotNull(parser.ServerCsvParser(parser._serverReportName));
            Assert.NotNull(parser.ServerCsvParser(null));
            Assert.NotNull(parser.ServerCsvParser(""));

            Assert.NotNull(parser.BJobCsvParser(parser._bjobs));
            Assert.NotNull(parser.BJobCsvParser(null));
            Assert.NotNull(parser.BJobCsvParser(""));

            Assert.NotNull(parser.JobCsvParser(parser._jobReportName));
            Assert.NotNull(parser.JobCsvParser(null));
            Assert.NotNull(parser.JobCsvParser(""));

            Assert.NotNull(parser.WanParser(parser._wanReportName));
            Assert.NotNull(parser.WanParser(null));
            Assert.NotNull(parser.WanParser(""));

            Assert.NotNull(parser.PluginCsvParser(parser._wanReportName));
            Assert.NotNull(parser.PluginCsvParser(null));
            Assert.NotNull(parser.PluginCsvParser(""));

            Assert.NotNull(parser.HvUnProtectedReader(parser._HvUnprotected));
            Assert.NotNull(parser.HvUnProtectedReader(null));
            Assert.NotNull(parser.HvUnProtectedReader(""));
        }
        [Fact]
        public void CsvParser_UnspecifiedFile_Default_Success()
        {
            var parser = new CCsvParser();

            Assert.NotNull(parser.ProxyCsvParser());
            Assert.NotNull(parser.SobrCsvParser());
            Assert.NotNull(parser.RepoCsvParser());
            Assert.NotNull(parser.CdpProxCsvParser());
            Assert.NotNull(parser.NasProxCsvParser());
            Assert.NotNull(parser.HvProxCsvParser());
            Assert.NotNull(parser.CapTierCsvParser());
            Assert.NotNull(parser.ConfigBackupCsvParser());
            Assert.NotNull(parser.ServerCsvParser());
            Assert.NotNull(parser.BJobCsvParser());
            Assert.NotNull(parser.JobCsvParser());
            Assert.NotNull(parser.WanParser());
            Assert.NotNull(parser.PluginCsvParser());
            Assert.NotNull(parser.HvUnProtectedReader());
            Assert.NotNull(parser.GetDynamicLicenseCsv());
            Assert.NotNull(parser.GetDynamicVbrInfo());
            Assert.NotNull(parser.GetDynamicConfigBackup());
            Assert.NotNull(parser.GetPhysProtected());
            Assert.NotNull(parser.GetPhysNotProtected());
            Assert.NotNull(parser.GetDynamicJobInfo());
            Assert.NotNull(parser.GetDynamicBjobs());
            Assert.NotNull(parser.GetDynamincConfigBackup());
            Assert.NotNull(parser.GetDynamincNetRules());
            Assert.NotNull(parser.GetDynamicRepo());
            Assert.NotNull(parser.GetDynamicSobrExt());
            Assert.NotNull(parser.GetDynamicSobr());
            Assert.NotNull(parser.GetDynamicCapTier());
            Assert.NotNull(parser.GetDynViProxy());
            Assert.NotNull(parser.GetDynHvProxy());
            Assert.NotNull(parser.GetDynNasProxy());
            Assert.NotNull(parser.GetDynCdpProxy());
            Assert.NotNull(parser.SessionCsvParser());
            Assert.NotNull(parser.BnrCsvParser());
            Assert.NotNull(parser.WaitsCsvReader());
            Assert.NotNull(parser.SobrExtParser());
            Assert.NotNull(parser.ViProtectedReader());
            Assert.NotNull(parser.ViUnProtectedReader());
            Assert.NotNull(parser.HvProtectedReader());
            Assert.NotNull(parser.SobrExtParser());
            Assert.NotNull(parser.SobrExtParser());
            Assert.NotNull(parser.SobrExtParser());

            //Assert.NotNull();
            //Assert.NotNull();
            Assert.NotNull(parser.GetDynamicVboGlobal());
            //Assert.NotNull(parser.GetDynamicVboProxies());
            //Assert.NotNull(parser.GetDynamicVboRbac());
            //Assert.NotNull(parser.GetDynamicVboJobs());
            //Assert.NotNull(parser.GetDynamicVboProcStat());
            //Assert.NotNull(parser.GetDynamicVboRepo());
            //Assert.NotNull(parser.GetDynamicVboSec());
            //Assert.NotNull(parser.GetDynVboController());
            //Assert.NotNull(parser.GetDynVboControllerDriver());
            //Assert.NotNull(parser.GetDynVboJobSess());
            //Assert.NotNull(parser.GetDynVboJobStats());
            //Assert.NotNull(parser.GetDynVboObjRepo());
            //Assert.NotNull(parser.GetDynVboOrg());
            //Assert.NotNull(parser.GetDynVboPerms());
            //Assert.NotNull(parser.GetDynVboProtStat());
            //Assert.NotNull(parser.GetDynVboLicOver());
            //Assert.NotNull(parser.GetDynVboMbProtRep());
            //Assert.NotNull(parser.GetDynVboMbStgConsumption());
        }
        [Theory]
        [InlineData("SOBRs")]
        [InlineData("Proxies")]
        public void VariableReturn_Test(string s)
        {
            CCsvParser p = new();
            Assert.NotNull(p.Test2());
        }
        /*
         
         
         
                 public IEnumerable<CCapTierCsv> CapTierCsvParser(string reportName)
        {
            if (String.IsNullOrEmpty(reportName))
            {
                return FileFinder(_capTier).GetRecords<CCapTierCsv>();
            }
            else
            {
                var reader = FileFinder(reportName);
                if (reader != null)
                    return reader.GetRecords<CCapTierCsv>();
                else
                {
                    throw new FileNotFoundException("File not found: " + reportName);
                }
            }
        }
         
         
         
         */
    }
}
