# Veeam Health Check — End-to-End Validation

The **pre-commit gate**: run the real tool and validate every output artifact before
committing any collection/report change. Generalizes the manual before/after report-replay
used to prove the `[Bjobs]` retirement was output-neutral.

## Files

| File | Purpose |
|---|---|
| `VhcE2EChecks.ps1` | Pure, unit-testable validation functions (HTML, CSV, log, JSON, baseline diff). |
| `Invoke-VhcE2E.ps1` | Orchestrator: optional build → run (import/live/both) → validate all artifacts → result JSON + exit code. |
| `VhcE2E.Tests.ps1` | Mock-based Pester unit tests for the check functions (UNTAGGED — runs in CI). |

A PAI skill wraps this for one-command runs: `bun ~/.claude/skills/VhcE2E/Tools/run-e2e.ts ...`.

## Gates
Build (0 errors) · **Tests:xUnit** (full `VhcXTests` suite) · **Tests:Pester** (module + validation + E2E checks, excl. `LiveVBR`) ·
Run (exit 0) · **HTML** (exists, size, anchors, no error leakage) ·
**CSV** (required present, all parse) · **Log** (completion marker, no unexpected errors) ·
**JSON** (required sections + row counts) · **BaselineDiff** (sections match saved baseline).

## Usage

```powershell
# Import-replay regression vs baseline (fast, deterministic — the default pre-commit check):
./Invoke-VhcE2E.ps1 -Mode import `
  -ImportPath 'C:\temp\vHC\Original\VBR\<host>\<ts>' `
  -Baseline   'C:\temp\vhc-e2e-baseline.json'

# Build first, then live end-to-end (run on/near the VBR server):
./Invoke-VhcE2E.ps1 -Mode live -LabServer vbr-v13-rtm -Build

# Seed/refresh the baseline AFTER an intentional, reviewed report change:
./Invoke-VhcE2E.ps1 -Mode import -ImportPath <dir> -Baseline <file> -UpdateBaseline

# Unit tests for the check logic (no build / no VBR — runs anywhere):
Invoke-Pester ./VhcE2E.Tests.ps1
```

Exit 0 = all gates green. The result is also written to `-ResultJson` when supplied.

## Baselines
Environment-specific (job names, counts). Keep one baseline per lab/collection. Re-seed only
on an intentional, reviewed change — never to "make it pass." A `BaselineDiff` failure names the
exact drifted section, which is precisely the regression signal you want before a commit.

## Modes
- **import** — replay a collected CSV set through `/import`. No live VBR. Use for regression.
- **live** — full collect + report against a VBR server (most faithful; run on/near the server).
- **both** — run both passes.

## Gotchas
- Windows-only (builds/runs `VeeamHealthCheck.exe` `net8.0-windows7.0` via `dotnet.exe`/`pwsh.exe`).
- Known-benign log errors (e.g. optional-CSV-missing in import replay) are ignorable via
  `-IgnoreLogPatterns`; the default already ignores `Failed to load VBR CSV data or no data found`.
