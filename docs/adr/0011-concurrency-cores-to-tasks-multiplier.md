# ADR 0011: Role-Aware Cores-to-Tasks Multiplier in Concurrency Analysis

* **Status:** Accepted
* **Date:** 2026-03-12
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot; Veeam Help Center sizing docs (v13.0.1.1071)

## Context and Problem Statement

`Invoke-VhcConcurrencyAnalysis` converts available task-cores to a suggested task
count on line 101 of the original script:

```powershell
$NonNegativeCores = EnsureNonNegative($SuggestedTasksByCores * 2)
```

The `* 2` factor is the inverse of `VpProxyCPUPerTask = 0.5` (0.5 cores per VP
Proxy task → 2 tasks per core). This value was correct for the original script's
primary target role (VMware Virtual Proxy) and was carried forward unchanged into
the refactored module without re-evaluation against other role types.

### Incorrect results for GP Proxy and CDP Proxy

| Role | Cores/task | Tasks/core | Hardcoded * 2 result | Correct result |
|---|---|---|---|---|
| VP Proxy | 0.5 | **2.0** | 10 cores × 2 = **20** ✓ | 20 |
| Repo/GW | 0.5 | **2.0** | 10 cores × 2 = **20** ✓ | 20 |
| GP Proxy | 2.0 | **0.5** | 10 cores × 2 = **20** ✗ | **5** |
| CDP Proxy | 4.0 | **0.25** | 10 cores × 2 = **20** ✗ | **2** |

A pure GP Proxy server with 10 available task-cores was reporting 20 suggested
tasks — 4× the correct value. A CDP Proxy server was over-reporting by 8×.

On mixed-role servers (e.g. VP Proxy + GP Proxy), the VP Proxy ratio of 2 tasks/core
remained the governing factor, causing the GP Proxy constraint to be silently ignored.

## Decision Drivers

- **Correctness:** `SuggestedTasks` must reflect the most restrictive role's
  resource ratio so that the output is actionable and does not over-promise capacity.
- **Consistency:** The per-task CPU thresholds (`GpProxyCPUPerTask`,
  `CdpProxyCPUPerTask`) already exist in `VbrConfig.json` and are used for
  `RequiredCores` calculation. Using the same values for the tasks/core multiplier
  ensures internal consistency.
- **Mixed-role correctness:** On a server hosting both VP Proxy and GP Proxy roles,
  the GP Proxy constraint (2 cores/task) is more restrictive. The suggested task
  count must honour the tightest constraint, not the loosest.

## Considered Options

### Option A — Keep hardcoded `* 2` (rejected)

Leave `$SuggestedTasksByCores * 2` unchanged.

**Rejected.** Produces 4× and 8× over-estimates for GP Proxy and CDP Proxy servers
respectively. The error is not marginal — it leads to confident but incorrect
capacity recommendations.

### Option B — Add separate `$SuggestedGPTasks`, `$SuggestedCDPTasks` paths (rejected)

Compute separate suggested-task figures per role type and aggregate.

**Rejected.** The existing `$SuggestedTasksByCores` represents task-cores already
deducted of OS overhead. Splitting per role would require re-partitioning the
available core budget across roles — a larger refactor with no clear boundary
conditions for mixed-role servers sharing a single CPU pool.

### Option C — Derive `$tasksPerCore` from config, take minimum across active roles (chosen)

```powershell
$tasksPerCore = 1.0 / $VPProxyCPUReq   # default: 0.5 cores/task → 2.0 tasks/core
if ((SafeValue $server.Value.TotalGPProxyTasks) -gt 0) {
    $tasksPerCore = [Math]::Min($tasksPerCore, 1.0 / $GPProxyCPUReq)   # 2 cores/task → 0.5
}
if ((SafeValue $server.Value.TotalCDPProxyTasks) -gt 0) {
    $tasksPerCore = [Math]::Min($tasksPerCore, 1.0 / $CDPProxyCPUReq)  # 4 cores/task → 0.25
}
$NonNegativeCores = EnsureNonNegative($SuggestedTasksByCores * $tasksPerCore)
```

`[Math]::Min` across all active role multipliers ensures the most restrictive
(fewest tasks per core) governs. VP Proxy and Repo/GW are included in the default
`$tasksPerCore` because those roles may be active with zero task counts on some
server rows (roles present but idle); checking `TotalGPProxyTasks -gt 0` and
`TotalCDPProxyTasks -gt 0` guards against applying the tighter constraint when
the role contributes no tasks.

**Accepted.**

## Decision

Option C. `$tasksPerCore` is derived from `1.0 / $VPProxyCPUReq` as the default
(covering VP Proxy and Repo/GW, both 0.5 cores/task), then narrowed by `Math::Min`
for each active GP Proxy or CDP Proxy role. The literal `* 2` is removed.

## Consequences

* **Good:** GP Proxy and CDP Proxy servers now report correct `SuggestedTasks`
  values — 4× and 8× lower respectively than the previous over-estimate.
* **Good:** Mixed-role servers (e.g. VP Proxy + GP Proxy) are correctly governed
  by the most restrictive ratio.
* **Good:** VP Proxy-only and Repo/GW-only servers are completely unaffected —
  `1.0 / 0.5 = 2.0`, identical to the former hardcoded `* 2`.
* **Neutral:** The default `$tasksPerCore` is anchored to `$VPProxyCPUReq`
  (0.5 cores/task). If a future threshold change alters that value, the multiplier
  will update automatically — consistent with the config-driven approach used
  throughout the module.
* **Bad:** No automated tests cover `Invoke-VhcConcurrencyAnalysis`. The fix is
  verified by manual arithmetic only. A test covering pure GP Proxy, pure CDP
  Proxy, and mixed VP+GP configurations is acknowledged as a gap.
