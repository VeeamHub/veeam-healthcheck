//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
//using VeeamHealthCheck;
//using static System.Reflection.Metadata.BlobBuilder;

//namespace VhcXTests.Functions.Reporting.CsvHandlers
//{
//    public class CCsvParser_TEST
//    {
//        public CCsvParser_TEST()
//        {
            
//        }

//        [Fact]
//        public void CsvParser_NoFile_ReturnNull()
//        {
//            var parser = new CCsvParser();
//            CVariables._vbrDir = "\\log";

//            Assert.Null(parser.ProxyCsvParser());
//            Assert.Null(parser.SobrCsvParser());
//            Assert.Null(parser.RepoCsvParser());
//            Assert.Null(parser.CdpProxCsvParser());
//            Assert.Null(parser.NasProxCsvParser());
//            Assert.Null(parser.HvProxCsvParser());
//            Assert.Null(parser.CapTierCsvParser());
//            Assert.Null(parser.ConfigBackupCsvParser());
//            Assert.Null(parser.ServerCsvParser());
//            Assert.Null(parser.BJobCsvParser());
//            Assert.Null(parser.JobCsvParser());
//            Assert.Null(parser.WanParser());
//            Assert.Null(parser.PluginCsvParser());
//            Assert.Null(parser.HvUnProtectedReader());
//            Assert.Null(parser.GetDynamicLicenseCsv());
//            Assert.Null(parser.GetDynamicVbrInfo());
//            Assert.Null(parser.GetDynamicConfigBackup());
//            Assert.Null(parser.GetPhysProtected());
//            Assert.Null(parser.GetPhysNotProtected());
//            Assert.Null(parser.GetDynamicJobInfo());
//            Assert.Null(parser.GetDynamicBjobs());
//            Assert.Null(parser.GetDynamincConfigBackup());
//            Assert.Null(parser.GetDynamincNetRules());
//            Assert.Null(parser.GetDynamicRepo());
//            Assert.Null(parser.GetDynamicSobrExt());
//            Assert.Null(parser.GetDynamicSobr());
//            Assert.Null(parser.GetDynamicCapTier());
//            Assert.Null(parser.GetDynViProxy());
//            Assert.Null(parser.GetDynHvProxy());
//            Assert.Null(parser.GetDynNasProxy());
//            Assert.Null(parser.GetDynCdpProxy());
//            Assert.Null(parser.SessionCsvParser());
//            Assert.Null(parser.BnrCsvParser());
//            Assert.Null(parser.WaitsCsvReader());
//            Assert.Null(parser.SobrExtParser());
//            Assert.Null(parser.ViProtectedReader());
//            Assert.Null(parser.ViUnProtectedReader());
//            Assert.Null(parser.HvProtectedReader());
//            Assert.Null(parser.SobrExtParser());
//            Assert.Null(parser.SobrExtParser());
//            Assert.Null(parser.SobrExtParser());
//            CVariables._vbrDir = "\\VBR";


//            CVariables._vb365Dir = "\\log";
//            Assert.Null(parser.GetDynamicVboGlobal());
//            Assert.Null(parser.GetDynamicVboProxies());
//            Assert.Null(parser.GetDynamicVboRbac());
//            Assert.Null(parser.GetDynamicVboJobs());
//            Assert.Null(parser.GetDynamicVboProcStat());
//            Assert.Null(parser.GetDynamicVboRepo());
//            Assert.Null(parser.GetDynamicVboSec());
//            Assert.Null(parser.GetDynVboController());
//            Assert.Null(parser.GetDynVboControllerDriver());
//            Assert.Null(parser.GetDynVboJobSess());
//            Assert.Null(parser.GetDynVboJobStats());
//            Assert.Null(parser.GetDynVboObjRepo());
//            Assert.Null(parser.GetDynVboOrg());
//            Assert.Null(parser.GetDynVboPerms());
//            Assert.Null(parser.GetDynVboProtStat());
//            Assert.Null(parser.GetDynVboLicOver());
//            Assert.Null(parser.GetDynVboMbProtRep());
//            Assert.Null(parser.GetDynVboMbStgConsumption());
            
//            CVariables._vb365Dir = "\\VB365";
//        }
//        //[Fact]
//        //public void CsvParser_RealFile_Success()
//        //{
//        //    var parser = new CCsvParser();

//        //    Assert.NotNull(parser.SessionCsvParser());
//        //    Assert.NotNull(parser.BnrCsvParser());
//        //    Assert.NotNull(parser.SobrExtParser());
//        //    Assert.NotNull(parser.WaitsCsvReader());
//        //    Assert.NotNull(parser.ViProtectedReader());
//        //    Assert.NotNull(parser.ViUnProtectedReader());
//        //    Assert.NotNull(parser.HvProtectedReader());
//        //    Assert.NotNull(parser.HvUnProtectedReader());
//        //    Assert.NotNull(parser.RegOptionsCsvParser());
//        //    Assert.NotNull(parser.ConfigBackupCsvParser());
//        //    Assert.NotNull(parser.NetTrafficCsvParser());
//        //    Assert.NotNull(parser.CapTierCsvParser());
//        //    Assert.NotNull(parser.PluginCsvParser());
//        //    Assert.NotNull(parser.WanParser());
//        //    Assert.NotNull(parser.JobCsvParser());
//        //    Assert.NotNull(parser.BJobCsvParser());
//        //    Assert.NotNull(parser.ServerCsvParser());
//        //    Assert.NotNull(parser.ProxyCsvParser());
//        //    Assert.NotNull(parser.SobrCsvParser());
//        //    Assert.NotNull(parser.RepoCsvParser());
//        //    Assert.NotNull(parser.CdpProxCsvParser());
//        //    Assert.NotNull(parser.HvProxCsvParser());
//        //    Assert.NotNull(parser.NasProxCsvParser());



//        //    // VBO Tests
//        //    Assert.NotNull(parser.GetDynamicVboGlobal());
//        //    Assert.NotNull(parser.GetDynamicVboGlobal());
//        //    Assert.NotNull(parser.GetDynamicVboProxies());
//        //    Assert.NotNull(parser.GetDynamicVboRbac());
//        //    Assert.NotNull(parser.GetDynamicVboJobs());
//        //    Assert.NotNull(parser.GetDynamicVboProcStat());
//        //    Assert.NotNull(parser.GetDynamicVboRepo());
//        //    Assert.NotNull(parser.GetDynamicVboSec());
//        //    Assert.NotNull(parser.GetDynVboController());
//        //    Assert.NotNull(parser.GetDynVboControllerDriver());
//        //    Assert.NotNull(parser.GetDynVboJobSess());
//        //    Assert.NotNull(parser.GetDynVboJobStats());
//        //    Assert.NotNull(parser.GetDynVboObjRepo());
//        //    Assert.NotNull(parser.GetDynVboOrg());
//        //    Assert.NotNull(parser.GetDynVboPerms());
//        //    Assert.NotNull(parser.GetDynVboProtStat());
//        //    Assert.NotNull(parser.GetDynVboLicOver());
//        //    Assert.NotNull(parser.GetDynVboMbProtRep());
//        //    Assert.NotNull(parser.GetDynVboMbStgConsumption());
//        //}
//        [Fact]
//        public void FileFinder_Exception_Null()
//        {
//            CCsvReader r = new();
//            var res = r.FileFinder("", "");

//            Assert.Null(res);
//        }

//    }
//}
