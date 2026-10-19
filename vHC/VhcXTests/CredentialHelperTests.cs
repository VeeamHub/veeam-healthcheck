using Xunit;
using VeeamHealthCheck.Functions.Collection.Security;

namespace VeeamHealthCheck.Tests
{
    public class CredentialHelperTests
    {
        [Theory]
        [InlineData("Simple123", "Simple123")]
        [InlineData("Pass'word", "Pass''word")]
        [InlineData("It's'a'test'", "It''s''a''test''")]
        [InlineData("NoSpecialChars", "NoSpecialChars")]
        public void EscapePasswordForPowerShell_ShouldEscapeSingleQuotes(string input, string expected)
        {
            var result = CredentialHelper.EscapePasswordForPowerShell(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Pass$word", "Pass`$word")]
        [InlineData("Test\"Quote", "Test\\\"Quote")]
        [InlineData("Back\\slash", "Back\\\\slash")]
        [InlineData("Backtick`test", "Backtick``test")]
        public void EscapePasswordForDoubleQuotes_ShouldEscapeSpecialChars(string input, string expected)
        {
            var result = CredentialHelper.EscapePasswordForDoubleQuotes(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EscapePasswordForPowerShell_ShouldHandleEmptyOrNull(string? input)
        {
            var result = CredentialHelper.EscapePasswordForPowerShell(input);
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        // Test passwords similar to the one that caused issues
        [InlineData("Test@123)45%End")]
        [InlineData("P@ss!word#2024")]
        [InlineData("Complex$Pass&Word*123")]
        [InlineData("Special(Char)Test%100")]
        [InlineData("Quote'Test\"With$Vars")]
        public void ComplexPasswords_ShouldBeProperlyEscaped(string password)
        {
            var escaped = CredentialHelper.EscapePasswordForPowerShell(password);
            
            // The escaped password should not break when used in a command
            Assert.NotNull(escaped);
            
            // Check for problematic characters
            bool hasProblematic = CredentialHelper.ContainsProblematicCharacters(password);
            Assert.True(hasProblematic);
        }

        [Fact]
        public void ContainsProblematicCharacters_ShouldDetectSpecialChars()
        {
            Assert.True(CredentialHelper.ContainsProblematicCharacters("Pass@word"));
            Assert.True(CredentialHelper.ContainsProblematicCharacters("Test$123"));
            Assert.True(CredentialHelper.ContainsProblematicCharacters("Special%Char"));
            Assert.True(CredentialHelper.ContainsProblematicCharacters("Paren(test)"));
            Assert.False(CredentialHelper.ContainsProblematicCharacters("SimplePassword123"));
            Assert.False(CredentialHelper.ContainsProblematicCharacters(""));
            Assert.False(CredentialHelper.ContainsProblematicCharacters(null));
        }

        [Fact]
        public void ConvertToSecureString_ShouldCreateSecureString()
        {
            var password = "TestPassword123!";
            var secureString = CredentialHelper.ConvertToSecureString(password);
            
            Assert.NotNull(secureString);
            Assert.Equal(password.Length, secureString.Length);
        }

        [Fact]
        public void ConvertToSecureString_ShouldThrowOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => CredentialHelper.ConvertToSecureString(null));
        }

        // --- General-purpose escaping for username / server-host fields ---
        // These guard against PowerShell argument injection via fields other
        // than the password (which was already escaped). See A6 security review.

        [Theory]
        [InlineData("administrator", "administrator")]
        [InlineData("DOMAIN\\user", "DOMAIN\\user")]
        [InlineData("o'brien", "o''brien")]
        public void EscapeForPowerShellSingleQuotes_ShouldDoubleSingleQuotes(string input, string expected)
        {
            var result = CredentialHelper.EscapeForPowerShellSingleQuotes(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("server01", "server01")]
        [InlineData("host\"name", "host\\\"name")]
        [InlineData("a$(b)", "a`$(b)")]
        [InlineData("back`tick", "back``tick")]
        [InlineData("path\\share", "path\\\\share")]
        public void EscapeForPowerShellDoubleQuotes_ShouldEscapeSpecialChars(string input, string expected)
        {
            var result = CredentialHelper.EscapeForPowerShellDoubleQuotes(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EscapeForPowerShellSingleQuotes_ShouldHandleEmptyOrNull(string? input)
        {
            Assert.Equal(string.Empty, CredentialHelper.EscapeForPowerShellSingleQuotes(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EscapeForPowerShellDoubleQuotes_ShouldHandleEmptyOrNull(string? input)
        {
            Assert.Equal(string.Empty, CredentialHelper.EscapeForPowerShellDoubleQuotes(input));
        }

        [Fact]
        public void EscapeForPowerShellSingleQuotes_MaliciousUsername_CannotBreakOutOfLiteral()
        {
            // Classic injection payload: close the quote, run a command, comment the rest.
            const string payload = "a';calc;#";
            var escaped = CredentialHelper.EscapeForPowerShellSingleQuotes(payload);

            // Every single quote is doubled, so embedded in '...' it stays one literal token.
            Assert.Equal("a'';calc;#", escaped);

            // Reconstruct the actual single-quoted argument the command builder emits and
            // assert there is no lone (unescaped) quote that would terminate the literal early.
            string argLiteral = $"'{escaped}'";
            Assert.False(HasUnescapedSingleQuote(argLiteral),
                "Escaped username must not contain a quote that breaks out of the single-quoted literal.");
        }

        [Fact]
        public void EscapeForPowerShellDoubleQuotes_MaliciousServer_NeutralizesQuoteAndSubexpression()
        {
            // Payload tries to close the double quote and inject a subexpression.
            const string payload = "host\";$(calc)";
            var escaped = CredentialHelper.EscapeForPowerShellDoubleQuotes(payload);

            // The double quote is escaped and the $ is backtick-escaped, so no breakout / evaluation.
            Assert.Equal("host\\\";`$(calc)", escaped);
            // Every '$' must be backtick-escaped so PowerShell never starts a subexpression.
            Assert.False(HasUnescapedDollar(escaped),
                "Escaped server must not contain a '$' that could begin a subexpression.");
        }

        // Returns true if the single-quoted literal contains a quote that is NOT part of
        // an escaped pair ('') and therefore would terminate the literal prematurely.
        private static bool HasUnescapedSingleQuote(string singleQuotedLiteral)
        {
            // Strip the surrounding delimiters.
            string inner = singleQuotedLiteral.Substring(1, singleQuotedLiteral.Length - 2);
            int i = 0;
            while (i < inner.Length)
            {
                if (inner[i] == '\'')
                {
                    // A valid escaped quote is exactly two quotes in a row.
                    if (i + 1 < inner.Length && inner[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }
                    return true; // lone quote -> breakout
                }
                i++;
            }
            return false;
        }

        // Returns true if the value contains a '$' that is NOT immediately preceded by a
        // backtick, i.e. a dollar PowerShell would treat as the start of a subexpression.
        private static bool HasUnescapedDollar(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '$' && (i == 0 || value[i - 1] != '`'))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
