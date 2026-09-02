// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Threading.Tasks;
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

        // A plain implementation of only PromptAsync - exactly the shape
        // WpfCredentialPrompter/AvaloniaCredentialPrompter use - rather than
        // a Moq mock. Moq's proxy overrides every interface member,
        // including C# default interface methods, so calling Prompt(...) on
        // a Mock<ICredentialPrompter> where only PromptAsync(...) is set up
        // does NOT fall through to ICredentialPrompter's real default-method
        // body; it returns the mock's own (unset) default (null) instead.
        // Confirmed empirically on a real Windows/xUnit run - this is why a
        // plain stub, not a mock, is required here.
        private sealed class StubCredentialPrompter : ICredentialPrompter
        {
            public (string Username, string Password)? Result { get; set; }

            public string LastHost { get; private set; }

            public int CallCount { get; private set; }

            public Task<(string Username, string Password)?> PromptAsync(string host)
            {
                this.LastHost = host;
                this.CallCount++;
                return Task.FromResult(this.Result);
            }
        }

        [Fact]
        public void GetCreds_GuiExecWithNoStoredCreds_DelegatesToCredentialPrompter()
        {
            CGlobals.Silent = false;
            CGlobals.GUIEXEC = true;
            CGlobals.REMOTEHOST = "test-vbr-host-that-has-no-stored-creds";
            CredentialStore.Remove(CGlobals.REMOTEHOST);

            // Stub implements only PromptAsync - Prompt(...) still resolves via
            // the interface's default method, exactly like production usage.
            var stubPrompter = new StubCredentialPrompter { Result = ("user", "pass") };
            CGlobals.CredentialPrompter = stubPrompter;

            var handler = new CredsHandler();
            var result = handler.GetCreds();

            Assert.Equal(("user", "pass"), result);
            Assert.Equal(1, stubPrompter.CallCount);
            Assert.Equal(CGlobals.REMOTEHOST, stubPrompter.LastHost);

            CredentialStore.Remove(CGlobals.REMOTEHOST);
        }
    }
}
