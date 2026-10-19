# VbrVersionSupportCheck indexes vhcVersionSections[3] without bounds check — IndexOutOfRange risk

**Category:** startup-cli
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Startup/CClientFunctions.cs:108-133`

## Summary
`VbrVersionSupportCheck()` splits `CGlobals.VHCVERSION` on `.` and unconditionally reads index `[0]` and `[3]`. If `VHCVERSION` is empty, null-ish, or has fewer than 4 dot-separated segments (e.g. a dev build versioned `1.2.3` or an unset/`""` value), `vhcVersionSections[3]` throws `IndexOutOfRangeException`. `VHCVERSION` is populated from `FileVersionInfo.FileVersion` (CVersionSetter), which is not guaranteed to be 4-part.

## Evidence
```csharp
string[] vhcVersionSections = CGlobals.VHCVERSION.Split('.');
int.TryParse(vhcVersionSections[0], out int vhcMajorVersion);
int.TryParse(vhcVersionSections[3], out int vhcBuildVersion);   // [3] unguarded
```
Note `CGlobals.VHCVERSION` defaults to `string.Empty`; `"".Split('.')` yields a 1-element array, so any call before version detection — or with a 1/2/3-part version — throws.

## Impact
On a VBR pre-v12 environment with a non-4-part VHC version string, the support-version guard crashes with an unhandled `IndexOutOfRangeException` instead of showing the intended "download 2.0.0.546" message. The crash bubbles to `Main`'s catch and exits 1, but the user gets a generic failure rather than the actionable downgrade instructions this method exists to provide. (Note: this method appears to be dead code — see separate finding — but if re-enabled the bug bites.)

## Suggested Fix
```csharp
var parts = (CGlobals.VHCVERSION ?? string.Empty).Split('.');
if (parts.Length < 4) { /* log and return — cannot evaluate build guard */ return; }
int.TryParse(parts[0], out int major);
int.TryParse(parts[3], out int build);
```

## Labels
bug, index-out-of-range, version-parsing, medium
