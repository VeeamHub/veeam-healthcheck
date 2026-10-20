using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using Xunit;
using Xunit.Abstractions;

namespace VhcGuiE2ETests
{
    /// <summary>
    /// Terms, scrub, Run against the local VBR server through the real window, then the report on disk.
    /// The console hide is NOT done by the Run click: CArgsParser.HideConsoleIfOwned hides it at startup, before the
    /// window exists. So the test asserts it hidden once the window is up AND again after Run is clicked, which is
    /// what a user sees (no black console behind the GUI while a run is in progress).
    /// </summary>
    public class VhcRunFlowTests
    {
        private readonly ITestOutputHelper output;

        public VhcRunFlowTests(ITestOutputHelper output) => this.output = output;

        [E2EFact(requiresLiveVbr: true)]
        public void Run_ScrubbedOnLocalVbr_HidesConsoleAndWritesScrubbedReport()
        {
            var timeout = TimeSpan.FromMinutes(double.TryParse(Environment.GetEnvironmentVariable("VHC_E2E_RUN_TIMEOUT_MIN"), out var m) ? m : 30);
            var outDir = Path.Combine(VhcAppSession.OutputRoot(), "run-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(outDir);
            var started = DateTime.Now.AddSeconds(-2);

            using var app = VhcAppSession.Launch();
            app.DeleteOnDispose(outDir);

            // The window is up, so the startup hide has already happened.
            AssertConsoleHidden(app, "after launch");

            // Scrub is switched on explicitly rather than assumed (the default follows the user's saved settings), and the
            // Explorer and browser pop-ups are switched off: nothing here should open windows on a shared desktop.
            Assert.True(VhcAppSession.PollTrue(() => app.CheckBox("scrubBox").IsEnabled, TimeSpan.FromSeconds(60)), "scrub box never became available");
            SetChecked(app.CheckBox("scrubBox"), true);
            SetChecked(app.CheckBox("explorerShowBox"), false);
            SetChecked(app.CheckBox("htmlCheckBox"), false);

            app.TextBox("pathBox").Text = outDir;
            Assert.True(VhcAppSession.PollTrue(() => SamePath(app.TextBox("pathBox").Text, outDir), TimeSpan.FromSeconds(5)), "path box did not take the output folder");

            // Accepting the terms raises a confirm dialog; a non-elevated session also gets the "Administrator Privileges Recommended"
            // confirm. Both are answered Yes (accept, continue) until Run enables.
            app.CheckBox("termsCheckBox").Toggle();
            Assert.True(
                VhcAppSession.PollTrue(
                    () =>
                    {
                        foreach (var dialog in app.OtherWindows())
                        {
                            this.output.WriteLine($"dialog before run: [{Redact.Clean(dialog.Title)}] {VhcAppSession.DialogText(dialog)}");
                            VhcAppSession.ClickDialogButton(dialog, "YesOkButton");
                        }

                        return app.Button("run").IsEnabled;
                    },
                    TimeSpan.FromSeconds(40)),
                "Run did not enable after accepting the terms");

            app.Button("run").Invoke();

            AssertConsoleHidden(app, "right after clicking Run");
            Assert.True(VhcAppSession.PollTrue(() => app.HasExited || !app.Button("run").IsEnabled, TimeSpan.FromSeconds(15)), "Run did not lock the GUI");
            System.Threading.Thread.Sleep(3000);
            if (!app.HasExited)
            {
                AssertConsoleHidden(app, "3s into the run");
            }

            WaitForRunToFinish(app, timeout);

            var anonDir = Path.Combine(outDir, "vHC-AnonymousReport");
            var report = Directory.Exists(anonDir)
                ? new DirectoryInfo(anonDir).GetFiles("*.html").Where(f => f.LastWriteTime >= started).OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
                : null;
            Assert.True(report != null, "no scrubbed HTML report under <out>/vHC-AnonymousReport (folders present: " + string.Join(",", Directory.GetDirectories(outDir).Select(Path.GetFileName)) + ")");
            AssertScrubbedReport(report);
            this.output.WriteLine($"report: {report.Name}, {report.Length} bytes");
        }

        private static void SetChecked(CheckBox box, bool wanted)
        {
            if ((box.IsChecked == true) != wanted)
            {
                box.Toggle();
            }

            Assert.True(VhcAppSession.PollTrue(() => (box.IsChecked == true) == wanted, TimeSpan.FromSeconds(5)), box.AutomationId + " did not become " + wanted);
        }

        private void AssertConsoleHidden(VhcAppSession app, string when)
        {
            // Windows Terminal (the default terminal on current Windows 11) opens its window asynchronously and hides it on the
            // app's ShowWindow(SW_HIDE) a moment later, so give it a few seconds before calling the console visible.
            VhcAppSession.PollTrue(() => app.Console().Hidden, TimeSpan.FromSeconds(10));
            var state = app.Console();
            this.output.WriteLine($"console {when}: {Redact.Clean(state.Describe())}");
            Assert.NotEqual(ConsoleKind.Unobservable, state.Kind);
            Assert.True(state.Hidden, $"the console is visible {when} ({Redact.Clean(state.Describe())})");
            if (state.Kind == ConsoleKind.PseudoConsole)
            {
                this.output.WriteLine("NOTE: default terminal delegation (ConPTY) is active; the pseudo window is always invisible, so this check proves only that no conhost/Windows Terminal window appeared.");
            }
        }

        /// <summary>Waits for the process to exit by itself (Environment.Exit(0) on success), answering the benign dialogs on the way.</summary>
        private void WaitForRunToFinish(VhcAppSession app, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (!app.HasExited)
            {
                if (sw.Elapsed > timeout)
                {
                    Assert.Fail("the run did not finish within " + timeout.TotalMinutes + " minutes");
                }

                Window dialog = null;
                try
                {
                    dialog = app.OtherWindows().FirstOrDefault();
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is FlaUI.Core.Exceptions.ElementNotAvailableException)
                {
                }

                if (dialog != null)
                {
                    var title = Redact.Clean(dialog.Title);
                    var text = VhcAppSession.DialogText(dialog);
                    this.output.WriteLine($"dialog during run: [{title}] {text}");
                    if (Regex.IsMatch(title + " " + text, "fail|error|exception", RegexOptions.IgnoreCase))
                    {
                        Assert.Fail($"the run raised an error dialog: [{title}] {text}");
                    }

                    VhcAppSession.ClickDialogButton(dialog, "YesOkButton");
                }

                System.Threading.Thread.Sleep(2000);
            }

            Assert.Equal(0, app.ExitCode);
            this.output.WriteLine("run finished in " + sw.Elapsed.TotalMinutes.ToString("0.0") + " min, exit code 0");
        }

        private void AssertScrubbedReport(FileInfo report)
        {
            var html = File.ReadAllText(report.FullName);
            Assert.True(report.Length > 20_000, "report is suspiciously small: " + report.Length + " bytes");
            Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);

            // The same core section ids the live jobs assert in .github/scripts/Test-VhcLiveRun.ps1 (CoreSections).
            foreach (var id in new[] { "license", "secsummary", "vbrserver", "proxies", "repos", "jobsummary" })
            {
                Assert.Matches("id=[\"']" + id + "[\"']", html);
            }

            Assert.Matches("id=[\"'](jobs|jobTable[\\w-]*)[\"']", html);

            // Scrub means this machine's name is absent from the HTML and the JSON export (as -Check Scrub does in the live jobs).
            var machine = Environment.MachineName;
            Assert.True(machine.Length >= 3, "machine name too short for a meaningful leak check");
            foreach (var file in new[] { report.FullName, Path.ChangeExtension(report.FullName, ".json") }.Where(File.Exists))
            {
                Assert.False(File.ReadAllText(file).Contains(machine, StringComparison.OrdinalIgnoreCase), Path.GetExtension(file) + " leaks the machine name");
            }
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.TrimEndingDirectorySeparator(a ?? string.Empty), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
    }
}
