# Job Sizing: Global Restore-Point Matching by Job Id — Design

**Date:** 2026-08-21
**Status:** Proposed

## Problem

Today `Get-VhcJob.ps1` computes Source Size GB / Est. On Disk GB per job via
`$Job.GetLastBackup()` → `Get-VBRRestorePoint -Backup $LastBackup`
(`Get-VhcJob.ps1:89-92`), then sums `OnDiskGB` over the resulting
`$RestorePoints` and derives `OriginalSize` by grouping restore points by
`ObjectId`, taking the latest `ApproxSize` per VM (`Get-VhcJob.ps1:94-126`).

`GetLastBackup()` returns only the single most-recent backup chain object for
a job. This has three known gaps:

1. **Confirmed root cause of the 0 MB / 0 GB bug this design set out to fix**:
   for policy/plug-in-backed platforms — HPE Morpheus VME Backup, Nutanix AHV
   Backup, and oVirt KVM Backup confirmed live, matching the same restriction
   already documented for Proxmox in the Protected Workloads investigation —
   `Get-VBRRestorePoint -Backup $LastBackup` throws:
   `Cannot get restore points from backup <name>, because it is encrypted or
   created by an enterprise application plug-in.` This exception is inside
   `Get-VhcJob.ps1`'s existing outer per-job `try/catch`
   (`Get-VhcJob.ps1:87-131`), so it's silently swallowed — `$TotalOnDiskGB`
   stays 0 and `$CalculatedOriginalSize` falls back to `$Job.Info.IncludedSize`,
   which is also unpopulated for these platforms, producing exactly the 0 MB
   Source Size / 0 GB Est. On Disk GB shown in the report for HPE Morpheus and
   Nutanix AHV jobs today.
2. A job that was ever retargeted to a different repository can still have an
   older backup chain physically present on disk under the old repository.
   That chain's restore points are invisible to `GetLastBackup()`, so their
   size is silently excluded from Source/On-Disk GB.
3. There is no visibility into restore points that don't belong to any live
   job at all — imported backups, orphaned chains from deleted jobs, or VMs
   dropped from a job's current scope. These consume disk space today with no
   way to see them in the report.

A live-lab query demonstrated that every restore point on the server can be
resolved back to its owning job via `.GetSourceJob()`, and that restore points
with no owning job (blank `SourceJob`) correspond exactly to
orphaned/imported/no-longer-in-scope restore points:

```powershell
$VBRRestorePoints | Select Name, CreationTime,
    @{n='SourceJob'; e={$_.GetSourceJob().Name}},
    @{n='Type'; e={$_.GetSourceJob().TypeToString}}
```

Surfacing those unmatched restore points as a per-repository "Old Backup Data"
metric is a natural next step, but is **explicitly out of scope** for this
design — this design only changes how Source Size GB / Est. On Disk GB get
computed for jobs that exist today.

## Solution

Replace the per-job `GetLastBackup()` + scoped `Get-VBRRestorePoint -Backup`
call with a single global, unscoped `Get-VBRRestorePoint` sweep performed once
per `Get-VhcJob` invocation, before the main per-job loop.

- For each restore point, resolve its owning job via `.GetSourceJob()`
  (defensively try/catch'd per item — same shape as `Get-VhcProtectedNames`'s
  existing per-backup try/catch in `Get-VhcProtectedWorkloads.ps1:63-70`) and
  bucket it into a dictionary keyed by the owning job's `Id`. Restore points
  with no resolvable owning job are left unmatched — not attributed to any
  job, no "Old Backup Data" bucket in this design.
- For policy-driven platforms — managed Windows/Linux Agents, Nutanix AHV
  Agent, HPE Morpheus VME Agent, oVirt KVM Agent — `.GetSourceJob()` resolves
  to a **per-machine child job**, not the top-level policy job that actually
  appears in `$Jobs` (confirmed via live-lab comparison: plain `GetSourceJob()`
  returned `VBR Managed Agents - Windows - WindowsAgent13.usdemo.veeam.local`,
  while the real policy job is `VBR Managed Agents - Windows`). Before keying
  into the dictionary, call `.GetParentJob()` on the resolved job and use its
  result if non-null — this walks up to the real top-level job and still
  exposes an `Id`, so matching stays Id-based throughout with no name
  comparison needed. For job types that are already top-level (VMware,
  Hyper-V, Cloud Director, Entra ID Tenant, standalone/native Agent, Replica),
  this call is a safe no-op — live-lab testing showed identical results
  whether or not it's applied.
- In the main per-job loop, look up `$Job.Id` in this dictionary instead of
  calling `GetLastBackup()`. The downstream math (sum for On-Disk GB,
  latest-per-`ObjectId` for Source Size GB) is unchanged — it already operates
  on a `$RestorePoints` collection regardless of how that collection was
  sourced.
- Matching is done by job **Id**, not Name, so a deleted-and-recreated job with
  an identical name can't have another job's old restore points misattributed
  to it.
- Restore points from multiple distinct backup chains that resolve to the
  same job Id (e.g. before/after a repository retarget) are **summed
  together** — an intentional behavior change that surfaces disk usage
  previously invisible to the report.
- No `Type` filter is applied — Replica jobs' `Snapshot`-type restore points
  continue to flow through unchanged, matching today's behavior.
- NAS jobs are unaffected — they're sized via a separate cmdlet
  (`Get-VBRUnstructuredBackupRestorePoint`, in `Get-VhciNasJob.ps1`) whose
  output never appears in `Get-VBRRestorePoint`.

## Architecture

```
Get-VhcJob.ps1 (Public, modified)
    │
    ├─ $Jobs = Get-VBRJob (+ standalone agent jobs via .GetJob(), ADR 0014) — unchanged
    │
    ├─ NEW: global restore-point sweep (before the main loop)
    │       $allRestorePoints   = Get-VBRRestorePoint             — one call, whole server
    │       $restorePointsByJob = @{}                             — [jobId string] -> ArrayList<RestorePoint>
    │       foreach rp in $allRestorePoints:
    │           try { $sourceJob = $rp.GetSourceJob() } catch { continue }
    │           if ($null -eq $sourceJob) { continue }             — orphaned/imported/out-of-scope, unmatched
    │           try { $parent = $sourceJob.GetParentJob(); if ($parent) { $sourceJob = $parent } } catch {}
    │                                                               — walk up from per-machine child job to real policy job (safe no-op if already top-level)
    │           $restorePointsByJob[$sourceJob.Id.ToString()].Add($rp)
    │
    └─ Main per-job loop (modified)
            $RestorePoints = $restorePointsByJob[$Job.Id.ToString()]   — replaces GetLastBackup()+scoped Get-VBRRestorePoint
            ... rest of loop (OnDiskGB sum, ObjectId-latest ApproxSize sum) — UNCHANGED
```

## Components

### 1. `Get-VhcJob.ps1` — new sweep (inserted before the main loop, around current line 85)

```powershell
$restorePointsByJob = @{}
try {
    $allRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)
    Write-LogFile "Collected $($allRestorePoints.Count) restore points for job-size matching"

    $matched = 0
    foreach ($rp in $allRestorePoints) {
        $sourceJob = $null
        try { $sourceJob = $rp.GetSourceJob() } catch { continue }
        if ($null -eq $sourceJob) { continue }

        # Policy-driven platforms (Managed Agents, Nutanix AHV Agent, HPE
        # Morpheus VME Agent, oVirt KVM Agent) resolve to a per-machine child
        # job here, not the top-level policy job in $Jobs. GetParentJob()
        # walks up to it; safe no-op for job types that are already top-level.
        try {
            $parentJob = $sourceJob.GetParentJob()
            if ($null -ne $parentJob) { $sourceJob = $parentJob }
        } catch {}

        $jobIdKey = $sourceJob.Id.ToString()
        if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
            $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        }
        [void]$restorePointsByJob[$jobIdKey].Add($rp)
        $matched++
    }
    Write-LogFile "Matched $matched of $($allRestorePoints.Count) restore points to a job Id ($($allRestorePoints.Count - $matched) unmatched/orphaned)"
} catch {
    Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
    Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
}
```

If the sweep fails outright, `$restorePointsByJob` stays `@{}` and every job
falls back to today's zero/`IncludedSize` behavior below — degraded, not
fatal.

### 2. `Get-VhcJob.ps1` — main loop (current lines 87-100, modified)

Replace:

```powershell
$LastBackup    = $Job.GetLastBackup()
$RestorePoints = @()
if ($null -ne $LastBackup) {
    $RestorePoints = Get-VBRRestorePoint -Backup $LastBackup
}
```

with:

```powershell
$RestorePoints = @()
$jobIdKey = $Job.Id.ToString()
if ($restorePointsByJob.ContainsKey($jobIdKey)) {
    $RestorePoints = $restorePointsByJob[$jobIdKey]
}
```

Lines 94-131 (the `$TotalOnDiskGB` sum, the `Group-Object ObjectId` → latest →
`ApproxSize` sum, and the `Info.IncludedSize` fallbacks) are unchanged — they
already consume `$RestorePoints` generically, regardless of source.

### 3. Tests — `Get-VhcJob.Tests.ps1` (extend)

Existing stubs already declare a no-op `Get-VBRRestorePoint` (lines 37-39) and
fake jobs with a `GetLastBackup` ScriptMethod (line 132). Add:

- A fake restore point factory with a `GetSourceJob` ScriptMethod,
  parameterized to return a matching fake job, `$null`, or throw. The fake job
  itself needs a `GetParentJob` ScriptMethod, parameterized to return a
  distinct parent fake job, `$null` (already top-level), or throw.
- Case: restore point resolves to a job Id → included in that job's
  `$RestorePoints`/`OnDiskGB`.
- Case: `GetSourceJob()` returns `$null` → excluded from every job, no crash.
- Case: `GetSourceJob()` throws → skipped, sweep continues, no crash (mirrors
  ISC-1's per-item try/catch pattern for `.GetJob()`).
- Case: `GetSourceJob()` returns a child job whose `GetParentJob()` returns a
  *different* job → the restore point is bucketed under the **parent's** Id,
  not the child's, and shows up in that parent job's `$RestorePoints`.
- Case: `GetSourceJob()` returns a job whose `GetParentJob()` returns `$null`
  (already top-level) → bucketed under the original job's Id, unchanged.
- Case: `GetParentJob()` throws → falls back to the original `GetSourceJob()`
  result's Id, sweep continues, no crash.
- Case: two restore points share a job Id but have different
  `ObjectId`/`CreationTimeUtc` (simulating pre/post-retarget chains) → both
  summed into `OnDiskGB`; only the latest per `ObjectId` feeds `OriginalSize`.

## Data Flow

1. `Get-VhcJob` fetches `$Jobs` (managed + standalone) — unchanged.
2. New: one unscoped `Get-VBRRestorePoint` sweep resolves every restore
   point's owning job via `GetSourceJob()`, walks up to the top-level policy
   job via `GetParentJob()` where applicable, and buckets by job `Id` into
   `$restorePointsByJob`.
3. The main per-job loop looks up `$Job.Id` in `$restorePointsByJob` instead
   of calling `GetLastBackup()`.
4. Existing sizing math (sum for On-Disk GB, latest-per-`ObjectId`
   `ApproxSize` sum for Source Size, `Info.IncludedSize` fallback) runs
   unchanged against whichever restore points were found.
5. Unmatched restore points (no resolvable owning job) are dropped — not
   attributed to any job, not surfaced anywhere in this design.

## Error Handling

| Failure | Behavior |
|---|---|
| `Get-VBRRestorePoint` (global sweep) throws | Logged via top-level try/catch; `Add-VhciModuleError`; `$restorePointsByJob` stays empty; every job falls back to `Info.IncludedSize`/0 On-Disk GB (today's existing "no last backup" fallback) |
| `GetSourceJob()` throws for a specific restore point | Caught per-item; that restore point is skipped, sweep continues |
| `GetSourceJob()` returns `$null` | Restore point is unmatched — excluded from every job's totals (orphaned backup, imported backup, or VM no longer in any job's scope) |
| `GetSourceJob()` resolves, but to a per-machine child job (Managed Agents, Nutanix AHV Agent, HPE Morpheus VME Agent, oVirt KVM Agent) | `.GetParentJob()` walk-up resolves to the real top-level policy job's Id before bucketing — confirmed via live-lab comparison across these platforms |
| `.GetParentJob()` throws | Caught; falls back to the original `GetSourceJob()` result's Id (best effort — same outcome as if this refinement didn't exist) |
| `.GetParentJob()` returns `$null` (job is already top-level) | Original `GetSourceJob()` result's Id is used — confirmed safe no-op via live-lab comparison for VMware, Hyper-V, Cloud Director, Entra ID Tenant |
| A job's Id has no entry in `$restorePointsByJob` | `$RestorePoints` stays `@()`; same fallback as today's "no last backup" case |
| Standalone agent job (via `.GetJob()`, ADR 0014) — `GetSourceJob()`/`GetParentJob()` resolution unverified | Falls back to `Info.IncludedSize`/0 if unmatched — not a regression vs. today, but needs live validation to confirm it's actually matching (see Open Items) |
| Job with restore points across >1 backup chain (e.g. post-retarget) | All matched chains are summed — an intentional behavior change, surfacing previously invisible disk usage |
| Replica job (`Snapshot`-type restore points) | No `Type` filter applied — included, same as today |
| NAS job | Unaffected — sized via a separate cmdlet/CSV path that never appears in `Get-VBRRestorePoint` output |

## Validation

Validated against a live lab (18 jobs, VBR v13) using a standalone, read-only
comparison script (`Test-JobSizingRestorePointMatching.ps1`, not part of the
codebase) that runs both the current `GetLastBackup()` logic and the proposed
global-sweep + `GetSourceJob()`/`GetParentJob()` logic side-by-side per job,
with no changes to `Get-VhcJob.ps1` itself.

**Sweep scale**: 5,752 restore points found server-wide; 280 matched to one of
the 18 live jobs, 5,472 unmatched. The unmatched count is far larger than
expected from the earlier ad hoc lab query — this environment has a
substantial amount of restore-point data with no resolvable owning job. That
data is untouched by this design (per the "Old Backup Data" scope decision
above) but is strong real-world support for [#192](https://github.com/VeeamHub/veeam-healthcheck/issues/192),
the deferred "Orphaned Backups" report-section backlog item.

**Per-job results**:

| Outcome | Count | Detail |
|---|---|---|
| Fixed (0/0 → real numbers) | 3 | `HPE Morpheus - Windows - Linux` (HPE Morpheus VME Backup): 0→190 GB Source, 0→72.99 GB On-Disk. `Nutanix AHV - Windows - Linux` (Nutanix AHV Backup): 0→66 GB / 0→8.93 GB. `OVIRT - Linux Backup` (oVirt KVM Backup): 0→25 GB / 0→5.59 GB. All three confirmed the `Get-VBRRestorePoint -Backup` "encrypted or created by an enterprise application plug-in" exception as root cause. |
| Regressed (real numbers → 0/0) | 0 | None. |
| Unchanged | 15 | Includes `VMware - Domain Controller` (245 restore points, matched identically old vs. new — the highest-volume job in the environment, confirming the sweep+match approach scales to a job with hundreds of points) and 5 jobs that have genuinely never produced a single backup/replica (`GetLastBackup()` itself throws `Backup for job ... not found`) — both approaches correctly report 0 On-Disk GB and fall back to `Info.IncludedSize` for Source Size in these cases, confirming that path is unaffected by this change. |

This directly confirms the tier-2 `GetParentJob()` walk-up works as designed:
`HPE Morpheus VME Backup`, `Nutanix AHV Backup`, and `oVirt KVM Backup` all
went from 0 matched restore points (old) to their real counts (6, 2, and 1
respectively) under the new approach, and `VBR Managed Agents -
Windows`/`-Linux` (which also resolve to a per-machine child job via
`GetSourceJob()`, per the Solution section above) matched identically old vs.
new (3 restore points each) — the walk-up doesn't disturb a path that already
worked.

## Open Items

- **Empirical validation** (same practice as ADR 0014): confirmed live for
  VMware, VMware Cloud Director, Hyper-V, managed Windows/Linux Agent,
  Nutanix AHV Agent, HPE Morpheus VME Agent, and oVirt KVM Agent (see
  Validation above). Still unverified: standalone (unmanaged) agent jobs
  (ADR 0014) and the Replica job type's `Snapshot`-type restore points — the
  two replica-type jobs in the test lab (`VMware - Replicas`, `Hyper-V -
  Replicas`) had never produced a single restore point, so the matching path
  itself was never exercised for that type. Any type where it doesn't resolve
  degrades gracefully to today's fallback (per Error Handling above) but
  should be documented as untested/unsupported by this change rather than
  assumed to work.
- **Performance on very large environments**: one global sweep + up to 2×N
  method calls (`GetSourceJob()` plus `GetParentJob()`, N = total restore
  points server-wide) replaces N `GetLastBackup()` + N scoped
  `Get-VBRRestorePoint` calls (N = job count). The validation lab's 5,752
  restore points (18 jobs, one job alone contributing 245 points) completed
  without any noticeable delay running interactively, but that's still a
  small/medium environment — this hasn't been measured against a real
  large environment (tens of thousands of restore points) and no mitigation
  beyond existing per-collector error handling is designed here.
- **"Old Backup Data" per-repository reporting** (explicitly deferred): the
  unmatched-restore-point bucket this design produces is a natural input for
  a future per-repository "leftover data" metric, but is not surfaced
  anywhere by this design. Tracked as a follow-on, not part of this spec.
- **ADR**: this script's job-sourcing design already has ADR 0014 on record.
  This change should get its own ADR entry once implemented, recording the
  Id-based global-match decision and the "sum across all matched chains"
  rule.
