---
title: "ExecBrowser crashes with NullReferenceException in CLI mode (/show:report)"
severity: Medium
labels: [bug, reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:318
confidence: High
---

## Summary

`CHtmlExporter.ExecBrowser` dereferences `Application.Current.Dispatcher`. A WPF `Application` is only created on the GUI path (`CArgsParser.LaunchUi`, line 75: `var app = new System.Windows.Application();`). When the tool runs in CLI mode (`vHC /run /show:report`), `Application.Current` is `null`, so opening the report throws `NullReferenceException`. The exception is swallowed by `ExportVbrHtml`'s catch, which then returns 1, so `RunFullVbrReport` logs "Init full report...failed!" even though the HTML/JSON/PDF files were all written successfully.

The method also instantiates a WPF `WebBrowser w1 = new();` that is never used — a leftover that drags in an ActiveX-backed control for nothing and is itself another reason this code requires an STA/WPF context.

## Impact

- CLI `/show:report` never opens the report and falsely reports the whole HTML export as failed.
- Misleading log/return codes make field troubleshooting harder (report exists, tool says export failed).

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:318-331` —

```csharp
private void ExecBrowser()
{
    Application.Current.Dispatcher.Invoke(delegate
    {
        WebBrowser w1 = new();          // dead object, never used

        var p = new Process();
        p.StartInfo = new ProcessStartInfo(this.latestReport)
        {
            UseShellExecute = true
        };
        p.Start();
    });
}
```

`vHC/HC_Reporting/Startup/CArgsParser.cs:124-126` sets `CGlobals.OpenHtml = true` for `/show:report` on the CLI path where no `Application` is ever constructed.

## Suggested fix

Remove the `WebBrowser` instantiation and the dispatcher dependency entirely — `Process.Start` with `UseShellExecute = true` needs neither. If GUI-thread marshaling is ever needed, guard with `Application.Current?.Dispatcher` and fall back to direct invocation.
