using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;
using Xunit.Abstractions;

namespace VhcGuiE2ETests
{
    /// <summary>
    /// What Avalonia.Headless cannot show: the real published exe, a real Win32 window, the real native folder
    /// picker. Controls are found by AutomationId. Avalonia's ControlAutomationPeer.GetAutomationIdCore returns
    /// AutomationProperties.AutomationId ?? Owner.Name, so the existing x:Name values are the ids (the same
    /// vocabulary VhcGuiTests uses). Each test launches its own exe and kills exactly that PID.
    /// </summary>
    public class VhcGuiE2ETests
    {
        private const string BaseTitle = "Veeam Health Check";
        private readonly ITestOutputHelper output;

        public VhcGuiE2ETests(ITestOutputHelper output) => this.output = output;

        [E2EFact]
        public void MainWindow_Launch_IsVisibleWithExpectedTitleAndControls()
        {
            using var app = VhcAppSession.Launch();
            var window = app.MainWindow;

            Assert.False(window.IsOffscreen, "main window is offscreen");
            Assert.True(Native.IsWindowVisible(window.Properties.NativeWindowHandle.Value), "main window is not a visible Win32 window");
            Assert.Equal(ControlType.Window, window.ControlType);

            // "Veeam Health Check" locally; "Veeam Health Check - Remote Mode" when only remote servers are configured.
            Assert.True(
                window.Title == BaseTitle || window.Title == BaseTitle + " - Remote Mode",
                "unexpected window title: " + Redact.Clean(window.Title));

            foreach (var id in new[] { "run", "termsCheckBox", "scrubBox", "pdfCheckBox", "htmlCheckBox", "explorerShowBox", "pathBox", "browseFolderBtn", "serverSelector", "productTypeSelector", "days7", "days30", "days90" })
            {
                Assert.NotNull(app.Find(id));
            }

            Assert.False(app.Button("run").IsEnabled, "Run must stay disabled until the terms are accepted");
        }

        [E2EFact]
        public void MainWindow_BeforeRun_ScreenshotIsCapturedAndNotBlank()
        {
            using var app = VhcAppSession.Launch();
            var dir = Path.Combine(VhcAppSession.OutputRoot(), "screenshots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "main-window-before-run.png");

            using var image = app.Screenshot(file);
            var bmp = image.Bitmap;

            Assert.True(File.Exists(file) && new FileInfo(file).Length > 0);
            Assert.True(bmp.Width >= 860 && bmp.Height >= 580, $"window capture too small: {bmp.Width}x{bmp.Height}");

            // A frame that is one flat colour is the failure mode of capturing a window that has not painted.
            var colours = new System.Collections.Generic.HashSet<int>();
            for (int y = 0; y < bmp.Height; y += 7)
            {
                for (int x = 0; x < bmp.Width; x += 7)
                {
                    colours.Add(bmp.GetPixel(x, y).ToArgb());
                }
            }

            Assert.True(colours.Count >= 8, "screenshot looks blank (" + colours.Count + " distinct colours sampled)");

            // The image shows the server name and output path of the lab host, so it is never uploaded; only this is logged.
            this.output.WriteLine($"screenshot: {Path.GetFileName(file)} {bmp.Width}x{bmp.Height}px, {new FileInfo(file).Length} bytes, {colours.Count} sampled colours (not uploaded: may show host identity)");
        }

        [E2EFact]
        public void BrowseButton_OpensNativeFolderPicker_AcceptsPathAndFillsPathBox()
        {
            var target = Path.Combine(VhcAppSession.OutputRoot(), "picked-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(target);
            try
            {
                using var app = VhcAppSession.Launch();
                var pathBox = app.TextBox("pathBox");
                var before = pathBox.Text;

                app.Button("browseFolderBtn").Invoke();

                var picker = app.WaitForOtherWindow(TimeSpan.FromSeconds(20));
                Assert.NotNull(picker);
                Assert.Equal("#32770", picker.Properties.ClassName.ValueOrDefault);
                this.output.WriteLine("picker title: " + Redact.Clean(picker.Title));

                // The Win32 common dialog's "Folder:" combo is control id 1148 and its OK button is id 1.
                var folderCombo = picker.FindFirstDescendant(cf => cf.ByAutomationId("1148"));
                var edit = folderCombo?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                    ?? picker.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                Assert.True(edit != null, "no edit field in the folder picker; descendants: " + string.Join(",", picker.FindAllDescendants().Select(d => d.Properties.AutomationId.ValueOrDefault + ":" + d.ControlType)));
                edit.AsTextBox().Text = target;

                var ok = picker.FindFirstDescendant(cf => cf.ByAutomationId("1"))?.AsButton();
                Assert.True(ok != null, "no OK button (id 1) in the folder picker");
                ok.Invoke();

                Assert.True(
                    VhcAppSession.PollTrue(() => SamePath(pathBox.Text, target), TimeSpan.FromSeconds(15)),
                    "pathBox did not take the picked folder (it still reads the original value: " + (pathBox.Text == before) + ")");
                Assert.True(VhcAppSession.PollTrue(() => app.OtherWindows().Count == 0, TimeSpan.FromSeconds(10)), "picker did not close");
            }
            finally
            {
                try { Directory.Delete(target, true); } catch { }
            }
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.TrimEndingDirectorySeparator(a ?? string.Empty), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
    }
}
