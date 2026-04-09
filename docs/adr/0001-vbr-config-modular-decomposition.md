# ADR 0001: Decompose Get-VBRConfig.ps1 into Orchestrator-Collector Module

* **Status:** Accepted
* **Date:** 2026-02-21
* **Decider:** Ben Thomas (@comnam90)
* **Consulted:** GitHub Copilot / Claude Code

## Context and Problem Statement

`Get-VBRConfig.ps1` had grown to 2,188 lines handling 16 unrelated collection domains
(servers, proxies, jobs, repositories, licensing, compliance, etc.) in a single script with
no internal structure. Specific problems:

1. **No error isolation** — an exception in any section aborts all remaining collection
2. **Untestable** — no public API boundary; functions cannot be invoked independently or
   unit-tested without a live VBR connection and full script execution
3. **Collaboration friction** — multiple contributors working on the same 2,100+ line file;
   new features broke existing ones; conflicting changes required manual merges
4. **Commit granularity** — every change touched the monolith, making history hard to read
   and blame hard to attribute
5. **Memory pressure** — intermediate result arrays accumulated across the full run instead
   of being streamed to disk
6. **Magic numbers** — CPU/RAM thresholds and task limits hardcoded; version-specific
   branches scattered throughout
7. **Variable scope leakage** — `$global:` and `$script:` variables used as implicit data
   channels between sections, creating hidden ordering constraints

## Decision Drivers

- **Maintainability:** smaller files are easier to read, easier to understand, and easier to
  assign ownership to
- **Testability:** each collector must be callable in isolation without a live VBR environment
- **Collaboration:** changes to one domain (e.g. jobs) must not conflict with changes to
  another (e.g. repositories); commits should be attributable to a single concern
- **Fault isolation:** one failing collector must not abort the others
- **DRY:** ~100 repetitive try/catch and logging blocks across the monolith
- **SOLID/KISS:** new Veeam features should require adding a file, not editing a 2,100-line one
- **Configuration:** externalize thresholds to `VbrConfig.json` (removes magic numbers)

## Considered Options

### Option A — Keep the monolith, add error handling inline

Wrap each section in try/catch without changing the structure. Low risk, no change to the
public interface from the C# collection layer.

**Rejected:** Does not address testability, scope leakage, memory, or maintainability.

### Option B — Partial decomposition (helper functions within the same file)

Extract repeated logic into helper functions in the same `.ps1` file.

**Rejected:** Does not provide a module boundary; functions still share global scope and
cannot be independently loaded or tested.

### Option C — Full Orchestrator-Collector pattern (chosen)

Decompose into:
1. **Orchestrator** (`Get-VBRConfig.ps1`, rewritten internals, filename preserved) —
   lifecycle, connection, config loading, collector invocation via `Invoke-VhcCollector`
2. **Module** (`vHC-VbrConfig.psm1` + manifest) — `Public/` (exported collectors, `Vhc`
   prefix) and `Private/` (internal helpers, `Vhci` prefix)
3. **Config** (`VbrConfig.json`) — thresholds, resource requirements, version-specific
   parameters

## Decision

Option C. The 2,188-line monolith is replaced by:

- `Get-VBRConfig.ps1` (rewritten internals) — orchestrates collection; calls
  `Invoke-VhcCollector` for each public function; passes data explicitly between dependent
  collectors via return values
- `vHC-VbrConfig/` module — 16+ public collector functions each covering one domain; 20+
  private helper functions; all CSV output via `Export-VhciCsv`
- `VbrConfig.json` — externalizes all numeric thresholds

Key architectural rules established by this decision (enforced in subsequent ADRs):
- Collectors communicate via parameters and return values only — no `$script:` side-channels
  (-> ADR 0006)
- `Invoke-VhcCollector` provides uniform timing, fault isolation, and manifest recording
  (-> ADR 0007)
- Private functions use `Vhci` prefix to make the module boundary visually unambiguous

## Consequences

* **Good:** Single-collector failures are caught and logged; the orchestrator continues with
  remaining collectors
* **Good:** Each public function can be dot-sourced and called independently in tests without
  a live VBR session (mock parameters)
* **Good:** 59 CSV outputs preserved with identical schemas; C# collection layer
  (`CCollections.cs`) requires no changes to file discovery
* **Good:** Threshold changes are `VbrConfig.json` edits, not code changes
* **Neutral:** Module must be loaded (`Import-Module`) before any function is callable;
  orchestrator handles this
* **Neutral:** Adding a new public collector requires placing the file in `Public/` **and**
  adding the name to `FunctionsToExport` in the manifest
