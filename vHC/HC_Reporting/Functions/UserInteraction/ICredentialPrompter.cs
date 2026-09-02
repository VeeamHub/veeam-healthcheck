// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// UI-framework-agnostic seam for the GUI credential prompt dialog. See
    /// IUiNotifier's doc comment for the threading contract - Prompt(...) is
    /// the same "safe off the UI thread only" blocking default wrapper
    /// around PromptAsync(...).
    /// </summary>
    public interface ICredentialPrompter
    {
        Task<(string Username, string Password)?> PromptAsync(string host);

        (string Username, string Password)? Prompt(string host) =>
            PromptAsync(host).GetAwaiter().GetResult();
    }
}
