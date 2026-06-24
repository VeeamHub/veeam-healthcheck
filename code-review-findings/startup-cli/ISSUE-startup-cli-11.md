---
title: "CHotfixDetector constructor silently ignores an invalid path, then Run() proceeds with null path"
severity: Medium
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CHotfixDetector.cs:24
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:247
confidence: High
---

## Summary
`CClientFunctions.RunHotfixDetector` validates the `/path=` value, but when the path argument is missing it falls back to `Console.ReadLine()` and passes the raw user input straight to `new CHotfixDetector(path)` without validation. The constructor calls `VerifyPath` and, on failure, simply skips initializing `originalPath`/`path` — no error, no throw. `Run()` is then called unconditionally and drives the full collection pipeline with `this.path == null` (e.g., `ps.RunVbrLogCollect(null, server)`, `this.path + "\\extracted"` → `"\extracted"` resolving to the current drive root).

## Impact
A user who mistypes the interactive path gets no error message; the hotfix run proceeds, dumps/extracts into unintended locations (drive-root-relative `\extracted`) or fails downstream with confusing PowerShell errors instead of "invalid path". In `/silent` mode the `Console.ReadLine()` prompt also hangs an unattended run forever.

## Evidence
`vHC/HC_Reporting/Startup/CHotfixDetector.cs:24-33`:

```csharp
public CHotfixDetector(string path)
{
    this.fixList = new List<string>();
    CClientFunctions funk = new();
    if (funk.VerifyPath(path))
    {
        this.originalPath = path;
        this.SetPath();
    }
    // else: fields silently left null
}
```

`vHC/HC_Reporting/Startup/CClientFunctions.cs:245-251` — ReadLine input is not re-validated before construction:

```csharp
this.LOG.Warning(this.logStart + "Please enter local path with adequate space for log files:", false);
path = Console.ReadLine();
...
CHotfixDetector hfd = new(path);
hfd.Run();
```

## Suggested fix
Fail fast: have the constructor throw `ArgumentException` (or expose an `IsValid` flag that `RunHotfixDetector` checks before calling `Run()`), validate the `ReadLine()` input in a loop with a retry/abort, and refuse the interactive prompt entirely when `CGlobals.Silent` is set (exit non-zero instead).
