---
title: "Remove dead/no-op immutability rendering and empty 'Immutability' subsection in security report"
severity: Low
labels: [maintainability, bug]
domain: reporting-vbr-tables
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTablesHelper.cs:43
  - vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:505
confidence: High
---

## Summary

Immutability content in the security report is wired to nothing:

1. `CHtmlTablesHelper.AddImmutabilityTables()` constructs a `CImmutabilityTable`, never uses it, and renders an **empty** tuple list. Grep shows no callers anywhere in the codebase — and the `Security/Immutability/CImmutabilityTable.cs` + `CImmutableRepos.cs` classes are referenced only here, making them effectively dead.
2. `CHtmlTables.AddSecurityReportSecuritySummaryTable()` emits `this.AddTable("Immutability", string.Empty)` — a subsection header titled "Immutability" followed by an empty `<table></table>`.

## Impact

The security report shows an "Immutability" heading with no content (user-visible artifact suggesting data failed to load), and dead classes/methods mislead maintainers into thinking immutability tables are rendered from here. Low severity, but it sits in the security report where blank sections undermine trust.

## Evidence

`CHtmlTablesHelper.cs:43-48`:

```csharp
public string AddImmutabilityTables()
{
    CImmutabilityTable ct = new();

    return this.WriteTupleListToHtml(new List<Tuple<string,string>>());
}
```

`ct` is unused; the rendered list is hardcoded empty; no call sites exist (`grep -rn "AddImmutabilityTables"` returns only the definition).

`CHtmlTables.cs:505`:

```csharp
s += this.AddTable("Immutability", string.Empty);
```

`AddTable(string, string)` (line 533) wraps the empty string in `<table class="content-table bold-first-col"></table>` under a "Immutability" subsection heading.

## Suggested fix

Either finish the feature (have `AddImmutabilityTables` call into `CImmutabilityTable`/`CImmutableRepos` and feed `AddTable("Immutability", ...)` with its output) or delete the no-op method, the empty subsection line, and the orphaned `Security/Immutability` classes. Decide one way; don't ship the empty header.
