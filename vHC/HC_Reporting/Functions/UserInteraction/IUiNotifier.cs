// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    /// <summary>
    /// UI-framework-agnostic seam for the OK/error and yes/no dialogs business
    /// logic needs in GUI mode. The two Async primitives are what WPF/Avalonia
    /// implementations provide (Avalonia's dialogs are Task-based only - there
    /// is no synchronous MessageBox.Show equivalent). ShowError/Confirm are
    /// default-implemented here as blocking wrappers so every existing
    /// synchronous call site (CClientFunctions, CCollections, CredsHandler,
    /// PSInvoker, CImpersonation) keeps working completely unchanged.
    ///
    /// SAFE to call ShowError/Confirm from any thread that is not the UI
    /// thread (a background Task, the CLI console thread, an impersonated
    /// WindowsIdentity.RunImpersonated delegate) - this is exactly WPF's
    /// existing Dispatcher.Invoke semantics: the calling thread blocks while
    /// the UI thread shows the dialog and processes it. NEVER call them from
    /// the UI thread itself - that deadlocks, because the awaited
    /// continuation needs the same thread that just blocked waiting for it.
    /// Most of VhcGui's own button-click handlers run on the UI thread and
    /// call ShowErrorAsync/ConfirmAsync directly with await/fire-and-forget -
    /// that's fine, ordinary async UI code. Two specific call sites are
    /// different: VhcGui's constructor path and AcceptButton_click both go
    /// through a synchronous business-logic method (PreRunCheck/AcceptTerms)
    /// that uses ShowError/Confirm internally - calling that method directly
    /// from the UI thread would deadlock, so those two wrap the call in
    /// Task.Run instead. See Part 2, Task 11.
    /// </summary>
    public interface IUiNotifier
    {
        Task ShowErrorAsync(string message, string title);
        Task<bool> ConfirmAsync(string message, string title);

        void ShowError(string message, string title) =>
            ShowErrorAsync(message, title).GetAwaiter().GetResult();

        bool Confirm(string message, string title) =>
            ConfirmAsync(message, title).GetAwaiter().GetResult();
    }
}
