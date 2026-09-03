using VeeamHealthCheck;
using Xunit;

namespace VhcXTests
{
    public class CAdminCheckTests
    {
        [Fact]
        public void IsAdmin_AnyOperatingSystem_DoesNotThrow()
        {
            var check = new CAdminCheck();

            var exception = Record.Exception(() => check.IsAdmin());

            Assert.Null(exception);
        }
    }
}
