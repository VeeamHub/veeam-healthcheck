# Veeam Health Check (vHC)

A Windows utility that collects configuration and session data from Veeam
Backup & Replication (VBR) and Veeam Backup for Microsoft 365 (VB365) and
compiles it into a configuration health-check report.

## Language

**Job**:
A configured backup, replication, or backup-copy task in VBR (`CBackupJob`,
returned by `Get-VBRJob`). Represents current configuration, not history.
_Avoid_: Task

**Backup**:
The chain object holding a Job's captured data over time (`CBackup`,
returned by `Get-VBRBackup` or `Job.GetLastBackup()`). A single Job can
accumulate more than one Backup over its lifetime — e.g. an older chain
left behind after the Job was retargeted to a different repository.
_Avoid_: Chain

**Restore Point**:
A single point-in-time recovery point within a Backup (`COib`, returned by
`Get-VBRRestorePoint`). Its `Type` is either Full/Increment (produced by
backup Jobs) or Snapshot (produced by replication Jobs) — never both.
_Avoid_: Recovery point, oib

**Policy Job**:
A single VBR Job that protects multiple machines under one policy
configuration — Managed Agent, Nutanix AHV, HPE Morpheus VME, and oVirt KVM
jobs all follow this shape. `Get-VBRJob` returns the Policy Job itself;
each protected machine's own Restore Points resolve (via `GetSourceJob()`)
to a separate per-machine Child Job, not the Policy Job.
_Avoid_: Managed job

**Child Job**:
A per-machine (or, for Backup Copy Jobs, per-source-job) object that
`GetSourceJob()` can resolve a Restore Point to, distinct from the Policy
Job or Backup Copy Job an administrator actually configured and that
`Get-VBRJob` returns. A Child Job's `Id` is not always reachable from its
parent's `Id` via `GetParentJob()` — confirmed reachable for Policy Job
platforms, confirmed *not* reachable for Backup Copy.
_Avoid_: Sub-job

**Standalone Agent Job**:
An unmanaged Veeam Agent job, not returned by `Get-VBRJob` — sourced
instead via `Get-VBRBackup | ?{IsAgentStandaloneJob} | .GetJob()`
([ADR 0014](docs/adr/0014-standalone-agent-jobs-via-getbackup-getjob.md)).
_Avoid_: Unmanaged job

**Orphaned Restore Point**:
A Restore Point with no resolvable owning Job — from a deleted job, an
imported backup, or a machine removed from a Policy Job's current scope.
Distinct from a Tape Backup's restore points, which are unmatched for a
different, expected reason (see below).
_Avoid_: Unmatched restore point (describes the symptom; use only when the
cause is genuinely unknown)

**Tape Backup**:
A Backup written to tape media, named by VBR as `<source job name> on
Tape` (e.g. `VMware - Backup to Vault Direct on Tape`). A real, queryable
Backup record, but with no corresponding `Get-VBRJob` entry — its Restore
Points are permanently unmatchable to a job by name or Id, by design.
_Avoid_: Archived backup
