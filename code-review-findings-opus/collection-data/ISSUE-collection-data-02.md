# RunScript builds -Command from an unescaped, double-quoted script path

**Category:** collection-data
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:56-94`, `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:29-54`

## Summary
`Ps7Executor.RunScript` and `IsModuleInstalled` embed their input directly inside a double-quoted `-Command` string. `RunScript` wraps `scriptPath` in literal double quotes inside `-Command "..."`, and `IsModuleInstalled` interpolates `moduleName` inside a single-quoted token within `-Command`. Neither escapes its input. If either value ever carries a quote or PowerShell metacharacter, the command is malformed or injectable.

## Evidence
```csharp
// PowerShell7Executor.cs:68 — scriptPath placed unescaped inside -Command "..."
string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{scriptPath}\" ";
```
```csharp
// PowerShell7Executor.cs:39 — moduleName interpolated into a -Command expression
string args = $"-NoProfile -Command \"if (Get-Module -ListAvailable -Name '{moduleName}') {{ Write-Host '0' }} else {{ Write-Host '1' }}\"";
```
By contrast, file-based invocation in `PSInvoker` uses `-File` with escaped, quoted args. Using `-Command` with a raw path means the path is parsed as a PowerShell expression, not a literal file path.

## Impact
A script path or module name containing a single/double quote or expression syntax can break out of the quoting and execute arbitrary PowerShell. Even with currently-static callers, this is a latent injection seam and a correctness trap (any path with a space/quote silently fails). `RunScript` using `-Command "<path>"` also means a path with spaces is interpreted as multiple tokens rather than one file.

## Suggested Fix
Prefer `-File` over `-Command` for running a script file, and escape inputs:
```csharp
string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{CredentialHelper.EscapeForPowerShellDoubleQuotes(scriptPath)}\"";
```
For `IsModuleInstalled`, escape `moduleName` for the single-quote context (double any embedded `'`) before interpolation.

## Labels
security, powershell-injection, maintainability
