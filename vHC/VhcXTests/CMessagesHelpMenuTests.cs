// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CMessagesHelpMenuTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("yes")]
        public void HelpMenu_ExperimentsOff_OmitsMonitorSection(string? flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            string help = CMessages.helpMenu;

            Assert.DoesNotContain("CONTINUOUS MONITORING", help);
            Assert.DoesNotContain("/monitor:", help);
            Assert.DoesNotContain("vhc-monitor", help);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        public void HelpMenu_ExperimentsOn_IncludesEveryMonitorCommand(string flag)
        {
            using var scope = new ExperimentsFlagScope(flag);

            string help = CMessages.helpMenu;

            Assert.Contains("CONTINUOUS MONITORING:", help);
            Assert.Contains("/monitor:setup", help);
            Assert.Contains("/monitor:run", help);
            Assert.Contains("/monitor:status", help);
            Assert.Contains("/monitor:disable", help);
        }

        [Fact]
        public void HelpMenu_ExperimentsOff_KeepsNeighbouringSectionsSeparatedByOneBlankLine()
        {
            using var scope = new ExperimentsFlagScope(null);

            string help = CMessages.helpMenu.Replace("\r\n", "\n");

            Assert.Contains("troubleshooting\n\nUNATTENDED / SILENT MODE:", help);
        }

        [Fact]
        public void HelpMenu_ExperimentsOn_PlacesMonitorSectionBetweenUtilityAndSilentSections()
        {
            using var scope = new ExperimentsFlagScope("1");

            string help = CMessages.helpMenu;

            int utility = help.IndexOf("UTILITY OPTIONS:", System.StringComparison.Ordinal);
            int monitor = help.IndexOf("CONTINUOUS MONITORING:", System.StringComparison.Ordinal);
            int silent = help.IndexOf("UNATTENDED / SILENT MODE:", System.StringComparison.Ordinal);

            Assert.True(utility >= 0 && utility < monitor && monitor < silent);
        }
    }
}
