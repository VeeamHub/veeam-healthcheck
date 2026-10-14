// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CMessagesHelpMenuTests
    {
        [Fact]
        public void HelpMenu_DoesNotMentionMonitoring()
        {
            string help = CMessages.helpMenu;

            Assert.DoesNotContain("CONTINUOUS MONITORING", help);
            Assert.DoesNotContain("/monitor:", help);
        }

        [Fact]
        public void HelpMenu_KeepsNeighbouringSectionsSeparatedByOneBlankLine()
        {
            string help = CMessages.helpMenu.Replace("\r\n", "\n");

            Assert.Contains("troubleshooting\n\nUNATTENDED / SILENT MODE:", help);
        }
    }
}
