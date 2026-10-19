---
title: "VhcGui.Run swallows background-task exceptions (silent failure) and force-exits with code 0"
severity: Medium
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/VhcGui.xaml.cs:248
confidence: High
---

## Summary
The GUI Run button starts the entire collection/report pipeline on a `Task.Factory.StartNew` thread. If `StartPrimaryFunctions()` throws, the task faults; the `ContinueWith` only hides the progress bar and never observes `t.Exception` or shows an error — the GUI just sits there looking idle with no report and no message. On success, `Environment.Exit(0)` is called from inside the task regardless of the int result `StartPrimaryFunctions` returned (the return value is discarded), so a failed report generation still exits 0.

## Evidence
`vHC/HC_Reporting/VhcGui.xaml.cs:248-261`:

```csharp
private void Run(bool import)
{
    System.Threading.Tasks.Task.Factory.StartNew(() =>
    {
        this.functions.StartPrimaryFunctions();   // int result discarded
        this.UpdateCollectionStatusText();
        this.OfferMonitorSetupIfNeeded();
        this.ShowCollectionWarningsIfAny();
        Environment.Exit(0);                      // exits 0 even on failure result
    }).ContinueWith(t =>
    {
        this.hideProgressBar();                   // runs only on fault; exception unobserved
    });
}
```

Note the `ContinueWith` body can only ever execute when the task faulted (the success path calls `Environment.Exit` first), which makes the missing error handling there especially pointed. The `import` parameter is also unused.

## Impact
Any exception during GUI collection (credential failure, PowerShell failure, IO error) is silently dropped: progress bar disappears, buttons stay disabled (`DisableButtons()` is never reversed), and the user must kill the app. Successful-looking exits always report code 0.

## Suggested fix
```csharp
Task.Run(() => this.functions.StartPrimaryFunctions())
    .ContinueWith(t =>
    {
        this.hideProgressBar();
        if (t.IsFaulted)
        {
            CGlobals.Logger.Error("Run failed: " + t.Exception?.GetBaseException().Message);
            this.Dispatcher.Invoke(() => MessageBox.Show(...));
            return;
        }
        ... status text / warnings ...
        Environment.Exit(t.Result);
    });
```
