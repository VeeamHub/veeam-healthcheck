---
title: "Unquoted -Server and path arguments in LogCollectionInfo/ServerDumpInfo break on spaces and allow argument injection"
severity: Medium
labels: [bug, security]
domain: collection-data
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:656
confidence: High
---

## Summary

`LogCollectionInfo` interpolates the script path, server name, and report path into the PowerShell argument string with no quoting at all, and `ServerDumpInfo` leaves `-Server {server}` unquoted. Unlike the main collection paths (which use `CredentialHelper.EscapeForPowerShellDoubleQuotes` plus quotes), these hotfix-detection paths take `CGlobals.REMOTEHOST` (user-supplied CLI value) and a filesystem path verbatim.

## Impact

- Any space in the script path (e.g., install under `C:\Program Files\...`), server name, or report path splits the argument and the script either fails to launch or receives truncated parameters. `AppDomain.CurrentDomain.BaseDirectory` containing a space is a realistic deployment.
- An attacker-influenced `/host` value can inject additional PowerShell parameters/expressions into the command line (argument injection), since nothing constrains or escapes `server` here.

These paths run during hotfix detection (`CHotfixDetector` → `RunServerDump` / `RunVbrLogCollect`), and failures there are not surfaced (no exit-code check, `UseShellExecute = true`, no stream capture).

## Evidence

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626` —

```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";
```

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:656` —

```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";
```

Compare with the correct pattern used at line 512-515 (`VbrConfigStartInfo`): quoted values + `EscapeForPowerShellDoubleQuotes`.

## Suggested fix

Quote every interpolated value and escape `server` with `CredentialHelper.EscapeForPowerShellDoubleQuotes`, matching `VbrConfigStartInfo`. Also check `ExitCode` in `RunServerDump`/`RunVbrLogCollect` and dispose the `Process` objects.
