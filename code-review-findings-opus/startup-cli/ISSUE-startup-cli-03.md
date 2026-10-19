# Hotfix detector runs twice (local + remote) when REMOTEEXEC is set — missing else/return

**Category:** startup-cli
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:334-342`

## Summary
The `/hotfix` dispatch block runs the remote hotfix detector and then **falls through** to also run the local detector unconditionally, because the `if (CGlobals.REMOTEEXEC)` branch has no `else` and no `return`/`continue` after the remote call.

## Evidence
```csharp
if (runHfd)
{
    if(CGlobals.REMOTEEXEC)
    {
        this.functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
    }

    this.functions.RunHotfixDetector(_hfdPath, string.Empty);   // <-- always runs too
}
```

## Impact
When a user runs `/hotfix /host=remote-vbr`, the tool performs the remote log collection AND then immediately performs a second, local hotfix detection against the machine VHC is running on. This doubles execution time, collects support logs from the wrong (local) server, and produces a confusing/incorrect second result set. At minimum it wastes the remote run; at worst the local run overwrites or pollutes the output of the remote run.

## Suggested Fix
Make the branches exclusive:
```csharp
if (runHfd)
{
    if (CGlobals.REMOTEEXEC)
        this.functions.RunHotfixDetector(_hfdPath, CGlobals.REMOTEHOST);
    else
        this.functions.RunHotfixDetector(_hfdPath, string.Empty);
}
```

## Labels
bug, hotfix, remote-execution, control-flow, high
