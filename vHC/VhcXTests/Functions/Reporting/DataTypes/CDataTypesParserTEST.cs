//using CsvHelper;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using VeeamHealthCheck.Functions.Reporting.DataTypes;
//using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
//using VeeamHealthCheck.Functions.Reporting.DataTypes;
//using Moq;

//namespace VhcXTests.Functions.Reporting.DataTypes
//{
//    public class CDataTypesParserTEST
//    {
//        //[Fact]
//        //public void JobInfo_GetDynamicBjobs_Success()
//        //{
//        //    var parser = new CCsvParser();
//        //    var bjobs = parser.GetDynamicBjobs();

//        //    Assert.NotNull(bjobs);
//        //}
//        [Fact]
//        public void JobInfo_GetDynamicBjobs_Fail()
//        {

//            var parser = new CCsvParser();
//            IEnumerable<dynamic> bjobs = null;
//            Assert.Null(bjobs);
//        }

//        //[Fact]
//        //public void SobrInfo_Null_Success()
//        //{
//        //    CDataTypesParser p = new();
//        //    var result = p.SobrInfo;

//        //    Assert.NotNull(result);
//        //}
//        //[Fact]
//        //public void SobrInfo_Null_Fail()
//        //{
//        //    CDataTypesParser p = new();
//        //    var result = p.SobrInfo;

//        //    Assert.Null(result);
//        //}
//        //[Fact]
//        //public void SobrInfo_List_Success()
//        //{
//        //    var parser = new Mock<CDataTypesParser>();
//        //    parser.Setup(x => x.SobrInfo).Returns(new List<CSobrTypeInfos>());

//        //    Assert.NotNull(parser);
//        //}
//        [Fact]
//        public void SobrInfo_EmptyList_Fail()
//        {

//        }


//        [Fact]
//        public void SobrCsvResults_ReturnList_Success()
//        {
//            var parser = new Mock<CDataTypesParser>();
            
//        }
//    }
//}
