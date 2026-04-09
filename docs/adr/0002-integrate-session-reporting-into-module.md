# ADR 0002: Integrate Session Reporting into vHC-VbrConfig Module

* **Status:** Accepted
* **Superseded in part by:** ADR 0004 (unified `GetTaskSessions()` path replaces the two-path architecture described in Implementation Overview below)
* **Date:** 2026-02-26
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot

## Context and Problem Statement

The current architecture in `PSInvoker.cs` spawns three separate OS processes sequentially to collect data:

1. `Get-VBRConfig.ps1` runs the `vHC-VbrConfig` module and attempts to cache session data via `Export-VhcSessionCache` → `Export-Clixml`.
2. `Get-VeeamSessionReport.ps1` (or `Version13` variant) attempts to read that cache and produce `VeeamSessionReport.csv`.

**The Problem:** `Export-Clixml`/`Import-Clixml` always produces `Deserialized.*` objects — property-bag snapshots that retain values but lose all .NET methods. Both session report scripts depend on method calls (`GetTaskSessions()`, `Logger.GetLog()`) that do not exist on deserialized objects. Because these are separate OS processes, there is no mechanism to pass live objects between them.

As a result:
- `SessionCache.xml` is functionally useless for these scripts (always falls back to DB query).
- Two separate VBR connections are opened and closed per run.
- Sessions are queried from the database twice (once by each process).
- Dead code and a misleading cache file persist in the repository.

## Decision Drivers

* **Performance:** Reduce the overhead of spawning multiple `powershell.exe` instances and opening multiple VBR connections.
* **Data Integrity:** Maintain "live" .NET objects with functional methods throughout the collection lifecycle.
* **Maintainability:** Align with the ongoing refactor to move logic into the unified `vHC-VbrConfig` module.
* **Simplicity:** Eliminate misleading or non-functional cache files (`SessionCache.xml`).

## Considered Options

1. **Integrate session report into the vHC-VbrConfig module (Recommended)**
2. **Keep separate processes and disable caching**
3. **Cache pre-processed output (CSV/JSON) instead of raw objects**

## Decision Outcome

Chosen option: **Option 1**, because it is the only path that solves the fundamental memory boundary issue while improving performance and aligning with long-term architectural goals.

### Consequences

* **Good:** Sessions are fetched once and shared via `$script:` scope; no serialization is required.
* **Good:** Eliminates an entire OS process spawn and a second VBR login/logout cycle.
* **Good:** `SessionCache.xml` and its read/write logic are removed entirely, eliminating dead code.
* **Good:** `PSInvoker.cs` currently routes to the V13 script only when `VBRMAJORVERSION == 13`; the new module function uses `>= 13`, which is correct for future VBR versions (v14+). This is a bug fix bundled into the refactor.
* **Bad:** Requires non-trivial refactoring of ~470 lines of PowerShell across two standalone scripts.
* **Bad:** Requires a small breaking change in `PSInvoker.cs` to remove the secondary script invocations.
* **Bad:** All sessions for the report interval are now held as live .NET objects in-process. For large deployments this may be significant; `$script:AllBackupSessions` must be nulled after `Get-VhcSessionReport` completes to release memory.
* **Neutral:** Detection of VBR version (V13 vs. standard) moves from C# branching logic into the PowerShell module, using the existing `Get-VhcMajorVersion` function already called in `Get-VBRConfig.ps1`.
* **Neutral:** `Invoke-VhcCollector` wrapping means a session report failure is non-fatal (missing CSV), which is the same behaviour as the current standalone script. The `Get-VhcSessionReport` function must guard against a null/empty `$script:AllBackupSessions` explicitly.

---

## Pros and Cons of the Options

### Option 1: Integrate into Module

* **Pros:** Best performance; shared in-memory state; removes redundant code; single VBR connection per run.
* **Cons:** High initial effort; requires small C# modification; regression risk without Pester test coverage.

### Option 2: Keep Separate Processes

* **Pros:** Lowest risk; no C# changes needed.
* **Cons:** Misses performance gains; leaves dead code and misleading files in the repo; continues inefficient VBR connection pattern.

### Option 3: Cache Pre-processed Output

* **Pros:** Achieves "single query" goal without merging scripts.
* **Cons:** Introduces tight coupling via file schema; adds complexity to a "cache contract" rather than simplifying the codebase.

---

## Implementation Overview

### PowerShell Changes

1. **Modify `Export-VhcSessionCache`:** Change it to store live session objects in `$script:AllBackupSessions` instead of serialising to XML. Remove the `Export-Clixml` call and the `SessionCache.xml` output entirely. Rename to `Get-VhcBackupSessions` to reflect its new role as a collector, not an exporter.

2. **Create `Get-VhcSessionReport`** (new Public module function):
   - Reads `$script:AllBackupSessions` populated by `Get-VhcBackupSessions`.
   - Calls `.GetTaskSessions()` on each session for all VBR versions (see ADR 0004 — the
     originally planned two-path architecture was superseded after live testing revealed the
     `>=13` session-level path produced per-job-run rows instead of per-VM rows).
   - Writes `VeeamSessionReport.csv` to `$script:ReportPath`.

3. **Update `Get-VBRConfig.ps1` orchestrator:**
   - Replace the `SessionCache` collector step with `Get-VhcBackupSessions`.
   - Add a `SessionReport` collector step calling `Get-VhcSessionReport`.

4. **Update `vHC-VbrConfig.psm1`:** Dot-source `Get-VhcSessionReport.ps1`; remove dot-source of `Export-VhcSessionCache.ps1` (or rename).

### C# Changes

5. **`PSInvoker.cs`:**
   - Remove `vbrSessionScript` and `vbrSessionScriptVersion13` fields.
   - Remove `VbrSessionStartInfo()` private method.
   - Remove the `this.ExecutePsScript(this.VbrSessionStartInfo())` call from `RunVbrConfigCollect()`.
   - Remove `UnblockFile` calls for the two session scripts.

### Cleanup

6. **Delete** `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-VeeamSessionReport.ps1` and `Get-VeeamSessionReportVersion13.ps1`.
7. **Delete** `SessionCache.xml` from the repository root (it is runtime data, not source).
8. **Delete** `Export-VhcSessionCache.ps1` (replaced by `Get-VhcBackupSessions.ps1`).

---

## Validation and Next Steps

1. Implement `Get-VhcBackupSessions` and `Get-VhcSessionReport` in the module.
2. Wire both into `Get-VBRConfig.ps1`.
3. Modify `PSInvoker.cs` to remove the session script process invocations.
4. Perform an end-to-end test on a VBR environment (both v12 and v13 if available).
5. Remove old standalone scripts and `SessionCache.xml`.
