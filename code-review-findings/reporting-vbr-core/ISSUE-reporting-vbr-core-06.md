---
title: "SetReportNameAndPath swallows exceptions and returns null, causing opaque downstream failures"
severity: Medium
labels: [reliability, bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:251
  - vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:181
confidence: High
---

## Summary

`SetReportNameAndPath` catches all exceptions, with every diagnostic log line commented out, and returns `null`. The null then flows into `WriteHtmlToFile` where `new StreamWriter(null)` throws `ArgumentNullException` — far from the root cause, with zero logging of why path construction failed. On the full-report path this is at least caught by `ExportVbrHtml`'s catch; on the security path, `ExportVbrSecurityHtml` (:181-192) has **no** try/catch at all, so the `ArgumentNullException` propagates out of the exporter and aborts the security report with an unrelated-looking stack.

`latestReport` derivatives (`.Replace(".html", ".pdf")`, `.json`, `.pptx`) would also NRE on a null `latestReport`.

## Impact

Any failure in report path construction (bad `desiredPath`, invalid chars in `backupServerName`/`INSTALLID`) becomes a silent null that detonates later as a confusing `ArgumentNullException`, with all the debugging context deliberately commented out.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:251-268` —

```csharp
catch (Exception)
{
    // log.Debug("Failed to set report name & path");
    // log.Debug(ex.Message);
    ... // 12 more commented-out diagnostic lines
    return null;
}
```

`vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:194-199` —

```csharp
private void WriteHtmlToFile(string htmlString)
{
    using (StreamWriter sw = new StreamWriter(this.latestReport))  // throws if null
```

`ExportVbrSecurityHtml` (:181) calls these with no exception handling, unlike its two sibling export methods.

## Suggested fix

Don't catch in `SetReportNameAndPath` (nothing in it should realistically throw except truly exceptional cases) or, if catching, log the exception and rethrow a descriptive `InvalidOperationException`. Never return null for a path the rest of the class depends on. Add the same try/catch+return-code structure to `ExportVbrSecurityHtml` as the other exporters, and delete the commented-out log block.
