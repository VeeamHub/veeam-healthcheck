using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using VeeamHealthCheck;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(VhcGuiTests.TestAppBuilder))]

// One headless Application and one UI thread are shared by the whole assembly and every
// test touches the same statics, so the project runs serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VhcGuiTests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // App.OnFrameworkInitializationCompleted reads CAppSettings once per session,
            // before any test constructor runs, so the stores must be redirected first.
            var dir = Path.Combine(Path.GetTempPath(), "vhcgui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            IsolatedStores.Redirect(dir);

            // Skia with real drawing is required for CaptureRenderedFrame().
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
    }
}
