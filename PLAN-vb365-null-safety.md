# VB365 Collector Script Null-Safety Implementation Plan

**Created:** 2026-03-25
**Status:** Planned (not started)
**Scope:** Fix VB365 collector errors when running against partially configured or session-less VB365 servers

Both script copies are identical (confirmed via diff). Apply every change to both:

- `vHC/HC_Reporting/Functions/Collection/PSCollections/Scripts/Collect-VB365Data.ps1`
- `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1`

---

## Background

The VB365 collector produces many errors when the target server has:
- No completed backup job sessions (`JobSessionIndex = 0`)
- Partially configured organizations (no Exchange/SharePoint connections)
- Default/self-signed certificates with no expiration date exposed
- Unconfigured email notifications

**Confirmed from real output:** VB365 v13.3.0.282, 3 jobs configured, 1 session exists but `JobSessionIndex = 0`. Result: 10 of 14 CSVs are 0 bytes.

---

## STREAM 1: Expression-Level Null Guards

These add null-safety inside expression strings evaluated via `Invoke-Expression`.

### Fix 1.1 -- License Expiry Dates (line ~996-997)

**Current:**
```powershell
'License Expiry=>$.ExpirationDate.ToShortDateString()'
'Support Expiry=>$.SupportExpirationDate.ToShortDateString()'
```

**Replacement:**
```powershell
'License Expiry=>if ($null -ne $.ExpirationDate) { $.ExpirationDate.ToShortDateString() }'
'Support Expiry=>if ($null -ne $.SupportExpirationDate) { $.SupportExpirationDate.ToShortDateString() }'
```

### Fix 1.2 -- Email Notification Settings (lines ~1006, 1008)

**Current:**
```powershell
'Notification Enabled=>$Global:VBOEnvironment.VBOEmailSettings.EnableNotification'
<#fixed Jul 21#>'Notify On=>Join((($Global:VBOEnvironment.VBOEmailSettings | select NotifyOn*,Supress*).psobject.Properties | ? { $_.Value -eq $true}).Name.Replace("NotifyOn",""))'
```

**Replacement:**
```powershell
'Notification Enabled=>if ($null -ne $Global:VBOEnvironment.VBOEmailSettings) { $Global:VBOEnvironment.VBOEmailSettings.EnableNotification }'
<#fixed Jul 21#>'Notify On=>if ($null -ne $Global:VBOEnvironment.VBOEmailSettings) { Join((($Global:VBOEnvironment.VBOEmailSettings | select NotifyOn*,Supress*).psobject.Properties | ? { $_.Value -eq $true}).Name.Replace("NotifyOn","")) }'
```

### Fix 1.3 -- Certificate Expiry Dates (lines ~1019, 1025, 1030, 1035, 1040)

Add `$null -ne $expiryDate -and` before existing MinValue check on all 5 lines.

**Current pattern:**
```powershell
'Server Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOSecuritySettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
```

**Replacement pattern (apply to all 5):**
```powershell
'Server Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOSecuritySettings.CertificateExpirationDate; if ($null -ne $expiryDate -and $expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
```

Lines:
- 1019: `VBOSecuritySettings` (Server Cert)
- 1025: `VBORestAPISettings` (API Cert)
- 1030: `VBOTenantAuthenticationSettings` (Tenant Auth Cert)
- 1035: `VBORestorePortalSettings` (Restore Portal Cert)
- 1040: `VBOOperatorAuthenticationSettings` (Operator Auth Cert)

### Fix 1.4 -- Organization EXO/SPO Connection Settings (lines ~1151-1154)

Wrap each expression with `if ($null -ne $.Office365ExchangeConnectionSettings)` / `$.Office365SharePointConnectionSettings`.

For the App Cert lines (1152, 1154), also guard `ApplicationCertificateThumbprint`.

### Fix 1.5 -- Retention Period Properties (lines ~1115-1125)

Add early return: `if ($null -eq $.RetentionPeriod) { "N/A" } elseif ...`

### Fix 1.6 -- Processing Options / SelectedItems (line ~1170)

Wrap with `if ($null -ne $.SelectedItems) { ... }`

### Fix 1.7 -- Selected/Excluded Objects from JobSessionIndex (lines ~1172-1173)

**Current:**
```powershell
'Selected Objects=>$objectCountStr = $Global:VBOEnvironment.JobSessionIndex[$($.Id.Guid)].LatestComplete; [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= objects)").Value'
```

**Replacement:**
```powershell
'Selected Objects=>$objectCountStr = $Global:VBOEnvironment.JobSessionIndex[$($.Id.Guid)].LatestComplete; if ($null -ne $objectCountStr) { [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= objects)").Value }'
```

Same for Excluded Objects.

### Fix 1.8 -- Schedule Policy (line ~1183)

Add `$null -ne $.SchedulePolicy -and` to outer `if` condition.

---

## STREAM 2: Empty JobSessionIndex Cascade Fixes

### Fix 2.1 -- JobStats empty guard (around line ~1196-1221)

Pre-filter sessions with `Where-Object { $null -ne $_ }`, wrap `mde` in `if/else` to produce empty-with-headers when no data.

### Fix 2.2 -- ProcessingStats empty guard (around line ~1225-1336)

Same pattern. Also wrap the line 1336 JSON transform with a null/count check.

### Fix 2.3 -- JobSessions empty guard (around line ~1340-1349)

Same pattern.

### Fix 2.4 -- Proxy "Objects Managed" and Repo "Daily Change Rate" (lines ~1071, 1074, 1107, 1112)

Add null filter inside expression pipelines: `$idx = $Global:VBOEnvironment.JobSessionIndex[$_]; if ($null -ne $idx ...)`

For Daily Change Rate display (line ~1112), guard with `if ($null -ne $dailyChangeAvg) { ... } else { "N/A" }`

---

## STREAM 3: Aggregation and CSV Export Fixes

### Fix 3.1 -- "No map found" message (line ~1392)

Change to: `"No data found for: "+$sectionName+". Skipping."`

### Fix 3.2 -- CSV export null safety (line ~1412)

Add null check before `ConvertTo-Csv`, write empty file with log message for null sections.

---

## Implementation Checklist

| # | Fix | Risk | Status |
|---|-----|------|--------|
| 1.1 | License expiry null-guard | Low | [ ] |
| 1.2 | Email settings null-guard | Low | [ ] |
| 1.3 | 5x certificate expiry null-guard | Low | [ ] |
| 1.4 | EXO/SPO connection settings | Low | [ ] |
| 1.5 | Retention period null-guard | Medium | [ ] |
| 1.6 | Processing Options / SelectedItems | Low | [ ] |
| 1.7 | Selected/Excluded Objects | Low | [ ] |
| 1.8 | Schedule Policy | Low | [ ] |
| 2.1 | JobStats empty pipeline guard | Medium | [ ] |
| 2.2 | ProcessingStats empty pipeline guard | Medium | [ ] |
| 2.3 | JobSessions empty pipeline guard | Medium | [ ] |
| 2.4 | Objects Managed / Daily Change Rate | Low | [ ] |
| 3.1 | "No map found" message improvement | None | [ ] |
| 3.2 | CSV export null safety net | Low | [ ] |

**Testing:** Run against (a) no jobs, (b) jobs with no completed sessions, (c) partially configured orgs, (d) fully working environment. Verify CSVs have headers in all cases and no "not valid" errors.
