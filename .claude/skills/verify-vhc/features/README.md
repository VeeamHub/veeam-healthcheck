# VHC verification map

This directory is the maintained source for verifying user-facing behavior of Veeam Health Check. Read this index before driving anything, then use the matching feature file as the recipe.

## Baseline preconditions

- Host is macOS or Linux with the .NET 10 SDK. No Windows, no VBR server.
- Run `.claude/skills/verify-vhc/scripts/vhc-verify.sh build`, then `doctor`, and require `DOCTOR: ready`.
- Inputs are the tracked golden CSVs under `vHC/HC_Reporting/Tools/GoldenBaselines/`. The helper copies them; never point a run at the originals.
- Always pass an explicit `/outdir` inside `.verify-vhc/evidence/<ID>/`.

## Driving conventions

- Prefer the helper subcommands. They stage inputs, run the real CLI, and assert on output content.
- Treat flag spellings as literal: `/import=<path>`, `/outdir=<path>`, `/run`, `/scrub:true`, `/scrub:false`. Flags are case-sensitive in the main switch (`/scrub:true`, not `/Scrub:True`).
- Paths passed to `/import` and `/outdir` must be absolute.
- Do not trust exit code `0` alone; assert on generated files.
- Never kill by process name. Cleanup kills only the recorded PID.

## Proof and skip reporting

- Record the feature ID and entry point with every artifact.
- Report an unreachable path with the attempted command and the unmet precondition (for example "needs Windows + VBR").
- Do not report a skipped entry point as verified through a different path. An `/import` report does not verify collection; a passing xUnit run does not verify live collection or the real Windows GUI; the headless GUI suite verifies only what [gui-headless](./gui-and-collection.md#gui-headless) lists.

## Features

- [CSV import](./csv-import.md) covers `/import` generating the HTML and JSON report from collected CSVs, over every documented folder layout (`import-layouts`). Drivable here.
- [Report render](./render.md) covers opening the generated report in a browser: console errors, nav anchors, section toggles, core sections, screenshots (`render`). Drivable here.
- [Scrubbed report](./scrubbed-report.md) covers `/scrub:true` anonymization of the same report. Drivable here.
- [CLI arguments](./cli-arguments.md) covers `/help`, no-action handling, bad import paths (`cli-checks`) and flags combined with `/import` (`cli-matrix`). Drivable here.
- [Build and tests](./build-and-tests.md) covers `dotnet build` and the xUnit suites, including the headless GUI project. Drivable here.
- [Windows host](./windows-host.md) covers `vhc-verify.sh winhost`: the current working tree built, tested (full `VhcXTests`), collected live (`--live`) and driven through the real GUI (`--gui`) on a remote Windows host over SSH, before a PR exists. Needs `VHC_WINHOST`; nothing about the host is committed.
- [GUI and live collection](./gui-and-collection.md) covers two things. `gui-headless` (Avalonia.Headless tests of the real windows) is drivable here. The real GUI window, VBR/VB365 collection, PDF export and Windows-only tests are **not drivable on macOS**; the file documents what a Windows host must do.

## Coverage matrix

Every surface a change to VHC can break, the check that gates it, and how well. Status: **gated** = a check that runs automatically and fails the build; **partial** = something runs but leaves part of the surface unproven; **none** = nothing checks it.

"CI" below means `.github/workflows/crossplatform-tests.yml` (job `test-crossplatform` = the `VhcXTests.CrossPlatform` subset plus the headless GUI tests, ubuntu + macOS; job `verify-vhc` = this skill's subcommands, ubuntu) or `.github/workflows/ci-cd.yaml`. `ci-cd.yaml` runs only on a push to `master` or `dev`, or on a manual dispatch, never on a PR. Its `build-and-test` job (Windows) runs the full `VhcXTests`. On a PR, `pr-release-prep.yml` (`test-same-repo-pr`, or `build-for-pr` for forks) builds on Windows and runs `dotnet test vHC/HC.sln`, so the full suite runs before merge too. Its three live jobs run on self-hosted runners after that, and `report-render-smoke` runs last and opens whatever reports they produced.

Known gaps, whatever the row says:

- The live jobs (rows 4-6) never run on a PR. To run one against a branch before merging, dispatch it: `gh workflow run "CI/CD Pipeline" --ref <branch>`.
- `/pdf` exits 134 off Windows (DinkToPdf native library), so no off-Windows check drives it.
- VB365 live collection has no runner.
- The real GUI window needs Windows. `vhc-verify.sh winhost` drives it on a Windows host you control ([windows-host](./windows-host.md)); it is a manual, pre-PR check, not a gate.

| # | Surface | Gate | Status | What is still unproven |
|---|---|---|---|---|
| 1 | Build + unit tests | CI `test-crossplatform` (build, `VhcXTests.CrossPlatform`, headless GUI, ubuntu + macOS); CI `verify-vhc` `build`; `pr-release-prep.yml` `test-same-repo-pr` / `build-for-pr` (Windows, `dotnet test vHC/HC.sln` on every PR); `ci-cd.yaml` `build-and-test` (Windows, full `VhcXTests`, push to master/dev) | gated | Windows-only xUnit tests run only in the Windows jobs and skip elsewhere |
| 2 | CLI parse | CI `verify-vhc` `cli-checks` (`/help`, `/bogus`, bad and empty `/import`, colon form); `CliArgumentMapTests` in `test-crossplatform` keeps the flag table honest | gated | Flag-combination logic beyond these cases is only in the Windows `CArgsParserTEST` |
| 3 | CLI flag behaviour | CI `verify-vhc` `cli-matrix` (`/lite` `/security` `/days:N` `/debug` `/show:*` `/scrub:false` `/pptx` with `/import`) | gated | Flags that only act during live collection (`/lite` skipping per-job exports, `/vbr`, `/vb365`, `/host=`, `/remote`, `/silent`, `/credfile=`) and `/pdf` (crashes off Windows). The live jobs run `/pdf` and `/pptx` after merge |
| 4 | Live v12 | `ci-cd.yaml` `integration-test-vbr-12` (self-hosted; push to master/dev or manual dispatch only). Fail-closed assertions in `.github/scripts/Test-VhcLiveRun.ps1`: `SessionLog`, `CsvContent`, `RowCounts`, `CsvOutputs`, `ReportContent`, `ReportTables`, `Json`, `Exports`, `Scrub` | partial | Never runs on a PR. Content checks cover the core sections and the CSV manifest (`live-csv-expectations.json`), not every section |
| 5 | Live v13, local SQL | `ci-cd.yaml` `integration-test-vbr-13-sql`, same checks | partial | Same as 4 |
| 6 | Live v13, remote | `ci-cd.yaml` `integration-test-vbr-2` (remote), same checks | partial | Same as 4 |
| 7 | VBR core sections gated | `render` / `report-render-smoke`: license, backup server, security summary, proxies, repositories, job summary exist with body rows; `import-report` asserts fixture rows in the HTML; live jobs run `ReportContent` and `ReportTables` on seven core sections (adds Job Info) | gated | Core sections only. Optional sections (SOBR, extents, tiers, object storage) are reported, never failed |
| 8 | VBR all sections consistent | `import-report` nav-anchor to section-id check; `import-layouts` (identical html across 6 layouts); `render` nav click + toggle on every section; empty tables render a "No data available" row (`CHtmlEmptyStateTests`) | partial | Section *content* beyond the core ones is not asserted. `CHtmlEmptyStateTests` runs in the full `VhcXTests` (Windows, on every PR via `pr-release-prep.yml`) |
| 9 | Render | CI `verify-vhc` `render`; `ci-cd.yaml` `report-render-smoke` on the live reports (skips with a notice if the live jobs produced none) | gated | Chromium only; no print-media or narrow-viewport check |
| 10 | JSON export | `import-report` parses the json and requires `VhcVersion` + non-empty `Sections`; live jobs run `Json` (top-level keys and six required section keys) | gated | Per-section json content beyond those keys. The live part runs after merge only |
| 11 | PDF content | `HtmlToPdfConverterTests` (print-CSS and argument tests run everywhere, the real render is `WindowsOnlyFact`); live jobs run `Exports` (`%PDF` magic plus at least one page object); `winhost` runs the `WindowsOnlyFact` render before a PR | partial | `/pdf` exits 134 off Windows, so no off-Windows run produces a PDF. The unit tests run on Windows on every PR; the live `Exports` check runs after merge or on a dispatch. `winhost --live` is scrubbed, which writes no PDF, so it does not run `Exports` |
| 12 | PPTX content | `cli-matrix` `/pptx` asserts a valid OOXML zip with `ppt/presentation.xml`; `HtmlToPptxConverterTests`; live jobs run `Exports` (at least one slide with text) | gated | Slide layout and fidelity. The live part runs after merge only |
| 13 | Scrub completeness | `import-scrub` asserts five known names are absent from the anonymised html; `ScrubbedExportPolicyTests`, `GoldenImportScrubTests`, `Vb365ScrubCompletenessTests`; live jobs run `Scrub` (real host name absent from html and json) | partial | The CI import check covers only five strings. The unit tests run on Windows on every PR; the live check runs after merge or on a dispatch |
| 14 | CSV import layouts | CI `verify-vhc` `import-layouts` (flat, `VBR`, `VBR/<server>/<ts>`, `Original/VBR`, `Original/VBR/<server>/<ts>`, recursive) | gated | VB365 trees and server-prefixed CSV names (`localhost_Proxies.csv`) are not staged |
| 15 | VB365 report | `render` opens the VB365 sibling html the CLI writes from VBR input (console + nav only); VB365 golden CSVs under `GoldenBaselines/VB365` drive `Vb365ReportCompilerTests`, `Vb365TablesEdgeCaseTests`, `Vb365ImportDetectionTests` and others in the full `VhcXTests`; `winhost` runs that suite on Windows before a PR | partial | The CI `verify-vhc` job does not import the VB365 golden CSVs, so VB365 content is asserted only by xUnit on Windows (every PR via `pr-release-prep.yml`, or `winhost` before one) |
| 16 | VB365 live collection | none (no VB365 runner) | none | Everything |
| 17 | PostgreSQL config DB | `CBackupServerDbTypeTests` (collected `_vbrinfo` value mapped, "Unknown" rather than blank, live value wins); golden fixture `VBRConfig-v13-PostgreSql`; `winhost --live` collects from a local VBR 13 on PostgreSQL and fails unless the `_vbrinfo` `DbType` it wrote is `PostgreSql`, then runs the live checks (profile `vbr-13-pg`) | partial | The live proof is manual (a person runs `winhost` on a host that has this setup) and no CI job covers PostgreSQL. The unit level runs in the full `VhcXTests` (Windows, every PR via `pr-release-prep.yml`) |
| 18 | GUI | CI `test-crossplatform` runs `VhcGuiTests` (Avalonia.Headless) on ubuntu + macOS; `ci-cd.yaml#gui-e2e` (FlaUI, push or dispatch, lab runner) drives the real window, console hide, folder picker and a scrubbed Run; `winhost --live --gui` runs the same suite in a Windows host's interactive desktop (4 of 4 passed there, elevated, via `dotnet VeeamHealthCheck.dll`) | partial | The CI job is blocked while the lab runner is a session-0 service. Not proven: a non-elevated user under Windows Terminal, where the E2E suite shows the console window staying visible (see [gui-and-collection](./gui-and-collection.md#gui-e2e)); pixel rendering. See also [windows-host](./windows-host.md) |
| 19 | Creds / remote | `integration-test-vbr-2` (remote host) in `ci-cd.yaml`; credential unit tests in `VhcXTests` (Windows, including the DPAPI `WindowsOnlyFact`s, which `winhost` runs before a PR) | partial | `/credfile=` `/savecreds` `/silent` with a real remote host are not driven by `winhost` (it collects locally with Windows auth) |
| 20 | Collection scripts | `pester-tests.yml` and `ps51-syntax-validation.yml` (both run on PRs), plus the three live jobs (`CsvOutputs`, `CsvContent`, `RowCounts`) | partial | `validate-csv-schemas.yml` is manual-dispatch only (push and PR triggers are commented out). Script output correctness against a real server is checked only in the live jobs, after merge |
| 21 | Release pipeline | `commit-lint.yml`, `version-preview.yml` (both on PRs); `beta-release.yml` only promotes the newest dev `ci-cd.yaml` run that succeeded with a clean `virustotal-scan`; `create-release` / `manual-release.yml` run only on publish | partial | The publish path itself is exercised only by publishing |
| 22 | PR verification | `vhc-verify.sh pr <N>` plus a `pr-verify.yml` workflow on every PR that reports whether each touched surface was actually exercised on the PR head SHA. It catches "green but not exercised", for example `ci-cd.yaml` changes that no PR check runs | partial | The workflow is advisory (`PR_VERIFY_STRICT: 'false'`), so a NEEDS-RUN or FAILED verdict does not fail the PR. It reports NEEDS-RUN for live-lab surfaces instead of dispatching them from CI, so a human still runs those |
| 23 | Windows verification before a PR | `vhc-verify.sh winhost [ID] [--live] [--gui] [--status]` on a Windows host over SSH: build, full `VhcXTests`, a live `/run` plus the `Test-VhcLiveRun.ps1` checks, the FlaUI suite in the interactive desktop; `--status` posts `verify-vhc/windows-host` on a clean `HEAD`, which `vhc-verify.sh pr` counts for the Windows unit-test, GUI E2E and live-PostgreSQL surfaces | partial | Manual and per-host: one machine, one VBR version and database engine, and a person has to run it and keep an RDP session open. See [windows-host](./windows-host.md) |
