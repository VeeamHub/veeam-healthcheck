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

            using var bmp = app.Screenshot(file);

            Assert.True(File.Exists(file) && new FileInfo(file).Length > 0);
            Assert.True(bmp.Width >= 860 && bmp.Height >= 580, $"window capture too small: {bmp.Width}x{bmp.Height}");

            // A frame that is one flat colour is the failure mode of capturing a window that has not painted.
            var colours = VhcAppSession.DistinctColours(bmp);
            Assert.True(colours >= 8, "screenshot looks blank (" + colours + " distinct colours sampled)");

            // The image shows the server name and output path of the lab host, so it is never uploaded; only this is logged.
            this.output.WriteLine($"screenshot: {Path.GetFileName(file)} {bmp.Width}x{bmp.Height}px, {new FileInfo(file).Length} bytes, {colours} sampled colours (not uploaded: may show host identity)");
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

                // Inspected on Windows 11 (the common item dialog Avalonia opens for a folder): the "Folder:" field is the Edit with
                // AutomationId 1152 and "Select Folder" is the Button with AutomationId 1. The path goes in as WM_SETTEXT on that
                // edit, because UIA's ValuePattern.SetValue on it hangs for the whole UIA timeout.
                var edit = picker.FindFirstDescendant(cf => cf.ByAutomationId("1152").And(cf.ByClassName("Edit")));
                Assert.True(edit != null, "no Folder edit (AutomationId 1152) in the picker; Win32 controls: " + string.Join(", ", picker.FindAllDescendants()
                    .Where(d => d.Properties.ClassName.ValueOrDefault is "Button" or "Edit" or "ComboBox" or "ComboBoxEx32")
                    .Select(d => d.Properties.ClassName.ValueOrDefault + "#" + d.Properties.AutomationId.ValueOrDefault)));
                var editHandle = edit.Properties.NativeWindowHandle.ValueOrDefault;
                Assert.True(editHandle != IntPtr.Zero, "the picker's Folder edit has no window handle");
                Native.SetEditText(editHandle, target);

                var ok = picker.FindFirstDescendant(cf => cf.ByAutomationId("1").And(cf.ByClassName("Button")))?.AsButton();
                Assert.True(ok != null, "no Select Folder button (AutomationId 1) in the picker");
                ok.Invoke();

                Assert.True(
                    VhcAppSession.PollTrue(() => SamePath(pathBox.Text, target), TimeSpan.FromSeconds(15)),
                    "pathBox did not take the picked folder (it still reads the original value: " + (pathBox.Text == before) + ")");
                Assert.True(
                    VhcAppSession.PollTrue(() => app.OtherWindows().Count == 0, TimeSpan.FromSeconds(10)),
                    "picker did not close; windows still open: " + string.Join(", ", app.OtherWindows().Select(w => w.Properties.ClassName.ValueOrDefault + " '" + Redact.Clean(w.Title) + "'")));
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
