---
title: "Get-VhcSessionReport.ps1 exports session data without Protect-VhciCsvInjection"
severity: Medium
labels: [security, bug]
domain: collection-security
files:
  - vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSessionReport.ps1:186
  - vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1:336
confidence: High
---

## Summary
The codebase added a CSV formula-injection neutralizer (`Protect-VhciCsvInjection`)
and a wrapper (`Export-VhciCsv`) that pipes objects through it before
`Export-Csv`. Several export paths bypass that wrapper and call `Export-Csv`
directly, so the neutralizer never runs. The most significant is
`Get-VhcSessionReport.ps1`, which exports per-VM/per-job session rows — fields
whose values (VM names, job names) are attacker-influenceable by anyone who can
name a VM or a backup job in the protected environment.

## Impact
A VM or job named `=cmd|'/c calc'!A1` (or `@`, `+`, `-`, leading TAB/CR prefixes)
is written verbatim into `VeeamSessionReport.csv`. When an operator opens that CSV
in Excel/LibreOffice, the formula executes — classic CSV/spreadsheet formula
injection leading to command execution on the analyst's workstation. This is the
exact threat the `Protect-VhciCsvInjection` helper was introduced (commit
`04716ab`) to neutralize, but this export path was not migrated to it.

## Evidence
`vHC/.../Public/Get-VhcSessionReport.ps1:186`:
```powershell
$allOutput | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
```
No `| Protect-VhciCsvInjection` in the pipeline, unlike the sibling
`Private/Export-VhciCsv.ps1:42` which does
`$allObjects | Protect-VhciCsvInjection | Export-Csv ...` and
`Get-NasInfo.ps1:131/134/180` which all pipe through the neutralizer.

`vHC/.../VBR/Get-VBRConfig.ps1:336` — collection manifest export, same bypass
(lower risk: fields are internal metadata/error strings, but `Error` can echo
remote-server-controlled exception text):
```powershell
$manifest | Export-Csv -Path (Join-Path $ReportPath "${VBRServer}_CollectionManifest.csv") -NoTypeInformation -Encoding UTF8
```

## Suggested fix
Insert the neutralizer into every `Export-Csv` pipeline that carries
environment-derived strings:
```powershell
$allOutput | Protect-VhciCsvInjection | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
```
Better: make `Export-VhciCsv` the only sanctioned export entry point and grep the
module to prove no raw `Export-Csv` survives outside it (add a Pester/CI guard so
new exports cannot regress).
