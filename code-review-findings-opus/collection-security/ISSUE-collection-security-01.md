# PowerShell argument injection via unescaped -Server / -file interpolation in log-collection paths

**Category:** collection-security
**Severity:** High
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:656`

## Summary
Two PowerShell invocation paths build their argument string by directly interpolating an attacker/operator-controlled hostname (`CGlobals.REMOTEHOST`) and a script path into the command line **without quoting and without the `CredentialHelper` escaping used everywhere else**, and launch the process with `UseShellExecute = true`. This bypasses the recently-added PowerShell argument-injection hardening and allows arbitrary additional arguments — and, because the value lands on a `cmd`/shell-resolved command line, potential command injection — through the `-Server` value.

## Evidence
`LogCollectionInfo` (line 623-643):
```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file {scriptLocation} -Server {server} -ReportPath {path}";
...
return new ProcessStartInfo()
{
    FileName = "powershell.exe",
    Arguments = argString,
    UseShellExecute = true,            // line 639
    ...
};
```

`ServerDumpInfo` (line 645-674):
```csharp
argString = $"-NoProfile -ExecutionPolicy unrestricted -file \"{scriptLocation}\" -Server {server}";   // line 656
...
UseShellExecute = true,               // line 669
```

`server` is `CGlobals.REMOTEHOST` (line 649-654), which is set from CLI input. Contrast with `VbrConfigStartInfo` (line 511) and `ConfigStartInfo` (line 690) which DO escape via `EscapeForPowerShellDoubleQuotes` and wrap the value in quotes. These two methods do neither.

A `REMOTEHOST` of `localhost -Command Start-Process calc` (or worse, using `;`/`&` resolved by the shell because `UseShellExecute = true`) is injected verbatim onto the PowerShell command line.

## Impact
An operator-supplied or config-sourced hostname containing spaces/semicolons/extra switches can inject additional PowerShell parameters or commands into a process launched with `-ExecutionPolicy unrestricted`. This is the exact class of bug the codebase hardened against for the credential paths, but these two log/dump paths were missed.

## Suggested Fix
- Wrap `{server}`, `{scriptLocation}`, and `{path}` in double quotes and run them through `CredentialHelper.EscapeForPowerShellDoubleQuotes(...)`, matching `VbrConfigStartInfo`/`ConfigStartInfo`.
- Set `UseShellExecute = false` (these processes are launched only to run a script; shell execution is not required and it adds a second injection layer).
- Optionally validate `REMOTEHOST` against a hostname/FQDN/IP allowlist regex at parse time.

## Labels
security, injection, powershell, command-injection
