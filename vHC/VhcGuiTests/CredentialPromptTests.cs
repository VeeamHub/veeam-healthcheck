using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Linq;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Startup;
using Xunit;
using static VhcGuiTests.UiDriver;

namespace VhcGuiTests
{
    public class CredentialPromptTests : GuiTestBase
    {
        [AvaloniaFact]
        public async Task AvaloniaCredentialPrompter_TypeCredsAndPressEnter_PersistsHostAndServer()
        {
            // AddServer leaves a never-seeded (null) list alone, so seed one first.
            SeedServers("vbr01");
            var owner = Track(new Window());
            owner.Show();
            AvaloniaHost.MainWindow = owner;

            var prompt = new AvaloniaCredentialPrompter().PromptAsync("vbr03");
            var dialog = Owned<CredentialPromptWindow>(owner);
            Track(dialog);

            Type(dialog, FindRequired<TextBox>(dialog, "UsernameBox"), "svc");
            Type(dialog, FindRequired<TextBox>(dialog, "PasswordBox"), "pw");

            // Connect is the IsDefault button; Enter must fire it.
            dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await Until(() => prompt.IsCompleted);

            var creds = await prompt;
            Assert.NotNull(creds);
            Assert.Equal("svc", creds.Value.Username);
            Assert.Equal("pw", creds.Value.Password);
            Assert.NotNull(CredentialStore.Get("vbr03"));
            Assert.Contains("vbr03", CAppSettings.Get().Servers);
        }
    }
}
