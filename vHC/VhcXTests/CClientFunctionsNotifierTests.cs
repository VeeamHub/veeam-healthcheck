// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Naming convention: [Method]_[Scenario]_[Expected].
    /// </summary>
    [Collection("GlobalState")]
    public class CClientFunctionsNotifierTests : IDisposable
    {
        private readonly IUiNotifier _origNotifier;

        public CClientFunctionsNotifierTests()
        {
            _origNotifier = CGlobals.Notifier;
        }

        public void Dispose()
        {
            CGlobals.Notifier = _origNotifier;
        }

        // A plain implementation of only the two Async primitives - exactly
        // the shape WpfUiNotifier/AvaloniaUiNotifier use - rather than a Moq
        // mock. Moq's proxy overrides every interface member, including C#
        // default interface methods, so calling Confirm(...) on a
        // Mock<IUiNotifier> where only ConfirmAsync(...) is set up does NOT
        // fall through to IUiNotifier's real default-method body; it returns
        // the mock's own (unset) default instead. Confirmed empirically on a
        // real Windows/xUnit run - this is why a plain stub, not a mock, is
        // required to test the default-interface-method wrapper itself.
        private sealed class StubUiNotifier : IUiNotifier
        {
            public bool ConfirmResult { get; set; }

            public Task ShowErrorAsync(string message, string title) => Task.CompletedTask;

            public Task<bool> ConfirmAsync(string message, string title) => Task.FromResult(this.ConfirmResult);
        }

        [Fact]
        public void IUiNotifier_ConfirmDefaultMethod_BlocksOnAsyncPrimitiveAndReturnsResult()
        {
            // Locks in the shape Part 1/Part 2 depend on: an implementation
            // providing only the two Async members must make the
            // default-interface Confirm(...) wrapper work with zero extra
            // code, exactly as WpfUiNotifier/AvaloniaUiNotifier only ever
            // implement the two Async members.
            var stubNotifier = new StubUiNotifier { ConfirmResult = true };
            CGlobals.Notifier = stubNotifier;

            bool result = CGlobals.Notifier.Confirm("msg", "title");

            Assert.True(result);
        }
    }
}
