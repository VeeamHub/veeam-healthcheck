// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Moq;
using VeeamHealthCheck.Functions.CredsWindow;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CredsHandlerPrompterTests : IDisposable
    {
        private readonly ICredentialPrompter _origPrompter;
        private readonly bool _origGuiExec;
        private readonly bool _origSilent;
        private readonly string _origRemoteHost;

        public CredsHandlerPrompterTests()
        {
            _origPrompter = CGlobals.CredentialPrompter;
            _origGuiExec = CGlobals.GUIEXEC;
            _origSilent = CGlobals.Silent;
            _origRemoteHost = CGlobals.REMOTEHOST;
        }

        public void Dispose()
        {
            CGlobals.CredentialPrompter = _origPrompter;
            CGlobals.GUIEXEC = _origGuiExec;
            CGlobals.Silent = _origSilent;
            CGlobals.REMOTEHOST = _origRemoteHost;
        }

        [Fact]
        public void GetCreds_GuiExecWithNoStoredCreds_DelegatesToCredentialPrompter()
        {
            CGlobals.Silent = false;
            CGlobals.GUIEXEC = true;
            CGlobals.REMOTEHOST = "test-vbr-host-that-has-no-stored-creds";
            CredentialStore.Remove(CGlobals.REMOTEHOST);

            // Mock implements only PromptAsync - Prompt(...) still resolves via
            // the interface's default method, exactly like production usage.
            var mockPrompter = new Mock<ICredentialPrompter>();
            mockPrompter.Setup(p => p.PromptAsync(CGlobals.REMOTEHOST))
                .ReturnsAsync(("user", "pass"));
            CGlobals.CredentialPrompter = mockPrompter.Object;

            var handler = new CredsHandler();
            var result = handler.GetCreds();

            Assert.Equal(("user", "pass"), result);
            mockPrompter.Verify(p => p.PromptAsync(CGlobals.REMOTEHOST), Times.Once);

            CredentialStore.Remove(CGlobals.REMOTEHOST);
        }
    }
}
