using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Startup;

namespace VhcXTests
{
    public class EntryPointTEST
    {
        [Fact]
        public void Main_WithArgs_Success()
        {
            string[] args = new string[1] { "run" };
            var result = EntryPoint.Main(args);

            Assert.Equal(1, result);

        }
        [Fact]
        public void Main_WithArgs_Fail()
        {
            string[] args = new string[1] { "rum" };
            var result = EntryPoint.Main(args);

            Assert.Equal(0, result);
        }
    }
}
