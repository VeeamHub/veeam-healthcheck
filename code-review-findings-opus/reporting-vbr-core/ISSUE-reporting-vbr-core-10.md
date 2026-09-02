# Orphaned WebBrowser control and per-call Process in ExecBrowser; security report path duplicated/unused

**Category:** reporting-vbr-core
**Severity:** Low
**Type:** Resource Leak
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:318-331`

## Summary
`ExecBrowser` instantiates a WPF `WebBrowser` control (`WebBrowser w1 = new();`) that is never added to
any visual tree, never shown, and never disposed — it exists only to be discarded. `WebBrowser` wraps an
unmanaged IE/WebView COM control, so creating and abandoning it leaks an unmanaged handle on every
"open HTML" action. The actual open is done separately via `Process.Start`, so `w1` serves no purpose.

## Evidence
```csharp
// CHtmlExporter.cs:318-331
private void ExecBrowser()
{
    Application.Current.Dispatcher.Invoke(delegate
    {
        WebBrowser w1 = new();   // created, never used, never disposed (unmanaged COM control)

        var p = new Process();
        p.StartInfo = new ProcessStartInfo(this.latestReport) { UseShellExecute = true };
        p.Start();               // Process handle also not disposed
    });
}
```

## Impact
Leaks an unmanaged WebBrowser/COM handle (and a `Process` handle) each time a report is opened. Minor in
practice for a one-shot desktop run, but it is a pure waste and a latent stability issue on repeated
exports.

## Suggested Fix
Delete the unused `WebBrowser w1` line entirely. Wrap the `Process` in a `using` (or call
`p.Dispose()`); `UseShellExecute=true` with a file path is sufficient to open the default browser
without a `WebBrowser` control.

## Labels
resource-leak, dead-code, com-interop, dispose
