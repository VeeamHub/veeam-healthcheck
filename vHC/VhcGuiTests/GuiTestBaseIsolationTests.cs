using System.IO;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcGuiTests
{
    public class GuiTestBaseIsolationTests
    {
        // A probe base is built and disposed inside the test, so "original" is whatever the
        // statics held when the test began and the result doesn't depend on other tests.
        private sealed class Probe : GuiTestBase
        {
            public string Settings => SettingsPath;

            public StubUiNotifier Notifier => Stub;

            public void Seed(string server) => SeedServers(server);
        }

        [Fact]
        public void Dispose_AfterMutatingStaticsAndSettings_RestoresOriginals()
        {
            var days = CGlobals.ReportDays;
            var scrub = CGlobals.Scrub;
            var remote = CGlobals.REMOTEEXEC;
            var vbr = CGlobals.IsVbr;
            var product = CGlobals.TargetProductType;
            var notifier = CGlobals.Notifier;
            var settingsPath = CAppSettings.StorePath;

            var probe = new Probe();
            Assert.Same(probe.Notifier, CGlobals.Notifier);
            CGlobals.ReportDays = 90;
            CGlobals.Scrub = !scrub;
            CGlobals.REMOTEEXEC = !remote;
            CGlobals.IsVbr = !vbr;
            CGlobals.TargetProductType = TargetProduct.Vbr;
            probe.Seed("leaky01");
            Assert.True(File.Exists(probe.Settings));
            Assert.Contains("leaky01", CAppSettings.Get().Servers);

            probe.Dispose();

            Assert.Equal(days, CGlobals.ReportDays);
            Assert.Equal(scrub, CGlobals.Scrub);
            Assert.Equal(remote, CGlobals.REMOTEEXEC);
            Assert.Equal(vbr, CGlobals.IsVbr);
            Assert.Equal(product, CGlobals.TargetProductType);
            Assert.Same(notifier, CGlobals.Notifier);
            Assert.Equal(settingsPath, CAppSettings.StorePath);
            Assert.False(Directory.Exists(Path.GetDirectoryName(probe.Settings)));
        }
    }
}
