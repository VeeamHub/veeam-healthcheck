---
title: "Use InvariantCulture consistently when parsing numbers/dates from collected CSVs"
severity: Medium
labels: [bug, reliability]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/ProtectedWorkloads/NasSourceInfo.cs:90
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CloudConnect/CCloudTenantBackupResourcesTable.cs:71
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:124
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummaryHelper.cs:74
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Security/CMalwareTable.cs:203
confidence: High
---

## Summary

Numeric and date parsing of collected CSV values is split between two conventions: some sites correctly pass `NumberStyles.Any, CultureInfo.InvariantCulture` (e.g. `CHtmlTables.cs:683`, `CHtmlTables.cs:1298-1303`), while at least five renderers parse with the current thread culture (`Convert.ToDouble`, bare `double.TryParse`, bare `DateTime.Parse`/`TryParse`). This is classic drift within the same review domain.

## Impact

On a machine whose OS culture uses `,` as decimal separator (de-DE, fr-FR, ru-RU — common for Veeam's EMEA field), `double.TryParse("1234.56")` either fails (value silently becomes 0) or, worse, succeeds with `.` treated as a thousands separator, producing **123456** — a 100x wrong number rendered in the report (quota GB, NAS sizes, source GB). Date parsing of `DetectionTime`/`StartTime` can fail or transpose day/month, skewing the malware-event window and wait-time stats.

## Evidence

- `ProtectedWorkloads/NasSourceInfo.cs:90` — `double sizeD = Convert.ToDouble(size);` (also lines 42-43, 64-66) — current culture.
- `CloudConnect/CCloudTenantBackupResourcesTable.cs:71` — `if (double.TryParse(raw.ToString(), out double mb))` — feeds the MB→GB conversion for quota/used/free columns.
- `Jobs Info/CJobInfoTable.cs:124,131` — `double.TryParse(diskGb, out onDiskGB);` — NAS job size columns.
- `Job Session Summary/CJobSessSummaryHelper.cs:74-75,202-204` — `DateTime.TryParse(w.StartTime, ...)`, `double.TryParse(session.DedupRatio, ...)`.
- `Security/CMalwareTable.cs:203` — `var dt = DateTime.Parse(item.DetectionTime);`.

Correct in-tree convention to match: `CHtmlTables.cs:683` — `double.TryParse(srcObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)`.

## Suggested fix

Standardize on `double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out ...)` and `DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out ...)` for all CSV-sourced values (the collection scripts write invariant-formatted output). Consider a small shared `CsvValue.ParseDouble/ParseDate` helper so future tables can't drift.
