using System.IO;
using VeeamHealthCheck.Startup;

namespace VhcGuiTests
{
    internal static class IsolatedStores
    {
        // Intentionally mirrors VhcXTests/IsolatedCredentialStore.cs, which this project can't
        // reference (Windows-only internal test project).
        public static void Redirect(string dir)
        {
            CAppSettings.StorePath = Path.Combine(dir, "settings.json");
            CredentialStore.StorePath = Path.Combine(dir, "creds.json");
            CredentialStore.InitializeCache();
        }
    }
}
