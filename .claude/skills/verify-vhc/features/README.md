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
- [GUI and live collection](./gui-and-collection.md) covers two things. `gui-headless` (Avalonia.Headless tests of the real windows) is drivable here. The real GUI window, VBR/VB365 collection, PDF export and Windows-only tests are **not drivable on macOS**; the file documents what a Windows host must do.

## Coverage matrix

Every surface a change to VHC can break, the check that gates it, and how well. Status: **gated** = a check that runs automatically and fails the build; **partial** = something runs but leaves part of the surface unproven; **none** = nothing checks it; **gated (pending PR)** = a parallel PR in flight adds the gate (named), so it is not gated on `dev` yet.

"CI" below means `.github/workflows/crossplatform-tests.yml` (job `test-crossplatform` = xUnit; job `verify-vhc` = this skill's subcommands, ubuntu) or `.github/workflows/ci-cd.yaml` (job `report-render-smoke` runs after the three live jobs).

| # | Surface | Gate | Status | What is still unproven |
|---|---|---|---|---|
| 1 | Build + unit tests | CI `test-crossplatform` (build, `VhcXTests.CrossPlatform`, headless GUI, ubuntu + macOS); CI `verify-vhc` `build`; `ci-cd.yaml` `build-and-test` (Windows, full `VhcXTests`) | gated (pending PR) | Windows-only xUnit tests are skipped off Windows. The PPTX/scrub unit tests PR (`test-export-scrub-coverage`) and the DB-type PR's `CHtmlEmptyStateTests` add the missing report-path unit coverage |
| 2 | CLI parse | CI `verify-vhc` `cli-checks` (`/help`, `/bogus`, bad and empty `/import`, colon form); `CliArgumentMapTests` in `test-crossplatform` keeps the flag table honest | gated | Flag-combination logic beyond these cases is only in the Windows `CArgsParserTEST` |
| 3 | CLI flag behaviour | CI `verify-vhc` `cli-matrix` (`/lite` `/security` `/days:N` `/debug` `/show:*` `/scrub:false` `/pptx` with `/import`) | gated (pending PR) | Flags that only act during live collection (`/lite` skipping per-job exports, `/vbr`, `/vb365`, `/host=`, `/remote`, `/silent`, `/credfile=`) and `/pdf` (crashes off Windows). Pending PR: `ci-live-checks-fail-closed` |
| 4 | Live v12 | `ci-cd.yaml` `integration-test-vbr-12` (self-hosted; push to master/dev or manual dispatch only), assertions in `.github/scripts/Test-VhcLiveRun.ps1` | partial | Never runs on a PR; fail-open today, `ci-live-checks-fail-closed` changes that. `report-render-smoke` now opens its report |
| 5 | Live v13, local SQL | `ci-cd.yaml` `integration-test-vbr-13-sql`, same caveats | partial | Same as 4 |
| 6 | Live v13, remote | `ci-cd.yaml` `integration-test-vbr-2` (remote), same caveats | partial | Same as 4 |
| 7 | VBR core sections gated | `render` / `report-render-smoke`: license, backup server, security summary, proxies, repositories, job summary exist with body rows; `import-report` asserts fixture rows in the HTML | gated (pending PR) | Only the six core ids. Live-run section assertions come with `ci-live-checks-fail-closed` |
| 8 | VBR all sections consistent | `import-report` nav-anchor to section-id check; `import-layouts` (identical html across 6 layouts); `render` nav click + toggle on every section | partial | Section *content* beyond the six core ones is not asserted; empty tables render nothing until the DB-type PR (`fix-report-dbtype-empty-sections`) adds the "No data available" row |
| 9 | Render | CI `verify-vhc` `render`; `ci-cd.yaml` `report-render-smoke` on the three real reports | gated | Chromium only; no print-media or narrow-viewport check |
| 10 | JSON export | `import-report` parses the json and requires `VhcVersion` + non-empty `Sections` | gated (pending PR) | Per-section json content and the live runs' json; pending `ci-live-checks-fail-closed` (runs JSON checks on all three live jobs) |
| 11 | PDF content | none today; `/pdf` crashes off Windows (DinkToPdf native library, exit 134) | gated (pending PR) | `test-export-scrub-coverage` (`HtmlToPdfConverterTests`); real PDF output needs Windows |
| 12 | PPTX content | `cli-matrix` `/pptx` asserts a valid OOXML zip with `ppt/presentation.xml` | gated (pending PR) | Slide content; pending `test-export-scrub-coverage` and the live-run export checks in `ci-live-checks-fail-closed` |
| 13 | Scrub completeness | `import-scrub` asserts five known names are absent from the anonymised html | gated (pending PR) | Only those five strings; pending `test-export-scrub-coverage` (`ScrubbedExportPolicyTests`) and the live-run scrub checks |
| 14 | CSV import layouts | CI `verify-vhc` `import-layouts` (flat, `VBR`, `VBR/<server>/<ts>`, `Original/VBR`, `Original/VBR/<server>/<ts>`, recursive) | gated | VB365 trees and server-prefixed CSV names (`localhost_Proxies.csv`) are not staged |
| 15 | VB365 report | `render` opens the VB365 sibling html the CLI writes from VBR input (console + nav only) | gated (pending PR) | No VB365 golden CSVs, so no VB365 content is asserted. Pending `test-vb365-fixtures` |
| 16 | VB365 live collection | none (no VB365 runner) | none | Everything |
| 17 | PostgreSQL config DB | none; the real v12 report shows a `PG SQL` row but nothing asserts DB type | gated (pending PR) | Pending `fix-report-dbtype-empty-sections` |
| 18 | GUI | CI `test-crossplatform` runs `VhcGuiTests` (Avalonia.Headless) on ubuntu + macOS | partial | The real window (Win32 console hide, native pickers) needs Windows; see [gui-and-collection](./gui-and-collection.md) |
| 19 | Creds / remote | `integration-test-vbr-2` (remote host) in `ci-cd.yaml`; credential unit tests in `VhcXTests` (Windows) | partial | DPAPI store and `/credfile=` `/savecreds` `/silent` are Windows-only and not driven in CI off Windows |
| 20 | Collection scripts | `pester-tests.yml`, `ps51-syntax-validation.yml`, `validate-csv-schemas.yml`, plus the three live jobs | partial | Script output correctness against a real server only in the live jobs (post-merge) |
| 21 | Release pipeline | `commit-lint.yml`, `version-preview.yml`; `create-release` / `beta-release.yml` / `manual-release.yml` run only on publish | partial | The publish path itself is exercised only by publishing; `beta-release-gates` (in flight) adds pre-publish gates |
