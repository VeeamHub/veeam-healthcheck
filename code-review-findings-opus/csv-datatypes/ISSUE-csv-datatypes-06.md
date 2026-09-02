# Integer division in RAM/size conversions truncates and can divide-by-zero in provisioning math

**Category:** csv-datatypes
**Severity:** Medium
**Type:** Bug
**File(s):** `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:738-741, 764` and `vHC/HC_Reporting/Functions/Reporting/DataTypes/ProxyData/CProxyDataFormer.cs:19, 32, 37, 43-45`

## Summary
Task-provisioning math performs integer division on RAM/core counts, both truncating fractional results and risking divide-by-zero. `MemoryTasksCount(int ram, int ramPerCore)` computes `ram / ramPerCore` as integer division before multiplying by 3; if a future caller passes `ramPerCore == 0` it throws `DivideByZeroException`. In `CProxyDataFormer`, `availableMem / memoryPerTask` casts the integer-division result and the `availableMem / 1` path is a no-op that ignores the intended per-task divisor.

## Evidence
```csharp
// CDataTypesParser.cs:738-741
private int MemoryTasksCount(int ram, int ramPerCore)
{
    return (int)Math.Round((decimal)(ram / ramPerCore) * 3, 0, MidpointRounding.ToPositiveInfinity);
    // ram / ramPerCore is INTEGER division -> truncates before the *3 and Round
}
```
```csharp
// CDataTypesParser.cs:764  computed but never used (dead), and would underflow if ram < 4
int availableMem = ram - 4;
```
```csharp
// CProxyDataFormer.cs:19  effectively availableMem (divide by 1), decimal cast after int math
int memTasks = (int)Math.Round((decimal)(availableMem / 1), 0, MidpointRounding.ToPositiveInfinity);
// :43-45  availableMem / memoryPerTask : int / double promotes, but availableMem already int-floored upstream
```

## Impact
Provisioning verdicts (Well/Over-provisioned) can be off due to truncation (e.g. `30 / 4 = 7` not `7.5`, then `*3 = 21` vs intended `22`), changing the over/under provisioning recommendation shown to the customer. The `ramPerCore`/`memoryPerTask` divisors are currently constants so no crash today, but the integer-division truncation is a live correctness issue and the divide-by-zero is a latent trap.

## Suggested Fix
Do the division in floating point and guard zero divisors:
```csharp
if (ramPerCore == 0) return 0;
return (int)Math.Round((decimal)ram / ramPerCore * 3, MidpointRounding.ToPositiveInfinity);
```
Remove the dead `availableMem = ram - 4` (or use it) and replace `availableMem / 1` with the real per-task value.

## Labels
integer-division, divide-by-zero, provisioning, correctness
