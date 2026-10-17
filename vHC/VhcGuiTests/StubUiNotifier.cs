using System.Collections.Generic;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.UserInteraction;

namespace VhcGuiTests
{
    /// <summary>
    /// Non-blocking <see cref="IUiNotifier"/> that records what the GUI asked and answers
    /// confirmations with <see cref="ConfirmResult"/>. Only the async members are implemented,
    /// so the default-interface Confirm/ShowError route through them.
    /// </summary>
    public sealed class StubUiNotifier : IUiNotifier
    {
        public bool ConfirmResult { get; set; } = true;

        public int Confirms { get; private set; }

        public List<string> Errors { get; } = new();

        public Task ShowErrorAsync(string message, string title)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string message, string title)
        {
            Confirms++;
            return Task.FromResult(ConfirmResult);
        }
    }
}
