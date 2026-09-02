# SetConfigBackupSettings dereferences FirstOrDefault() result without null check (masked by broad catch)

**Category:** reporting-vbr-core
**Severity:** Low
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:74-97`

## Summary
`SetConfigBackupSettings` does `cv = configBackupCsv.FirstOrDefault();` and then immediately accesses
`cv.Enabled`. When the config-backup CSV is empty or missing, `FirstOrDefault()` returns null and
`cv.Enabled` throws `NullReferenceException`. The whole method is wrapped in `catch (Exception)`, so the
NRE is swallowed and logged as a generic "Error processing config backup data" — the config-backup
section is then silently absent with no indication of why.

## Evidence
```csharp
// CBackupServerTableHelper.cs:78-89
CCsvParser config = new();
CConfigBackupCsv cv = new();
var configBackupCsv = config.ConfigBackupCsvParser();
cv = configBackupCsv.FirstOrDefault();          // can be null

this.backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv.Enabled);   // NRE if null
...
catch (Exception e)
{
    log.Error("Error processing config backup data");
    log.Error("\t" + e.Message);                 // NRE swallowed as generic error
}
```

## Impact
Empty/missing config-backup CSV (a normal state — e.g. config backup never configured) is reported as an
error rather than handled, and the section is dropped. Confusing logs and a missing report section for a
benign condition.

## Suggested Fix
Guard the result: `if (cv == null) { log.Info("No config backup data found"); return; }` before using
it, rather than relying on the catch-all. Distinguish "no data" (info) from a true parse error (error).

## Labels
bug, null-deref, exception-swallowing, error-handling
