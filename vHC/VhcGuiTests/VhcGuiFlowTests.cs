using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.ManageServers;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;
using static VhcGuiTests.UiDriver;

namespace VhcGuiTests
{
    /// <summary>
    /// Main-window flows driven through real mouse and keyboard input. Collection never starts:
    /// tests that click Run replace the RunAction seam first.
    /// </summary>
    public class VhcGuiFlowTests : GuiTestBase
    {
        private async Task<VhcGui> ShowReadyWindow()
        {
            SeedServers("vbr01");
            var w = ShowMainWindow();
            await WaitForStartup(w);
            return w;
        }

        private static async Task AcceptTerms(VhcGui w)
        {
            var run = FindRequired<Button>(w, "run");
            Click(w, FindRequired<CheckBox>(w, "termsCheckBox"));
            await Until(() => run.IsEnabled);
        }

        private static IEnumerable<string> ServersInSettings() =>
            CAppSettings.Get().Servers ?? Enumerable.Empty<string>();

        [AvaloniaFact]
        public async Task TermsCheckBox_ClickAndConfirm_EnablesRun()
        {
            var w = await ShowReadyWindow();
            Stub.ConfirmResult = true;
            var run = FindRequired<Button>(w, "run");
            Assert.False(run.IsEnabled);

            Click(w, FindRequired<CheckBox>(w, "termsCheckBox"));

            await Until(() => run.IsEnabled);
            Assert.Equal(1, Stub.Confirms);
        }

        [AvaloniaFact]
        public async Task RunButton_ClickAfterAccept_LocksGuiAndInvokesRun()
        {
            var w = await ShowReadyWindow();
            var calls = new List<bool>();
            w.RunAction = b => calls.Add(b);
            Type(w, FindRequired<TextBox>(w, "pathBox"), Path.Combine(TempDir, "out"));
            await AcceptTerms(w);

            Click(w, FindRequired<Button>(w, "run"));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { false }, calls);
            foreach (var name in new[] { "explorerShowBox", "htmlCheckBox", "pdfCheckBox", "scrubBox", "termsCheckBox", "pathBox", "browseFolderBtn", "clearCredsCheckBox", "serverSelector", "manageServersBtn", "productTypeSelector", "RescanBox" })
            {
                Assert.False(FindRequired<Control>(w, name).IsEnabled, name + " should be disabled while running");
            }

            Assert.Equal(1, FindRequired<ProgressBar>(w, "pBar").Opacity);
            Assert.False(FindRequired<Button>(w, "run").IsEnabled);
            Assert.Equal("vbr01", CGlobals.REMOTEHOST);
        }

        [AvaloniaFact]
        public async Task SetUiAsync_NoVeeamNoServersUserAddsServer_EntersRemoteMode()
        {
            Stub.ConfirmResult = true;
            var w = ShowMainWindow();

            var dlg = await OwnedDialogAppears<ManageServersDialog>(w);
            Type(dlg, FindRequired<TextBox>(dlg, "newServerBox"), "vbr01");
            Click(dlg, FindRequired<Button>(dlg, "addBtn"));
            Click(dlg, FindRequired<Button>(dlg, "doneBtn"));

            await Until(() => w.Title == "Veeam Health Check - Remote Mode");
            Assert.Equal("vbr01", FindRequired<ComboBox>(w, "serverSelector").SelectedItem);
            Assert.Contains("vbr01", ServersInSettings());
        }

        [AvaloniaFact]
        public async Task ManageServersButton_AddServerAndDone_AddsServerKeepingSelection()
        {
            var w = await ShowReadyWindow();

            Click(w, FindRequired<Button>(w, "manageServersBtn"));
            var dlg = Owned<ManageServersDialog>(w);
            Type(dlg, FindRequired<TextBox>(dlg, "newServerBox"), "vbr02");
            Click(dlg, FindRequired<Button>(dlg, "addBtn"));
            Click(dlg, FindRequired<Button>(dlg, "doneBtn"));

            var selector = FindRequired<ComboBox>(w, "serverSelector");
            await Until(() => selector.ItemCount == 2);
            Assert.Equal("vbr01", selector.SelectedItem);
            Assert.Equal(new[] { "vbr01", "vbr02" }, ServersInSettings().OrderBy(s => s).ToArray());
        }

        [AvaloniaFact]
        public async Task RunButton_ClickWithUnwritablePath_ShowsErrorAndDoesNotRun()
        {
            var w = await ShowReadyWindow();
            var calls = new List<bool>();
            w.RunAction = b => calls.Add(b);
            await AcceptTerms(w);

            // A directory cannot be created underneath a regular file.
            var file = Path.Combine(TempDir, "not-a-dir");
            File.WriteAllText(file, "x");
            Type(w, FindRequired<TextBox>(w, "pathBox"), Path.Combine(file, "sub"));

            Click(w, FindRequired<Button>(w, "run"));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(Stub.Errors);
            Assert.Contains("Failed to validate desired output path", Stub.Errors[0]);
            Assert.Empty(calls);
            Assert.True(FindRequired<ComboBox>(w, "serverSelector").IsEnabled);
        }

        [AvaloniaFact]
        public async Task TermsCheckBox_ClickAndDecline_RevertsAndKeepsRunDisabled()
        {
            var w = await ShowReadyWindow();
            Stub.ConfirmResult = false;
            var terms = FindRequired<CheckBox>(w, "termsCheckBox");

            Click(w, terms);

            await Until(() => terms.IsEnabled);
            Assert.Equal(1, Stub.Confirms);
            Assert.False(terms.IsChecked);
            Assert.False(FindRequired<Button>(w, "run").IsEnabled);
        }

        [AvaloniaTheory]
        [InlineData("days30", 30)]
        [InlineData("days90", 90)]
        [InlineData("days7", 7)]
        public async Task PeriodRadio_Click_SetsReportDays(string pill, int expected)
        {
            var w = await ShowReadyWindow();
            if (expected == 7)
            {
                // days7 is already checked at startup; move off it so the click is a real change.
                Click(w, FindRequired<RadioButton>(w, "days30"));
                Assert.Equal(30, CGlobals.ReportDays);
            }

            Click(w, FindRequired<RadioButton>(w, pill));

            Assert.Equal(expected, CGlobals.ReportDays);
            Assert.True(FindRequired<RadioButton>(w, pill).IsChecked);
        }

        [AvaloniaFact]
        public async Task VhcGui_ConstructedWithReportDays30_KeepsDays30()
        {
            SeedServers("vbr01");
            CGlobals.ReportDays = 30;

            var w = ShowMainWindow();
            await WaitForStartup(w);

            Assert.True(FindRequired<RadioButton>(w, "days30").IsChecked);
            Assert.Equal(30, CGlobals.ReportDays);
        }

        [AvaloniaFact]
        public async Task ScrubBox_UncheckThenRecheck_TogglesPdfAvailability()
        {
            var w = await ShowReadyWindow();
            var scrub = FindRequired<CheckBox>(w, "scrubBox");
            var pdf = FindRequired<CheckBox>(w, "pdfCheckBox");
            Assert.True(CGlobals.Scrub);
            Assert.False(pdf.IsEnabled);

            Click(w, scrub);
            Assert.False(CGlobals.Scrub);
            Assert.True(pdf.IsEnabled);

            Click(w, pdf);
            Assert.True(CGlobals.EXPORTPDF);

            Click(w, scrub);
            Assert.True(CGlobals.Scrub);
            Assert.False(pdf.IsChecked);
            Assert.False(pdf.IsEnabled);
            Assert.False(CGlobals.EXPORTPDF);
        }

        [AvaloniaFact]
        public async Task PathBox_TypePath_UpdatesDesiredPath()
        {
            var w = await ShowReadyWindow();
            var target = Path.Combine(TempDir, "reports");

            Type(w, FindRequired<TextBox>(w, "pathBox"), target);

            Assert.Equal(target, CGlobals.desiredPath);
        }

        [AvaloniaFact]
        public async Task ManageServersButton_Cancel_LeavesServerListUnchanged()
        {
            var w = await ShowReadyWindow();
            var selector = FindRequired<ComboBox>(w, "serverSelector");

            Click(w, FindRequired<Button>(w, "manageServersBtn"));
            var dlg = Owned<ManageServersDialog>(w);
            Type(dlg, FindRequired<TextBox>(dlg, "newServerBox"), "vbr09");
            Click(dlg, FindRequired<Button>(dlg, "addBtn"));
            Click(dlg, FindRequired<Button>(dlg, "cancelBtn"));

            await Until(() => !w.OwnedWindows.Any());
            Assert.Equal(1, selector.ItemCount);
            Assert.Equal("vbr01", selector.SelectedItem);
            Assert.Equal(new[] { "vbr01" }, ServersInSettings().ToArray());
        }

        [AvaloniaFact]
        public async Task ProductTypeSelector_ArrowDown_SetsTargetProductType()
        {
            var w = await ShowReadyWindow();
            var selector = FindRequired<ComboBox>(w, "productTypeSelector");
            Assert.Equal(TargetProduct.Auto, CGlobals.TargetProductType);

            selector.Focus();
            w.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, selector.SelectedIndex);
            Assert.Equal(TargetProduct.Vbr, CGlobals.TargetProductType);
        }

        [AvaloniaFact]
        public async Task ThemeToggleButton_Click_CyclesThemeAndPersists()
        {
            var w = await ShowReadyWindow();
            var button = FindRequired<Button>(w, "ThemeToggleButton");
            var start = Application.Current.RequestedThemeVariant;
            var startLabel = button.Content;
            var expected = start == ThemeVariant.Dark ? ThemeVariant.Light
                : start == ThemeVariant.Light ? ThemeVariant.Default
                : ThemeVariant.Dark;

            Click(w, button);

            Assert.Equal(expected, Application.Current.RequestedThemeVariant);
            Assert.Equal(CThemePreference.FromVariant(expected), CAppSettings.Get().ThemePreference);
            Assert.NotEqual(startLabel, button.Content);
        }

        [AvaloniaFact]
        public async Task SetUiAsync_NoVeeamNoServersUserDeclines_NeverRunsPreRunCheck()
        {
            Stub.ConfirmResult = false;
            var w = ShowMainWindow();

            await Until(() => Stub.Confirms == 1);
            for (int i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.NotEqual(true, FindRequired<CheckBox>(w, "scrubBox").IsChecked);
            Assert.NotEqual("Veeam Health Check - Remote Mode", w.Title);
            Assert.False(FindRequired<Button>(w, "run").IsEnabled);
            Assert.Empty(w.OwnedWindows);
        }

        private static async Task<T> OwnedDialogAppears<T>(Window owner) where T : Window
        {
            await Until(() => owner.OwnedWindows.OfType<T>().Any());
            return Owned<T>(owner);
        }
    }
}
