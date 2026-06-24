---
title: "SetConfigBackupSettings relies on NullReferenceException for empty config-backup CSV"
severity: Low
labels: [reliability]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:81
confidence: High
---

## Summary

`SetConfigBackupSettings` does `cv = configBackupCsv.FirstOrDefault();` and then immediately dereferences `cv.Enabled`. When the config-backup CSV is empty or missing (parser yields no rows), `cv` is null and the method takes a `NullReferenceException` into its catch, logging the generic "Error processing config backup data" + `Object reference not set...`. Control flow by NRE: the log misrepresents a normal "no data" case as a processing error, and the pre-initialized `CConfigBackupCsv cv = new();` on the previous line is dead (immediately overwritten).

## Impact

Noise errors in the log on every system without config-backup data, masking real parse failures; backup-server section silently shows ConfigBackupEnabled=false with an error in the log that suggests something broke.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:79-96` —

```csharp
CConfigBackupCsv cv = new();                     // dead initialization
var configBackupCsv = config.ConfigBackupCsvParser();
cv = configBackupCsv.FirstOrDefault();           // null when CSV empty/missing

this.backupServer.ConfigBackupEnabled = CObjectHelpers.ParseBool(cv.Enabled);  // NRE
...
catch (Exception e)
{
    log.Error("Error processing config backup data");
```

## Suggested fix

```csharp
var cv = config.ConfigBackupCsvParser()?.FirstOrDefault();
if (cv == null)
{
    log.Info("No config backup data found; leaving config backup settings unset.");
    return;
}
```

Keep the catch for genuine parse errors only.
