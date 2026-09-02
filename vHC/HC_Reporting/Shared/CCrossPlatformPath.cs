using System.IO;
using System.Linq;

namespace VeeamHealthCheck.Shared
{
    internal static class CCrossPlatformPath
    {
        /// <summary>
        /// Path.Combine, but strips a leading '\' or '/' from each segment first, since
        /// much of this codebase's path fragments are @"\..." literals written assuming
        /// Windows is always the separator (Path.Combine treats a leading separator as an
        /// absolute-path anchor and silently discards everything before it).
        /// Callers must pass trusted, relative literal segments — this does not sanitize
        /// untrusted/user-controlled input (e.g. job names; see
        /// IndividualJobSessionsHelper.SanitizeFileName for that).
        /// </summary>
        internal static string Combine(string basePath, params string[] segments)
        {
            var trimmed = segments.Select(s => s.TrimStart('\\', '/'));
            return Path.Combine(new[] { basePath }.Concat(trimmed).ToArray());
        }
    }
}
