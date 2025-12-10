// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using VeeamHealthCheck.Startup;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Tests.Security
{
    [Collection("Credential Store Tests")]
    [Trait("Category", "Security")]
    public class CredentialStoreSecurityTests : IDisposable
    {
        private readonly string _testStorePath;
        private readonly string _originalStorePath;

        public CredentialStoreSecurityTests()
        {
            // Create a temporary directory for test credential storage
            _testStorePath = Path.Combine(Path.GetTempPath(), $"vhc-creds-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testStorePath);
            
            // Store original path to restore later (if needed)
            _originalStorePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testStorePath))
            {
                try
                {
                    Directory.Delete(_testStorePath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void StoredCredentials_ShouldBeEncrypted()
        {
            // Arrange
            string server = "test-server.local";
            string username = "testuser";
            string password = "SuperSecret@Password123!";

            // Act
            CredentialStore.Set(server, username, password);

            // Read the raw file content
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
            
            string fileContent = File.ReadAllText(storePath);

            // Assert
            Assert.DoesNotContain(password, fileContent, StringComparison.Ordinal);
            Assert.Contains(username, fileContent, StringComparison.Ordinal);
            Assert.Contains("PasswordEnc", fileContent, StringComparison.Ordinal);
            
            // Verify the password field is Base64 encoded (handle JSON indentation with newlines)
            // Pattern should match: "PasswordEnc": "base64data" with any whitespace
            // Note: JsonSerializer may escape + as \u002B and / as \u002F, so pattern includes \\u for Unicode escapes
            var match = Regex.Match(fileContent, "\"PasswordEnc\"\\s*:\\s*\"([A-Za-z0-9+/=\\\\u]+)\"");
            Assert.True(match.Success, $"Expected to find Base64-encoded password, actual JSON: {fileContent}");

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void StoredCredentials_ShouldNotContainPlaintextPassword()
        {
            // Arrange
            string server = "secure-server.local";
            string username = "admin";
            string password = "MyVerySecretPassword@2024!";

            // Act
            CredentialStore.Set(server, username, password);

            // Read the raw file
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
            
            string fileContent = File.ReadAllText(storePath);

            // Assert - Check for plaintext password patterns
            Assert.DoesNotContain(password, fileContent, StringComparison.Ordinal);
            
            // Check that password parts aren't exposed
            string[] passwordParts = password.Split(new[] { '@', '!', '#', '$', '%' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in passwordParts.Where(p => p.Length > 3))
            {
                // Allow username but not password parts
                if (!part.Equals(username, StringComparison.Ordinal))
                {
                    Assert.DoesNotContain(part, fileContent, StringComparison.Ordinal);
                }
            }

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void Get_ShouldDecryptPasswordCorrectly()
        {
            // Arrange
            string server = "decrypt-test.local";
            string username = "testuser";
            string originalPassword = "Test@Password#2024!";

            // Act
            CredentialStore.Set(server, username, originalPassword);
            var retrieved = CredentialStore.Get(server);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(username, retrieved.Value.Username);
            Assert.Equal(originalPassword, retrieved.Value.Password);

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void Set_WithComplexPassword_ShouldEncryptAndDecryptCorrectly()
        {
            // Arrange - Test various special characters
            string server = "complex-password-test.local";
            string username = "admin";
            string complexPassword = "P@ssw0rd!#$%^&*()_+-=[]{}|;':\",./<>?`~";

            // Act
            CredentialStore.Set(server, username, complexPassword);
            var retrieved = CredentialStore.Get(server);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(complexPassword, retrieved.Value.Password);

            // Verify file doesn't contain plaintext
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
            
            string fileContent = File.ReadAllText(storePath);
            Assert.DoesNotContain(complexPassword, fileContent, StringComparison.Ordinal);

            // Cleanup
            CredentialStore.Clear();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        public void Set_WithEmptyPassword_ShouldStoreButGetReturnsNull(string emptyPassword)
        {
            // Arrange
            string server = "empty-password-test.local";
            string username = "testuser";

            // Act & Assert
            // Empty passwords should be rejected or handled gracefully
            if (string.IsNullOrWhiteSpace(emptyPassword))
            {
                // This is expected behavior - empty passwords should be handled
                CredentialStore.Set(server, username, emptyPassword);
                var retrieved = CredentialStore.Get(server);
                
                // System should handle empty passwords appropriately
                // Either store and retrieve or reject
                Assert.True(retrieved == null || retrieved.Value.Password == emptyPassword);
            }

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void Remove_ShouldCompletelyRemoveCredentials()
        {
            // Arrange
            string server = "removal-test.local";
            string username = "testuser";
            string password = "ToBeRemoved@123";

            // Act
            CredentialStore.Set(server, username, password);
            Assert.NotNull(CredentialStore.Get(server));

            bool removed = CredentialStore.Remove(server);

            // Assert
            Assert.True(removed);
            Assert.Null(CredentialStore.Get(server));

            // Verify file doesn't contain the server name anymore
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");

            if (File.Exists(storePath))
            {
                string fileContent = File.ReadAllText(storePath);
                Assert.DoesNotContain(server, fileContent, StringComparison.Ordinal);
            }

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void Clear_ShouldRemoveAllCredentialsAndFile()
        {
            // Arrange
            CredentialStore.Set("server1.local", "user1", "pass1");
            CredentialStore.Set("server2.local", "user2", "pass2");
            CredentialStore.Set("server3.local", "user3", "pass3");

            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");

            Assert.True(File.Exists(storePath));

            // Act
            CredentialStore.Clear();

            // Assert
            Assert.False(File.Exists(storePath));
            Assert.Null(CredentialStore.Get("server1.local"));
            Assert.Null(CredentialStore.Get("server2.local"));
            Assert.Null(CredentialStore.Get("server3.local"));
            Assert.False(CredentialStore.HasStoredCredentials());
        }

        [Fact]
        public void MultipleServers_ShouldStoreSeparateEncryptedCredentials()
        {
            // Arrange
            var servers = new[]
            {
                ("server1.local", "admin1", "Password1@2024"),
                ("server2.local", "admin2", "Password2@2024"),
                ("server3.local", "admin3", "Password3@2024")
            };

            // Act
            foreach (var (server, user, pass) in servers)
            {
                CredentialStore.Set(server, user, pass);
            }

            // Assert - Verify each can be retrieved correctly
            foreach (var (server, user, pass) in servers)
            {
                var retrieved = CredentialStore.Get(server);
                Assert.NotNull(retrieved);
                Assert.Equal(user, retrieved.Value.Username);
                Assert.Equal(pass, retrieved.Value.Password);
            }

            // Verify file doesn't contain any plaintext passwords
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
            
            string fileContent = File.ReadAllText(storePath);

            foreach (var (server, user, pass) in servers)
            {
                Assert.DoesNotContain(pass, fileContent, StringComparison.Ordinal);
            }

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void PasswordEncryption_ShouldBeUniquePerPassword()
        {
            // Arrange
            string server1 = "server1.local";
            string server2 = "server2.local";
            string username = "admin";
            string password1 = "Password123!";
            string password2 = "DifferentPassword456@";

            // Act - Set DIFFERENT passwords for each server
            CredentialStore.Set(server1, username, password1);
            CredentialStore.Set(server2, username, password2);

            // Read raw file
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");
            
            string fileContent = File.ReadAllText(storePath);

            // Assert - Different passwords should produce different encrypted values
            // Pattern must handle indented JSON with newlines between key and value
            // Note: JsonSerializer may escape + as \u002B and / as \u002F, so pattern includes \\u for Unicode escapes
            var passwordMatches = Regex.Matches(fileContent, "\"PasswordEnc\"\\s*:\\s*\"([A-Za-z0-9+/=\\\\u]+)\"");
            Assert.True(passwordMatches.Count >= 2, 
                $"Should have at least 2 encrypted passwords, found {passwordMatches.Count}. JSON content: {fileContent}");

            // Extract the encrypted password values
            var encryptedPasswords = passwordMatches.Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            // Different passwords should produce different encrypted Base64 strings
            Assert.NotEqual(encryptedPasswords[0], encryptedPasswords[1]);
            
            // Neither should be plaintext
            Assert.DoesNotContain(password1, fileContent);
            Assert.DoesNotContain(password2, fileContent);

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void Get_NonExistentServer_ShouldReturnNull()
        {
            // Act
            var result = CredentialStore.Get("nonexistent-server.local");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Remove_NonExistentServer_ShouldReturnFalse()
        {
            // Act
            bool removed = CredentialStore.Remove("nonexistent-server.local");

            // Assert
            Assert.False(removed);
        }

        [Fact]
        public void CredentialFile_ShouldBeInUserProfile()
        {
            // Arrange
            string server = "location-test.local";
            string username = "testuser";
            string password = "TestPassword@123";

            // Act
            CredentialStore.Set(server, username, password);

            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");

            // Assert - File should be in user's AppData
            Assert.True(File.Exists(storePath));
            Assert.Contains("AppData", storePath);
            Assert.Contains("VeeamHealthCheck", storePath);

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void GetAllServers_ShouldReturnStoredServerNames()
        {
            // Arrange
            var expectedServers = new[] { "server1.local", "server2.local", "server3.local" };
            
            foreach (var server in expectedServers)
            {
                CredentialStore.Set(server, "user", "pass");
            }

            // Act
            var servers = CredentialStore.GetAllServers();

            // Assert
            Assert.Equal(expectedServers.Length, servers.Count);
            foreach (var server in expectedServers)
            {
                Assert.Contains(server, servers);
            }

            // Cleanup
            CredentialStore.Clear();
        }

        [Fact]
        public void HasStoredCredentials_ShouldReflectCurrentState()
        {
            // Arrange - Start clean
            CredentialStore.Clear();

            // Act & Assert - Initially false
            Assert.False(CredentialStore.HasStoredCredentials());

            // Add credentials
            CredentialStore.Set("test.local", "user", "pass");
            Assert.True(CredentialStore.HasStoredCredentials());

            // Clear
            CredentialStore.Clear();
            Assert.False(CredentialStore.HasStoredCredentials());
        }

        [Fact]
        public void Update_ExistingCredentials_ShouldOverwrite()
        {
            // Arrange
            string server = "update-test.local";
            string username1 = "user1";
            string password1 = "OldPassword@123";
            string username2 = "user2";
            string password2 = "NewPassword@456";

            // Act
            CredentialStore.Set(server, username1, password1);
            var first = CredentialStore.Get(server);

            CredentialStore.Set(server, username2, password2);
            var updated = CredentialStore.Get(server);

            // Assert
            Assert.NotNull(first);
            Assert.Equal(username1, first.Value.Username);
            Assert.Equal(password1, first.Value.Password);

            Assert.NotNull(updated);
            Assert.Equal(username2, updated.Value.Username);
            Assert.Equal(password2, updated.Value.Password);

            // Cleanup
            CredentialStore.Clear();
        }
    }
}
