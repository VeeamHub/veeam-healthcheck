using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VeeamHealthCheck.Functions.ManageServers;
using VeeamHealthCheck.Startup;
using Xunit;
using static VhcGuiTests.UiDriver;

namespace VhcGuiTests
{
    public class ManageServersDialogTests : GuiTestBase
    {
        [AvaloniaFact]
        public async Task ManageServersDialog_RemoveServerWithCredsAndConfirm_DeletesCredsAndServer()
        {
            SeedServers("vbr01", "vbr02");
            CredentialStore.Set("vbr01", "svc", "pw");
            Stub.ConfirmResult = true;

            var owner = ShowOwner();
            var dialog = new ManageServersDialog(new[] { "vbr01", "vbr02" }, Array.Empty<string>());
            Track(dialog);
            var result = dialog.ShowDialog<bool>(owner);

            var pending = FindRequired<TextBlock>(dialog, "pendingText");
            var before = pending.Text;
            var rows = FindRequired<ItemsControl>(dialog, "serverRows");
            var remove = rows.GetVisualDescendants().OfType<Button>().Single(b => (string)b.Tag == "vbr01");
            Click(dialog, remove);
            Assert.NotEqual(before, pending.Text);

            Click(dialog, FindRequired<Button>(dialog, "doneBtn"));
            await Until(() => result.IsCompleted);

            Assert.True(result.Result);
            Assert.Equal(1, Stub.Confirms);
            Assert.Null(CredentialStore.Get("vbr01"));
            Assert.Equal(new[] { "vbr02" }, CAppSettings.Get().Servers);
        }
    }
}
