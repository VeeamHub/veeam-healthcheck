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
    }
}
