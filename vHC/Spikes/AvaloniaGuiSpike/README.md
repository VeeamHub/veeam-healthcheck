# [SPIKE] Avalonia GUI prototype — throwaway, not production code

Answers one question: **is porting `VhcGui.xaml`/`VhcGui.xaml.cs` from WPF to
Avalonia mechanically straightforward, and does it actually build/run on
macOS?** Not wired to any real business logic (`CClientFunctions`, collection,
etc.) — the "run" click just simulates work with `Thread.Sleep`.

Recreates the parts of the real GUI most likely to hide porting risk:

- The `ModernButton` hover/disabled style (WPF `ControlTemplate` + `Style.Triggers`
  → Avalonia `Style` selectors with pseudo-classes like `:pointerover`/`:disabled`)
- A background `Task` that marshals UI updates back via a dispatcher
  (`Dispatcher.Invoke` → `Dispatcher.UIThread.Post`)
- Dynamically-set `Foreground`/`SolidColorBrush` on status text
  (`UpdateCollectionStatusText`)
- A modal completion dialog standing in for `MessageBox.Show(...)` and
  `CredentialPromptWindow`'s `DialogResult` pattern

## Run it

```
cd vHC/Spikes/AvaloniaGuiSpike
dotnet run
```

## Findings

- Builds and runs on macOS with the same `net8.0` SDK already installed for
  the main project — no Windows-only tooling required, confirming `UseWPF`
  (not `EnableWindowsTargeting`/the Windows APIs elsewhere in the app) is the
  actual compile blocker.
- WPF's `Style.Triggers` on `IsMouseOver`/`IsEnabled` ports to Avalonia
  selector syntax (`Button.modern:pointerover`) — same concept, different
  syntax, no structural rework needed.
- `Dispatcher.Invoke(Action)` (blocking/sync) → `Dispatcher.UIThread.Post`
  (fire-and-forget) or `InvokeAsync` (awaitable) — same "marshal back to the
  UI thread" shape as today's code.
- **Real gap:** Avalonia has no built-in `MessageBox`, and `Window.ShowDialog<T>`
  is async-only (no synchronous blocking call like WPF's `MessageBox.Show`).
  Turns out this does **not** force an async cascade through business logic,
  though — see the next finding.
- **Blocking on the async dialog from a background thread is safe and
  verified working**, not just reasoned about: `VerifyBlockingDialogPattern()`
  in `MainWindow.axaml.cs` does
  `Dispatcher.UIThread.InvokeAsync(async () => { await Task.Delay(800); return true; })`
  from inside `Task.Run(...)`, then calls `.GetAwaiter().GetResult()` on the
  result and blocks for the full 800ms before returning `true` — confirmed via
  `dotnet run` output (`result=True elapsedMs=805 PASS`), not just successful
  compilation. Two things this proves: (1) `InvokeAsync` correctly resolves to
  the `Func<Task<TResult>> → Task<TResult>` overload rather than the
  `Func<TResult> → Task<TResult>` one, which would have silently produced
  `Task<Task<bool>>` and a bogus/unawaited result - `bool task = ...` wouldn't
  even have compiled if it had; (2) blocking a **non-UI** thread on that call
  is exactly WPF's `Dispatcher.Invoke` semantics, not a deadlock risk - only
  blocking from the UI thread itself would deadlock. This means almost none of
  `CClientFunctions`/`CCollections`/`CredsHandler`/`PSInvoker` needs to become
  `async` at all: `IUiNotifier`/`ICredentialPrompter` expose async primitives
  (`ShowErrorAsync`/`ConfirmAsync`/`PromptAsync`) plus synchronous wrapper
  methods (default interface methods that call
  `...Async(...).GetAwaiter().GetResult()`) that every existing synchronous
  call site keeps using unchanged. Only the two call sites that already run
  **on** the UI thread directly - `VhcGui`'s constructor-time `PreRunCheck`
  call, and `AcceptButton_click` - need real `async`/`await`.
- **Pin the Avalonia version to the installed SDK's compiler.** `Avalonia
  12.1.1` (current latest) failed silently on this machine: it ships a Roslyn
  source generator (`Avalonia.Generators.dll`) built against C# compiler
  4.14, but the installed `dotnet 8.0.416` SDK only runs compiler 4.11. The
  generator didn't error — it just silently produced nothing, so
  `InitializeComponent`/named-control fields never existed and every
  `.axaml.cs` file failed with `CS0103` for names that "should" have existed.
  Downgrading to `Avalonia 11.3.20` (the last 11.x release, still maintained)
  fixed it immediately. Whatever real migration happens should either pin to
  the 11.x line or bump the solution to a .NET 9/10 SDK before adopting 12.x.
- Confirmed working end-to-end at runtime, not just compile time: `dotnet run`
  kept the process alive and the `PathBox_TextChanged` handler fired on
  startup exactly as WPF's equivalent would, so the generated code-behind
  members are wired up correctly, not just type-checking.
- Minor csproj gotcha: don't add an explicit `<AvaloniaResource Include="**/*.axaml" />`
  item — modern Avalonia already classifies `.axaml` files as `AvaloniaXaml`
  by default and merges them into `AvaloniaResource` internally. Adding your
  own item on top double-registers every file and fails with a confusing
  "Duplicate x:Class directive" error.

## Reference

[Avalonia's official WPF migration cheat sheet](https://raw.githubusercontent.com/AvaloniaUI/avalonia-docs/refs/heads/main/docs/migration/wpf/cheat-sheet.md)
confirms the `Dispatcher`/`Style.Triggers` findings above ("no triggers in
Avalonia" — pseudo-classes replace them entirely). It does **not** mention
`MessageBox` or `DialogResult` anywhere, so the async-only modal dialog gap
found here isn't documented upstream — budget real time for it in the actual
migration. It also flags `RoutedCommand` as having no built-in equivalent,
which doesn't affect `VhcGui` since it only uses plain `Click` handlers.
