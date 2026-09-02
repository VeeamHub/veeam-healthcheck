---
title: "Culture-sensitive date parsing falsely flags license/certs as expired when cultures differ"
severity: Medium
labels: [bug]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:55
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:585
confidence: High
---

## Summary

Dates in the VB365 CSVs are written by the collector with `ToShortDateString()` (the **collection** machine's culture) and parsed in the report with `DateTime.TryParse(...)` (the **report** machine's current culture). When the report is generated on a machine with a different culture (the documented import scenario: customer collects, SE imports), parsing either fails — leaving `expireDate == DateTime.MinValue`, which is `< DateTime.Now`, so the license/cert is shaded **red as expired** — or worse, silently swaps day/month (e.g. "04/06/2026" en-US vs de-DE).

## Impact

Wrong high-visibility findings: a valid license shown as expired, certificates flagged as expired/expiring, or expiry warnings missed because 4 June became 6 April. Affects license expiry (`CM365Tables.cs:55-70`) and all five certificate expiry checks in `Vb365Security()` (`CM365Tables.cs:585-642`).

## Evidence

Collector, `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1:1005`:

```powershell
'License Expiry=>$.ExpirationDate.ToShortDateString()'
```

Report, `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:55-62`:

```csharp
DateTime.TryParse(gl.LicenseExpiry, out DateTime expireDate);
...
if (expireDate < DateTime.Now)
{
    s += this.form.TableData(gl.LicenseExpiry, string.Empty, 1);   // red = expired
}
```

A failed `TryParse` leaves `expireDate = default` (year 0001), which always takes the "expired" branch. The same pattern repeats at `CM365Tables.cs:585-589` for the five cert dates. The numeric parses (`decimal.TryParse`, `double.TryParse`, `int.TryParse`) throughout the file likewise use current culture while the collector emits invariant-style `#,##0.000` strings.

## Suggested fix

Collector: emit round-trip dates (`ToString("yyyy-MM-dd")`). Report: parse with `DateTime.TryParseExact`/`CultureInfo.InvariantCulture`, and treat parse failure as "unknown" (no shading) rather than letting `MinValue` fall into the expired branch.
