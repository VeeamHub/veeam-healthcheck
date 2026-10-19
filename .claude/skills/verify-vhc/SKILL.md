---
name: verify-vhc
description: Prove Veeam Health Check (VHC Core) behavior off Windows. Builds the .NET solution, runs the CLI in /import mode against the repo's golden CSV fixtures to generate real HTML/JSON reports (plain and scrubbed), and opens the reports in a browser, exercises the CLI flags, and runs the cross-platform and headless GUI test suites, capturing evidence. Use when a change touches report generation, CSV parsing, scrubbing, CLI arguments or anything else that must be shown working, and no Windows + VBR server is available. States plainly what only a Windows host can verify.
---

# Verify Veeam Health Check (macOS / Linux)

VHC is a Windows utility (Avalonia GUI + CLI). It collects data from a Veeam Backup & Replication (VBR) or VB365 server with PowerShell, then turns CSVs into an HTML/JSON/PDF/PPTX report. **Collection and the GUI need Windows plus a VBR server. Report generation does not**: the CLI's `/import` mode renders a full report from a folder of CSVs, and that runs natively on macOS and Linux. This skill drives that path and the test suites.

## What a green run proves, and what it does not

| Surface | Drivable here | Proof |
|---|---|---|
| Solution builds | yes | `build` |
| CSV parsing + report generation (VBR) | yes | `import-report` |
| Scrub / anonymize path | yes | `import-scrub` |
| CSV folder layouts (flat, `VBR/<server>/<ts>`, `Original/VBR`, recursive) | yes | `import-layouts` |
| The report works when opened in a browser (console, nav, toggles, core sections, screenshots) | yes | `render` |
| CLI argument parsing, `/help`, bad-input handling | yes | `cli-checks`, [cli-arguments](features/cli-arguments.md) |
| CLI flags combined with `/import` (`/lite`, `/security`, `/days:N`, `/debug`, `/show:*`, `/pptx`) | yes | `cli-matrix` |
| xUnit suites (1,117 passed + 15 skipped, and 121 tests) | yes | `tests` |
| Avalonia GUI logic and dialogs, headless (22 tests) | yes, headless only | `tests`, [gui-and-collection](features/gui-and-collection.md#gui-headless) |
| Pester suites, every tracked `*.Tests.ps1` (collector logic with mocked cmdlets, `.github/scripts`) | yes, needs `pwsh` | `pester` (also run by `tests`) |
| Live collection (PowerShell, SQL, registry, WMI, logs) | **no, needs Windows + VBR/VB365** | [gui-and-collection](features/gui-and-collection.md) |
| Real GUI window (Win32 hide, native picker, Windows rendering) | **no, needs Windows** (it throws at startup on macOS) | [gui-and-collection](features/gui-and-collection.md) |
| PDF export (`/pdf`), Windows-only tests (DPAPI, registry) | **no / skipped** | [gui-and-collection](features/gui-and-collection.md) |

An `/import` run proves the report pipeline given known inputs. It says nothing about whether the collection scripts gather correct data on a real server. Never report a Windows-only surface as verified from this skill.

The feature map and the coverage matrix (which of the 21 surfaces are gated, partial or unchecked) are in [features/README.md](features/README.md). Read it, then use the matching feature file as the recipe.

## Helper

One script, repo-relative, executable. Run from anywhere inside the repo:

```bash
.claude/skills/verify-vhc/scripts/vhc-verify.sh build               # dotnet build vHC/HC.sln (Debug); reverts the csproj version auto-bump
.claude/skills/verify-vhc/scripts/vhc-verify.sh doctor              # read-only readiness check
.claude/skills/verify-vhc/scripts/vhc-verify.sh import-report [ID]  # /import against golden CSVs, assert on HTML + JSON
.claude/skills/verify-vhc/scripts/vhc-verify.sh import-scrub  [ID]  # same with /scrub:true, assert names are gone
.claude/skills/verify-vhc/scripts/vhc-verify.sh cli-checks    [ID]  # /help, /bogus, bad /import path: asserts on log lines, exit code, absent output
.claude/skills/verify-vhc/scripts/vhc-verify.sh cli-matrix    [ID]  # /import + /lite /security /days:N /debug /show:* /scrub:false /pptx, per-flag assertions
.claude/skills/verify-vhc/scripts/vhc-verify.sh import-layouts [ID] # /import over 6 folder layouts, reports must be identical
.claude/skills/verify-vhc/scripts/vhc-verify.sh render        [ID]  # open the import output (plain + scrubbed) in a browser via tools/report-smoke; needs bun
.claude/skills/verify-vhc/scripts/vhc-verify.sh ci            [ID]  # build + every subcommand above except tests (what CI runs)
.claude/skills/verify-vhc/scripts/vhc-verify.sh tests         [ID]  # all three test projects plus pester, logs captured
.claude/skills/verify-vhc/scripts/vhc-verify.sh pester        [ID]  # every tracked *.Tests.ps1, same discovery as pester-tests.yml; needs pwsh + Pester 5+
.claude/skills/verify-vhc/scripts/vhc-verify.sh cleanup       [ID]  # stop what we started, remove scratch input, keep evidence
```

`ID` names the evidence folder; default is a timestamp.

## Launch

VHC is a short-lived CLI, so "launch" means build once, then start each drive as its own process with its own output directory.

```bash
.claude/skills/verify-vhc/scripts/vhc-verify.sh build
```

Ready when the script prints `build ok: vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll`. Requires the .NET 10 SDK. The csproj targets `net10.0-windows7.0`; the repo sets `EnableWindowsTargeting` so it builds off Windows.

Each drive runs `dotnet <dll> /import=<abs dir> /outdir=<abs dir> /run /scrub:<bool>` in the background, with the PID written to `.verify-vhc/run.pid` and a 180 s timeout (`VHC_VERIFY_TIMEOUT` overrides). `render` also needs `bun` and a browser (system Chrome on macOS is picked up automatically; elsewhere `cd tools/report-smoke && bun install --frozen-lockfile && bunx playwright install chromium`). Always pass `/outdir`; without it the tool writes to a literal Windows-style `C:\temp\vHC` folder name under the current directory (git-ignored, safe to delete).

## Doctor

```bash
.claude/skills/verify-vhc/scripts/vhc-verify.sh doctor
```

Read-only. Checks: .NET SDK 10+, built DLL present and newer than every `.cs` source (a stale binary is the usual cause of a confusing result), the golden CSV fixtures exist, no leftover verification process, csproj not left with an un-reverted version bump. Exit 0 and `DOCTOR: ready` means worth driving. Run it first whenever anything looks off.

## Drive

Recipes live in the feature files. The primary one:

```bash
.claude/skills/verify-vhc/scripts/vhc-verify.sh import-report my-run
```

It copies the tracked golden CSVs (`vHC/HC_Reporting/Tools/GoldenBaselines/{VBRConfig,NasInfo,SessionReports}`) into `.verify-vhc/scratch/my-run/input`, runs the real CLI against the copy, and asserts the generated report contains rows that exist only in those CSVs (job `Production-VM-Backup`, repository `Linux Hardened Repo`, proxy `PROXY01`, VBR version `12.1.2.172`), that every nav anchor has a section, and that the tracked fixtures are byte-identical afterwards (11 assertions; 12 for `import-scrub`).

Drive the real CLI, not test hooks. Do not call internal classes or write a report by hand.

## Evidence

Everything lands in `.verify-vhc/evidence/<ID>/` (git-ignored):

- `command.txt`, `stdout.log`, `exit_code.txt`: the action and raw output
- `output/vHC-Report/*.html|json` (or `output/vHC-AnonymousReport/` for scrub): the resulting state
- `MANIFEST.sha256`: hashes of every output file
- `tests-crossplatform.log`, `tests-vhcxtests.log`, `tests-gui.log`: for the `tests` subcommand

Proof standard: exercise the CLI entry point; capture the command and its output, then assert on the **content** of the generated files (side effects), not only that files exist; confirm tracked inputs were not modified. Mocks are not used: the inputs are real CSV files in the production schema.

Do not trust the exit code alone: `EntryPoint.Main` returns 0 even when import resolution fails (see the Gotchas in [cli-arguments](features/cli-arguments.md)). The assertions on the files are the proof.

## Cleanup

```bash
.claude/skills/verify-vhc/scripts/vhc-verify.sh cleanup my-run
```

Stops only the PID recorded in `.verify-vhc/run.pid` (and only if its command line is this DLL), removes `.verify-vhc/scratch/`, never touches `.verify-vhc/evidence/`. Never `pkill` or kill by process name. Run cleanup after every failed attempt too. After cleanup, confirm `ls .verify-vhc/evidence/<ID>` still lists the report and `MANIFEST.sha256`.

## Isolation

Runs are independent processes with their own `/outdir` and a copy of the inputs, so two can run side by side if given different IDs (the pidfile tracks only the latest; give parallel runs separate checkouts or run them serially). Never point `/outdir` or `/import` at a directory you did not create for this run.

## Maintenance

Keep the map honest with `/maintain-verification-skill` when CLI flags, report sections or fixtures change.
