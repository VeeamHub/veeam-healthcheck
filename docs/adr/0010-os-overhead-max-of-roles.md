# ADR 0010: OS Overhead as Max-of-Roles Rather Than Additive Sum

* **Status:** Accepted
* **Date:** 2026-03-11
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot; Veeam Help Center sizing docs (v13.0.1.1071)

## Context and Problem Statement

`Get-VhciServerOsOverhead` returns the fixed CPU and RAM overhead that
`Invoke-VhcConcurrencyAnalysis` subtracts from a server's available resources
before calculating `SuggestedTasks`. Prior to this change, the function
**summed** each active role's overhead:

```
Repo/GW active  → +1 CPU, +4 GB
VP Proxy active → +2 CPU, +2 GB
GP Proxy active → +2 CPU, +4 GB
```

A server hosting all three roles was charged 5 CPU + 10 GB of overhead before
any task-based requirements were considered. On an 8-core/16 GB backup server
(which also doubles as a proxy/repo host in small environments), the combined
fixed overhead of OS roles plus the Backup Server's own 4-CPU/8-GB reservation
exceeded available resources, driving `SuggestedTasks` to 0 via
`EnsureNonNegative` clamping — even though the server was actively running 14
concurrent tasks.

## Decision Drivers

- **Correctness:** The OS runs once per server. Stacking per-role overhead
  multiples the same physical constraint.
- **Veeam sizing guide alignment:** The original script comments read "2 CPU
  core per OS Vi and Hv", "2 CPU core per OS General Purpose Proxy", "1 CPU
  core per OS Repository/Gateway". "Per OS [role type]" describes the minimum
  server configuration *for a server running that role*, not a per-role-install
  surcharge. The Veeam sizing docs confirm this: each role lists a single
  minimum CPU/RAM figure that represents the service-layer floor for a server
  of that type.
- **JeOS note:** Veeam's own documentation states that "Component hardware
  requirements must be added to the Veeam JeOS system requirements." JeOS is
  the OS base (2 cores, 8 GB). Role requirements sit on top of that base as
  per-role service overhead — they do not themselves stack additively when
  multiple roles share a host.
- **Actionability of output:** `SuggestedTasks` clamped to 0 is not a useful
  signal when the underlying cause is formula overflow rather than genuine
  resource exhaustion.

## Considered Options

### Option A — Flat constant (rejected)

Use a single fixed overhead (e.g. 2 CPU, 4 GB) for all servers regardless of
which roles are active.

**Rejected.** Loses the role-type specificity that the sizing guide provides.
A pure GP Proxy server has a different service floor than a pure repository
server; a flat value cannot express both accurately.

### Option B — Additive per-role sum (original/current) (rejected)

Sum overhead for every active role, as the original `Get-VBRConfig.ps1` did.

**Rejected.** Over-counts on multi-role servers. The Veeam sizing guide's
per-role figures are minimums for a server *of that type*, not independent
charges that compound when roles coexist. The additive model causes
`SuggestedTasks` to clamp to 0 on otherwise functional under-resourced servers
rather than reporting a meaningful positive suggestion.

### Option C — Max of active role overheads, independently for CPU and RAM (chosen)

For each active role, compute that role's CPU and RAM overhead. The server's
total overhead is the maximum CPU seen across any active role, and the maximum
RAM seen across any active role. CPU and RAM are maxed independently because
the role with the highest CPU floor need not be the same as the role with the
highest RAM floor.

```powershell
$cpu = [Math]::Max($cpu, $Thresholds.RepoOSCPU)   # 2
$ram = [Math]::Max($ram, $Thresholds.RepoOSRAM)   # 4 GB

$cpu = [Math]::Max($cpu, $Thresholds.VpProxyOSCPU) # 2
$ram = [Math]::Max($ram, $Thresholds.VpProxyOSRAM) # 2 GB
# → cpu=2, ram=4  (not cpu=4, ram=6)
```

**Accepted.**

## Decision

Option C. `Get-VhciServerOsOverhead` replaces `+=` with `[Math]::Max()` for
both CPU and RAM. The return signature `@{ CPU = [int]; RAM = [int] }` is
unchanged; all callers in `Invoke-VhcConcurrencyAnalysis` are unaffected.

### Threshold correction: `RepoOSCPU` 1 → 2

The Veeam Gateway Server sizing page specifies a 2-core minimum. Repositories
and gateways share the `RepoOSCPU` / `RepoOSRAM` keys in `VbrConfig.json`
(they use a combined condition in `Get-VhciServerOsOverhead`). Since the
gateway requires 2 cores as its floor, `RepoOSCPU` is updated from 1 to 2 to
reflect the higher of the two role requirements.

### Sizing guide validation (v13.0.1.1071)

| Key | Value | Source |
|---|---|---|
| `VpProxyOSCPU` | 2 | VMware Proxy: 2 cores minimum |
| `VpProxyOSRAM` | 2 | VMware Proxy: 2 GB RAM base |
| `VpProxyCPUPerTask` | 0.5 | VMware Proxy: +1 core per 2 tasks |
| `VpProxyRAMPerTask` | 1 | VMware Proxy: +1 GB per task |
| `GpProxyOSCPU` | 2 | GP Proxy: 2 cores minimum |
| `GpProxyOSRAM` | 4 | GP Proxy: 4 GB RAM base (NAS/unstructured) |
| `GpProxyCPUPerTask` | 2 | GP Proxy: 2 cores required per additional task |
| `GpProxyRAMPerTask` | 4 | GP Proxy: 4 GB per concurrent task (NAS/unstructured) |
| `RepoOSCPU` | **2** | Gateway: 2 cores minimum (corrected from 1) |
| `RepoOSRAM` | 4 | Repo: 4 GB base; Gateway: 4 GB base |

### Known limitation: `RepoGwRAMPerTask` conflates repo and gateway tasks

`RepoGwRAMPerTask` is currently set to 1 GB, which is correct for standard
backup repositories (Veeam specifies 1 GB RAM per concurrently processed
machine disk). However, the Veeam Gateway Server sizing page specifies **4 GB
RAM per concurrent machine, file share, or object storage source** for gateways
serving NAS/object/cloud workloads.

The current code applies a single `RepoGwRAMPerTask` value to both
`TotalRepoTasks` and `TotalGWTasks` in `Invoke-VhcConcurrencyAnalysis`. Fixing
this under-estimate for gateway tasks requires splitting the shared key into
separate `RepoRAMPerTask` and `GwRAMPerTask` keys, with corresponding logic
changes in `Get-VhciServerOsOverhead`, `Invoke-VhcConcurrencyAnalysis`, and
`VbrConfig.json` key validation. This is deferred to a future ADR.

## Consequences

* **Good:** Matches the original script's intent — "per OS [role type]" denotes
  a per-server service floor, not a per-role-installed surcharge.
* **Good:** Multi-role servers produce meaningful `SuggestedTasks` values
  instead of 0 caused by overflow clamping.
* **Good:** `RepoOSCPU` now aligns with Veeam's published gateway minimum.
* **Good:** Single-role servers are completely unaffected by the aggregation
  change — `max(x)` with one value equals `x`.
* **Neutral:** `RequiredCores` / `RequiredRAM` calculations in
  `Invoke-VhcConcurrencyAnalysis` remain additive by design — overhead per
  role is legitimately additive for sizing *requirements*; only the
  `SuggestedTasks` overhead path changes.

* **Bad:** Under-estimates fixed RAM overhead on multi-role servers that
  combine roles with a 4 GB OS base (GP Proxy, Repo/GW). Specifically:
  may under-report by up to **(N−1) × 2 GB** relative to the Veeam docs
  "subtract 2 GB from each but one role" formula. See Amendment 2026-03-12
  for full analysis. Accepted trade-off: the delta is bounded and the
  max-of-roles approach avoids clamping `SuggestedTasks` to 0 on
  multi-role servers.

## Amendments

### 2026-03-12 — Quantify RAM under-estimation vs Veeam docs formula

**Trigger:** External review noted that the Veeam limitations page
([system_requirements_limitations.html](https://helpcenter.veeam.com/docs/vbr/userguide/system_requirements_limitations.html))
describes a different model for all-in-one installations:

> "For all-in-one installations, you can subtract 2 GB of memory resources
> from each but one role. These 2 GB are allotted to the OS itself, assuming
> each component is installed on the dedicated server."

This is an **additive-minus-deduction** model:

```
Total base RAM = Σ(role base RAM) − 2 GB × (N − 1)
```

**Divergence from max-of-roles:**

The two models agree only when all active roles share the same 2 GB OS base
(e.g. VP Proxy only). They diverge when roles with a 4 GB base
(`GpProxyOSRAM`, `RepoOSRAM`) are co-installed:

| Active roles | Docs model | Max-of-roles | Delta |
|---|---|---|---|
| VP Proxy only (2 GB base) | 2 GB | 2 GB | 0 |
| VP Proxy + Repo (2 + 4 GB) | 4 GB | 4 GB | 0 |
| VP Proxy + Repo + GP Proxy (2 + 4 + 4 GB) | 6 GB | 4 GB | **−2 GB** |
| Repo + GP Proxy (4 + 4 GB) | 6 GB | 4 GB | **−2 GB** |

In general, this implementation may **under-report fixed RAM overhead by up
to (N−1) × 2 GB** on multi-role servers that combine roles with a 4 GB OS
base.

**Why max-of-roles is retained (deliberate simplification):**

Implementing the docs formula correctly would require distinguishing the
"OS share" (always 2 GB, the deductible component) from the "service
minimum" (role-specific non-deductible floor) within each role's base value.
The current threshold keys (`GpProxyOSRAM`, `RepoOSRAM`) encode the full
role base, not the OS share alone. Splitting them would require new threshold
keys, updated validation, and corresponding logic changes across
`Get-VhciServerOsOverhead`, `Invoke-VhcConcurrencyAnalysis`, and
`VbrConfig.json`.

The max-of-roles model is retained as a deliberate simplification. The
under-estimate is bounded — at most 2 GB per additional 4 GB-base role
beyond the first — which is small relative to per-task RAM requirements
(4 GB per GP Proxy task, 1 GB per repo task). Implementing the full docs
formula is deferred unless Veeam clarifies that the "subtract 2 GB" rule
applies to the full role base rather than solely the OS layer.

**No code changes in this amendment.** The "Bad" consequence entry above
is updated to reflect this quantification.

### 2026-03-12 — Extend max-of-roles to BackupServer role

**Trigger:** Code review of `Invoke-VhcConcurrencyAnalysis` identified that the
backup server row was handled by a separate additive block after the overhead
calculation:

```powershell
# old code
$overhead = Get-VhciServerOsOverhead ...   # e.g. 4 GB (max of VP Proxy, Repo, GP Proxy)

$RequiredCores += $BSCPUReq                # e.g. +8 → 10 total fixed CPU
$RequiredRAM   += $BSRAMReq                # e.g. +16 → 20 total fixed RAM
$SuggestedTasksByCores -= $BSCPUReq
$SuggestedTasksByRAM   -= $BSRAMReq
```

**Double-count:** The BackupServer requirement (8 cores / 16 GB for v13) is a
*total* figure representing the sizing floor for a server running the VBR
service. It implicitly includes the same OS layer that `Get-VhciServerOsOverhead`
already accounts for via the per-role overhead. Adding `$BSCPUReq` / `$BSRAMReq`
on top of `$overhead.CPU` / `$overhead.RAM` charges the shared OS twice on backup
server rows.

**Fix:** Extend the max-of-roles principle already established in this ADR.
Introduce `$fixedCPU` / `$fixedRAM` computed as `Max(overhead, BS requirement)`:

```powershell
$fixedCPU = $overhead.CPU
$fixedRAM = $overhead.RAM
if ($serverName -eq $BackupServerName) {
    $fixedCPU = [Math]::Max($fixedCPU, $BSCPUReq)
    $fixedRAM = [Math]::Max($fixedRAM, $BSRAMReq)
}
```

The separate additive `if ($serverName -eq $BackupServerName)` block is removed
entirely. `$fixedCPU` / `$fixedRAM` replace `$overhead.CPU` / `$overhead.RAM`
in all four dependent calculations (`$RequiredCores`, `$RequiredRAM`,
`$SuggestedTasksByCores`, `$SuggestedTasksByRAM`).

**Numerical impact (lab-m01: 8 cores / 15 GB, v13 BS + VP proxy + Repo + GP proxy):**

| | Before | After |
|---|---|---|
| Role OS overhead | max(2,2,2) = **2 CPU**, max(2,4,4) = **4 GB** | same |
| Fixed CPU | 2 + 8 = **10** | max(2, 8) = **8** |
| Fixed RAM | 4 + 16 = **20** | max(4, 16) = **16** |
| Required Cores | task_based + 10 | task_based + 8 (−2) |
| Required RAM (GB) | task_based + 20 | task_based + 16 (−4) |
