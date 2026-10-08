---
project: veeam-healthcheck
task: "Project ISA — Veeam Health Check (VHC Core)"
effort: E3
effort_source: explicit
phase: observe
progress: 0/22
mode: interactive
started: 2026-05-15T18:30:00Z
updated: 2026-05-15T18:45:00Z
---

# Veeam Health Check — Project ISA

> **Status:** Seed-generated draft. Human-author review required before this ISA is treated as authoritative.
> Run `Skill("ISA", "interview me on /Users/adam.congdon/code/veeam-healthcheck/ISA.md")` to deepen Vision, Principles, refine Criteria.

## Problem

Veeam administrators and field engineers need a fast, offline way to assess the configuration health of a Veeam Backup & Replication (VBR) or Veeam Backup for Microsoft 365 (VB365) installation. Vendor logs, registry settings, SQL state, and PowerShell-only configuration data are scattered across the server and tedious to correlate by hand. Pre-sales, support, and customer-success workflows currently rely on ad-hoc scripts and manual screenshots, producing inconsistent assessments and slow turnaround on customer engagements.

## Vision

A field engineer runs `VeeamHealthCheck.exe /run` on a customer's Veeam server, accepts a short prompt, and within minutes opens a single self-contained HTML report that tells the whole story: job analytics, concurrency heat maps, configuration findings, and curated guidance with links back to Veeam docs. The customer can hand the report to their CISO; the SE can hand it to a TAM. Scrub mode lets the same report leave the customer's environment without leaking IPs, hostnames, or credentials. Nothing phones home.

## Out of Scope

- **Phone-home telemetry or cloud upload of customer data.** The tool runs locally and writes locally; remote operation is opt-in and targets the customer's own VBR host.
- **Veeam Cloud Service Provider (VCSP) environments.** VCSP-specific topologies are explicitly not supported.
- **Non-Windows execution of the main `VeeamHealthCheck.exe`.** A separate `VhcXTests.CrossPlatform` project exists for portable unit tests, but the GUI/CLI binary is Windows-only.
- **Active mitigation or change.** VHC is a read-only assessment tool. It never modifies the VBR/VB365 configuration it inspects.
- **NOT A Replacement for Veeam ONE or other live monitoring.** Continuous monitoring is the responsibility of the companion `vhc-monitor` (see `COMPANION_SPEC.md`), not VHC Core.

## Constraints

- **Runtime:** .NET 10.0 targeting `net10.0-windows7.0` (upgraded from .NET 8; see Decisions 2026-05-15). Avalonia for GUI. PowerShell 7 SDK embedded for script execution. No Mono, no cross-compile of the main binary.
- **Deployment:** Single-file self-contained `.exe` packaged via `VeeamHealthCheck.zip`. Must run from an arbitrary directory without an installer.
- **Privileges:** Runs at any privilege level. **Elevation is not required.** Limited-privilege runs surface a clearly-degraded capability set (e.g. WMI/SQL probes that need admin are skipped with a recorded reason) rather than hard-failing. Full-fidelity collection benefits from Veeam Backup Administrator role.
- **Output isolation:** Default working directory `C:\temp\vHC`. No write outside the configured `outdir`. A specific free-disk-space minimum has not been measured against current report sizes — the historical "500 MB on `C:\`" figure was an initial guess and is flagged for re-measurement (see Decisions 2026-05-15).
- **Dependencies:** CsvHelper, DocumentFormat.OpenXml, HtmlToOpenXml, DinkToPdf are the only externally-visible runtime dependencies; new dependencies require justification.
- **Security:** Scrub mode must anonymize IPs, server names, credentials, **and email addresses** (CWE-359 fix per commit `1412047`) before any artifact leaves the customer environment.
- **Compatibility:** Supports VBR v12.3 and v13 (Windows and Linux backup infrastructure); supports VB365 v6, v7, v8. Earlier VBR (v11, pre-12.3) is explicitly handled by the legacy v2 line.
- **Community-supported.** This is a VeeamHub community tool, not an official Veeam product. No Veeam Support escalation path; issues route through GitHub.

## Principles

<!-- TODO: author Principles — substrate-independent truths the work must respect.
Candidates to consider:
- Read-only assessment beats live modification for safety.
- A single HTML file is more durable than a multi-file report.
- Determinism in scrub mode is worth more than performance.
- Field engineers trust tools whose source they can read. -->

## Goal

Deliver a Windows executable that, when run on a host with Veeam VBR or VB365 installed, collects configuration and operational data across a configurable reporting window (7/12/30/90 days), processes it into typed report objects, and emits a single-page HTML report (with optional PDF, PowerPoint, and scrubbed variants) — in under fifteen minutes on a representative customer environment, with zero outbound network traffic beyond the targeted Veeam infrastructure.

## Criteria

- [ ] **ISC-1**: `dotnet restore vHC/HC.sln` exits 0 on a clean checkout.
- [ ] **ISC-2**: `dotnet build vHC/HC.sln --configuration Release` exits 0 with no errors and zero new warnings beyond the suppressed analyzer list in the csproj.
- [ ] **ISC-3**: `dotnet test vHC/VhcXTests/VhcXTests.csproj` passes on Windows with 100% of declared tests green.
- [ ] **ISC-4**: `dotnet test vHC/VhcXTests.CrossPlatform/` (or the equivalent) passes on macOS/Linux, demonstrating the cross-platform-safe test slice.
- [ ] **ISC-5**: Pester tests under `Tools/Scripts/HealthCheck/**/*.Tests.ps1` all pass under the CI workflow (`ci-cd.yaml`).
- [ ] **ISC-6**: CodeQL workflow (`codeql.yml`) reports no new high-severity findings on the default branch.
- [ ] **ISC-7**: Product detection (`CClientFunctions.ModeCheck()`) correctly identifies VBR mode when `Veeam.Backup.Service` is running and VB365 mode when `Veeam.Archiver.Service` is running.
- [ ] **ISC-8**: A full run against a VBR v12.3 lab environment produces an HTML report at `C:\temp\vHC\…\Veeam Health Check Report_VBR_*.html` that opens in a modern browser with no broken images or missing tables.
- [ ] **ISC-9**: A full run against a VB365 v8 lab environment produces an HTML report at `C:\temp\vHC\…\Veeam Health Check Report_VB365_*.html` with the VB365-specific table set rendered.
- [ ] **ISC-10**: `/pdf` flag produces a PDF that opens in Acrobat/Preview without rendering errors.
- [ ] **ISC-11**: `/pptx` flag produces a PowerPoint that opens in PowerPoint 2019+ without recovery prompts.
- [ ] **ISC-12**: `/scrub:true` produces a report in which no real customer IPv4 address, FQDN, Windows credential, or email From/To header value appears in the output HTML (regex sweep of the rendered file).
- [ ] **ISC-13**: `/import:<path>` reproduces a report from previously-collected CSV data without requiring access to a live Veeam server.
- [ ] **ISC-14**: `/remote /host=<fqdn>` executes against a remote VBR host using the same code path, surfacing the same report shape.
- [ ] **ISC-15**: Default reporting window of 7 days, configurable via `/days:7|12|30|90`, is honored end-to-end (collected → processed → rendered).
- [ ] **ISC-16**: Single-file deployment: extracting `VeeamHealthCheck.zip` and running `VeeamHealthCheck.exe` requires no separate installer, no admin install step, and no MSI.
- [ ] **ISC-17**: Tool runs at any privilege level. When launched non-elevated, it announces the degraded-capability set up front, runs the probes it can, and records skipped/limited probes (with reasons) in the report — it does **not** hard-fail on lack of elevation.
- [ ] **ISC-18**: Credentials stored under `%AppData%\VeeamHealthCheck\creds.json` are encrypted at rest using a per-user Windows DPAPI scope (verified by `CredentialStoreSecurityTests`).
- [ ] **ISC-19**: `/clearcreds` removes the stored credential file and re-prompts on next run.
- [ ] **ISC-20**: **Anti:** No code path in the running binary issues an outbound network request to a non-customer-controlled host (no analytics, no auto-update ping, no telemetry). Verified by a network capture of a clean `/run`.
- [ ] **ISC-21**: **Anti:** The tool never writes outside the configured `outdir` (default `C:\temp\vHC`). Verified by Process Monitor trace on a `/run`.
- [ ] **ISC-22**: **Anti:** The tool never modifies VBR/VB365 configuration. Verified by a before/after `Get-VBR*` baseline diff on a `/run`.

## Test Strategy

| isc | type | check | threshold | tool |
|-----|------|-------|-----------|------|
| ISC-1 | command | `dotnet restore vHC/HC.sln` | exit 0 | dotnet CLI |
| ISC-2 | command | `dotnet build vHC/HC.sln -c Release` | exit 0, 0 new warnings | dotnet CLI |
| ISC-3 | command | `dotnet test vHC/VhcXTests/VhcXTests.csproj` | 100% pass | dotnet test |
| ISC-4 | command | cross-platform test project run on macOS/Linux | 100% pass | dotnet test |
| ISC-5 | command | Pester invocation in `ci-cd.yaml` | 100% pass | Pester |
| ISC-6 | workflow | GitHub `codeql.yml` run | no new high-severity | CodeQL |
| ISC-7 | unit | mock-process test of `ModeCheck()` | both branches covered | xUnit + Moq |
| ISC-8 | integration | full VBR lab run | HTML opens, no broken anchors | manual + DOM check |
| ISC-9 | integration | full VB365 lab run | HTML opens with VB365 tables | manual + DOM check |
| ISC-10 | integration | `/pdf` run | PDF opens cleanly | Acrobat / Preview |
| ISC-11 | integration | `/pptx` run | PPTX opens without recovery | PowerPoint |
| ISC-12 | integration | scrub mode regex sweep | zero leaks across IP/FQDN/creds/email | grep / ContentValidation tests |
| ISC-13 | integration | `/import` against archived CSV bundle | report reproduced | manual |
| ISC-14 | integration | remote run against lab VBR | report shape matches local run | manual |
| ISC-15 | integration | each `/days:` value | window honored end-to-end | report metadata check |
| ISC-16 | smoke | unzip + run on clean Windows VM | no MSI required | manual |
| ISC-17 | unit/integration | non-elevated launch | runs, reports degraded capabilities, skips elevation-only probes with reasons | `SilentModeTests` / manual |
| ISC-18 | unit | `CredentialStoreSecurityTests` | DPAPI scope verified | xUnit |
| ISC-19 | integration | `/clearcreds` + relaunch | re-prompts | manual |
| ISC-20 | integration | clean `/run` with packet capture | zero non-Veeam outbound | Wireshark / pktmon |
| ISC-21 | integration | clean `/run` with Process Monitor | no writes outside outdir | Procmon |
| ISC-22 | integration | before/after VBR config diff | zero deltas attributable to VHC | PowerShell `Get-VBR*` |

## Features

| name | description | satisfies | depends_on | parallelizable |
|------|-------------|-----------|------------|----------------|
| Entry & arg parsing | `Startup/EntryPoint.cs`, `Startup/CArgsParser.cs`, `VhcGui.axaml.cs` — routes between GUI and CLI, parses flags. | ISC-7, ISC-15, ISC-16, ISC-17 | — | yes |
| Mode detection | `CClientFunctions.ModeCheck()` — detects VBR vs VB365 by running processes. | ISC-7 | Entry & arg parsing | yes |
| Collection — VBR | `Functions/Collection/` + `Tools/Scripts/HealthCheck/VBR/` PowerShell scripts; writes CSV to `C:\temp\vHC\Original\VBR\…`. | ISC-8, ISC-14, ISC-15 | Mode detection | yes |
| Collection — VB365 | `Functions/Collection/` + `Tools/Scripts/HealthCheck/VB365/`. | ISC-9, ISC-15 | Mode detection | yes |
| Credentials | `CredsWindow/` + `%AppData%\VeeamHealthCheck\creds.json` via DPAPI; `/clearcreds`. | ISC-18, ISC-19 | Entry & arg parsing | yes |
| Data processing | `Functions/Reporting/CsvHandlers/CCsvReader.cs`, `CCsvParser.cs`, `DataFormers/CDataFormer.cs`. | ISC-8, ISC-9, ISC-13 | Collection (either) | no |
| HTML rendering — VBR | `Functions/Reporting/Html/VBR/CHtmlCompiler.cs` + `VbrTables/`. | ISC-8 | Data processing | yes |
| HTML rendering — VB365 | `Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs`. | ISC-9 | Data processing | yes |
| PDF export | DinkToPdf wrapper invoked by `/pdf`. | ISC-10 | HTML rendering | yes |
| PowerPoint export | HtmlToOpenXml + DocumentFormat.OpenXml; `/pptx`. | ISC-11 | HTML rendering | yes |
| Scrub mode | `CScrubHandler` — anonymizes IPs, hostnames, credentials, and email From/To. | ISC-12, ISC-20 | HTML rendering | yes |
| Remote execution | `/remote /host=<fqdn>` path through Collection. | ISC-14 | Collection (either) | no |
| Import mode | `/import:<path>` — bypass collection, render from prior CSV. | ISC-13 | Data processing | yes |
| CI/CD | `.github/workflows/ci-cd.yaml`, `codeql.yml` — build, .NET tests, Pester tests, CodeQL. | ISC-1, ISC-2, ISC-3, ISC-5, ISC-6 | — | yes |
| Cross-platform test slice | `vHC/VhcXTests.CrossPlatform/` — portable unit tests for logic that doesn't need the main project's Windows-targeted dependencies. | ISC-4 | — | yes |
| Documentation site | MkDocs Material at `docs/` deployed to GitHub Pages via CI. | — | — | yes |

## Decisions

- **2026-05-15** — Seed-generated draft created from README, `COMPANION_SPEC.md`, repository structure, and the most recent 30 commits. ISCs ISC-1..ISC-22 are seeded conservatively; expect refinement when a human author runs the Interview workflow. Principles section deliberately left as a TODO — those are author-driven and Seed will not fabricate them.
- **2026-05-15** — Out of Scope captures the four loud constraints surfaced by the README + recent commit history: no phone-home, no VCSP, no non-Windows main binary, read-only. These were chosen as the prose guardrails most likely to be violated by future drift.
- **2026-05-15** — Cross-platform test project (`VhcXTests.CrossPlatform`) treated as a first-class Feature and pinned to ISC-4 rather than folded into ISC-3 — the two are independently meaningful: Windows tests catch WPF/COM regressions, cross-platform tests keep portable logic honest in CI on Linux runners.
- **2026-05-15** — *refined:* **Runtime upgrade to .NET 10 is in scope.** The Seed draft pinned the Constraint to .NET 8.0 because that matches the current csproj; on review, Adam flagged that .NET 8 is reaching the end of its useful window and the project should target .NET 10. Constraint updated to "currently .NET 8.0; planned upgrade to .NET 10." A future ISC family (TBD when the upgrade work is scoped) will cover: csproj targets bumped, CI runner images updated, dependency compatibility audit (CsvHelper, DocumentFormat.OpenXml, HtmlToOpenXml, DinkToPdf, PowerShell SDK), build/test/Pester green on .NET 10.
- **2026-05-15** — *refined:* **Elevation is not a hard requirement.** The Seed draft incorrectly stated the tool refuses to start when not elevated. Corrected by Adam: VHC runs at any privilege level with a degraded capability surface when elevation is missing. Constraint and ISC-17 reworded; ID preserved per the ID-stability rule. Verification at VERIFY phase should include a non-elevated lab run and confirm the "skipped probes" list is surfaced in the report.
- **2026-05-15** — *refined:* **The 500 MB free-disk minimum is unverified.** The Seed draft pulled "500 MB on `C:\`" from the README, which itself was an initial-creation guess. Constraint reworded to flag the figure as unmeasured. Action: measure peak outdir size across a representative VBR full run, a VB365 full run, and the worst-case `/days:90 /pdf /pptx` matrix; replace the hand-wavy threshold with a measured one and update README accordingly.
- **Source artifacts consulted:** `README.md`, `COMPANION_SPEC.md`, `CLAUDE.md`, `vHC/HC.sln`, top-level `Functions/` subdirectories, last 30 `git log --oneline` commits.

<!-- TODO: run `Skill('ISA', 'interview me on this file')` to author Principles, refine Vision, and audit Criteria against actual lab runs. -->
