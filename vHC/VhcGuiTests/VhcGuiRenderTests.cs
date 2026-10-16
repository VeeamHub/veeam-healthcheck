using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using VeeamHealthCheck.Shared;
using Xunit;
using static VhcGuiTests.UiDriver;

namespace VhcGuiTests
{
    public class VhcGuiRenderTests : GuiTestBase
    {
        private static readonly string[] NamedControls =
        {
            "appHeaderText", "importButton", "importDivider", "ThemeToggleButton", "aboutButton",
            "serverCardTitle", "serverLabel", "serverSelector", "manageServersBtn", "productTypeLabel", "productTypeSelector",
            "outPath", "pathBox", "browseFolderBtn",
            "exportOptionsLabel", "pdfCheckBox", "explorerShowBox", "htmlCheckBox",
            "dataCollectionLabel", "collectionPeriodLabel", "days7", "days30", "days90", "RescanBox",
            "securityPrivacyLabel", "scrubBox", "clearCredsCheckBox",
            "termsCheckBox", "pBar", "progressText", "run",
        };

        [AvaloniaFact]
        public async Task VhcGui_RemoteServerPersisted_RendersWithAllNamedControls()
        {
            var w = await ShowReadyWindow();

            foreach (var name in NamedControls)
            {
                FindRequired<Control>(w, name);
            }

            Assert.False(w.FindControl<Button>("run").IsEnabled);
            Assert.False(w.FindControl<CheckBox>("pdfCheckBox").IsEnabled);
            Assert.Equal("Veeam Health Check - Remote Mode", w.Title);
            Assert.Equal("vbr01", w.FindControl<ComboBox>("serverSelector").SelectedItem);
            Assert.True(CGlobals.REMOTEEXEC);

            var frame = w.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.Equal(900, frame.Size.Width);
            Assert.Equal(620, frame.Size.Height);
        }
    }
}
