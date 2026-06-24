---
title: "Vb365Security(): malformed/duplicated HTML structure (headers rebuilt per row, stray tags)"
severity: Low
labels: [bug, maintainability]
domain: reporting-vb365
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:660
confidence: High
---

## Summary

Inside the `foreach (var g in global)` loop, `Vb365Security()` closes the first table and opens the second one **per record** (`</table><table border ="1"><tr><br/>` at line 660), places a `<br/>` directly inside a `<tr>` (invalid), and emits a stray duplicate `</tr>` between the Restore Portal and Operator Auth rows (lines 696-697: `s += "</tr>"; s += "</tr><tr>";`). If Security.csv ever contained more than one row, the section would render the second table's headers repeatedly. Minor related smell: the cert-expiry warnings use `serverDateShade |= 3;` (lines 599, 611, 621, 631, 641) where plain assignment is meant — harmless today only because the value is always 0 when reached.

Also, "RBAC Roles Defined" (line 657) renders a bool (`True`/`False`) where the header implies a count — `rbacCsv.Count` is already available at line 528.

## Impact

Invalid HTML that browsers happen to repair today; brittle against multi-row input and any future table styling/parsing (e.g. the PPTX/PDF converters consume this same HTML string). The bool-vs-count loses information that's already computed.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:660`:

```csharp
s += "</table><table border =\"1\"><tr><br/>";
```

`CM365Tables.cs:696-697`:

```csharp
s += "</tr>";
s += "</tr><tr>";
```

`CM365Tables.cs:657`:

```csharp
s += this.form.TableData(rbacRowsCount.ToString(), string.Empty);
```

## Suggested fix

Move the second table's header emission outside the row loop, drop the `<br/>` inside `<tr>` and the duplicate `</tr>`, replace `|=` with `=`, and display `rbacCsv.Count.ToString()` instead of the bool.
