# ADR 0005 — NAS BackupSize/DataSize: fallback to Progress when BackupStats is zero

**Status:** Accepted

**Date:** 2026-02-26

**Decider:** Ben Thomas (@comnam90)

---

## Context

`CBackupSession.BackupStats` aggregates byte counts written by block-based workloads (VMware,
Hyper-V, agents). For File Backup (NAS) jobs (`Info.Platform = 'ENasBackup'`, `JobType = 13000`)
the Veeam API leaves `BackupStats.BackupSize` and `BackupStats.DataSize` at `0`, regardless of
how much data was actually transferred.

The actual byte counts are available on the parent session's `Progress` object:
- `Progress.TransferedSize` — bytes written to the backup repository (equivalent to BackupSize)
- `Progress.ReadSize`       — bytes read from the source for the backup (equivalent to DataSize)

These fields are populated for all job types.

This was a pre-existing bug in the original standalone `Get-VeeamSessionReport.ps1` and was
carried forward unchanged into the refactored `Get-VhcSessionReport.ps1` module function.

---

## Decision

Use a **zero-fallback**: if `BackupStats.BackupSize` equals `0`, read from `Progress.TransferedSize`
(and likewise `BackupStats.DataSize` / `Progress.ReadSize`).

No explicit job-type branching is introduced. The fallback applies any time `BackupStats` is
empty, making the fix inherently self-describing and safe for future unknown job types that
exhibit the same API behaviour.

---

## Options Considered

### Option 1 — Zero-fallback to Progress fields (chosen)

```powershell
'BackupSize(GB)' = if ($task.JobSess.BackupStats.BackupSize -gt 0) {
    [math]::Round(($task.JobSess.BackupStats.BackupSize / 1GB), 4)
} else {
    [math]::Round(($task.JobSess.Progress.TransferedSize / 1GB), 4)
}
```

**Pros:**
- Minimal change (2 conditionals, no new parameters)
- Self-healing for any job type where BackupStats is absent
- No coupling to `Info.Platform` or `JobType` values
- KISS — easy to reason about, easy to revert

**Cons:**
- Does not signal *why* the fallback fired (silent substitution)
- `Progress.TransferedSize` and `BackupStats.BackupSize` are conceptually the same metric but
  derived differently; in theory they could diverge in edge cases

### Option 2 — Explicit NAS platform check

```powershell
$isNas = $task.JobSess.Info.Platform -eq 'ENasBackup'
'BackupSize(GB)' = if ($isNas) {
    [math]::Round(($task.JobSess.Progress.TransferedSize / 1GB), 4)
} else {
    [math]::Round(($task.JobSess.BackupStats.BackupSize / 1GB), 4)
}
```

**Pros:**
- Explicit intent — clearly signals NAS-specific handling
- No silent fallback; behaves deterministically per platform

**Cons:**
- Couples code to a platform enum string; breaks if Veeam renames the value
- Requires updating if other job types also have zero BackupStats
- More lines; harder to read at a glance

### Option 3 — Always use Progress fields

```powershell
'BackupSize(GB)' = [math]::Round(($task.JobSess.Progress.TransferedSize / 1GB), 4)
```

**Pros:**
- Simplest of all options — single field, no branching

**Cons:**
- `Progress.TransferedSize` may not equal `BackupStats.BackupSize` for VMware jobs
  (dedup/compression applied at the datastore vs. the transport layer can produce different values)
- Potential regression for the dominant VMware use case; not safe without broader testing

---

## Consequences

- NAS File Backup jobs will show non-zero `BackupSize(GB)` and `DataSize(GB)` in the session
  report CSV, matching what the Veeam console displays.
- VMware and agent jobs are unaffected (`BackupStats.BackupSize > 0`, fallback never fires).
- Any future job type whose `BackupStats` is zero will also benefit from the fallback.
