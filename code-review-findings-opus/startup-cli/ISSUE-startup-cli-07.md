# /outdir, /path and /host path arguments are not validated against traversal or quoting

**Category:** startup-cli
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:386-398`, `:237-261`, `vHC/HC_Reporting/Startup/CClientFunctions.cs:254-293`

## Summary
`ParsePath()` splits the argument on `=` and returns the raw remainder with no validation, normalization, or canonicalization. The value flows directly into `CGlobals.desiredPath` (via `ApplyOutDir`) which becomes the base for every output path, log file, scrub key file, and CSV directory. There is no check for path traversal, invalid characters, environment-variable injection, UNC roots, or length. `VerifyPath()` rejects UNC (`\\`) for the hotfix path but `ApplyOutDir`/`/outdir=` performs no such check and will happily create directories anywhere the process can write.

## Evidence
```csharp
private string ParsePath(string input)
{
    string[] outputDir = input.Split('=', 2);
    return outputDir[1];          // raw, unvalidated
}
```
```csharp
private void ApplyOutDir(string parsedOutDir)
{
    if (string.IsNullOrEmpty(parsedOutDir)) return;
    CGlobals.desiredPath = parsedOutDir;     // no Path.GetFullPath, no validation
    CGlobals.mainlog = new CLogger("HealthCheck");
}
```
`/host=` similarly takes the raw remainder and stores it as `REMOTEHOST`, which later becomes a directory segment in `CVariables.GetVbrDirWithTimestamp()` (`Path.Combine(basePath, serverName, timestamp)`) — a hostname containing `..` or path separators would redirect output.

## Impact
- A malformed or hostile `/outdir=` (e.g. containing `..\..\Windows\Temp` or an unexpected drive) silently relocates all report output, logs, and the scrub key file (which maps obfuscated→real names) to an attacker- or mistake-chosen location. The scrub key file leaking outside the intended tree is a confidentiality concern given scrubbed mode exists to protect those very mappings.
- `REMOTEHOST` embedded into `Path.Combine` without sanitization allows directory redirection via a crafted host string.
- `ParsePath` throws/returns null only on missing `=`; values like `/outdir=` (empty) are handled, but `/outdir="C:\a` with an unbalanced quote is passed through verbatim.

## Suggested Fix
Canonicalize and validate in `ParsePath`/`ApplyOutDir`:
```csharp
var full = Path.GetFullPath(parsedOutDir);      // resolves .. segments
if (full.IndexOfAny(Path.GetInvalidPathChars()) >= 0) { /* reject */ }
CGlobals.desiredPath = full;
```
Sanitize `REMOTEHOST` against `Path.GetInvalidFileNameChars()` before it is used as a directory segment, and apply the same UNC rejection used in `VerifyPath()` to `/outdir=` if local-only output is intended.

## Labels
security, path-traversal, input-validation, scrub, medium
