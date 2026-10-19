---
title: "MemoryTasksCount truncates via integer division before rounding; availableMem computed but unused"
severity: Low
labels: [bug, maintainability]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:738
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:764
confidence: High
---

## Summary

`MemoryTasksCount` performs integer division **before** casting to decimal, so the `Math.Round(..., MidpointRounding.ToPositiveInfinity)` is applied to an already-truncated value and the rounding mode is dead:

```csharp
private int MemoryTasksCount(int ram, int ramPerCore)
{
    return (int)Math.Round((decimal)(ram / ramPerCore) * 3, 0, MidpointRounding.ToPositiveInfinity);
}
```

`ram / ramPerCore` is `int / int`. For `ram = 7, ramPerCore = 4`: `7 / 4 = 1` → 3 tasks, whereas the intended math (`7 / 4.0 = 1.75 → *3 = 5.25 → round up = 6`) gives 6. Repository memory-based task capacity is systematically understated for any RAM not an exact multiple of 4 GB.

Additionally, `CalcRepoOptimalTasks` computes `int availableMem = ram - 4;` (`CDataTypesParser.cs:764`) — presumably the intended OS-memory reservation — and then never uses it; `MemoryTasksCount(ram, 4)` is called with the unreserved `ram`.

## Impact

The repository "Provisioning" verdict (`WellProvisioned`/`OverProvisioned`) compares `assignedTasks` against these task counts (`CDataTypesParser.cs:775-828`). Truncation + the unused OS reservation skew the comparison, so borderline repositories can be classified incorrectly. Severity Low because the verdict is advisory and the error is bounded (±2 tasks), but the dead `MidpointRounding` and dead `availableMem` indicate the formula does not implement what was intended.

The same truncate-then-round pattern appears in `CProxyDataFormer.MemoryTasks` (`CProxyDataFormer.cs:43-46`) — there `memoryPerTask` is `double` so the division itself is fine, but `CProxyDataFormer.cs:19` has the same dead rounding on `availableMem / 1`.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:738-741` (quoted above) and `:764-766`:

```csharp
int availableMem = ram - 4;                   // never used
int memTasks = this.MemoryTasksCount(ram, 4); // uses full ram, truncating division
int coreTasks = cores * 3;
```

## Suggested fix

```csharp
private int MemoryTasksCount(int ram, int ramPerCore)
{
    return (int)Math.Round((decimal)ram / ramPerCore * 3, 0, MidpointRounding.ToPositiveInfinity);
}
```

and either use `availableMem` in the call (`MemoryTasksCount(availableMem, 4)`) or delete it. Add a unit test pinning the expected task counts for non-multiple-of-4 RAM values.
