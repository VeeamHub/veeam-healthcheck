# ADR 0021: Global Restore-Point Sweep with Tiered (Id-Based + Gated Name-Based) Job Matching Replaces Per-Job GetLastBackup()

* **Status:** Proposed
* **Date:** 2026-08-21
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** Claude Code (design, empirical validation across 4 live labs)

## Context and Problem Statement

`Get-VhcJob.ps1` computes Source Size GB / Est. On Disk GB per job via
`$Job.GetLastBackup()` → `Get-VBRRestorePoint -Backup $LastBackup`
(`Get-VhcJob.ps1:89-92`). `GetLastBackup()` returns only the single
most-recent backup chain object for a job, and this call path fails
outright for whole classes of jobs:

1. **On-prem policy/plug-in-backed platforms** — HPE Morpheus VME Backup,
   Nutanix AHV Backup, oVirt KVM Backup, Proxmox Backup — where
   `Get-VBRRestorePoint -Backup $LastBackup` throws `Cannot get restore
   points from backup <name>, because it is encrypted or created by an
   enterprise application plug-in.` The exception is inside the existing
   outer per-job `try/catch`, so it's silently swallowed: 0 MB Source Size /
   0 GB Est. On Disk GB reported.
2. **Public cloud plug-in platforms** (AWS EC2/RDS/FSx/EFS, Azure IaaS/SQL,
   GCE) — where `$Job.GetLastBackup()` itself throws `Backup for job <name>
   does not exist`, even when the job has real, current restore points.
   These jobs record backups under per-machine child backup names (e.g.
   `Linux-01 Backup`) rather than under any backup owned by the parent
   job's own `Id`, so the parent job object can never find a backup tied to
   itself. Same downstream effect: 0/0 reported.

Both failure modes produce a plausible-looking 0/0 with no visible error in
the report — the exact kind of silent wrongness a health-check tool exists
to avoid.

## Considered Options

### Option A — Per-job, `Job.GetObjectsInJob()` → `ObjectId` → `Get-VBRRestorePoint -ObjectId` ("Forward")

A documented, scoped `Get-VBRRestorePoint` parameter. Candidate advantages:
no global unscoped fetch, no reverse-matching heuristics, might sidestep
the `-Backup` "encrypted or plug-in" restriction.

**Rejected.** Confirmed unsafe across two independent live labs whenever two
jobs protect the same object — `Get-VBRRestorePoint -ObjectId` returns
every restore point for that object across *any* job that ever touched it,
with no way to filter to "produced by this job":
- On-prem: duplicated `Hyper-V - Windows/Linux`'s exact numbers onto
  `Hyper-V - Replicas`, and `VMware - HPE - BfSS`'s exact On-Disk figure
  onto `VMware - HPE - Snapshot Only`.
- Cloud lab: `VMs DIrect to Vault` (13.03 GB) and `VMs for Direct Restore to
  Azure / AWS` (12.74 GB) — two distinct, real jobs — collapsed to 0.00 GB
  and 25.77 GB, where 25.77 = 13.03 + 12.74 exactly. Forward pooled both
  jobs' restore points onto one and zeroed the other.
- Silently returned 0/0 for `VBR Managed Agents - Linux` with no error at
  all, and found only 1 of 9 real objects for a VMware Cloud Director vApp
  job (`GetObjectsInJob()` returned the vApp container, not its nested
  VMs).
- Not reliably faster either: 9.6s vs. the sweep's 17.35s on the on-prem
  lab (5,752 restore points / 18 jobs), but 6.52s vs. the sweep's 1.27s on
  the cloud lab (40 restore points / 18 jobs) — Forward's cost scales with
  job count × per-job round-trip, not restore-point volume, so its
  apparent speed advantage was an artifact of one lab's ratio, not a
  property of the approach.

### Option B — Global sweep, Id-based matching only (`GetSourceJob()` + `GetParentJob()` walk-up), no name-based fallback

Resolves every restore point's owning job via `.GetSourceJob()`, walking up
per-machine child jobs (policy-driven platforms) via `.GetParentJob()`.

**Rejected as insufficient alone.** Fixes the on-prem plug-in platforms
(confirmed: HPE Morpheus, Nutanix AHV, oVirt KVM, Proxmox all go from 0/0
to real numbers), but does nothing for the public cloud platforms —
`.GetSourceJob()` doesn't return blank for these, it **throws**
(`Unable to get job for backup: <id>`), and there is no `Id`-yielding path
back to a job at all.

### Option C — Global sweep with tiered matching (chosen)

## Decision

Replace the per-job `GetLastBackup()` + scoped `Get-VBRRestorePoint -Backup`
call with a single global, unscoped `Get-VBRRestorePoint` sweep, resolved
in two tiers, with `Snapshot`-type (Replica) restore points routed around
both tiers entirely. (Whether the sweep runs at all is a separate decision,
gated behind a performance check — see [ADR 0022](0022-allowlist-gate-for-restore-point-sweep.md).)

**Tier 1 — Id-based, via `GetSourceJob()`:** for each non-`Snapshot`
restore point, resolve via `.GetSourceJob()`, then `.GetParentJob()` if
non-null (walks a per-machine child job up to the real top-level policy
job for Managed Agents, Nutanix AHV, HPE Morpheus, oVirt KVM — confirmed
live; safe no-op for already-top-level types). **The resolved `Id` must
then be validated against a `HashSet` built from `$Jobs`'s own Ids** — a
non-null `GetSourceJob()` result is not sufficient evidence of a match.
Confirmed live: Backup Copy jobs expose their restore points through a
per-source-job/per-VM child object (e.g. `GetSourceJob().Name` returning
`Backup Copy - VMware to Vault\VMware - Backup to Vault Direct`) whose `Id`
was never a member of `$Jobs`, and `.GetParentJob()` either returns that
same child back or walks up only one level to another child — never
reaching the real copy job's own `Id`. Without this check, tier 1 "succeeds"
by bucketing the restore point under a key nothing ever looks up, silently
losing the data and pre-empting tier 2's chance to resolve it correctly.

**Tier 2 — name-based fallback, via `.GetBackup().GetParentOrThis().Name`,
GATED:** for restore points tier 1 could not resolve at all (threw,
returned `$null`, or resolved to an unknown Id), call
`.GetBackup().GetParentOrThis().Name` and match against `$Jobs` by name.
This is the only path that resolves for public cloud plug-in jobs and for
Backup Copy jobs whose per-source child object has no Id-based route back
to the real job. **A tier-2 match is only accepted if the resolved job has
zero tier-1 matches already** — otherwise it's discarded ("suppressed").
This is a correctness requirement, not caution: live-lab evidence proves
restore-point display names are not reliable identity across distinct
backup objects. A suppressed restore point named `Windows01`
(`BackupId 740675ec...`) and the currently tier-1-matched `Windows01`
(`BackupId f4f2c7be...`) under `HPE Morpheus - Windows - Linux` are two
different backup objects that happen to share a display name — the
policy's earlier machine pair (`linux01`/`win01-1`) was swapped out for the
current one (`Rocky01`/`Windows01`). Ungated matching would have
misattributed ~117 GB of a decommissioned machine's data onto the
currently-active job. The accepted cost: `Nutanix AHV - Windows - Linux`
has 2 suppressed restore points that *might* be a legitimate second chain
for the same VMs — genuinely undecidable from the available VBR API
surface, since the HPE Morpheus case proves a name match alone can't tell
the two scenarios apart.

**Snapshot / Replica — routed around the sweep entirely:** `Snapshot`-type
restore points never resolve via `GetSourceJob()` (100% throw rate,
5,461/5,461 in the on-prem lab — exceptions at this scale measured at ~43s
for that population alone) and are excluded from tier 2 as well. Instead,
jobs where `TypeToString -like "*Replication*"` are sized via the same
`GetLastBackup()` + `Get-VBRRestorePoint -Backup` call used today,
unchanged — already correct for replicas, since replication jobs never hit
the plug-in/encryption restriction that motivated this design.

Restore points from multiple distinct tier-1-matched backup chains that
resolve to the same job Id (e.g. before/after a repository retarget) are
summed together — an intentional behavior change surfacing disk usage
previously invisible to the report.

## Rationale

- **Id-based matching is the safe default; name-based matching must be
  gated.** `Id` survives a job rename; a display name does not survive a
  machine swap. Tier 1 is trusted unconditionally once it has any match;
  tier 2 only rescues jobs tier 1 found nothing for at all.
- **A resolved `Id` is only trustworthy if it's a member of `$Jobs`.**
  `GetSourceJob()`/`GetParentJob()` can return a real, non-null object that
  still isn't one of the jobs this report is sizing (Backup Copy's
  per-source/per-VM children). Treating "non-null" as "match" is the
  specific bug that caused two Backup Copy jobs to silently regress to 0/0
  in testing before this check was added.
- **Tape backups are a permanent, expected unmatched category, not a gap.**
  VBR names tape backups `<source job name> on Tape` (confirmed live in the
  console's Tape view); they're real backup records with no corresponding
  `Get-VBRJob` entry. Tier 2's name lookup correctly can't match them, and
  they fall to unmatched — the same treatment as any other orphaned or
  imported restore point.

## Consequences

### Positive
- Fixes the confirmed 0/0 bug for HPE Morpheus VME, Nutanix AHV, oVirt KVM,
  and Proxmox Backup jobs (tier 1), and for public cloud plug-in jobs
  (AWS/Azure/GCE, tier 2) and Backup Copy jobs whose `-Backup` scoping
  resolves to a per-source child object (tier 2, via the `$knownJobIds`
  check).
- Zero regressions across 4 live labs, 56 jobs tested: `VMware - Domain
  Controller` (245 restore points, the highest-volume job tested) and every
  previously-correct job matched old exactly.
- Surfaces previously-invisible multi-chain disk usage (post-retarget
  scenarios) for tier-1-matched jobs.

### Negative
- Real, measured performance cost when the sweep runs — see
  [ADR 0022](0022-allowlist-gate-for-restore-point-sweep.md) for how this
  is contained.
- Accepted undercount risk: `Nutanix AHV - Windows - Linux`'s 2
  tier-2-suppressed restore points may be legitimate additional history for
  the same VMs, discarded because there's no safe way to distinguish that
  from the HPE Morpheus stale-machine pattern.
- The Replica per-job path is validated as behavior-preserving (both
  replica jobs in the on-prem lab reported 0 restore points before and
  after this change) but has never been exercised against a live replica
  chain with real snapshots.
- `VMware Cloud Director - vApp Backup` jobs' Source Size double-count
  (the vApp container's own restore point double-counts against its
  children's `ApproxSize`, filed as
  [#193](https://github.com/VeeamHub/veeam-healthcheck/issues/193)) is
  unaffected by this change — it's a pre-existing aggregation bug in a
  correctly-scoped restore-point set, not a matching bug this design
  addresses.

## Validation

Validated via a standalone, read-only comparison script
(`Test-JobSizingRestorePointMatching.ps1`, not part of the codebase) run
against 4 live VBR v13 labs, 56 jobs total, with no changes to
`Get-VhcJob.ps1` itself:

| Lab | Jobs | Restore points | Result |
|---|---|---|---|
| On-prem | 18 | 5,752 | Tier 1 alone fixed HPE Morpheus, Nutanix AHV, oVirt KVM; 0 regressions; 15 unchanged |
| Cloud | 18 | 40 | Tier 2 alone fixed `Linux-01` (0/0 → 157.00 GB / 29.30 GB, 36 restore points); 0 regressions |
| Agent | 4 | 12 | Confirmed standalone (ADR 0014) and managed agent jobs resolve via tier 1 identically to today |
| Backup Copy | 16 | 112 | Confirmed the `$knownJobIds` fix (2 Backup Copy jobs matched old exactly after the fix); confirmed Proxmox as a 4th tier-1 platform; confirmed `web01-schedule-backups` as a 2nd independent tier-2 case |

Full detail, including the HPE Morpheus `BackupId` evidence and the tape-
backup naming confirmation (cross-checked against the live VBR console), is
recorded in
[`docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md`](../superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md).
