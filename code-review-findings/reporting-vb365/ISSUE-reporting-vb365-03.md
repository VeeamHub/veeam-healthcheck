---
title: "Guard against decimal divide-by-zero in Globals() license usage percentage"
severity: High
labels: [bug, reliability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:53
confidence: High
---

## Summary

`Globals()` computes `licUsed / licFor * 100` with `decimal` operands. Unlike `double`, **decimal division by zero throws `DivideByZeroException`**. `licFor` is 0 whenever the `Licensed For` field is empty, missing, or unparseable (`decimal.TryParse` failure leaves the out var at 0). The exception is swallowed by the section's empty catch, so the entire Global Configuration row (license status, expiry, support expiry, etc.) silently disappears from the report.

## Impact

The whole Overview/Global Configuration section renders as an empty table whenever the license CSV has a blank/odd `Licensed For` value — high-visibility wrong output with no error reported. The percentage is also computed before it's needed, so even rows whose shading wouldn't use it are destroyed.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:51-53`:

```csharp
decimal.TryParse(gl.LicensedFor, out decimal licFor);
decimal.TryParse(gl.LicensesUsed, out decimal licUsed);
decimal percentUsed = licUsed / licFor * 100;
```

and the swallowing catch at `CM365Tables.cs:127-129`:

```csharp
catch (Exception)
{
}
```

`CGlobalCsv` is index-mapped with `MissingFieldFound = null` (`CCsvReader.cs:85`), so a short row yields `null` → `TryParse` false → `licFor == 0` → throw.

## Suggested fix

```csharp
decimal percentUsed = licFor > 0 ? licUsed / licFor * 100 : 0;
```

(or compute the percentage lazily inside the `LicensesUsed` shading block with the same guard).
