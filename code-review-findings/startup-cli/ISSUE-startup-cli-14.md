---
title: "/import:<bad-path> logs an error but continues the run against the default path"
severity: Medium
labels: [bug, reliability]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:158
  - vHC/HC_Reporting/Startup/CArgsParser.cs:420
confidence: High
---

## Summary
When `/import:<path>` is given and the path does not exist, `ParseImportPath` logs an error and returns `null` — but the switch case has already set `IMPORT = true` and `run = true`, and `ParseAllArgs` does not treat the null result as fatal. The run proceeds in import mode with `IMPORT_PATH = null`, which `ResolveImportPath` (CClientFunctions.cs:367-369) silently substitutes with the default path (`CGlobals.desiredPath ?? CVariables.outDir`).

## Impact
A user importing a colleague's exported CSVs with a typo'd path gets a report generated from whatever stale data happens to live under `C:\temp\vHC` — wrong data presented as if it were the requested import — or a confusing "No valid CSV directory found in: C:\temp\vHC" error referencing a path they never specified. Either way the explicit user intent (use *this* path) is dropped after a log line most users won't see.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:158-167`:

```csharp
case var importMatch when new Regex("^/import[:=](.+)$", RegexOptions.IgnoreCase).IsMatch(a):
    run = true;
    CGlobals.IMPORT = true;
    CGlobals.RunFullReport = true;
    CGlobals.IMPORT_PATH = this.ParseImportPath(a);   // null on missing dir
    if (!string.IsNullOrEmpty(CGlobals.IMPORT_PATH))
    {
        CGlobals.Logger.Info("Import path set to: " + CGlobals.IMPORT_PATH);
    }
    break;   // no failure handling when null
```

`ParseImportPath` (lines 419-425) validates `Directory.Exists(path)` and returns `null` on failure; nothing upstream converts that into an exit.

## Suggested fix
Treat an explicit-but-invalid import path as fatal:

```csharp
CGlobals.IMPORT_PATH = this.ParseImportPath(a);
if (string.IsNullOrEmpty(CGlobals.IMPORT_PATH))
{
    Environment.Exit(SilentExit.GenericFailure); // error already logged by ParseImportPath
}
```
