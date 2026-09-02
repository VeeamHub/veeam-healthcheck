# VB365 HTML report is never generated — compiler disposed immediately after construction

**Category:** startup-cli
**Severity:** Critical
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CReportModeSelector.cs:65-70`

## Summary
`StartM365Report()` constructs a `CVb365HtmlCompiler` and then immediately calls `.Dispose()` on it without ever invoking the method that actually produces the VB365 report. Compare with `StartVbrReport()` (line 54-63), which calls `html.RunFullVbrReport()` before disposing, and `StartSecurityReport()` (line 72-77), which calls `html.RunSecurityReport()`. For VB365 the run step is missing entirely, so a VB365-only health check (or the VB365 half of a combined run) silently produces no report.

## Evidence
```csharp
private void StartM365Report()
{
    this.LOG.Info("Starting VB365 Report genration", false);
    CVb365HtmlCompiler compiler = new();
    compiler.Dispose();          // <-- constructed then disposed; nothing run
}
```
vs the VBR path that does run:
```csharp
private int StartVbrReport()
{
    ...
    CHtmlCompiler html = new();
    var res = html.RunFullVbrReport();   // <-- actual work
    html.Dispose();
    return res;
}
```

## Impact
Any `/import` or `/run` that targets VB365 will appear to succeed (logs "Starting VB365 Report genration", exit code 0) but emit no VB365 HTML. This is a silent, total feature failure for the VB365 product line. If `CVb365HtmlCompiler` does its work in its constructor today, the code is fragile-by-accident and the explicit-run contract is broken; if it does not, the report is simply never written.

## Suggested Fix
Mirror the VBR path — invoke the compiler's run method and capture its result before disposing:
```csharp
private void StartM365Report()
{
    this.LOG.Info("Starting VB365 Report generation", false);
    using CVb365HtmlCompiler compiler = new();
    compiler.RunFullReport();   // use the actual public run method on the compiler
}
```
Verify against `Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs` for the correct entry-point name, and consider returning its result so `FileChecker()` can propagate a non-zero exit when VB365 generation fails.

## Labels
bug, vb365, report-generation, critical
