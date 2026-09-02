---
title: "Hotfix detector runs twice in remote mode (missing else), and the remoteServer parameter is ignored anyway"
severity: Medium
labels: [bug]
domain: startup-cli
files:
  - vHC/HC_Reporting/Startup/CArgsParser.cs:334
  - vHC/HC_Reporting/Startup/CClientFunctions.cs:222
confidence: High
---

## Summary
When `/hotfix` is combined with remote execution, `ParseAllArgs` calls `RunHotfixDetector` for the remote host and then unconditionally calls it a second time for local execution — there is no `else`/`return` between the two calls. Separately, `CClientFunctions.RunHotfixDetector(string path, string remoteServer)` never uses its `remoteServer` parameter, so the "remote" invocation is identical to the local one.

## Impact
`/hotfix /remote /host=X` runs the full log collection + parse pipeline twice against the same local target (doubling runtime and log collection into the same directory), and never actually targets the remote host the user asked for.

## Evidence
`vHC/HC_Reporting/Startup/CArgsParser.cs:334-342`:

```csharp
if (runHfd)
{
    if(CGlobals.REMOTEEXEC)
    {
        this.functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
    }

    this.functions.RunHotfixDetector(_hfdPath, string.Empty);
}
```

`vHC/HC_Reporting/Startup/CClientFunctions.cs:222-252` — signature is `RunHotfixDetector(string path, string remoteServer)` but the body only references `path`; `remoteServer` is dead.

## Suggested fix
Either add `else` (or `return` after the remote call) and actually thread `remoteServer` into `CHotfixDetector`, or drop the unused parameter and the remote branch entirely:

```csharp
if (runHfd)
{
    this.functions.RunHotfixDetector(_hfdPath);
}
```
