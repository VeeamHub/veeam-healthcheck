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
a job. This has four known gaps:

1. **Confirmed root cause of the 0 MB / 0 GB bug this design set out to fix**:
   for on-prem policy/plug-in-backed platforms — HPE Morpheus VME Backup,
   Nutanix AHV Backup, and oVirt KVM Backup confirmed live, matching the same
   restriction already documented for Proxmox in the Protected Workloads
   investigation — `Get-VBRRestorePoint -Backup $LastBackup` throws:
   `Cannot get restore points from backup <name>, because it is encrypted or
   created by an enterprise application plug-in.` This exception is inside
   `Get-VhcJob.ps1`'s existing outer per-job `try/catch`
   (`Get-VhcJob.ps1:87-131`), so it's silently swallowed — `$TotalOnDiskGB`
   stays 0 and `$CalculatedOriginalSize` falls back to `$Job.Info.IncludedSize`,
   which is also unpopulated for these platforms, producing exactly the 0 MB
   Source Size / 0 GB Est. On Disk GB shown in the report for HPE Morpheus and
   Nutanix AHV jobs today.
2. **A second, distinct confirmed root cause for public cloud plug-in
   platforms** (AWS EC2/RDS/FSx/EFS, Azure IaaS/SQL, GCE): here
   `$Job.GetLastBackup()` itself throws `Backup for job <name> does not
   exist` — even for a job with real, current restore points on disk. Unlike
   gap 1, this isn't a restriction on querying an existing backup; the
   backups for these jobs are recorded under **per-machine child backup
   names** (e.g. `Linux-01 Backup`, `Windows-01 Backup`) rather than under
   any backup directly owned by the parent job's own `Id`, so the parent job
   object can't find a backup tied to itself at all. Same downstream effect:
   silently swallowed by the outer `try/catch`, 0 MB / 0 GB reported.
3. A job that was ever retargeted to a different repository can still have an
   older backup chain physically present on disk under the old repository.
   That chain's restore points are invisible to `GetLastBackup()`, so their
   size is silently excluded from Source/On-Disk GB.
4. There is no visibility into restore points that don't belong to any live
   job at all — imported backups, orphaned chains from deleted jobs, or VMs
   dropped from a job's current scope. These consume disk space today with no
   way to see them in the report.

A live-lab query demonstrated that most restore points on the server can be
resolved back to their owning job via `.GetSourceJob()`, and that restore
points with no owning job (blank `SourceJob`) correspond to
orphaned/imported/no-longer-in-scope restore points:

```powershell
$VBRRestorePoints | Select Name, CreationTime,
    @{n='SourceJob'; e={$_.GetSourceJob().Name}},
    @{n='Type'; e={$_.GetSourceJob().TypeToString}}
```

However, for the public cloud platforms in gap 2, `.GetSourceJob()` doesn't
return blank — it **throws**: `Unable to get job for backup: <id>`. A second
live-lab query found a working alternative path for exactly this case:
`.GetBackup().GetParentOrThis().Name` resolves to the real top-level job's
name (not Id — `.GetParentOrThis().GetJob()` also throws for these
platforms), confirmed against the same real job as `Get-VBRJob` returns. See
Solution's tier 2 below.

Surfacing unmatched restore points (gap 4) as a per-repository "Old Backup
Data" metric is a natural next step, but is **explicitly out of scope** for
this design — this design only changes how Source Size GB / Est. On Disk GB
get computed for jobs that exist today.

## Solution

Replace the per-job `GetLastBackup()` + scoped `Get-VBRRestorePoint -Backup`
call with a single global, unscoped `Get-VBRRestorePoint` sweep performed once
per `Get-VhcJob` invocation, before the main per-job loop. The sweep resolves
each restore point to its owning job in two tiers; `Snapshot`-type (Replica)
restore points are excluded from both tiers and sized separately.

### Tier 1 — Id-based, via `GetSourceJob()`

- For each non-`Snapshot`-type restore point, resolve its owning job via
  `.GetSourceJob()` (defensively try/catch'd per item — same shape as
  `Get-VhcProtectedNames`'s existing per-backup try/catch in
  `Get-VhcProtectedWorkloads.ps1:63-70`) and bucket it into a dictionary keyed
  by the owning job's `Id`. Restore points with no resolvable owning job fall
  through to tier 2.
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
  Hyper-V, Cloud Director, Entra ID Tenant, standalone/native Agent), this
  call is a safe no-op — live-lab testing showed identical results whether or
  not it's applied.
- `Snapshot`-type restore points (see below) are skipped before the
  `GetSourceJob()` call entirely — a cheap `.Type` property check with no
  method-call cost. Empirically, 100% of them (5,461/5,461 in the on-prem
  validation lab) throw on `GetSourceJob()` rather than resolving, and
  exceptions at this scale are expensive (measured ~43s of throw cost for
  that population alone in an earlier, unoptimized version of this design).

### Tier 2 — name-based fallback, via `.GetBackup().GetParentOrThis().Name`, GATED

- For restore points tier 1 could not resolve at all (`GetSourceJob()` threw
  or returned `$null`), call `.GetBackup().GetParentOrThis().Name` and match
  the result against `$Jobs` by name.
- **Why this tier exists**: for public cloud plug-in platforms, `.GetSourceJob()`
  doesn't return blank, it throws outright, and there is no `Id`-yielding path
  back to a job at all. `.GetBackup().GetParentOrThis().Name` is the only
  path that resolves. Confirmed live on the cloud lab: all 36 restore points
  for the `Linux-01` job resolved via tier 2 (0 via tier 1), taking it from
  0/0 to 157.00 GB Source / 29.30 GB On-Disk GB.
- **Gated**: a tier-2 name match is only accepted if the resolved job has
  **zero** tier-1 (`Id`-based) matches already; otherwise it's discarded
  ("suppressed"). Gating is a correctness requirement, not just caution —
  live-lab evidence shows restore-point display names are not reliable
  identity across genuinely distinct backup objects: a suppressed restore
  point named `Windows01` (`BackupId 740675ec...`, dated March 2026) and the
  currently tier-1-matched `Windows01` (`BackupId f4f2c7be...`, dated August
  2026) under `HPE Morpheus - Windows - Linux` are two *different* backup
  objects that happen to share a display name — the policy's earlier machine
  pair (`linux01`/`win01-1`) was swapped out for the current one
  (`Rocky01`/`Windows01`). Ungated tier-2 matching would have misattributed
  this stale, decommissioned machine's restore points (8 points, ~117 GB
  On-Disk) onto the currently-active job. Gating trusts tier 1 completely
  once it has any match and only lets tier 2 rescue jobs tier 1 found
  nothing for at all.
- **Known cost of gating**: `Nutanix AHV - Windows - Linux` has 2 tier-2
  matches suppressed (`windows12`/`linux12`, an older chain) alongside 2
  tier-1 matches (same display names, a newer chain). Whether these are the
  same real VMs on a chain that reset, or a decommissioned/recreated pair
  reusing the same names (exactly the HPE Morpheus pattern above), is
  **undecidable from the available VBR API surface** — display-name
  collisions across genuinely different backup objects are proven possible,
  so there's no safe way to distinguish the two cases. Gating accepts this
  occasional, small undercount (here: 8.93 GB reported vs. a possible ~17.86
  GB) in exchange for never misattributing stale data to a live job.

### Snapshot / Replica handling — routed around the sweep entirely

- `Snapshot`-type restore points (VBR's own documentation: "Restore points
  created by replication jobs are represented as snapshots. Restore points
  created by backup jobs are represented as full and increment backup
  files.") never resolve via tier 1, and are excluded from tier 2 as well
  (tier 2 only processes points tier 1 attempted and failed to resolve via
  `GetSourceJob()` — `Snapshot`-type points skip that attempt altogether).
- Instead, jobs where `$Job.TypeToString -like "*Replication*"` are sized via
  the same `GetLastBackup()` + `Get-VBRRestorePoint -Backup` call used today,
  unchanged — this path is already correct for replicas, since replication
  jobs never hit the plug-in/encryption restriction that motivated this
  design (that restriction is specific to certain backup-job platforms).
- Validated behavior-preserving: both replica jobs in the on-prem lab
  (`VMware - Replicas`, `Hyper-V - Replicas`) reported 0 restore points
  before and after this change. **Not yet exercised against a live replica
  chain with real snapshots** — see Open Items.

### Unchanged from the original design

- Matching is done by job **Id** for tier 1 — a deleted-and-recreated job
  with an identical name can't have another job's old restore points
  misattributed to it via tier 1. (Tier 2 is explicitly name-based, which is
  exactly why it needs gating, above.)
- Restore points from multiple distinct tier-1-matched backup chains that
  resolve to the same job Id (e.g. before/after a repository retarget) are
  **summed together** — an intentional behavior change that surfaces disk
  usage previously invisible to the report.
- NAS jobs are unaffected — they're sized via a separate cmdlet
  (`Get-VBRUnstructuredBackupRestorePoint`, in `Get-VhciNasJob.ps1`) whose
  output never appears in `Get-VBRRestorePoint`.

### Out of scope: VMware Cloud Director Source Size double-count

`VMware Cloud Director - vApp Backup`-type jobs' Source Size GB is
double-counted **today, in production, independent of this design**: the
existing `Group-Object ObjectId` → latest → `ApproxSize` sum
(`Get-VhcJob.ps1:104-126`, unchanged by this design) sums the vApp
container's own restore point *and* its 8 child VMs' restore points, and the
container's `ApproxSize` reports the same aggregate the children already sum
to — confirmed live: container `v13` = 1366.97 GB, its 8 children sum to
1366.99 GB, reported total = 2733.96 GB vs. an actual ~1367 GB (matching
VBR's own UI "Total size: 1.33 TB"). This is a bug in how to aggregate a
*correctly-scoped* set of restore points, not in *which* restore points
belong to a job — this design's tier 1/tier 2 matching doesn't touch it.
Tracked as a separate backlog item, not part of this spec.

## Architecture

```
Get-VhcJob.ps1 (Public, modified)
    │
    ├─ $Jobs = Get-VBRJob (+ standalone agent jobs via .GetJob(), ADR 0014) — unchanged
    │
    ├─ NEW: global restore-point sweep (before the main loop)
    │       $allRestorePoints   = Get-VBRRestorePoint             — one call, whole server
    │       $restorePointsByJob = @{}                             — [jobId string] -> ArrayList<RestorePoint>
    │       $tier1MatchedJobIds = HashSet<string>                 — job Ids tier 1 touched at least once
    │
    │       Tier 1 (Id-based), for each rp in $allRestorePoints:
    │           if (rp.Type -eq 'Snapshot') { unresolved.Add(rp); continue }   — routed to Replica handling below
    │           try { $sourceJob = rp.GetSourceJob() } catch { unresolved.Add(rp); continue }
    │           if ($null -eq $sourceJob) { unresolved.Add(rp); continue }
    │           try { $parent = $sourceJob.GetParentJob(); if ($parent) { $sourceJob = $parent } } catch {}
    │           $restorePointsByJob[$sourceJob.Id.ToString()].Add(rp)
    │           $tier1MatchedJobIds.Add($sourceJob.Id.ToString())
    │
    │       Tier 2 (name-based, GATED), for rp in $unresolved (excluding Type=Snapshot):
    │           try { $name = rp.GetBackup().GetParentOrThis().Name } catch { continue }   — still unmatched
    │           $jobId = lookup $name in $jobIdByName; if not found, continue               — still unmatched
    │           if ($tier1MatchedJobIds.Contains($jobId)) { continue }                       — suppressed
    │           $restorePointsByJob[$jobId].Add(rp)
    │
    │       Replica handling, for each job where TypeToString -like "*Replication*":
    │           $restorePointsByJob[job.Id] = Get-VBRRestorePoint -Backup job.GetLastBackup()   — today's method, unchanged
    │
    └─ Main per-job loop (modified)
            $RestorePoints = $restorePointsByJob[$Job.Id.ToString()]   — replaces GetLastBackup()+scoped Get-VBRRestorePoint
            ... rest of loop (OnDiskGB sum, ObjectId-latest ApproxSize sum) — UNCHANGED
```

## Components

### 1. `Get-VhcJob.ps1` — new sweep + Replica handling (inserted before the main loop, around current line 85)

```powershell
$restorePointsByJob = @{}
try {
    $allRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)

    $jobIdByName = @{}
    foreach ($j in @($Jobs)) {
        if (-not $jobIdByName.ContainsKey($j.Name)) { $jobIdByName[$j.Name] = $j.Id.ToString() }
    }

    $tier1MatchedJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
    $unresolved         = [System.Collections.ArrayList]::new()
    $tier1Matched       = 0
    $tier2Matched       = 0

    # Tier 1: Id-based via GetSourceJob() (+ GetParentJob() walk-up for
    # policy-driven per-machine child jobs). Snapshot-type (replication)
    # restore points are skipped here - they never resolve via GetSourceJob()
    # and are sized separately below.
    foreach ($rp in $allRestorePoints) {
        if ($rp.Type -eq 'Snapshot') { [void]$unresolved.Add($rp); continue }

        $sourceJob = $null
        try { $sourceJob = $rp.GetSourceJob() } catch {}
        if ($null -eq $sourceJob) { [void]$unresolved.Add($rp); continue }

        try {
            $parentJob = $sourceJob.GetParentJob()
            if ($null -ne $parentJob) { $sourceJob = $parentJob }
        } catch {}

        $jobIdKey = $sourceJob.Id.ToString()
        if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
            $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        }
        [void]$restorePointsByJob[$jobIdKey].Add($rp)
        [void]$tier1MatchedJobIds.Add($jobIdKey)
        $tier1Matched++
    }

    # Tier 2: name-based fallback, GATED - only accepted if the resolved job
    # has zero tier-1 matches. Display names collide across genuinely
    # different backup objects (confirmed live - see Solution), so this
    # cannot be trusted to override an existing Id-based match.
    foreach ($rp in $unresolved) {
        if ($rp.Type -eq 'Snapshot') { continue }   # handled by Replica loop below

        $jobIdKey = $null
        try {
            $parentName = $rp.GetBackup().GetParentOrThis().Name
            if ($parentName -and $jobIdByName.ContainsKey($parentName)) { $jobIdKey = $jobIdByName[$parentName] }
        } catch {}
        if (-not $jobIdKey) { continue }
        if ($tier1MatchedJobIds.Contains($jobIdKey)) { continue }

        if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
            $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        }
        [void]$restorePointsByJob[$jobIdKey].Add($rp)
        $tier2Matched++
    }

    # Replica jobs: sized via today's proven-correct per-job method, unchanged.
    foreach ($Job in @($Jobs | Where-Object { $_.TypeToString -like "*Replication*" })) {
        $jobIdKey = $Job.Id.ToString()
        $LastBackup = $null
        try { $LastBackup = $Job.GetLastBackup() } catch {}
        $replicaPoints = @()
        if ($null -ne $LastBackup) {
            try { $replicaPoints = @(Get-VBRRestorePoint -Backup $LastBackup -WarningAction SilentlyContinue) } catch {}
        }
        $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        foreach ($rp in $replicaPoints) { [void]$restorePointsByJob[$jobIdKey].Add($rp) }
    }

    Write-LogFile "Restore point matching: $tier1Matched tier-1, $tier2Matched tier-2, $($allRestorePoints.Count - $tier1Matched - $tier2Matched) unmatched/orphaned/snapshot"
} catch {
    Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
    Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
}
```

If the sweep fails outright, `$restorePointsByJob` stays `@{}` (or partially
populated, depending where the failure occurred) and every affected job falls
back to today's zero/`IncludedSize` behavior below — degraded, not fatal.

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

- A fake restore point factory with `GetSourceJob`, `GetBackup`, and `Type`
  properties/ScriptMethods, parameterized to return a matching fake job,
  `$null`, or throw. The fake job itself needs a `GetParentJob` ScriptMethod
  (distinct parent, `$null`, or throw) and a `GetParentOrThis` ScriptMethod
  on the fake backup.
- Case: restore point resolves via tier 1 (`GetSourceJob()`) → included in
  that job's `$RestorePoints`/`OnDiskGB`.
- Case: `GetSourceJob()` returns `$null` → falls through to tier 2.
- Case: `GetSourceJob()` throws → falls through to tier 2 (mirrors ISC-1's
  per-item try/catch pattern for `.GetJob()`).
- Case: `GetSourceJob()` returns a child job whose `GetParentJob()` returns a
  *different* job → the restore point is bucketed under the **parent's** Id,
  not the child's.
- Case: `GetSourceJob()` returns a job whose `GetParentJob()` returns `$null`
  (already top-level) → bucketed under the original job's Id, unchanged.
- Case: `GetParentJob()` throws → falls back to the original `GetSourceJob()`
  result's Id.
- Case: `.Type -eq 'Snapshot'` → skipped by both tiers; not present in
  `$restorePointsByJob` via the sweep at all.
- Case: `GetSourceJob()` throws, `GetBackup().GetParentOrThis().Name`
  resolves to a job with **zero** tier-1 matches → included via tier 2.
- Case: `GetSourceJob()` throws, `GetBackup().GetParentOrThis().Name`
  resolves to a job that **already has** a tier-1 match → suppressed, not
  included (this is the gating rule under test).
- Case: Replica-type job (`TypeToString -like "*Replication*"`) →
  `$restorePointsByJob` for it is populated via `GetLastBackup()` +
  `Get-VBRRestorePoint -Backup`, not via tier 1/2, even if some of its
  restore points would otherwise resolve.
- Case: Replica-type job whose `GetLastBackup()` throws (never run) → falls
  back to `Info.IncludedSize`/0, same as today.
- Case: two restore points share a job Id (via tier 1) but have different
  `ObjectId`/`CreationTimeUtc` (simulating pre/post-retarget chains) → both
  summed into `OnDiskGB`; only the latest per `ObjectId` feeds `OriginalSize`.

## Data Flow

1. `Get-VhcJob` fetches `$Jobs` (managed + standalone) — unchanged.
2. New: one unscoped `Get-VBRRestorePoint` sweep runs tier 1 (`GetSourceJob()`
   + `GetParentJob()` walk-up, skipping `Snapshot`-type points) over every
   restore point, then tier 2 (gated, name-based fallback via
   `GetBackup().GetParentOrThis().Name`) over whatever tier 1 didn't resolve,
   bucketing results by job `Id` into `$restorePointsByJob`.
3. Replica-type jobs (`TypeToString -like "*Replication*"`) have their entry
   in `$restorePointsByJob` populated separately via `GetLastBackup()` +
   `Get-VBRRestorePoint -Backup` — today's method, unchanged.
4. The main per-job loop looks up `$Job.Id` in `$restorePointsByJob` instead
   of calling `GetLastBackup()` (except for Replica jobs, which already used
   that lookup result from step 3).
5. Existing sizing math (sum for On-Disk GB, latest-per-`ObjectId`
   `ApproxSize` sum for Source Size, `Info.IncludedSize` fallback) runs
   unchanged against whichever restore points were found.
6. Restore points still unmatched after both tiers (no resolvable owning job,
   or a tier-2 candidate that was suppressed) are dropped — not attributed to
   any job, not surfaced anywhere in this design.

## Error Handling

| Failure | Behavior |
|---|---|
| `Get-VBRRestorePoint` (global sweep) throws | Logged via top-level try/catch; `Add-VhciModuleError`; `$restorePointsByJob` stays empty; every job falls back to `Info.IncludedSize`/0 On-Disk GB (today's existing "no last backup" fallback) |
| `GetSourceJob()` throws or returns `$null` for a specific restore point | Falls through to tier 2 |
| `.Type -eq 'Snapshot'` | Skipped by tier 1 (no `GetSourceJob()` attempt) and tier 2; sized via the Replica per-job path instead (see below) |
| `GetSourceJob()` resolves, but to a per-machine child job (Managed Agents, Nutanix AHV Agent, HPE Morpheus VME Agent, oVirt KVM Agent) | `.GetParentJob()` walk-up resolves to the real top-level policy job's Id before bucketing — confirmed via live-lab comparison across these platforms |
| `.GetParentJob()` throws | Caught; falls back to the original `GetSourceJob()` result's Id |
| `.GetParentJob()` returns `$null` (job is already top-level) | Original `GetSourceJob()` result's Id is used — confirmed safe no-op via live-lab comparison for VMware, Hyper-V, Cloud Director, Entra ID Tenant |
| Tier 2: `GetBackup()`/`GetParentOrThis()` throws, or the resolved name isn't in `$Jobs` | Restore point stays unmatched — excluded from every job's totals |
| Tier 2: name resolves, but that job already has ≥1 tier-1 match | Suppressed — discarded rather than attributed, since display names are not reliable identity across distinct backup objects (see Solution) |
| A job's Id has no entry in `$restorePointsByJob` | `$RestorePoints` stays `@()`; same fallback as today's "no last backup" case |
| Standalone agent job (via `.GetJob()`, ADR 0014) — tier 1/2 resolution unverified | Falls back to `Info.IncludedSize`/0 if unmatched — not a regression vs. today, but needs live validation to confirm it's actually matching (see Open Items) |
| Job with restore points across >1 tier-1-matched backup chain (e.g. post-retarget) | All matched chains are summed — an intentional behavior change, surfacing previously invisible disk usage |
| Replica job (`Snapshot`-type restore points) | Sized via `GetLastBackup()` + `Get-VBRRestorePoint -Backup`, same call production uses today — not routed through tier 1/2 at all |
| NAS job | Unaffected — sized via a separate cmdlet/CSV path that never appears in `Get-VBRRestorePoint` output |

## Validation

Validated against two live labs (VBR v13) using a standalone, read-only
comparison script (`Test-JobSizingRestorePointMatching.ps1`, not part of the
codebase) that runs both the current `GetLastBackup()` logic and the proposed
sweep side-by-side per job, with no changes to `Get-VhcJob.ps1` itself.

### On-prem lab (18 jobs, 5,752 restore points) — exercises tier 1

**Sweep scale**: 280 restore points matched via tier 1, 0 via tier 2, 10
tier-2 matches suppressed by gating, 5,461 `Snapshot`-type points routed to
Replica handling, 1 point genuinely unmatched by either tier. The 5,461
`Snapshot`-type points don't belong to either of this lab's two current
replica jobs (both report 0 restore points via `GetLastBackup()`, meaning
neither has ever completed a replication cycle under its current
configuration) — since only replication jobs produce `Snapshot`-type points,
these are near-certainly orphaned chains from one or more **deleted**
replica jobs, not data this design's replica handling could ever attribute
correctly. Real-world support for
[#192](https://github.com/VeeamHub/veeam-healthcheck/issues/192), the
deferred "Orphaned Backups" report-section backlog item.

**Per-job results** (7.59x old method's runtime — 18.71s vs. 2.47s;
17.56s of that is the sweep, dominated by the one-time
`Get-VBRRestorePoint` fetch):

| Outcome | Count | Detail |
|---|---|---|
| Fixed (0/0 → real numbers) | 3 | `HPE Morpheus - Windows - Linux`: 0→190 GB Source, 0→72.99 GB On-Disk. `Nutanix AHV - Windows - Linux`: 0→66 GB / 0→8.93 GB. `OVIRT - Linux Backup`: 0→25 GB / 0→5.59 GB. All three confirmed the `Get-VBRRestorePoint -Backup` "encrypted or created by an enterprise application plug-in" exception as root cause, and all three fixed by **tier 1 alone** — tier 2 matched zero restore points in this lab. |
| Regressed (real numbers → 0/0) | 0 | None. |
| Unchanged | 15 | Includes `VMware - Domain Controller` (245 restore points, matched identically old vs. new — the highest-volume job in the environment) and jobs that have genuinely never produced a single backup/replica (`GetLastBackup()` itself throws) — both approaches correctly report 0/`Info.IncludedSize`. |

Identical outcome across three separate runs of the current script (behavior-
preserving through the `Type=Snapshot` skip and gating refinements).

### Cloud lab (18 jobs, 40 restore points) — exercises tier 2

**Sweep scale**: 4 restore points matched via tier 1 (the lab's two VMware
jobs), 36 matched via tier 2, 0 suppressed, 0 unmatched.

**Per-job results** (4.17x old method's runtime — 1.27s vs. 0.31s):

| Outcome | Count | Detail |
|---|---|---|
| Fixed (0/0 → real numbers) | 1 | `Linux-01` (Azure IaaS Backup): 0→157.00 GB Source, 0→29.30 GB On-Disk, 36 restore points — all 36 resolved via tier 2 (`GetSourceJob()` threw for every one; `GetLastBackup()` also threw `Backup for job Linux-01 does not exist`, confirming gap 2). This is the exact motivating scenario for tier 2. |
| Regressed | 0 | None. |
| Unchanged | 17 | Includes 15 AWS/Azure/GCE policy jobs with no restore points at all (both approaches correctly report 0), and 2 VMware jobs matched identically old vs. new via tier 1. |

Both labs together: 3 jobs fixed by tier 1 alone, 1 job fixed by tier 2
alone, 0 regressions across 36 real jobs — each tier earns its place in a
different environment, and neither tier alone would have fixed both.

## Open Items

- **Empirical validation** (same practice as ADR 0014): confirmed live for
  tier 1 (VMware, VMware Cloud Director, Hyper-V, managed Windows/Linux
  Agent, Nutanix AHV Agent, HPE Morpheus VME Agent, oVirt KVM Agent) and tier
  2 (AWS/Azure/GCE public cloud plug-in jobs) — see Validation above. Still
  unverified: standalone (unmanaged) agent jobs (ADR 0014), and the Replica
  per-job path against a live replica chain with real snapshots (both
  replica jobs in the on-prem lab have never produced a restore point, so
  that path's *result* was validated as behavior-preserving but never
  actually exercised with data flowing through it).
- **Performance — measured across both labs**: on-prem, 7.59x old (18.71s vs.
  2.47s; the one-time restore-point fetch is ~84% of the sweep and isn't
  something this design can optimize further). Cloud, 4.17x old (1.27s vs.
  0.31s). An earlier, unoptimized version of tier 1 (no `Type=Snapshot`
  short-circuit) was 20.33x old on the on-prem lab — almost entirely
  `GetSourceJob()` throwing for `Snapshot`-type points that can never
  resolve; the short-circuit fixed that without changing any job's reported
  numbers. Still unmeasured against a real large environment (tens of
  thousands of restore points), and no mitigation beyond existing
  per-collector error handling is designed here.
- **Tier-2 gating trade-off** (see Solution): confirmed correct for HPE
  Morpheus (a genuine machine swap, proven by differing `BackupId`s behind
  an identical display name). `Nutanix AHV - Windows - Linux`'s 2 suppressed
  restore points are undecidable from the API — could be a legitimate second
  chain for the same VMs, could be the same stale-name pattern as HPE
  Morpheus. No further investigation is expected to resolve this; it's
  accepted as the cost of the gating rule.
- **Restore-point accounting gap (on-prem lab)**: tier 1 matched 280 restore
  points, but summing `NewRestorePointCount` across all 18 jobs (plus
  standalone agent jobs, which this lab has zero of) totals only 277 — 3
  tier-1-matched restore points resolve to job Ids that don't appear
  anywhere in `$Jobs`. Not a correctness problem for any job's reported
  numbers (those points aren't attributed to any job either way), but the
  cause isn't identified — worth a look before this ships, since it means
  `Get-VBRJob` (+ the standalone-agent supplement) doesn't cover every job Id
  `GetSourceJob()` can return.
- **VMware Cloud Director Source Size double-count** (see Solution, "Out of
  scope"): confirmed pre-existing in production, unrelated to this design's
  matching logic. Track as its own backlog item.
- **"Old Backup Data" per-repository reporting** (explicitly deferred): the
  unmatched-restore-point bucket this design produces is a natural input for
  a future per-repository "leftover data" metric, but is not surfaced
  anywhere by this design. Tracked as a follow-on
  ([#192](https://github.com/VeeamHub/veeam-healthcheck/issues/192)), not
  part of this spec.
- **ADR**: this script's job-sourcing design already has ADR 0014 on record.
  This change should get its own ADR entry once implemented, recording the
  Id-based tier-1 / gated name-based tier-2 decision and the "sum across all
  tier-1-matched chains" rule.
