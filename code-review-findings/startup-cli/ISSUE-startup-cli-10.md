---
title: "ApplyOutDir swaps CGlobals.mainlog mid-run, but components hold the old CLogger in readonly fields"
severity: Medium
labels: [bug, maintainability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:643
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:20
  - vHC/HC_Reporting/Startup/CHotfixDetector.cs:18
confidence: High
---

## Summary
When `/outdir=` is parsed, `ApplyOutDir` replaces `CGlobals.mainlog` with a fresh `CLogger` so subsequent log lines land under the new output dir. But multiple classes capture `CGlobals.Logger` into `private readonly CLogger LOG` fields at construction time — notably `CArgsParser`'s own `functions` field (`CClientFunctions`, constructed *before* args are parsed), `CHotfixDetector`, `CCollections`, and `CReportModeSelector`. Anything logging through a captured reference keeps writing to the *old* log file under `C:\temp\vHC\Original\Log` even though the user redirected output.

## Impact
With `/run /outdir=D:\Reports`, the run's logs are split across two files in two directories: `GetVbrVersion`, hotfix-detector, and other `CClientFunctions`-routed messages go to `C:\temp\vHC\Original\Log\...` (which `/outdir` was supposed to avoid — e.g., when `C:` is locked down or space-constrained, the reason users pass `/outdir` in the first place), while parser-level messages go to the new location. Troubleshooting from "the" log file misses half the story.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:639-644`:

```csharp
private void ApplyOutDir(string parsedOutDir)
{
    if (string.IsNullOrEmpty(parsedOutDir)) return;
    CGlobals.desiredPath = parsedOutDir;
    CGlobals.mainlog = new CLogger("HealthCheck");
}
```

`vHC/HC_Reporting/Startup/CArgsParser.cs:32` — captured before parsing begins:

```csharp
private readonly CClientFunctions functions = new();
```

`vHC/HC_Reporting/Startup/CClientFunctions.cs:20`:

```csharp
private readonly CLogger LOG = CGlobals.Logger;   // snapshot of the OLD logger
```

Same pattern at `CHotfixDetector.cs:18` and `CReportModeSelector.cs:13`. Also note the original logger already created its log file/directory under the default path during static init, so the stray `C:\temp\vHC` directory tree is created even when the user redirects everything.

## Suggested fix
Don't snapshot the logger. Either make `LOG` a property (`private static CLogger LOG => CGlobals.Logger;`) in the consuming classes, or — better — make `CLogger` re-resolve its target path lazily so a single logger instance follows `desiredPath`, removing the need to swap instances at all.
