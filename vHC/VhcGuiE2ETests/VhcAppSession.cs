using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;

namespace VhcGuiE2ETests
{
    /// <summary>Redacts this machine's identity from any text that may reach a public CI log.</summary>
    internal static class Redact
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            foreach (var secret in new[] { Environment.MachineName, Environment.UserName, Environment.UserDomainName }.Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                text = text.Replace(secret, "<redacted>", StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }
    }

    /// <summary>
    /// One launched VeeamHealthCheck.exe plus the UIA handle onto it. Disposing kills exactly the PID this class
    /// started (and the processes it spawned), never anything found by name: other VHC runs and VBR share the box.
    /// </summary>
    internal sealed class VhcAppSession : IDisposable
    {
        public static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(90);

        private readonly UIA3Automation automation = new();
        private readonly IntPtr processHandle;
        private readonly Process process;
        private readonly List<string> cleanupDirs = new();

        private VhcAppSession(Native.PROCESS_INFORMATION pi, HashSet<IntPtr> consoleBaseline)
        {
            this.processHandle = pi.hProcess;
            this.Pid = pi.dwProcessId;
            this.ConsoleBaseline = consoleBaseline;
            this.process = Process.GetProcessById(this.Pid);
            if (pi.hThread != IntPtr.Zero)
            {
                Native.CloseHandle(pi.hThread);
            }
        }

        public int Pid { get; }

        public HashSet<IntPtr> ConsoleBaseline { get; }

        public bool HasExited => this.process.HasExited;

        public int ExitCode => this.process.ExitCode;

        public Window MainWindow { get; private set; }

        public static string ExePath()
        {
            var exe = Environment.GetEnvironmentVariable(E2EFactAttribute.ExeVariable);
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                throw new FileNotFoundException($"{E2EFactAttribute.ExeVariable} is set but does not point to an existing file.");
            }

            return Path.GetFullPath(exe);
        }

        public static string OutputRoot()
        {
            var root = Environment.GetEnvironmentVariable("VHC_E2E_OUT");
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Path.GetTempPath(), "vhc-e2e");
            }

            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>
        /// Starts the exe on its own new console, exactly as a double-click does (Explorer gives a GUI-launched
        /// console app a fresh console with only that app on it). That is the case the app's hide logic targets.
        /// </summary>
        public static VhcAppSession Launch()
        {
            using (var self = Process.GetCurrentProcess())
            {
                if (self.SessionId == 0 || !Environment.UserInteractive)
                {
                    throw new InvalidOperationException(
                        "The test process is in session 0 / non-interactive; GUI tests need an interactive desktop session. " +
                        "Start the runner from an interactive logon (run.cmd at logon, auto-logon, no screen lock).");
                }
            }

            string exe = ExePath();
            var baseline = Native.VisibleConsoleHostWindows();
            var si = new Native.STARTUPINFO { cb = Marshal.SizeOf<Native.STARTUPINFO>() };
            var cmd = new StringBuilder("\"" + exe + "\"");
            if (!Native.CreateProcessW(exe, cmd, IntPtr.Zero, IntPtr.Zero, false, Native.CREATE_NEW_CONSOLE, IntPtr.Zero, Path.GetDirectoryName(exe), ref si, out var pi))
            {
                throw new InvalidOperationException("CreateProcess failed, Win32 error " + Marshal.GetLastWin32Error());
            }

            var session = new VhcAppSession(pi, baseline);
            try
            {
                session.MainWindow = session.WaitForMainWindow();
            }
            catch
            {
                session.Dispose();
                throw;
            }

            return session;
        }

        /// <summary>Directories to delete on dispose (the output folder of a run).</summary>
        public void DeleteOnDispose(string dir) => this.cleanupDirs.Add(dir);

        public static T Poll<T>(Func<T> probe, TimeSpan timeout, Func<bool> abort = null) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                T value = null;
                try
                {
                    value = probe();
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is FlaUI.Core.Exceptions.ElementNotAvailableException || ex is InvalidOperationException)
                {
                    // The tree is mid-update; try again.
                }

                if (value != null)
                {
                    return value;
                }

                if (abort != null && abort())
                {
                    return null;
                }

                Thread.Sleep(250);
            }

            return null;
        }

        public static bool PollTrue(Func<bool> probe, TimeSpan timeout) =>
            Poll(() => { try { return probe() ? new object() : null; } catch { return null; } }, timeout) != null;

        private Window WaitForMainWindow()
        {
            var cf = this.automation.ConditionFactory;
            var window = Poll(
                () => this.automation.GetDesktop().FindAllChildren(cf.ByProcessId(this.Pid))
                    .Where(e => e.Properties.ClassName.ValueOrDefault != "ConsoleWindowClass")
                    .FirstOrDefault(e => e.FindFirstDescendant(cf.ByAutomationId("run")) != null),
                WindowTimeout,
                () => this.process.HasExited);
            if (window == null)
            {
                throw new TimeoutException(this.process.HasExited
                    ? "VeeamHealthCheck.exe exited before its main window appeared (exit code " + this.process.ExitCode + ")."
                    : "The main window (containing control 'run') did not appear within " + WindowTimeout.TotalSeconds + "s.");
            }

            return window.AsWindow();
        }

        public AutomationElement Find(string automationId, AutomationElement scope = null)
        {
            var root = scope ?? this.MainWindow;
            var el = Poll(() => root.FindFirstDescendant(this.automation.ConditionFactory.ByAutomationId(automationId)), TimeSpan.FromSeconds(15));
            return el ?? throw new InvalidOperationException("Control with AutomationId '" + automationId + "' was not found.");
        }

        public CheckBox CheckBox(string id) => this.Find(id).AsCheckBox();

        public Button Button(string id, AutomationElement scope = null) => this.Find(id, scope).AsButton();

        public TextBox TextBox(string id) => this.Find(id).AsTextBox();

        /// <summary>Top-level windows of the app other than the main window (dialogs, pickers).</summary>
        public IReadOnlyList<Window> OtherWindows()
        {
            var cf = this.automation.ConditionFactory;
            var main = this.MainWindow.Properties.NativeWindowHandle.ValueOrDefault;
            return this.automation.GetDesktop().FindAllChildren(cf.ByProcessId(this.Pid))
                .Where(e => e.Properties.ClassName.ValueOrDefault != "ConsoleWindowClass")
                .Where(e => e.Properties.NativeWindowHandle.ValueOrDefault != main)
                .Select(e => e.AsWindow())
                .ToList();
        }

        public Window WaitForOtherWindow(TimeSpan timeout) =>
            Poll(() => this.OtherWindows().FirstOrDefault(), timeout, () => this.process.HasExited);

        /// <summary>Message of an in-app dialog (NotifierDialog), already redacted for logs.</summary>
        public static string DialogText(Window dialog)
        {
            var msg = dialog.FindFirstDescendant(cf => cf.ByAutomationId("MessageText"));
            return Redact.Clean(msg?.Name ?? msg?.AsLabel()?.Text ?? string.Empty);
        }

        public static void ClickDialogButton(Window dialog, string automationId)
        {
            var button = dialog.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsButton()
                ?? throw new InvalidOperationException("Dialog has no button '" + automationId + "'.");
            button.Invoke();
        }

        public CaptureImage Screenshot(string path)
        {
            this.MainWindow.SetForeground();
            Thread.Sleep(500);
            var image = Capture.Element(this.MainWindow);
            image.ToFile(path);
            return image;
        }

        public ConsoleState Console() => ConsoleProbe.Inspect(this.Pid, this.ConsoleBaseline);

        public void Dispose()
        {
            try
            {
                if (!this.process.HasExited)
                {
                    this.process.Kill(entireProcessTree: true);
                    this.process.WaitForExit(15000);
                }
            }
            catch
            {
                Native.TerminateProcess(this.processHandle, 1);
            }
            finally
            {
                Native.CloseHandle(this.processHandle);
                this.process.Dispose();
                this.automation.Dispose();
                foreach (var dir in this.cleanupDirs)
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
        }
    }
}
