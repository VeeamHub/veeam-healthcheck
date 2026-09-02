---
title: "Escape PowerShell -Server/-file args in LogCollectionInfo and ServerDumpInfo (hardening commit missed them)"
severity: High
labels: [security, bug]
domain: collection-security
files:
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626
  - vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:656
confidence: High
---

## Summary
The PowerShell-hardening commit `f9f315c` ("escape username/host in all PowerShell
command builders") claims to fix "all command-builder sites, not just the four the
review named." Two builders were missed: `LogCollectionInfo` and `ServerDumpInfo`.
Both interpolate `CGlobals.REMOTEHOST` (and the script path) into the argument
string **unquoted and unescaped**, and both launch with `UseShellExecute = true` —
the most dangerous combination in the file. Every other builder routes the host
through `CredentialHelper.EscapeForPowerShellDoubleQuotes` and wraps it in quotes;
these two bypass the helper entirely.

## Impact
A `REMOTEHOST` value containing a space or shell metacharacter
(`;`, `&`, `|`, `$(...)`, quotes) breaks out of the `-Server` argument. Because
`UseShellExecute = true` hands the command line to the shell, an attacker- or
list-supplied host such as `localhost -Command calc.exe` or
`a;Start-Process calc` runs arbitrary PowerShell with the VHC process's
privileges. `REMOTEHOST` is operator-influenced via `/server:` (CArgsParser:259)
and the GUI server list (VhcGui:74), and can originate from a managed-server dump,
so it is not fully trusted. This is the exact A6/argument-injection class the
hardening commit was meant to close.

## Evidence
`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626` — `LogCollectionInfo`:
```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";
...
return new ProcessStartInfo() {
    FileName = "powershell.exe",
    Arguments = argString,
    UseShellExecute = true,         // shell parses the line
    ...
};
```
`-Server {server}` is unquoted and never passed through `EscapeForPowerShellDoubleQuotes`.
`server` is `CGlobals.REMOTEHOST` (see caller `RunVbrLogCollect` → `CHotfixDetector.cs:159`).

`vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:656` — `ServerDumpInfo`:
```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";
...
UseShellExecute = true,
```
Here `server` comes directly from `CGlobals.REMOTEHOST` (lines 649-654), again
unquoted/unescaped. Contrast with `VbrConfigStartInfo` (line 511) and
`ConfigStartInfo` (line 690) which both do
`EscapeForPowerShellDoubleQuotes(CGlobals.REMOTEHOST)` and wrap in `"..."`.

## Suggested fix
Apply the same treatment as the other builders: quote and escape the host, and
quote the script/report paths:
```csharp
string escapedServer = CredentialHelper.EscapeForPowerShellDoubleQuotes(server);
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server \"{escapedServer}\" -ReportPath \"{path}\"";
```
Additionally prefer `UseShellExecute = false` for these process launches so the
shell is never given the raw command line, matching the hardened builders.
