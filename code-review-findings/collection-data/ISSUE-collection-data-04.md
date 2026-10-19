---
title: "TestMfa error-parsing loop is dead code; method always returns true"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:297
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:308
confidence: High
---

## Summary

In `TestMfa`, stderr is fully drained with `ReadToEnd()` at line 298, then the error-detection loop at line 308 calls `res.StandardError.ReadLine()` — which immediately returns `null` because the stream is already at EOF. The loop body (the only place `ParseErrors` runs and errors are logged) never executes. `mfaFound` is initialized to `true` and is returned unchanged, so the method reports "MFA check passed" regardless of what PowerShell actually said.

## Impact

The PowerShell 5 MFA fallback check (`CCollections.RunLocalMfaCheck` → `TestMfa`) is a no-op: MFA-blocked or auth-failed connections are reported as success, collection proceeds, and the user gets an empty/partial report with no MFA diagnostic — exactly the failure class this method exists to catch. The captured `stdErr` string is read into a variable and then never used for decisions.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:297-324` —

```csharp
string stdOut = res.StandardOutput.ReadToEnd();
string stdErr = CCollections.StripAnsiCodes(res.StandardError.ReadToEnd());   // stream drained here
...
bool mfaFound = true;
string errString = string.Empty;
while ((errString = res.StandardError.ReadLine()) != null)   // always null: stream at EOF
{
    var errResults = this.ParseErrors(errString);
    ...
}

this.PushPsErrorsToMainLog(errorarray);   // errorarray always empty

return mfaFound;   // always true
```

Note also `mfaFound` is never set to `false` anywhere — even the inner branch sets `mfaFound = true` before returning.

## Suggested fix

Parse the already-captured `stdErr` string (split on newlines) instead of re-reading the stream, and derive the return value from `ParseErrors` results plus `res.ExitCode`. Clarify the boolean contract (true = passed) and make the failure path actually return false.
