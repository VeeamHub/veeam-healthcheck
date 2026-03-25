# ADR 0012: Agent Backup Session Collection via Get-VBRComputerBackupJobSession and Get-VBRTaskSession

* **Status:** Accepted
* **Date:** 2026-03-13
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot

## Context and Problem Statement

`Get-VhcBackupSessions` used only `Get-VBRBackupSession`, which silently excludes
agent/computer backup job sessions (`EpAgentBackup` type). These sessions are a
separate family managed by `Get-VBRComputerBackupJobSession` and do not appear in
`Get-VBRBackupSession` output.

Live investigation confirmed the gap: a job named `Physical Servers - Linux` produced
4 sessions visible via `CBackupSession::GetByJob()` and `Get-VBRComputerBackupJobSession`,
but zero rows via `Get-VBRBackupSession`. Any customer running Windows or Linux agent
backup jobs via VBR had those sessions silently absent from `VeeamSessionReport.csv`.

### Why GetTaskSessions() could not be reused

Agent sessions resolve to `Veeam.Backup.PowerShell.Infos.VBRSession` (a PowerShell
wrapper type), which does not have a `GetTaskSessions()` method. The existing code
called `$BackupSessions.GetTaskSessions()` on the full collection, which would fail
on `VBRSession` objects.

### Why Get-VBRTaskSession works for all types

`Get-VBRTaskSession` declares its `-Session` parameter with type
`Veeam.Backup.PowerShell.Infos.VBRSession` and a `VBRSessionTransformationAttribute`
transformer. This attribute coerces `CBackupSession` and `CBackupCopySession` .NET
objects to the expected wrapper type, so the same cmdlet accepts all three session
families. Live testing confirmed correct `CBackupTaskSession` output for VM, Backup
Copy, and agent sessions.

### JobName suffix on agent task sessions

For agent task sessions, Veeam appends the machine name to `$task.JobName`:
```
Physical Servers - Linux - lab-m01-utl01.lab.garagecloud.net
```
The parent `VBRSession.Name` contains the clean job name (`Physical Servers - Linux`).
For VM and Backup Copy sessions, `$task.JobName` is already the clean job name.

## Decision

1. In `Get-VhcBackupSessions`: add `Get-VBRComputerBackupJobSession` (filtered by
   `$cutoff`) to the returned array alongside the existing `Get-VBRBackupSession`
   results. Wrap the agent collection in `try/catch` with a WARNING so that failure
   (e.g. cmdlet unavailable on older VBR builds) degrades gracefully without
   aborting the whole collector.

2. In `Get-VhcSessionReport`: replace the single `$BackupSessions.GetTaskSessions()`
   call with a per-session loop using `Get-VBRTaskSession -Session $session`. This
   works for all session types via the implicit coercion described above.

3. Use `$task.ObjectPlatform.IsEpAgentPlatform` to detect agent task sessions and
   take `JobName` from the parent `$session.Name` instead of `$task.JobName`.

## Rationale

- `Get-VBRTaskSession` is a single cmdlet that handles all session types, keeping
  the processing code path unified (no branching by type).
- The `IsEpAgentPlatform` flag is a stable, self-describing property on the task
  object itself, making the intent clear and the logic testable with mock objects.
- Wrapping only the agent collection in `try/catch` (not the VM collection) means
  the failure mode is a degraded result (agent sessions absent, warned in logs)
  rather than a total collector failure.

## Consequences

### Positive
- Agent/computer backup job sessions now appear in `VeeamSessionReport.csv`.
- `JobName` column contains the clean job name for all session types.
- Single processing loop for all session types; no type-dispatch branching.
- VM and Backup Copy session behaviour is unchanged.

### Neutral
- `ProcessingMode`, `BottleneckDetails`, and `PrimaryBottleneck` are empty for
  agent sessions: `$task.Logger.GetLog()` returns no records post-completion for
  `EpAgentBackup` sessions (confirmed via live testing).
- `BackupStats` and `Progress` fields on `$task.JobSess` are populated for agent
  sessions (confirmed: `BackupStats.BackupSize = 72 GB`, `DataSize = 136 GB`), so
  the existing ADR 0005 fallback logic is not needed for agent sessions.

### Negative
- None identified.

## Validation

Live testing on VBR v13 confirmed:
- `Get-VBRComputerBackupJobSession` returns 4 sessions for `Physical Servers - Linux`
  (1 success, 3 retries/failures) within the reporting window.
- `Get-VBRTaskSession` on a `VBRSession` object returns `CBackupTaskSession` with
  `Name = 'lab-m01-utl01.lab.garagecloud.net'` and `JobName` containing the machine suffix.
- `$task.JobSess.BackupStats.BackupSize = 72208083488` (~67 GB) confirms size data
  is available via the existing code path.
- `$task.ObjectPlatform.IsEpAgentPlatform = True` for the agent task session.
