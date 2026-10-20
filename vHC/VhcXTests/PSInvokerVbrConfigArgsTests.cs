// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// The experimental Orphaned &amp; Superseded Backups collection is benched unless VHC_EXPERIMENTS
    /// is set. PSInvoker carries that to Get-VBRConfig.ps1 as the explicit -Experiments switch, the
    /// same way -RescanHosts travels, so the collector never reads the environment variable itself.
    /// </summary>
    [Collection("GlobalState")]
    [Trait("Category", "OrphanedSupersededBackups")]
    public class PSInvokerVbrConfigArgsTests
    {
        private const string Script = @"C:\scripts\Get-VBRConfig.ps1";

        private static string Build(bool rescanHosts = false, bool remoteExec = false) =>
            PSInvoker.BuildVbrConfigArgs(Script, "srv", 13, 14, rescanHosts, remoteExec);

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        [InlineData("TRUE")]
        public void BuildVbrConfigArgs_ExperimentsEnabled_PassesExperimentsSwitch(string flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            Assert.Contains("-Experiments ", Build());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("yes")]
        public void BuildVbrConfigArgs_ExperimentsDisabled_OmitsExperimentsSwitch(string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            Assert.DoesNotContain("-Experiments", Build());
        }

        [Fact]
        public void BuildVbrConfigArgs_RescanHostsAndRemoteExecution_StillPassed()
        {
            using var scope = new ExperimentsFlagScope(null);

            string args = Build(rescanHosts: true, remoteExec: true);

            Assert.Contains("-RescanHosts ", args);
            Assert.Contains("-RemoteExecution ", args);
            Assert.Contains("-VBRServer \"srv\" -VBRVersion \"13\" -ReportInterval 14 ", args);
        }
    }
}
