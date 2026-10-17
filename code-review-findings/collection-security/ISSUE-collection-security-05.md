---
title: "EscapeForPowerShellDoubleQuotes mixes Win32-argv and PowerShell escaping models"
severity: Medium
labels: [security, maintainability]
domain: collection-security
files:
  - vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs:41
confidence: Low
---

## Summary
`EscapeForPowerShellDoubleQuotes` escapes four characters with two
*incompatible* escaping conventions in a single pass: `"` → `\"` and `\` → `\\`
(the Win32 `CommandLineToArgvW` / C-runtime argv convention), while `$` → `` `$ ``
and `` ` `` → ``` `` ``` (the PowerShell double-quoted-string convention). The
escaped value is then embedded inside a PowerShell double-quoted literal
(`-Server "{escapedServer}"`) that is itself part of a process command line. The
two layers (process argv parsing, then PowerShell string parsing) do not both
honor backslash escaping, so a value like `a\"; <command>` can re-expose a quote
or terminate the literal depending on which parser wins.

## Impact
The password field is base64-encoded before interpolation, so the live risk is
limited to the username and host fields that flow through this helper. A
maliciously crafted username/host containing backslash-before-quote sequences may
still break out of the intended argument despite "escaping," undermining the very
injection defense added in commit `f9f315c`. Confidence is Low because a concrete
breakout depends on the exact pwsh.exe command-line parse, but the mixed model is
fragile and hard to reason about, which is itself a security-relevant defect for
an escaping primitive.

## Evidence
`vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs:41`:
```csharp
case '"':  sb.Append("\\\""); break;   // Win32 argv escaping
case '\\': sb.Append("\\\\"); break;   // Win32 argv escaping
case '$':  sb.Append("`$");   break;   // PowerShell escaping
case '`':  sb.Append("``");   break;   // PowerShell escaping
```
Two escaping grammars are applied to one string that crosses two parsers.

## Suggested fix
Pick one boundary and escape for exactly that grammar. Preferred: stop building
PowerShell command lines by string interpolation for the host/user fields — pass
them as bound parameters (e.g. via the `System.Management.Automation.PowerShell`
API with `.AddParameter(...)`, already used in `ExecuteEmbeddedScript`), which
eliminates string-level escaping entirely. If string building must stay, escape
for the PowerShell single-quoted context (`'` → `''`, the simplest safe grammar,
already implemented as `EscapeForPowerShellSingleQuotes`) and quote with single
quotes consistently, rather than mixing argv and PS conventions.
