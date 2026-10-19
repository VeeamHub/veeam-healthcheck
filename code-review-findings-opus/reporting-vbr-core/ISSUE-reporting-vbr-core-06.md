# SetReportNameAndPath swallows exceptions and returns null, causing a misleading downstream crash

**Category:** reporting-vbr-core
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:227-269,194-200`

## Summary
`SetReportNameAndPath` wraps its body in a `try { ... } catch (Exception) { return null; }` with the
entire diagnostic logging block commented out. When it returns `null`, `latestReport` becomes null and
the immediately-following `WriteHtmlToFile()` does `new StreamWriter(null)`, which throws
`ArgumentNullException`. That exception is then caught by the broad handler in `ExportVbrHtml`, which
logs a generic "Failed at HTML Export" and returns failure — so the true cause (path construction
failure) is lost and the operator sees a misleading message.

## Evidence
```csharp
// CHtmlExporter.cs:251-268
catch (Exception)
{
    // log.Debug("Failed to set report name & path");   // all diagnostics commented out
    // ...
    return null;
}

// CHtmlExporter.cs:194-200 — null path flows straight into StreamWriter
private void WriteHtmlToFile(string htmlString)
{
    using (StreamWriter sw = new StreamWriter(this.latestReport))  // NRE/ArgumentNull if null
    {
        sw.Write(htmlString);
    }
}
```
`SetReportNameAndPath` also returns `htmlCore` (an empty string) when `scrub` is neither true nor false
in the `else if (!scrub)` chain — unreachable for bool today but the empty-string default would write to
a bogus path rather than failing loudly.

## Impact
A recoverable/diagnosable failure (e.g. bad `InstallID`, path issue) is converted into a confusing
secondary crash with no actionable log, because the catch block's logging is disabled. Harder field
troubleshooting; the report silently fails to write.

## Suggested Fix
Restore (uncomment) the diagnostic logging in the catch block, and have callers check for a null/empty
path before calling `WriteHtmlToFile` — log a clear "could not determine report output path" error and
abort the export cleanly instead of letting `StreamWriter` throw.

## Labels
bug, error-handling, exception-swallowing, logging
