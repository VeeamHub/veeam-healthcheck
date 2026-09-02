---
title: "Index-based CSV mapping in VB365 POCOs silently misaligns if collector columns change"
severity: Medium
labels: [maintainability, bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/VB365/CSecurityCsv.cs:10
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/VB365/CGlobalCsv.cs:10
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/VB365/CLocalRepos.cs:10
confidence: Medium
---

## Summary

The three typed VB365 CSV classes map columns purely by position (`[Index(0)]`…`[Index(21)]`) while every other VB365 CSV is consumed dynamically by (normalized) header name. The collection script (`Collect-VB365Data.ps1`) defines these columns by name and contains several commented-out columns **in the middle of the list** (e.g. `'Server Cert PK Exportable?'`, `'API Cert PK Exportable?'` between the cert and expiry columns). If any of those are re-enabled, or a column is added/reordered in a future collector version, every field after the insertion point silently shifts — cert expiry dates read from the "self-signed" column, license counts read from exclusions, etc. — with no parse error.

## Impact

Whole-section wrong data that looks plausible (strings into strings) and survives `MissingFieldFound = null` / `HeaderValidated = null` (`CCsvReader.cs:85-86`) without any warning. Old/new tool–script version mismatches (a supported scenario: re-running report generation against previously collected data) are the realistic trigger.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/VB365/CSecurityCsv.cs:31-38`:

```csharp
[Index(6)]
public string APIPort { get; set; }

[Index(7)]
public string APICert { get; set; }

[Index(8)]
public string APICertExpires { get; set; }
```

Collector with a dormant column between them, `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1:1033-1035`:

```powershell
'API Cert=>$Global:VBOEnvironment.VBORestAPISettings.CertificateFriendlyName'
#'API Cert PK Exportable?=>Test-CertPKExportable(...)'
'API Cert Expires=>...'
```

## Suggested fix

Replace `[Index(n)]` with `[Name("...")]` attributes matching the normalized headers (the `PrepareHeaderForMatch` in `CCsvReader.cs:76-84` already lowercases/strips punctuation, so `[Name("apicertexpires")]` etc.), making the typed readers resilient to column insertion/reordering like the dynamic readers already are.
