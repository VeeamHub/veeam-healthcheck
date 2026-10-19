using System;
using System.Linq;

namespace VhcGuiE2ETests
{
    internal enum ConsoleKind
    {
        /// <summary>The process has no console we could attach to (it exited, or has none).</summary>
        Unobservable,

        /// <summary>A console exists but has no window handle at all.</summary>
        WindowLess,

        /// <summary>Classic conhost: the console window is a real top-level window we can test for visibility.</summary>
        Classic,

        /// <summary>ConPTY / default terminal (Windows Terminal): GetConsoleWindow is a hidden pseudo window, so its own visibility proves nothing.</summary>
        PseudoConsole,
    }

    internal sealed record ConsoleState(ConsoleKind Kind, string ClassName, bool WindowVisible, int NewVisibleConsoleHostWindows)
    {
        /// <summary>
        /// Deterministic for Classic (the window is hidden) and for WindowLess. For PseudoConsole it can only say
        /// that no new console-host window (conhost or Windows Terminal) became visible since launch.
        /// </summary>
        public bool Hidden => this.Kind switch
        {
            ConsoleKind.Classic => !this.WindowVisible && this.NewVisibleConsoleHostWindows == 0,
            ConsoleKind.WindowLess => this.NewVisibleConsoleHostWindows == 0,
            ConsoleKind.PseudoConsole => this.NewVisibleConsoleHostWindows == 0,
            _ => false,
        };

        public string Describe() =>
            $"kind={this.Kind}, class={this.ClassName}, windowVisible={this.WindowVisible}, newVisibleConsoleHosts={this.NewVisibleConsoleHostWindows}";
    }

    /// <summary>
    /// Finds the console window of a process we started from outside it. The app hides its console with
    /// ShowWindow(GetConsoleWindow(), SW_HIDE), so the probe asks the same question: attach to the target's
    /// console, read GetConsoleWindow, and test IsWindowVisible. That is the only reliable way to map a PID to its
    /// console window: EnumWindows + GetWindowThreadProcessId reports the conhost process, not the app.
    /// </summary>
    internal static class ConsoleProbe
    {
        private static readonly object Gate = new();

        public static ConsoleState Inspect(int pid, System.Collections.Generic.HashSet<IntPtr> baseline)
        {
            IntPtr hwnd;
            lock (Gate)
            {
                // A process has one console at a time. Drop the test host's (if any), borrow the app's, drop it again.
                Native.FreeConsole();
                try
                {
                    if (!Native.AttachConsole((uint)pid))
                    {
                        return new ConsoleState(ConsoleKind.Unobservable, string.Empty, false, 0);
                    }

                    hwnd = Native.GetConsoleWindow();
                }
                finally
                {
                    Native.FreeConsole();
                }
            }

            int fresh = Native.VisibleConsoleHostWindows().Count(h => !baseline.Contains(h));
            if (hwnd == IntPtr.Zero)
            {
                return new ConsoleState(ConsoleKind.WindowLess, string.Empty, false, fresh);
            }

            string cls = Native.ClassName(hwnd);
            bool visible = Native.IsWindowVisible(hwnd);
            var kind = cls == "ConsoleWindowClass" ? ConsoleKind.Classic : ConsoleKind.PseudoConsole;
            return new ConsoleState(kind, cls, visible, fresh);
        }
    }
}
