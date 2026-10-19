# Culture-sensitive number and date parsing across all VB365 tables

**Category:** reporting-vb365
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:51-55, 247, 292-293, 401-402, 585-589, 1286-1289, 1645-1652` (and all other `TryParse` calls)

## Summary
Every numeric and date parse in the VB365 tables uses the parameterless `int.TryParse` / `double.TryParse` / `decimal.TryParse` / `DateTime.TryParse`, which bind to the **current thread culture**. The CSVs are produced by PowerShell collection (invariant/`.`-decimal, US-style dates), but the report can run on a machine with a non-US locale (e.g. `de-DE` uses `,` as decimal separator and `dd.MM.yyyy` dates). On such a host, capacities/free space/RAM/CPU/thread counts and certificate/license expiry dates parse to 0 / `default(DateTime)`, silently corrupting every threshold and coloring decision.

## Evidence
Examples:
```csharp
decimal.TryParse(gl.LicensedFor, out decimal licFor);            // line 51
DateTime.TryParse(gl.LicenseExpiry, out DateTime expireDate);    // line 55
double.TryParse(g.Free, out double freeSpace);                   // line 401
DateTime.TryParse(g.ServerCertExpires, out DateTime sCertExpiry);// line 585
double.TryParse(sizeLimitArray[0], out double sizeLimitNumber);  // line 1286
int.TryParse(selItems, out int selectedItemsCount);              // line 1645
```
None pass `CultureInfo.InvariantCulture`. Note CA1305 is project-suppressed, but the consequence here is a real correctness bug (wrong coloring / `default` expiry dates treated as "expired"), not a style nit.

## Impact
On non-US-locale hosts: `DateTime.TryParse` of US-format expiry strings fails → `default(DateTime)` which is `< DateTime.Now` → every certificate and the license show as **expired/red** (false alarms). Decimal/double parses fail → 0 → false low-space/divide-by-zero behavior. The report becomes untrustworthy depending on where it is generated.

## Suggested Fix
Parse with `CultureInfo.InvariantCulture` (and `DateTimeStyles.None`) to match the PowerShell-emitted CSV format, e.g.:
```csharp
double.TryParse(g.Free, NumberStyles.Any, CultureInfo.InvariantCulture, out double freeSpace);
DateTime.TryParse(g.ServerCertExpires, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sCertExpiry);
```
Apply consistently to all VB365 parses (ideally via a small shared helper).

## Labels
bug, globalization, culture, parsing, vb365, reporting
