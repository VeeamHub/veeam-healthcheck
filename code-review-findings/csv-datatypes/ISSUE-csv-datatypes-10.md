---
title: "CNetTrafficRulesCsv index map contradicts its own documented column layout — EncryptionEnabled may read the wrong column"
severity: Medium
labels: [bug]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CNetTrafficRulesCsv.cs:11
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:459
confidence: Medium
---

## Summary

The header comment in `CNetTrafficRulesCsv` documents a 10-column layout starting at `TargetIPStart`:

```
// TargetIPStart  TargetIPEnd  EncryptionEnabled  ThrottlingEnabled  ThrottlingUnit  ThrottlingValue  ThrottlingWindowEnabled  ThrottlingWindowOptions  Name  Id
```

but the class maps **12** positional columns starting with two `Source*` fields, placing `EncryptionEnabled` at `[Index(4)]`. Since `[Index]` mapping reads by position and ignores headers (see ISSUE-03), if the actual export matches the documented layout, `EncryptionEnabled` reads the `ThrottlingUnit` column, `SourceIpStart` reads `TargetIPStart`, and so on — every field shifted by two.

The collection side gives no protection: `Get-VhcTrafficRules.ps1` pipes the raw cmdlet output (`Get-VBRNetworkTrafficRule | Export-VhciCsv`), so the column order is whatever property order the Veeam PS module emits for that version — there is no `Select-Object` pinning the 12-column order the class assumes.

## Impact

The only field the report consumes is `EncryptionEnabled` (`CDataTypesParser.NetTrafficRulesParser`, line 459: `cv.EncryptionEnabled = c.EncryptionEnabled;`), which feeds the security/encryption summary. If the column order is the documented one (or shifts between VBR PS module versions), the report's "traffic encryption enabled" indication is derived from an unrelated column — strings like `"Mbps"` won't parse as bool and the rule will read as not-encrypted. A wrong security posture statement is a high-trust failure for this tool, hence Medium despite the single consumer.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CNetTrafficRulesCsv.cs:11-26`:

```csharp
// TargetIPStart	TargetIPEnd	EncryptionEnabled	ThrottlingEnabled	...
[Index(0)]
public string SourceIpStart { get; set; }   // comment says column 0 is TargetIPStart

[Index(1)]
public string SourceIPEnd { get; set; }

[Index(2)]
public string TargetIpStart { get; set; }

[Index(3)]
public string TargetIpEnd { get; set; }

[Index(4)]
public string EncryptionEnabled { get; set; }   // comment says EncryptionEnabled is column 2
```

## Suggested fix

- Pin the export in `Get-VhcTrafficRules.ps1` with an explicit `Select-Object SourceIPStart, SourceIPEnd, TargetIPStart, TargetIPEnd, EncryptionEnabled, ...` so the contract is explicit.
- Switch the class to `[Name(...)]` mapping (the headers are clean property names here, so name matching works directly) and fix or delete the stale comment.
- Verify against a real `_trafficRules.csv` from current VBR versions which layout actually ships.
