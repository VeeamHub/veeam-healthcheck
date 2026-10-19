using System;
using System.Security;
using System.Text;

namespace VeeamHealthCheck.Functions.Collection.Security
{
    public static class CredentialHelper
    {
        /// <summary>
        /// Base64-encodes a password ONLY to avoid quoting/escaping issues when
        /// passing it as a PowerShell argument. This is argument-safety encoding,
        /// NOT encryption — Base64 is trivially reversible and provides zero
        /// confidentiality. The script decodes it straight back to plaintext.
        /// </summary>
        public static string EncodePasswordToBase64(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(passwordBytes);
        }

        /// <summary>
        /// Escapes an arbitrary value (password, username, server/host, etc.) for
        /// embedding inside a PowerShell <b>single-quoted</b> string literal.
        /// Within a single-quoted string the only metacharacter is the single
        /// quote itself, which is escaped by doubling it.
        /// </summary>
        public static string EscapeForPowerShellSingleQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("'", "''");
        }

        /// <summary>
        /// Escapes an arbitrary value (password, username, server/host, etc.) for
        /// embedding inside a PowerShell <b>double-quoted</b> string literal that is
        /// passed via a process command line. Escapes the quote, backslash,
        /// subexpression (<c>$</c>) and backtick characters.
        /// </summary>
        public static string EscapeForPowerShellDoubleQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (char c in value)
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
        /// Escapes a password for use in PowerShell command line arguments
        /// (single-quoted context). Delegates to
        /// <see cref="EscapeForPowerShellSingleQuotes"/>.
        /// </summary>
        public static string EscapePasswordForPowerShell(string password)
        {
            return EscapeForPowerShellSingleQuotes(password);
        }

        /// <summary>
        /// Escapes a password for use in PowerShell scripts with double quotes.
        /// Delegates to <see cref="EscapeForPowerShellDoubleQuotes"/>.
        /// </summary>
        public static string EscapePasswordForDoubleQuotes(string password)
        {
            return EscapeForPowerShellDoubleQuotes(password);
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