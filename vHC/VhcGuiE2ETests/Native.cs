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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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
