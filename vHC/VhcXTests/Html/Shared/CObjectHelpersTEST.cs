using Moq;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;

namespace VhcXTests.Html.Shared
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
    }
}
