# Dead/stub data models in Analysis/DataModels (empty SOBR, unused Repository)

**Category:** analysis-monitor
**Severity:** Low
**Type:** Maintainability
**File(s):** `vHC/HC_Reporting/Functions/Analysis/DataModels/SOBR.cs:9-11`, `vHC/HC_Reporting/Functions/Analysis/DataModels/Repository.cs:9-18`

## Summary
`SOBR` is a completely empty `internal class` with no members. `Repository` is a two-property model (`Name`, `Sobr`) that, with the auto-generated default `using` block and empty constructor, has the hallmarks of a scaffolded-but-unused type. These add noise to the `Analysis.DataModels` namespace and imply a data model that does not exist.

## Evidence
```csharp
// SOBR.cs:9-11
internal class SOBR
{
}
```
```csharp
// Repository.cs:9-18
internal class Repository
{
    public Repository() { }
    public string Name { get; set; }
    public string Sobr { get; set; }
}
```

## Impact
Maintainability only. An empty `SOBR` model is misleading (SOBR — Scale-Out Backup Repository — is a real, important VBR concept, so a reader may assume analysis logic lives here when it does not). Confirm no reflection/serialization depends on these before removing.

## Suggested Fix
Remove `SOBR.cs` if unused, or populate it with the intended SOBR fields if it is a placeholder for planned work (add a `// TODO` with intent). Verify `Repository` has live references; if not, delete it. Trim the unused `using System.Linq/Text/Threading.Tasks` directives.

## Labels
maintainability, dead-code, analysis
