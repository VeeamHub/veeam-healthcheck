// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Diagnostics;
using System.Reflection;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Functions.Collection.Security;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Regression tests for the log-collection / server-dump PowerShell argument
    /// builders, which previously interpolated the operator-controlled server
    /// (REMOTEHOST) unquoted and unescaped (code-review cd-13/cs-01).
    /// </summary>
    [Trait("Category", "Security")]
    public class PSInvokerInjectionTests
    {
        private const string MaliciousServer = "host\";calc;\"";

        private static ProcessStartInfo InvokePrivate(string method, params object[] args)
        {
            var m = typeof(PSInvoker).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(m);
            return (ProcessStartInfo)m.Invoke(new PSInvoker(), args);
        }

        [Fact]
        public void LogCollectionInfo_MaliciousServer_IsQuotedAndEscaped()
        {
            var psi = InvokePrivate("LogCollectionInfo", @"C:\scripts\collect.ps1", @"C:\out", MaliciousServer);

            string expected = CredentialHelper.EscapeForPowerShellDoubleQuotes(MaliciousServer);
            Assert.Contains($"-Server \"{expected}\"", psi.Arguments);
            // The raw, unescaped breakout sequence must not survive.
            Assert.DoesNotContain($"-Server {MaliciousServer}", psi.Arguments);
        }

        [Fact]
        public void ServerDumpInfo_MaliciousRemoteHost_IsQuotedAndEscaped()
        {
            string original = CGlobals.REMOTEHOST;
            try
            {
                CGlobals.REMOTEHOST = MaliciousServer;
                var psi = InvokePrivate("ServerDumpInfo", @"C:\scripts\dump.ps1");

                string expected = CredentialHelper.EscapeForPowerShellDoubleQuotes(MaliciousServer);
                Assert.Contains($"-Server \"{expected}\"", psi.Arguments);
                Assert.DoesNotContain($"-Server {MaliciousServer}", psi.Arguments);
            }
            finally
            {
                CGlobals.REMOTEHOST = original;
            }
        }
    }
}
