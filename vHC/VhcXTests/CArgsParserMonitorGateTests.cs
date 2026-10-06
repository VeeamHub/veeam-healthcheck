// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CArgsParserMonitorGateTests
    {
        [Theory]
        [InlineData("/monitor:setup", null)]
        [InlineData("/monitor:run", null)]
        [InlineData("/monitor:status", null)]
        [InlineData("/monitor:disable", null)]
        [InlineData("/monitor:disable", "")]
        [InlineData("/monitor:disable", "0")]
        [InlineData("/monitor:disable", "false")]
        [InlineData("/monitor:status", "yes")]
        public void TryHandleMonitorCommand_ExperimentsOff_DoesNotHandleTheArgument(string arg, string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);
            var parser = new CArgsParser(new string[] { });

            bool handled = parser.TryHandleMonitorCommand(arg, out int exitCode);

            Assert.False(handled);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("/run")]
        [InlineData("/gui")]
        [InlineData("/monitor")]
        [InlineData("/monitor:")]
        [InlineData("/monitor:bogus")]
        public void TryHandleMonitorCommand_ExperimentsOn_IgnoresArgumentsThatAreNotMonitorCommands(string arg)
        {
            using var scope = new ExperimentsFlagScope("1");
            var parser = new CArgsParser(new string[] { });

            bool handled = parser.TryHandleMonitorCommand(arg, out int exitCode);

            Assert.False(handled);
            Assert.Equal(0, exitCode);
        }
    }
}
