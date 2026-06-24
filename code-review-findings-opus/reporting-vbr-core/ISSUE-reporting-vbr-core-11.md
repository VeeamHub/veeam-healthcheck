# CObjectHelpers.ParseBool silently returns false for valid truthy values, hiding report sections

**Category:** reporting-vbr-core
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CObjectHelpers.cs:7-37`; consumer `vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:83-89`

## Summary
`ParseBool` only recognizes the exact literals `true/True/TRUE` and `false/False/FALSE`; every other
input (including `" true"` with whitespace, `"1"`, `"yes"`, `"tRuE"`, or any unexpected casing) falls
through to `return false`. It also does not trim. Because the result gates whether config-backup detail
fields are populated, a CSV value that does not match the hard-coded casing is silently treated as
"disabled" and the corresponding report data is dropped.

## Evidence
```csharp
// CObjectHelpers.cs:24-36
else if (value == "true" || value == "True" || value == "TRUE")  { return true; }
else if (value == "false" || value == "False" || value == "FALSE") { return false; }
else { return false; }   // anything else -> false, no trim, no bool.TryParse

// CBackupServerTableHelper.cs:83-89 — drives whether config-backup details are emitted
this.backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv.Enabled);
if (this.backupServer.ConfigBackupEnabled == true)
{
    this.backupServer.ConfigBackupTarget = cv.Target;
    this.backupServer.ConfigBackupEncryption = CObjectHelpers.ParseBool(cv.EncryptionOptions);
    ...
}
```

## Impact
A backup-server whose config-backup is actually enabled can render as disabled (with target/encryption/
retention omitted) purely because the source CSV used a different casing or had surrounding whitespace —
a silently wrong report section. The same applies to the `EncryptionOptions` flag.

## Suggested Fix
Replace the literal chain with `bool.TryParse(value?.Trim(), out var b)` (case-insensitive by spec) and
optionally accept `"1"/"0"` / `"yes"/"no"` if the collection scripts emit those. Return a documented
default only on genuine parse failure.

## Labels
bug, parsing, data-correctness, robustness
