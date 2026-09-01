// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Moq;
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

        [Fact]
        public void IUiNotifier_ConfirmDefaultMethod_BlocksOnAsyncPrimitiveAndReturnsResult()
        {
            // Locks in the shape Part 1/Part 2 depend on: a mock implementing
            // only the two Async members must make the default-interface
            // Confirm(...) wrapper work with zero extra code, exactly as
            // WpfUiNotifier/AvaloniaUiNotifier will only ever implement the
            // two Async members.
            var mockNotifier = new Mock<IUiNotifier>();
            mockNotifier.Setup(n => n.ConfirmAsync("msg", "title"))
                .ReturnsAsync(true);
            CGlobals.Notifier = mockNotifier.Object;

            bool result = CGlobals.Notifier.Confirm("msg", "title");

            Assert.True(result);
        }
    }
}
