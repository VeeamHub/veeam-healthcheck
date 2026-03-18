# ADR 0003: Capture ProcessingMode via Log Parsing, Not Structured Properties

* **Status:** Accepted
* **Date:** 2026-02-26
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot

## Context and Problem Statement

`Get-VhcSessionReport.ps1` needs to capture the VMware transport mode (NBD, HotAdd, SAN, NBDSSL) used during each task session and write it to the `ProcessingMode` column of `VeeamSessionReport.csv`.

During investigation of the v13 per-VM granularity fix (see ADR 0002), three potential sources for this value were identified and evaluated on live VBR v12 and v13 systems.

---

## Options Considered

### Option A — `ProcessingMode` direct property

`$session.ProcessingMode` and `$task.ProcessingMode` on the PowerShell-returned objects.

**Findings:**
- Empty string on both `CBackupSession` and `CBackupTaskSession` objects on v13 (confirmed live).
- Not tested on v12; expected to be similarly empty based on the same object model.

**Verdict:** Not viable.

---

### Option B — `WorkDetails.SourceProxyMode` integer with enum mapping

`$task.WorkDetails.SourceProxyMode` returns a plain `System.Int32`. Through .NET reflection (Option 3 — scanning all loaded Veeam assemblies for matching enum types) the closest matching enum was identified as **`Veeam.Backup.Core.CVddkModes+EVddkMode`**:

```
0 = san
1 = hotadd
2 = nbd
3 = nbdssl
```

Confirmed consistent with live v13 data: a task with `SourceProxyMode = 2` had `[nbd]` in its log. Note that the higher-level enums `Veeam.Backup.Model.ETransportMode` and `Veeam.Backup.PowerShell.Infos.VBRProxyTransportMode` both map `2 = HotAdd` — using either of those would produce incorrect output.

**How the reflection was done:**

```powershell
# Option 1 - check if it's a typed enum (it wasn't):
$task.WorkDetails.SourceProxyMode.GetType().FullName  # returns System.Int32

# Option 2 - check property definition type (also System.Int32):
$task.WorkDetails.GetType().GetProperty('SourceProxyMode').PropertyType.FullName

# Option 3 - scan all Veeam assemblies for matching enum types:
[AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.FullName -match 'Veeam' } |
    ForEach-Object { try { $_.GetTypes() } catch { @() } } |
    Where-Object { $_.IsEnum -and $_.Name -match 'Proxy|Transport|Mode' } |
    ForEach-Object {
        Write-Host "`n=== $($_.FullName) ==="
        [Enum]::GetValues($_) | ForEach-Object { "  $([int]$_) = $_" }
    }
```

**Reasons not chosen:**
- `SourceProxyMode` is stored as a plain `System.Int32` with no compile-time association to `CVddkModes+EVddkMode`. The mapping is inferred, not enforced — a Veeam internal change could silently break the output with no error or warning.
- `CVddkModes` is an internal core class (not a public model or PowerShell surface). It is more likely to change across VBR major versions without notice than a log message format.
- Backup Copy jobs and other non-VMware job types have no meaningful transport mode. The correct value for those tasks is an empty string. What `SourceProxyMode` returns for those tasks (e.g. `0 = san`) has not been verified and may be misleading.
- Not tested on v12 — mapping consistency across versions is unconfirmed.

---

### Option C — Log parsing via `Logger.GetLog()` (chosen)

The `Logger.GetLog().UpdatedRecords` collection on task objects contains entries with titles in the format:

```
Using backup proxy <name> for disk <disk> [<mode>]
```

The bracketed mode token (`nbd`, `hotadd`, `san`, `nbdssl`) is extracted by the regex:

```powershell
[regex]'\bUsing \b.+\s(\[[^\]]*\])'
```

The mode value is extracted by stripping the brackets from the matched title.

**Confirmed working on:**
- v12: `CBackupTaskSession` objects returned by `GetTaskSessions()`
- v13: `CBackupTaskSession` objects returned by `GetTaskSessions()` (validated on a VMware backup job — Backup Copy jobs intentionally produce no match as they have no transport mode)

**Properties of this approach:**
- Captures the mode that **actually ran**, not the configured/selected mode. If HotAdd was configured but the proxy fell back to NBD, the log reflects the fallback; `SourceProxyMode` would reflect the originally selected mode.
- Self-documenting: the log entry text is human-readable and stable across VBR versions (Veeam does not version-bump log message formats in patch releases).
- Returns empty string naturally for job types without a transport mode (Backup Copy, tape, etc.) — no special-casing needed.
- Already in use in the existing codebase; carries no additional risk.

**Trade-off:** Requires calling `Logger.GetLog()` on each task, which performs a DB/XML read. This is already required for `BottleneckDetails` and `PrimaryBottleneck` extraction, so it adds no additional I/O compared to the status quo.

---

## Decision

**Option C — log parsing** is retained as the mechanism for capturing `ProcessingMode`.

`SourceProxyMode` (Option B) is documented here for future reference but is not used due to its untyped integer nature, inferred-only enum mapping, and unconfirmed cross-version and cross-job-type behaviour.

## Consequences

- No change to the `ProcessingMode` extraction logic in `Get-VhcSessionReport.ps1`.
- `Logger.GetLog()` continues to be called per task (already required for bottleneck columns).
- If a future VBR version exposes `ProcessingMode` as a typed, public property on `CBackupTaskSession`, this decision should be revisited.
