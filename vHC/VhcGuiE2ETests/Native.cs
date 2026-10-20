using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace VhcGuiE2ETests
{
    internal static class Native
    {
        public const uint CREATE_NEW_CONSOLE = 0x00000010;
        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessW(string lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const uint PW_RENDERFULLCONTENT = 0x00000002;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>Renders a window's own content into a device context, even when other windows cover it.</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        /// <summary>Raises or lowers the window's topmost flag without needing foreground rights (a scheduled task has none).</summary>
        public static bool SetTopmost(IntPtr hwnd, bool topmost) =>
            SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        public const uint WM_SETTEXT = 0x000C;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

        /// <summary>Sets a Win32 edit control's text with WM_SETTEXT. UIA's ValuePattern.SetValue on the common file dialog's edit can hang for the full UIA timeout.</summary>
        public static void SetEditText(IntPtr hwnd, string text) => SendMessageW(hwnd, WM_SETTEXT, IntPtr.Zero, text);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        public static extern bool CloseDesktop(IntPtr hDesktop);

        private const uint DESKTOP_SWITCHDESKTOP = 0x0100;

        /// <summary>
        /// True when this process can reach the desktop that currently receives input. When the session is locked (or the RDP
        /// session is disconnected) the input desktop is the secure Winlogon one, OpenInputDesktop with switch access is denied,
        /// and UI Automation sees an empty tree.
        /// </summary>
        public static bool CanReachInputDesktop()
        {
            var desktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
            if (desktop == IntPtr.Zero)
            {
                return false;
            }

            CloseDesktop(desktop);
            return true;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        public static string WindowText(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            return GetWindowTextW(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        }

        public static string ClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            return GetClassNameW(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        }

        /// <summary>Visible top-level windows whose class is a console host (classic conhost or Windows Terminal).</summary>
        public static HashSet<IntPtr> VisibleConsoleHostWindows()
        {
            var found = new HashSet<IntPtr>();
            EnumWindows((h, _) =>
            {
                if (IsWindowVisible(h))
                {
                    var cls = ClassName(h);
                    if (cls == "ConsoleWindowClass" || cls == "CASCADIA_HOSTING_WINDOW_CLASS")
                    {
                        found.Add(h);
                    }
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
