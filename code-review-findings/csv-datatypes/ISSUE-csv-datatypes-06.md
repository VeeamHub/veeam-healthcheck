---
title: "Add VBR v13+ branch to CProxyDataFormer.CalcProxyTasks — proxies on v13 get wrong provisioning verdicts"
severity: High
labels: [bug]
domain: csv-datatypes
files:
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/ProxyData/CProxyDataFormer.cs:19
  - vHC/HC_Reporting/Functions/Reporting/DataTypes/ProxyData/CProxyDataFormer.cs:29
confidence: Medium
---

## Summary

`CalcProxyTasks` only sets `coreTasks`/`memTasks` for `CGlobals.VBRMAJORVERSION == 11` and `== 12`. For any other version — notably VBR **13**, which this repo actively supports (v13 collection fixes shipped, e.g. issue #112) — `coreTasks` stays `0` and `memTasks` stays at its nonsense initializer `availableMem / 1` (= total RAM in GB).

## Impact

For every proxy on a v13 server, `SetProvisionStatus(assignedTasks, 0, ram)` takes the `coreTasks < memTasks` branch:

- `assignedTasks == 0` → reported **WellProvisioned** (matches `coreTasks` of 0);
- any real `assignedTasks > 0` → reported **OverProvisioned**, regardless of cores/RAM.

The "Provisioning" column in the proxy table is therefore systematically wrong for v13 environments — exactly the recommendation customers act on. The same verdict feeds `CDataTypesParser.ProxyInfo()` via `CalcProxyOptimalTasks` (CDataTypesParser.cs:982-986, 1020, 1107).

## Evidence

`vHC/HC_Reporting/Functions/Reporting/DataTypes/ProxyData/CProxyDataFormer.cs:16-40`:

```csharp
public string CalcProxyTasks(int assignedTasks, int cores, int ram)
{
    int availableMem = ram ; // TODO double-check OS mem requirements
    int memTasks = (int)Math.Round((decimal)(availableMem / 1), 0, MidpointRounding.ToPositiveInfinity);
    int coreTasks = 0;

    if (cores == 0 && ram == 0)
    {
        return "NA";
    }

    if (CGlobals.VBRMAJORVERSION == 11)
    {
        coreTasks = cores;
        memTasks = this.MemoryTasks(availableMem, 2);
    }
    else if (CGlobals.VBRMAJORVERSION == 12)
    {
        coreTasks = (cores) * 2;
        memTasks = this.MemoryTasks(availableMem, 1);
    }
    // no v13+ branch: coreTasks remains 0
    return this.SetProvisionStatus(assignedTasks, coreTasks, memTasks);
}
```

A sibling bug exists in `CDataTypesParser.CalcRepoOptimalTasks` (CDataTypesParser.cs:743-829), where the v12 sizing adjustment is commented out (lines 768-773) and one fixed formula is applied to all versions.

## Suggested fix

- Add an explicit `>= 13` branch with the v13 sizing rules (or default unknown/newer versions to the latest known rule set rather than to `coreTasks = 0`).
- Guard the fall-through: if no version branch matched, return `"NA"` instead of computing a verdict from a zero `coreTasks`.
- Revisit the commented-out v12 logic in `CalcRepoOptimalTasks` at the same time.
