using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;

namespace VhcXTests.Functions.Reporting.DataTypes
{
    public class CDataTypesParserTEST
    {
        [Fact]
        public void JobInfo_GetDynamicBjobs_Success()
        {
            var parser = new CCsvParser();
            var bjobs = parser.GetDynamicBjobs();

            Assert.NotNull(bjobs);
        }
        [Fact]
        public void JobInfo_GetDynamicBjobs_Fail()
        {

            var parser = new CCsvParser();
            var bjobs = parser.GetDynamicBjobs();
            bjobs = null;
            Assert.Null(bjobs);
        }
    }
}
