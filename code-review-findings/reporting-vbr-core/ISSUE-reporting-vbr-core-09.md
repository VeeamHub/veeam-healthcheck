---
title: "VM-to-server matching by StartsWith/Contains miscounts protected workloads"
severity: Medium
labels: [bug]
domain: reporting-vbr-core
files:
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:841
  - vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:363
confidence: Medium
---

## Summary

`ServerXmlFromCsv` attributes VMs to managed servers with `p.Path.StartsWith(c.Name)` — a plain ordinal-ish prefix test on names. A server named `esx1` matches VM paths belonging to `esx10`, `esx11`, etc. Because the loop also maintains a global `countedVMs` de-dup list, the *first* server processed steals VMs from later, longer-named servers, so "Protected/Total VMs" per server is wrong whenever one server name is a prefix of another (very common: `host1`/`host10`). `StartsWith(string)` is also culture-sensitive (no `StringComparison`), so Turkish-I style locales can mismatch entirely.

`ProtectedWorkloadsToXml` has the same class of bug at :363/:371: `p.name.Contains(v.Name)` marks a VM as "protected by physical agent" on any substring hit — VM `SQL` matches agent `MYSQL01`, VM `01` matches almost everything.

Additionally, the per-server VM loops are O(servers × VMs) with `List.Contains` inside (O(n) each), so large environments (10k VMs × hundreds of servers) pay an avoidable quadratic-plus cost.

## Impact

Wrong protected/unprotected counts per managed server and inflated "VM protected by physical" lists — these are headline numbers in the report. Performance degradation on large estates.

## Evidence

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:839-846` —

```csharp
foreach (var p in protectedVms)
{
    if (!countedVMs.Contains(p.Name))
    {
        if (p.Path.StartsWith(c.Name))   // "esx1" matches "esx10/..." paths
        {
            vmCount++;
            protectedCount++;
```

`vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:363` —

```csharp
if (p.name.Contains(v.Name))   // substring, not equality
{
    vmProtectedByPhys.Add(v.Name);
}
```

## Suggested fix

Match on the path *segment*: parse the first path component (`p.Path.Split('\\', '/')[0]`) and compare with `string.Equals(..., StringComparison.OrdinalIgnoreCase)`, or require `StartsWith(c.Name + "\\", Ordinal...)`. For phys/VM correlation use exact (case-insensitive) name equality. Replace `countedVMs` list with a `HashSet<string>` and pre-group VMs by host segment to make the pass linear.
