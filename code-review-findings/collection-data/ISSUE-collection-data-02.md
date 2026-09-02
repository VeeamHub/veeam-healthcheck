---
title: "Eliminate WaitForExit-before-ReadToEnd and sequential stream reads that can deadlock child PowerShell processes"
severity: High
labels: [bug, reliability]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:160
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:294
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:333
  - vHC/HC_Reporting/Functions/Collection/CCollections.cs:508
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:81
confidence: High
---

## Summary

Several process-execution sites redirect both stdout and stderr but either (a) call `WaitForExit()` before reading the streams, or (b) read the two streams sequentially with `ReadToEnd()`. Both are the classic .NET redirected-stream deadlock documented on `Process.StandardOutput`: once the child fills the ~4KB pipe buffer of an unread stream, the child blocks on write and never exits, while the parent blocks waiting — a permanent hang.

Notably, `ExecutePsScript` (PSInvoker.cs:410-419) was already fixed with concurrent `Task.Run` readers and a comment explaining exactly this deadlock — the other call sites were not updated.

## Impact

Hung vHC process (no error, no report) whenever a PowerShell child produces more than the pipe buffer of output before exiting. MFA tests and the failover executor routinely emit module-import warnings and verbose errors, so this is reachable in the field.

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:160-163` (ExecutePsScriptWithFailover) — wait happens before any read; guaranteed deadlock if child output exceeds the buffer:

```csharp
process.WaitForExit();

string stdOut = process.StandardOutput.ReadToEnd();
string stdErr = CCollections.StripAnsiCodes(process.StandardError.ReadToEnd());
```

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:294-298` (TestMfa) — same pattern:

```csharp
res.WaitForExit();
...
string stdOut = res.StandardOutput.ReadToEnd();
string stdErr = CCollections.StripAnsiCodes(res.StandardError.ReadToEnd());
```

`vHC/HC_Reporting/Functions/Collection/CCollections.cs:333-335` (MfaTestPassed) and `:508-510` (RunLocalMfaCheckNoCredentials), and `PowerShell7Executor.cs:81-83` (RunScript) — milder variant: stdout is drained to end while stderr is unread; a child blocked writing a large stderr never closes stdout:

```csharp
string stdOut = process.StandardOutput.ReadToEnd();
string stdErr = StripAnsiCodes(process.StandardError.ReadToEnd());
process.WaitForExit();
```

## Suggested fix

Apply the same pattern already used in `ExecutePsScript`: start `Task.Run(() => proc.StandardOutput.ReadToEnd())` and the stderr equivalent immediately after `Start()`, then `WaitForExit()`, then await both tasks. Better: centralize process execution in one helper and route all call sites through it.
