---
title: "TestMfaVB365 return semantics are inverted relative to ExecutePsScriptWithFailover"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:368
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:204
confidence: High
---

## Summary

`TestMfaVB365`'s contract (per its own code comment and its caller) is "true = MFA failure, stops collection". But its return value is `ExecutePsScriptWithFailover(...)`, which returns `true` on SUCCESS (exit code 0). So a successful `Connect-VBOServer` (exit 0) makes `TestMfaVB365` return `true` → the caller treats that as an MFA failure, skips VB365 collection, and `WeighSuccessContinuation()` calls `Environment.Exit(1)`. Conversely, a failed connection returns `false` → collection proceeds against a server it could not connect to.

## Impact

VB365 collection behavior is exactly backwards: connectable servers abort the program; unreachable/MFA-blocked servers proceed to collection (which then fails later or produces an empty report). Either way the user gets a wrong outcome on the VB365 path.

A secondary defect compounds this: the argument string passed to `ExecutePsScriptWithFailover` is a raw command (`Import-Module ...; Connect-VBOServer ...`) with no `-Command` switch. The failover executor tries `pwsh.exe` first, and pwsh's default first-argument semantics is `-File`, so PS7 will fail with "not recognized as the name of a script file" regardless of server state — masking the inversion on PS7 hosts and making behavior dependent on which PowerShell happens to run.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:351-369` —

```csharp
if (creds == null)
{
    CGlobals.Logger.Error("Credentials required for remote VB365 execution.");
    return true; // true = MFA failure, stops collection
}
...
string argString = "Import-Module Veeam.Archiver.PowerShell -WarningAction Ignore; " + ...
return this.ExecutePsScriptWithFailover(argString, ...);   // true == script SUCCEEDED
```

`vHC/HC_Reporting/Functions/Collection/CCollections.cs:204-211` —

```csharp
if (!this.TestPsMfaVb365(p))
{
    this.ExecVb365Scripts(p);     // runs only when the connection test FAILED
}
else
{
    this.WeighSuccessContinuation();   // exits the program when the test SUCCEEDED
}
```

## Suggested fix

Invert the return (`return !this.ExecutePsScriptWithFailover(...)`) or, better, rename to `CanConnectVb365()` returning success=true and fix the caller to `if (TestPsMfaVb365(p)) ExecVb365Scripts(p);`. Also prefix the argument string with `-NoProfile -Command "..."` so pwsh and powershell parse it identically.
