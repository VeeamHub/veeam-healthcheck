using System.IO;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcGuiTests
{
    // Tests run serially in declaration order per class; the second one proves the first one's
    // mutations did not survive GuiTestBase.Dispose.
    public class GuiTestBaseIsolationTests : GuiTestBase
    {
        [Fact]
        public void Baseline_MutateStaticsAndSettings_ChangesAreVisibleWithinTest()
        {
            CGlobals.ReportDays = 90;
            CGlobals.Scrub = true;
            CGlobals.REMOTEEXEC = true;
            CGlobals.IsVbr = true;
            CGlobals.TargetProductType = TargetProduct.Vbr;
            SeedServers("leaky01");

            Assert.True(File.Exists(SettingsPath));
            Assert.Contains("leaky01", CAppSettings.Get().Servers);
        }

        [Fact]
        public void Baseline_AfterPreviousTestMutations_StartsClean()
        {
            Assert.Equal(7, CGlobals.ReportDays);
            Assert.False(CGlobals.Scrub);
            Assert.False(CGlobals.REMOTEEXEC);
            Assert.False(CGlobals.IsVbr);
            Assert.Equal(TargetProduct.Auto, CGlobals.TargetProductType);
            Assert.False(File.Exists(SettingsPath));
            Assert.DoesNotContain("leaky01", CAppSettings.Get().Servers ?? new System.Collections.Generic.List<string>());
            Assert.Same(Stub, CGlobals.Notifier);
        }
    }
}
