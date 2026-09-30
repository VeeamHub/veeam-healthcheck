// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Monitor;
using Xunit;

namespace VhcXTests
{
    public class CVhcMonitorIntegrationTests
    {
        [Fact]
        public void CaptureStatus_ExeNotBundled_ReportsNothingElseWithoutProbing()
        {
            // The test host directory never contains vhc-monitor.exe, so the bundle
            // probe is false. The heavier probes (scheduled task, version, last run)
            // must then be skipped: they spawn processes and cannot affect the display.
            Assert.False(CVhcMonitorIntegration.IsExePresentInBundle());

            var snapshot = CVhcMonitorIntegration.CaptureStatus();

            Assert.False(snapshot.Bundled);
            Assert.False(snapshot.TaskActive);
            Assert.Null(snapshot.Version);
            Assert.Null(snapshot.LastRun);
        }
    }
}
