# Unguarded array indexing on split OS/RAM strings crashes the Backup Server table

**Category:** reporting-vb365
**Severity:** High
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:826-844`

## Summary
`Vb365Controllers()` parses the OS version and RAM by blindly indexing the result of `String.Split()` **outside** any try/catch. If `osversion` does not contain at least 4 space-delimited tokens (or the 4th token lacks a `.`), or `ram` is empty, the code throws `IndexOutOfRangeException`. Because this is the outer `try` body's tail (the `try` at line 742 wraps the whole loop), the exception is caught by the empty `catch` at line 887 — which **drops the entire Backup Server section** plus any rows already accumulated for the current server.

## Evidence
`CM365Tables.cs:826-844`:
```csharp
string[] osVersionString = osVersion.Split();
string[] osVersionNumbers = osVersionString[3].Split(".");   // [3] — no length check
int.TryParse(osVersionNumbers[0], out int osversion);
int.TryParse(osVersionNumbers[1], out int osSubVersion);     // [1] — no length check
...
string[] ramInt = ram.Split();
int.TryParse(ramInt[0], out int ramNumber);                  // [0] on possibly-empty split
```
Note the Proxies table (`CM365Tables.cs:270-289`) wraps the identical OS-parse in its own inner `try/catch`, so this Controllers copy is an inconsistent, less-defensive variant.

## Impact
A single backup server whose collected OS string is shorter than expected (locale differences, trimmed `Get-ComputerInfo` output, missing data on import) wipes out the whole "Backup Server" table — one of the most important VB365 sections. The empty catch hides the failure, so it looks like the server simply has no data.

## Suggested Fix
Guard array access (length checks) and/or wrap the OS/RAM parsing in a local try/catch mirroring the Proxies table. Prefer explicit bounds checks:
```csharp
var osTokens = (osVersion ?? "").Split();
if (osTokens.Length >= 4)
{
    var nums = osTokens[3].Split('.');
    if (nums.Length >= 1) int.TryParse(nums[0], out osversion);
    if (nums.Length >= 2) int.TryParse(nums[1], out osSubVersion);
}
var ramTokens = (ram ?? "").Split();
if (ramTokens.Length >= 1) int.TryParse(ramTokens[0], out ramNumber);
```

## Labels
bug, null-deref, index-out-of-range, exception-swallowing, vb365, controllers
