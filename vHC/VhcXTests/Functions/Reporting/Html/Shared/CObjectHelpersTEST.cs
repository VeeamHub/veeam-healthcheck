using Moq;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Collection.DB;

namespace VhcXTests.Functions.Reporting.Html.Shared
{
    public class CObjectHelpersTEST
    {
        [Fact]
        public void ParseBool_ValidString_True()
        {
            var result = CObjectHelpers.ParseBool("true");

            Assert.True(result);

        }
        [Fact]
        public void ParseBool_NotValidString_True()
        {
            var result = CObjectHelpers.ParseBool("truse");

            Assert.False(result);

        }
        [Fact]
        public void CBJobCsv_String_Success()
        {
            //var result = Mock<CBjobCsv>();
            CBjobCsv c = new();

            c.JobType = "test";
            c.Name = "testName";
            c.RepositoryId = "id";
            c.actualSize = "42";

            Assert.Equal("test", c.JobType);
            Assert.Equal("testName", c.Name);
            Assert.Equal("id", c.RepositoryId);
            Assert.Equal("42", c.actualSize);
        }

        [Fact]
        public void CRegReader_DbString_Null()
        {
            var reader = new CRegReader();
            var result = reader.DbString;

            Assert.Equal(null, result);
        }
        [Fact]
        public void CRegReader_HostString_Null()
        {
            var reader = new CRegReader();
            var result = reader.HostString;

            Assert.Equal(null, result);
        }

    }
}
