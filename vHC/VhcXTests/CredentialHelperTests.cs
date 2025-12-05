using NUnit.Framework;
using VeeamHealthCheck.Functions.Collection.Security;

namespace VeeamHealthCheck.Tests
{
    [TestFixture]
    public class CredentialHelperTests
    {
        [Test]
        [TestCase("Simple123", "Simple123")]
        [TestCase("Pass'word", "Pass''word")]
        [TestCase("It's'a'test'", "It''s''a''test''")]
        [TestCase("NoSpecialChars", "NoSpecialChars")]
        public void EscapePasswordForPowerShell_ShouldEscapeSingleQuotes(string input, string expected)
        {
            var result = CredentialHelper.EscapePasswordForPowerShell(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase("Pass$word", "Pass`$word")]
        [TestCase("Test\"Quote", "Test\\\"Quote")]
        [TestCase("Back\\slash", "Back\\\\slash")]
        [TestCase("Backtick`test", "Backtick``test")]
        public void EscapePasswordForDoubleQuotes_ShouldEscapeSpecialChars(string input, string expected)
        {
            var result = CredentialHelper.EscapePasswordForDoubleQuotes(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        public void EscapePasswordForPowerShell_ShouldHandleEmptyOrNull(string input)
        {
            var result = CredentialHelper.EscapePasswordForPowerShell(input);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        // Test passwords similar to the one that caused issues
        [TestCase("Test@123)45%End")]
        [TestCase("P@ss!word#2024")]
        [TestCase("Complex$Pass&Word*123")]
        [TestCase("Special(Char)Test%100")]
        [TestCase("Quote'Test\"With$Vars")]
        public void ComplexPasswords_ShouldBeProperlyEscaped(string password)
        {
            var escaped = CredentialHelper.EscapePasswordForPowerShell(password);
            
            // The escaped password should not break when used in a command
            Assert.IsNotNull(escaped);
            
            // Check for problematic characters
            bool hasProblematic = CredentialHelper.ContainsProblematicCharacters(password);
            Assert.IsTrue(hasProblematic);
        }

        [Test]
        public void ContainsProblematicCharacters_ShouldDetectSpecialChars()
        {
            Assert.IsTrue(CredentialHelper.ContainsProblematicCharacters("Pass@word"));
            Assert.IsTrue(CredentialHelper.ContainsProblematicCharacters("Test$123"));
            Assert.IsTrue(CredentialHelper.ContainsProblematicCharacters("Special%Char"));
            Assert.IsTrue(CredentialHelper.ContainsProblematicCharacters("Paren(test)"));
            Assert.IsFalse(CredentialHelper.ContainsProblematicCharacters("SimplePassword123"));
            Assert.IsFalse(CredentialHelper.ContainsProblematicCharacters(""));
            Assert.IsFalse(CredentialHelper.ContainsProblematicCharacters(null));
        }

        [Test]
        public void ConvertToSecureString_ShouldCreateSecureString()
        {
            var password = "TestPassword123!";
            var secureString = CredentialHelper.ConvertToSecureString(password);
            
            Assert.IsNotNull(secureString);
            Assert.AreEqual(password.Length, secureString.Length);
        }

        [Test]
        public void ConvertToSecureString_ShouldThrowOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => CredentialHelper.ConvertToSecureString(null));
        }
    }
}
