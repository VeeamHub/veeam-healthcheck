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
- Do not report a skipped entry point as verified through a different path. An `/import` report does not verify collection; a passing xUnit run does not verify the GUI.

## Features

- [CSV import](./csv-import.md) covers `/import` generating the HTML and JSON report from collected CSVs. Drivable here.
- [Scrubbed report](./scrubbed-report.md) covers `/scrub:true` anonymization of the same report. Drivable here.
- [CLI arguments](./cli-arguments.md) covers `/help`, no-action handling and bad import paths. Drivable here.
- [Build and tests](./build-and-tests.md) covers `dotnet build` and the xUnit suites. Drivable here.
- [GUI and live collection](./gui-and-collection.md) covers the Avalonia GUI, VBR/VB365 collection, PDF export and Windows-only tests. **Not drivable on macOS**; documents what a Windows host must do.
