// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
namespace VeeamHealthCheck.Functions.Collection.DB
{
    /// <summary>
    /// Derives the Console\ directory from a registry-sourced VBR install path (CorePath's Backup\
    /// folder, or the Mount Service's own install folder) that points at a sibling component
    /// directory rather than Console\ itself. Uses manual backslash parsing rather than
    /// System.IO.Path, since these are always Windows-style paths even when this logic runs on the
    /// Linux/macOS cross-platform test runners (mirrors TestMfa.ps1's Resolve-VeeamConsolePath).
    /// </summary>
    internal static class CVbrConsolePathResolver
    {
        public static string? SiblingConsoleDir(string? installPath)
        {
            if (string.IsNullOrEmpty(installPath))
            {
                return null;
            }

            string trimmed = installPath.TrimEnd('\\', '/');
            int lastSeparator = trimmed.LastIndexOf('\\');
            if (lastSeparator <= 0)
            {
                return null;
            }

            string parent = trimmed.Substring(0, lastSeparator);
            return parent + "\\Console";
        }
    }
}
