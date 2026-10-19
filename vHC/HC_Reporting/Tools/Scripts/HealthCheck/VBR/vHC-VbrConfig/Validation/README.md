# vHC-VbrConfig Validation Harness

Empirical validation of the collector: create **real** VBR job objects of every
type/config, run the collector, assert the emitted CSVs captured them correctly,
then delete everything by prefix and reconcile inventory back to baseline.

> **Real validation trumps unit tests.** Mocks cover branch logic; this harness
> covers the only thing mocks cannot — *does the real cmdlet shape map to the CSV
> field we think it does.*

## Files

| File | Purpose |
|---|---|
| `VhcvGuards.ps1` | Reversibility guards A–D (prefix, disabled-on-create, snapshot, prefix-only cleanup + reconcile). |
| `VhcvPrereq.ps1` | `Test-VhcvPrereq` — detects which job families the lab can actually create. |
| `Invoke-VhcvHarness.ps1` | Orchestrator: snapshot → (create → collect → verify) → cleanup → reconcile. |
| `VhcvGuards.Tests.ps1` | Mock-based unit tests for the guard logic (UNTAGGED — runs in CI). |
| `Integration/*.Tests.ps1` | Per-type live integration tests (`-Tag 'LiveVBR'` — excluded in CI). *(Phase 6)* |

## Status

- **Chunk 0.2 (current):** guards + snapshot + reconcile + safety gate implemented and
  unit-tested. **No job factories yet** — `Invoke-VhcvHarness.ps1` supports only the safe
  `-ScaffoldCheck` round-trip (baseline → reconcile, zero mutation).
- **Phase 6 (later):** per-type job factories + config-variation matrix + integration tests.

## Safety contract (why this is safe to ship now)

1. **Hard prefix** — every created object is named `vHC-VALIDATE-*` via `New-VhcvName`.
2. **Disabled on create** — `New-VhcvJob` disables each job and verifies it took.
3. **Baseline snapshot** — captured before anything is created.
4. **Prefix-only cleanup + reconcile** — every `Remove-*` is filtered by the prefix;
   `Assert-VhcvReconciled` fails hard if any prefixed object leaks **or** any baseline
   object went missing.
5. **Hard gate** — the orchestrator refuses to run without
   `VHC_ALLOW_LIVE_MUTATION=YES_I_HAVE_A_LAB` and `VHC_LAB_VBR=<lab-host>`.

## Running

```powershell
# CI / safe default — unit tests only, no live VBR:
Invoke-Pester ./Validation -ExcludeTag 'LiveVBR'

# Guard round-trip against a live LAB (reads inventory, creates nothing):
$env:VHC_ALLOW_LIVE_MUTATION = 'YES_I_HAVE_A_LAB'
$env:VHC_LAB_VBR = 'vbr-lab.example.com'
./Validation/Invoke-VhcvHarness.ps1 -ScaffoldCheck

# Full live validation (Phase 6, once factories land — creates & deletes real objects):
# Invoke-Pester ./Validation/Integration -Tag 'LiveVBR'
```
