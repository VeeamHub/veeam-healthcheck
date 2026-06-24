---
title: "ProtectedWorkloadsToXml swallows all exceptions without logging; ineffective null guard uses || before dereferencing both collections"
severity: Medium
labels: [reliability, bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:393
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:269
confidence: High
---

## Summary

The entire Protected Workloads computation is wrapped in `catch (Exception) { return 1; }` — no log entry at all. Any data problem (missing column, malformed CSV row, null `Name`/`Path` on a record causing `p.Path.StartsWith(...)`/`p.name.Contains(...)` to NRE) silently drops the whole Protected Workloads section from the report with nothing in the log to explain it.

Separately, the Hyper-V guard at :269 uses `||` and then dereferences **both** collections:

```csharp
if (HvProtectedVms != null || HvUnProtectedVms != null)
{
    HvProtectedVms = HvProtectedVms.ToList();      // NRE if only the other one was non-null
    HvUnProtectedVms = HvUnProtectedVms.ToList();
```

Today both readers happen to return `Enumerable.Empty<>` rather than null (CCsvParser:705-728), so the guard is dead code — but it is wrong as written and will NRE the section if a reader is ever changed to return null (the apparent assumption of whoever wrote the guard).

## Impact

Silent loss of a key report section; impossible to diagnose from logs. The `||` guard is a latent NRE and actively misleading to maintainers.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:393-396` —

```csharp
catch (Exception)
{
    return 1;
}
```

Caller side (`CHtmlTables.AddProtectedWorkLoadsTable`) renders from the public list fields, which remain null/stale when this returns 1.

## Suggested fix

Log the exception (message + stack) before returning 1, mirroring `JobSessionInfoToXml`'s structure but with logging. Change the Hyper-V guard to `&&` (or remove it, since the readers never return null) and rely on the empty-enumerable contract.
