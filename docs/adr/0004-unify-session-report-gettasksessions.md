# ADR 0004: Use GetTaskSessions() for All VBR Versions in Session Report

* **Status:** Accepted
* **Date:** 2026-02-26
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot
* **Supersedes (in part):** ADR 0002 (Implementation Overview, two-path architecture)

## Context and Problem Statement

ADR 0002 specified a two-path architecture in `Get-VhcSessionReport`:

- **VBR >= 13:** Iterate `$script:AllBackupSessions` directly (session-level rows).
- **VBR < 13:** Call `GetTaskSessions()` on each session (task-level, per-VM rows).

During implementation and live testing on both VBR v12 and v13, the `>=13` session-level path
was found to have a granularity bug: `Get-VBRBackupSession` on v13 returns one object per
**job run** (e.g. `VMware - Backup to DC220.E3 (Incremental)`), not one object per **VM**.
The `Name` field on these objects is the job run name, not the VM hostname.

This produced incorrect report data on v13:
- `VMName` column: job run names instead of actual VM hostnames
- `ProcessingMode` column: always empty (the property does not exist at session level)
- `BottleneckDetails`: uses `Load:` log prefix (session level) rather than `Busy:` (task level)

Live testing confirmed that `GetTaskSessions()` works correctly on **both** v12 and v13,
returning `CBackupTaskSession` objects with actual VM names and task-level log access.

## Decision

Use `GetTaskSessions()` for **all VBR versions**. Remove the `if ($VBRVersion -ge 13)` branch
and the `-VBRVersion` parameter from `Get-VhcSessionReport` entirely.

## Rationale

- `GetTaskSessions()` returns per-VM rows on both v12 and v13 (confirmed live).
- `VMName` becomes the actual VM hostname on v13 (was job-run name before).
- `ProcessingMode` is now populated via log parsing on v13 (was always empty before).
- `BottleneckDetails` values are identical between the two levels (same underlying data,
  different prefix: `Load:` at session level vs `Busy:` at task level, both stripped before
  CSV write). See ADR 0003 for the log-parsing decision.
- The `-VBRVersion` parameter was the only reason the branch existed; removing it simplifies
  the function signature to match other module functions that need no version awareness.

## Consequences

### Positive
- `VMName` column now contains actual VM hostnames on v13 (previously job-run names).
- `ProcessingMode` column now populated on v13 (previously always empty).
- Single code path for all VBR versions; no branching logic to maintain.
- `-VBRVersion` parameter removed from `Get-VhcSessionReport` — caller (`Get-VBRConfig.ps1`)
  no longer needs to pass it.

### Neutral
- `BottleneckDetails` string format on v13 changes from `Load:` to `Busy:` prefix source,
  but the prefix is stripped before writing to CSV. The actual value in the CSV is unchanged.
- `BackupSize`/`DataSize` on v13 are read from `$task.JobSess.BackupStats` (same session-level
  aggregate as before, just accessed via the task's parent-session reference).
- Row count for single-VM jobs is unchanged (1:1). For multi-VM jobs on v13, the unified path
  will produce more rows (one per VM instead of one per job run) — this is correct behaviour.

### Negative
- None identified.

## Validation

Live comparison test (`Test-SessionUnification.ps1`) was run on VBR v13:
- Row counts matched for this single-VM test environment (1 VM per job).
- `VMName`: CURRENT showed job-run names; UNIFIED showed `TestVM01` / `TestVM01-LFuO`.
- `ProcessingMode`: CURRENT empty for all rows; UNIFIED showed `nbd` for VMware backup jobs.
- `BottleneckDetails`: identical numeric values across both paths.

Test script deleted after validation (see commit history for reference).
