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
a job. This has two known gaps:

1. A job that was ever retargeted to a different repository can still have an
   older backup chain physically present on disk under the old repository.
   That chain's restore points are invisible to `GetLastBackup()`, so their
   size is silently excluded from Source/On-Disk GB.
2. There is no visibility into restore points that don't belong to any live
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
  parameterized to return a matching fake job, `$null`, or throw.
- Case: restore point resolves to a job Id → included in that job's
  `$RestorePoints`/`OnDiskGB`.
- Case: `GetSourceJob()` returns `$null` → excluded from every job, no crash.
- Case: `GetSourceJob()` throws → skipped, sweep continues, no crash (mirrors
  ISC-1's per-item try/catch pattern for `.GetJob()`).
- Case: two restore points share a job Id but have different
  `ObjectId`/`CreationTimeUtc` (simulating pre/post-retarget chains) → both
  summed into `OnDiskGB`; only the latest per `ObjectId` feeds `OriginalSize`.

## Data Flow

1. `Get-VhcJob` fetches `$Jobs` (managed + standalone) — unchanged.
2. New: one unscoped `Get-VBRRestorePoint` sweep resolves every restore
   point's owning job via `GetSourceJob()` and buckets by job `Id` into
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
| A job's Id has no entry in `$restorePointsByJob` | `$RestorePoints` stays `@()`; same fallback as today's "no last backup" case |
| Standalone agent job (via `.GetJob()`, ADR 0014) — `GetSourceJob()` resolution unverified | Falls back to `Info.IncludedSize`/0 if unmatched — not a regression vs. today, but needs live validation to confirm it's actually matching (see Open Items) |
| Job with restore points across >1 backup chain (e.g. post-retarget) | All matched chains are summed — an intentional behavior change, surfacing previously invisible disk usage |
| Replica job (`Snapshot`-type restore points) | No `Type` filter applied — included, same as today |
| NAS job | Unaffected — sized via a separate cmdlet/CSV path that never appears in `Get-VBRRestorePoint` output |

## Open Items

- **Empirical validation required before merge** (same practice as ADR 0014):
  confirm `GetSourceJob().Id` correctly resolves and matches `$Job.Id` across
  every job type this script iterates — VMware, Hyper-V, Cloud Director vApp,
  Entra ID Tenant, managed Windows/Linux Agent, standalone agent, Nutanix AHV
  Agent, HPE Morpheus VME Agent, and Replica. Any type where it doesn't
  resolve degrades gracefully to today's fallback (per Error Handling above)
  but should be documented as untested/unsupported by this change rather than
  assumed to work.
- **Performance on very large environments**: one global sweep + N
  `GetSourceJob()` calls (N = total restore points server-wide) replaces N
  `GetLastBackup()` + N scoped `Get-VBRRestorePoint` calls (N = job count).
  For environments with long retention and many objects (e.g. an hourly-point
  job running for months), the global restore-point count can be far larger
  than the job count. Should be measured against a real large environment
  before rollout; no mitigation is designed here beyond the existing
  per-collector error handling.
- **"Old Backup Data" per-repository reporting** (explicitly deferred): the
  unmatched-restore-point bucket this design produces is a natural input for
  a future per-repository "leftover data" metric, but is not surfaced
  anywhere by this design. Tracked as a follow-on, not part of this spec.
- **ADR**: this script's job-sourcing design already has ADR 0014 on record.
  This change should get its own ADR entry once implemented, recording the
  Id-based global-match decision and the "sum across all matched chains"
  rule.
