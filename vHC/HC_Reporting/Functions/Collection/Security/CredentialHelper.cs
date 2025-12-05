using System;
using System.Security;
using System.Text;

namespace VeeamHealthCheck.Functions.Collection.Security
{
    public static class CredentialHelper
    {
        /// <summary>
        /// Escapes a password for use in PowerShell command line arguments
        /// </summary>
        public static string EscapePasswordForPowerShell(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            // For PowerShell, we need to escape special characters
            // The safest approach is to use single quotes and escape any single quotes in the password
            return password.Replace("'", "''");
        }

        /// <summary>
        /// Escapes a password for use in PowerShell scripts with double quotes
        /// </summary>
        public static string EscapePasswordForDoubleQuotes(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in password)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '$':
                        sb.Append("`$");
                        break;
                    case '`':
                        sb.Append("``");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Creates a SecureString from a plain text password
        /// </summary>
        public static SecureString ConvertToSecureString(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            var secureString = new SecureString();
            foreach (char c in password)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        /// <summary>
        /// Validates if a password contains problematic characters
        /// </summary>
        public static bool ContainsProblematicCharacters(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            // Characters that commonly cause issues in command line contexts
            char[] problematicChars = { '"', '\'', '`', '$', '\\', ')', '(', '%', '!', '@', '#', '&', '*', '|', '<', '>', ';' };
            return password.IndexOfAny(problematicChars) >= 0;
        }
    }
}