// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Unit tests for the unattended / silent mode contract introduced
    /// for the fleet-deployable VHC scenario. Tests cover CLI parsing,
    /// CredentialStore.SetTransient transient cache behavior,
    /// CredsHandler silent-mode short-circuit, and the
    /// PSInvoker VB365 -Username / -PasswordBase64 plumbing.
    ///
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("Credential Store Tests")]
    [Trait("Category", "Silent")]
    public class SilentModeTests : IDisposable
    {
        private readonly string _credStorePath;
        private readonly bool _origSilent;
        private readonly string _origCredFilePath;
        private readonly bool _origSaveCredsOnly;
        private readonly bool _origClearStored;
        private readonly string _origRemoteHost;

        public SilentModeTests()
        {
            _origSilent = CGlobals.Silent;
            _origCredFilePath = CGlobals.CredFilePath;
            _origSaveCredsOnly = CGlobals.SaveCredsOnly;
            _origClearStored = CGlobals.ClearStoredCreds;
            _origRemoteHost = CGlobals.REMOTEHOST;

            _credStorePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VeeamHealthCheck", "creds.json");

            // Start each test from a clean slate
            CGlobals.Silent = false;
            CGlobals.CredFilePath = null;
            CGlobals.SaveCredsOnly = false;
            CGlobals.ClearStoredCreds = false;
            CGlobals.REMOTEHOST = string.Empty;
        }

        public void Dispose()
        {
            CGlobals.Silent = _origSilent;
            CGlobals.CredFilePath = _origCredFilePath;
            CGlobals.SaveCredsOnly = _origSaveCredsOnly;
            CGlobals.ClearStoredCreds = _origClearStored;
            CGlobals.REMOTEHOST = _origRemoteHost;
        }

        // ----------------------------------------------------------------
        // CArgsParser tests
        // ----------------------------------------------------------------

        [Fact]
        public void CArgsParser_SilentFlag_SetsCGlobalsSilent()
        {
            // Arrange
            CGlobals.Silent = false;
            var parser = new CArgsParser(new[] { "/silent" });

            // Act – use the internal seam so the parser does not exit the test process.
            var method = typeof(CArgsParser).GetMethod(
                "ApplyFlagsForTest",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(parser, new object[] { new[] { "/silent" } });

            // Assert
            Assert.True(CGlobals.Silent);
        }

        [Fact]
        public void CArgsParser_SilentAndSavecreds_ReturnsConflictExit2()
        {
            // /silent + /savecreds is mutually exclusive per the plan.
            // The parser surfaces the conflict via ValidateSilentArgs (exit-code returning seam),
            // so we can assert the conflict without actually exiting the process.
            var parser = new CArgsParser(new[] { "/silent", "/savecreds" });

            CGlobals.Silent = true;
            CGlobals.SaveCredsOnly = true;

            var method = typeof(CArgsParser).GetMethod(
                "ValidateSilentArgs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            int exit = (int)method.Invoke(parser, Array.Empty<object>());

            Assert.Equal(2, exit);
        }

        [Fact]
        public void CArgsParser_CredfileMalformed_ReturnsExit6()
        {
            // Write a deliberately malformed JSON file to a temp location and
            // assert LoadCredFile reports exit code 6.
            string tempFile = Path.Combine(Path.GetTempPath(), $"vhc-credfile-malformed-{Guid.NewGuid()}.json");
            File.WriteAllText(tempFile, "{ this is not json");

            try
            {
                var parser = new CArgsParser(new[] { "/credfile=" + tempFile });
                var method = typeof(CArgsParser).GetMethod(
                    "LoadCredFile",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                int exit = (int)method.Invoke(parser, new object[] { tempFile });

                Assert.Equal(6, exit);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void CArgsParser_CredfileValid_PopulatesTransientCacheWithoutDiskWrite()
        {
            // Arrange – build a valid credfile JSON with one host entry, a
            // Base64 password, and verify that after LoadCredFile:
            //   * CredentialStore.Get(host) returns the credentials
            //   * the on-disk creds.json mtime is unchanged (or absent)
            string host = "transient-host.local";
            string username = "DOMAIN\\svc-vhc";
            string password = "TransientPassword@123";
            string passwordBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));

            string credFile = Path.Combine(Path.GetTempPath(), $"vhc-credfile-valid-{Guid.NewGuid()}.json");
            var payload = new System.Collections.Generic.Dictionary<string, object>
            {
                [host] = new { username = username, passwordBase64 = passwordBase64 }
            };
            File.WriteAllText(credFile, JsonSerializer.Serialize(payload));

            // Capture creds.json baseline (it may or may not exist; both are valid)
            bool diskFileExisted = File.Exists(_credStorePath);
            DateTime? baselineMtime = diskFileExisted ? File.GetLastWriteTimeUtc(_credStorePath) : (DateTime?)null;

            try
            {
                var parser = new CArgsParser(new[] { "/credfile=" + credFile });
                var method = typeof(CArgsParser).GetMethod(
                    "LoadCredFile",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                int exit = (int)method.Invoke(parser, new object[] { credFile });

                // Assert exit success and credentials reachable via CredentialStore.Get
                Assert.Equal(0, exit);

                var stored = CredentialStore.Get(host);
                Assert.NotNull(stored);
                Assert.Equal(username, stored.Value.Username);
                Assert.Equal(password, stored.Value.Password);

                // Assert no disk write
                bool diskFileExistsNow = File.Exists(_credStorePath);
                Assert.Equal(diskFileExisted, diskFileExistsNow);
                if (diskFileExisted && baselineMtime.HasValue)
                {
                    Assert.Equal(baselineMtime.Value, File.GetLastWriteTimeUtc(_credStorePath));
                }
            }
            finally
            {
                if (File.Exists(credFile)) File.Delete(credFile);
                CredentialStore.Remove(host); // best-effort cleanup of in-memory cache
            }
        }

        // ----------------------------------------------------------------
        // CredsHandler tests
        // ----------------------------------------------------------------

        [Fact]
        public void CredsHandler_GetCreds_SilentNoStored_ReturnsNullWithoutPrompting()
        {
            // Arrange - silent mode, no stored credentials for an obviously fake host
            string fakeHost = $"silent-no-stored-{Guid.NewGuid()}";
            CGlobals.REMOTEHOST = fakeHost;
            CGlobals.Silent = true;
            CredsHandler.PromptCallCount = 0;

            var handler = new CredsHandler();

            // Act
            var result = handler.GetCreds();

            // Assert
            Assert.Null(result);
            Assert.Equal(0, CredsHandler.PromptCallCount);
        }

        [Fact]
        public void CredsHandler_GetCreds_SilentWithStored_ReturnsCreds()
        {
            // Arrange - silent mode with credentials present in the store.
            string host = $"silent-with-stored-{Guid.NewGuid()}";
            string user = "DOMAIN\\svc-vhc";
            string pass = "Stored@Password123";

            CGlobals.REMOTEHOST = host;
            CGlobals.Silent = true;
            CredentialStore.Set(host, user, pass);
            CredsHandler.PromptCallCount = 0;

            try
            {
                var handler = new CredsHandler();

                // Act
                var result = handler.GetCreds();

                // Assert
                Assert.NotNull(result);
                Assert.Equal(user, result.Value.Username);
                Assert.Equal(pass, result.Value.Password);
                Assert.Equal(0, CredsHandler.PromptCallCount);
            }
            finally
            {
                CredentialStore.Remove(host);
            }
        }

        // ----------------------------------------------------------------
        // CredentialStore tests
        // ----------------------------------------------------------------

        [Fact]
        public void CredentialStore_SetTransient_DoesNotWriteToDisk()
        {
            // Arrange - capture baseline mtime/existence of creds.json.
            string host = $"transient-setter-{Guid.NewGuid()}";
            bool diskFileExisted = File.Exists(_credStorePath);
            DateTime? baselineMtime = diskFileExisted ? File.GetLastWriteTimeUtc(_credStorePath) : (DateTime?)null;

            try
            {
                // Act
                CredentialStore.SetTransient(host, "transient-user", "TransientPassword@123");

                // Assert - in-memory cache is populated, on-disk file untouched.
                var stored = CredentialStore.Get(host);
                Assert.NotNull(stored);
                Assert.Equal("transient-user", stored.Value.Username);
                Assert.Equal("TransientPassword@123", stored.Value.Password);

                bool diskFileExistsNow = File.Exists(_credStorePath);
                Assert.Equal(diskFileExisted, diskFileExistsNow);
                if (diskFileExisted && baselineMtime.HasValue)
                {
                    Assert.Equal(baselineMtime.Value, File.GetLastWriteTimeUtc(_credStorePath));
                }
            }
            finally
            {
                CredentialStore.Remove(host);
            }
        }

        // ----------------------------------------------------------------
        // PSInvoker tests
        // ----------------------------------------------------------------

        [Fact]
        public void PSInvoker_VB365Args_IncludesUsernameAndPasswordBase64WhenNeedsCredentials()
        {
            // Arrange - simulate remote VB365 with stored credentials.
            string host = $"vb365-args-{Guid.NewGuid()}";
            string user = "DOMAIN\\svc-vbo";
            string pass = "VboPassword@123";
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(pass);
            string expectedB64 = Convert.ToBase64String(passwordBytes);

            CGlobals.REMOTEHOST = host;
            CGlobals.REMOTEEXEC = true;
            CredentialStore.Set(host, user, pass);

            try
            {
                var invoker = new VeeamHealthCheck.Functions.Collection.PSCollections.PSInvoker();

                // Act - invoke the internal seam that returns the assembled VB365 arg string.
                // BuildVb365Arguments has a single overload taking `out string safeArgs`.
                // Reflection requires passing a placeholder slot for the out param;
                // we ignore the resulting safeArgs value.
                var method = typeof(VeeamHealthCheck.Functions.Collection.PSCollections.PSInvoker)
                    .GetMethod("BuildVb365Arguments", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                object[] invokeArgs = new object[] { null };
                string args = (string)method.Invoke(invoker, invokeArgs);

                // Assert. The username is escaped for the double-quoted argument
                // context (backslash is doubled), so assert the escaped form that
                // the command builder now emits — this guards the injection fix.
                string expectedUserInArgs = CredentialHelper.EscapeForPowerShellDoubleQuotes(user);
                Assert.Contains("-Username", args);
                Assert.Contains(expectedUserInArgs, args);
                Assert.Contains("-PasswordBase64", args);
                Assert.Contains(expectedB64, args);
            }
            finally
            {
                CredentialStore.Remove(host);
                CGlobals.REMOTEEXEC = false;
            }
        }

        // ----------------------------------------------------------------
        // CMessages help-menu tests
        // ----------------------------------------------------------------

        [Fact]
        public void HelpMenu_ContainsSilentModeFlags()
        {
            string help = CMessages.helpMenu;
            Assert.Contains("/silent", help);
            Assert.Contains("/savecreds", help);
            Assert.Contains("/credfile=", help);
        }

        [Fact]
        public void HelpMenu_ContainsSilentModeExitCodes()
        {
            string help = CMessages.helpMenu;
            Assert.Contains("UNATTENDED", help);
            Assert.Contains("Exit codes", help);
        }
    }
}
