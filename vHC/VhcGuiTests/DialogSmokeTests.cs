using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VeeamHealthCheck.Functions.AboutDialog;
using VeeamHealthCheck.Functions.UserInteraction;
using Xunit;
using static VhcGuiTests.UiDriver;

namespace VhcGuiTests
{
    public class DialogSmokeTests : GuiTestBase
    {
        [AvaloniaFact]
        public async Task AboutDisclaimerDialog_ClickClose_Closes()
        {
            var owner = ShowOwner();
            var dialog = Track(new AboutDisclaimerDialog());
            var closed = dialog.ShowDialog(owner);

            // Never click kbLink: it launches a browser.
            var link = FindRequired<HyperlinkButton>(dialog, "kbLink");
            Assert.Equal(new Uri("https://www.veeam.com/kb2462"), link.NavigateUri);
            // Run is an inline, not a Control, so FindControl<T> can't reach it; use the name scope.
            var header = Assert.IsType<Run>(NameScope.GetNameScope(dialog).Find("InsHeader"));
            Assert.False(string.IsNullOrEmpty(header.Text));

            var close = dialog.GetVisualDescendants().OfType<Button>().Single(b => b.IsCancel);
            Click(dialog, close);

            await Until(() => closed.IsCompleted);
            Assert.Empty(owner.OwnedWindows);
        }

        [AvaloniaFact]
        public async Task AvaloniaUiNotifier_ConfirmClickNo_ReturnsFalse()
        {
            var owner = ShowOwner();

            var answer = new AvaloniaUiNotifier().ConfirmAsync("msg", "title");
            var dialog = Owned<NotifierDialog>(owner);
            Track(dialog);

            var no = FindRequired<Button>(dialog, "NoButton");
            Assert.True(no.IsVisible);
            Assert.Equal("Yes", FindRequired<Button>(dialog, "YesOkButton").Content);
            Assert.Equal("msg", FindRequired<TextBlock>(dialog, "MessageText").Text);

            Click(dialog, no);
            await Until(() => answer.IsCompleted);
            Assert.False(answer.Result);
        }
    }
}
