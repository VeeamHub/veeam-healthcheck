# GUI Run() task: Environment.Exit(0) races with ContinueWith and ignores report exit code

**Category:** startup-cli
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/VhcGui.xaml.cs:248-261`

## Summary
The GUI `Run()` kicks off work on a background task that ends with `Environment.Exit(0)`, with a `.ContinueWith(t => hideProgressBar())` chained after it. `Environment.Exit(0)` terminates the process immediately, so (a) the report's actual return code from `StartPrimaryFunctions()` is discarded and hard-coded to 0, and (b) the `ContinueWith` continuation is racing the process teardown and frequently never runs. Additionally, any exception thrown inside the task body is swallowed by the unobserved-task antipattern — the continuation does not pass `TaskContinuationOptions.OnlyOnFaulted` nor inspect `t.Exception`.

## Evidence
```csharp
private void Run(bool import)
{
    System.Threading.Tasks.Task.Factory.StartNew(() =>
    {
        this.functions.StartPrimaryFunctions();   // int result thrown away
        this.UpdateCollectionStatusText();
        this.OfferMonitorSetupIfNeeded();
        this.ShowCollectionWarningsIfAny();
        Environment.Exit(0);                       // kills process before ContinueWith
    }).ContinueWith(t =>
    {
        this.hideProgressBar();                    // racing teardown; t.Exception ignored
    });
}
```

## Impact
- The GUI always exits 0 regardless of whether report generation failed.
- If `StartPrimaryFunctions()` throws, the exception is captured on the faulted Task and never surfaced (no logging, no message box); the `Environment.Exit` line is skipped, the continuation runs `hideProgressBar`, and the user is left with a half-disabled UI and no error. This is a silent failure that hides real collection/report crashes from the user.
- `UpdateCollectionStatusText` / `ShowCollectionWarningsIfAny` marshal back to the UI thread via `Dispatcher.Invoke`; calling `Environment.Exit(0)` immediately afterward can tear the dispatcher down mid-invoke.

## Suggested Fix
Capture the result, observe faults, update UI, then exit deterministically:
```csharp
Task.Run(() => this.functions.StartPrimaryFunctions())
    .ContinueWith(t =>
    {
        this.hideProgressBar();
        if (t.IsFaulted)
        {
            CGlobals.Logger.Error("Run failed: " + t.Exception?.GetBaseException().Message, false);
            MessageBox.Show("Health check failed — see log.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        this.UpdateCollectionStatusText();
        this.ShowCollectionWarningsIfAny();
        this.OfferMonitorSetupIfNeeded();
        // exit (with t.Result) only after UI work completes, e.g. via Dispatcher.InvokeShutdown
    }, TaskScheduler.FromCurrentSynchronizationContext());
```

## Labels
bug, wpf, threading, exit-code, silent-failure, high
