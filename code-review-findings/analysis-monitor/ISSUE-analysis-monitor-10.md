---
title: "Remove dead Analysis data models: empty SOBR class and unused Repository class"
severity: Low
labels: [maintainability]
domain: analysis-monitor
files:
  - vHC/HC_Reporting/Functions/Analysis/DataModels/SOBR.cs:9
  - vHC/HC_Reporting/Functions/Analysis/DataModels/Repository.cs:9
confidence: High
---

## Summary

`SOBR` is a completely empty class and `Repository` (Name/Sobr only) has no references anywhere outside its own file. A repo-wide grep for `DataModels.SOBR`, `new SOBR`, `DataModels.Repository`, and `new Repository(` finds no consumers. Both files also carry boilerplate unused usings (`System.Linq`, `System.Threading.Tasks`, etc.).

## Impact

Dead code in the Analysis namespace misleads readers into thinking SOBR/repository analysis models exist; the actual SOBR/repo report logic lives elsewhere (CSV dynamic objects in `Functions/Reporting`). Zero runtime impact.

## Evidence

`vHC/HC_Reporting/Functions/Analysis/DataModels/SOBR.cs:9-11` —
```csharp
internal class SOBR
{
}
```
`vHC/HC_Reporting/Functions/Analysis/DataModels/Repository.cs:9-18` —
```csharp
internal class Repository
{
    public Repository() { }
    public string Name { get; set; }
    public string Sobr { get; set; }
}
```
No usages found outside `Functions/Analysis/DataModels/` (verified by grep across `vHC/HC_Reporting`).

## Suggested fix

Delete both files (or populate them if SOBR/repo analysis is genuinely planned — in that case add a TODO referencing the tracking issue).
