---
title: "Vb365Controllers(): unguarded OS-version string indexing can blank the whole Backup Server section"
severity: High
labels: [bug, reliability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:826
confidence: High
---

## Summary

`Vb365Controllers()` parses the OS version with `osVersion.Split()[3]` and then `osVersionNumbers[1]` with **no** bounds checking and no local try/catch. If `osversion` is empty (VMC log details missing — a known collection gap) `Split()` yields `[""]` and index `[3]` throws `IndexOutOfRangeException`; if the OS string's 4th token has no dot (e.g. "Microsoft Windows Server 2022"), `osVersionNumbers[1]` throws. The method-level empty catch then swallows it, leaving the entire "Backup Server" table headerless/empty (plus an unclosed `<tr>`).

The identical parse in `Vb365Proxies()` **is** wrapped in its own try/catch (`CM365Tables.cs:270-289`) — the protection was added on one copy of the duplicated code but not the other.

## Impact

The Backup Server section — version, RAM/CPU sizing checks, installed components — silently renders empty whenever the OS version string is absent or shaped differently. This is a primary section of the VB365 report.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:826-829` (no guard):

```csharp
string[] osVersionString = osVersion.Split();
string[] osVersionNumbers = osVersionString[3].Split(".");
int.TryParse(osVersionNumbers[0], out int osversion);
int.TryParse(osVersionNumbers[1], out int osSubVersion);
```

vs. the guarded twin in `Vb365Proxies()`, `CM365Tables.cs:270-289`:

```csharp
try
{
    string[] osVersionString = osversion.Split();
    string[] osVersionNumbers = osVersionString[3].Split(".");
    ...
}
catch (Exception)
{
}
```

## Suggested fix

Extract one shared, defensive `GetOsShade(string osVersion)` helper (length-check arrays, return 0 on failure) and use it in both `Vb365Controllers` and `Vb365Proxies`, removing the duplicated fragile parse.
