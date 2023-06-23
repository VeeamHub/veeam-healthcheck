using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.Html;

namespace VhcXTests.Functions.Reporting.Html
{
    public class CHtmlExporterTEST
    {
        [Fact]
        public void ExportVb365_Result_Positive()
        {
            Thread.Sleep(10);
            CHtmlExporter e = new("TestSystem");
            int res = e.ExportVb365Html("testHTML");

            Assert.Equal(0, res);

        }
        [Fact]
        public void ExportVbr_Scrub_Positive()
        {
            Thread.Sleep(200);
            CHtmlExporter e = new("TestSystem");
            var res = e.ExportVbrHtml("fakeString", true);

            Assert.Equal(0, res);
        }
        [Fact]
        public void ExportVbr_NoScrub_Positive()
        {
            Thread.Sleep(300);
            CHtmlExporter e = new("TestSystem");
            var res = e.ExportVbrHtml("fakeString", false);

            Assert.Equal(0, res);
        }
        //[Fact]
        //public void ExportSecurity_Scrub_Positive()
        //{
        //    CHtmlExporter e = new("TestSystem");
        //    var res = e.ExportVbrSecurityHtml("fakeString", true);

        //    Assert.Equal(0, res);
        //}

        //[Fact]
        //public void OpenExplorerIfEnabled_True_Success()
        //{
        //    CHtmlExporter e = new("TestServer");
        //    var res = e.OpenHtmlIfEnabled(true);

        //    Assert.Equal(0, res);   
        //}
        [Fact]
        public void OpenExplorerIfEnabled_False_Success()
        {
            CHtmlExporter e = new("TestServer");
            var res = e.OpenHtmlIfEnabled(false);

            Assert.Equal(1, res);
        }
    }
}
