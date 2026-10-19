# CLI arguments

A user drives VHC from the command line with slash-style flags. This covers help output, what happens when no action is given, and what happens when `/import` points at a bad path.

## Sub-features

- `cli-help` prints the help menu for `/help`.
- `cli-no-action` warns and prints guidance when flags are given but no action (`/run`, `/gui`, `/hotfix`).
- `cli-import-bad-path` logs an error and writes no report when the import path has no CSVs.
- `cli-flag-forms` accepts `/import=<path>` and `/import:<path>`; `/outdir=<path>` redirects all output.

## Recognized flags

Every flag `CArgsParser.ParseAllArgs` recognizes, matched case-sensitively except the pattern forms (`/import`, `/credfile`, `/path`, `/outdir`, `/host`). `CliArgumentMapTests` (in `vHC/VhcXTests.CrossPlatform`) fails when this table and the parser disagree, so add or remove a row here whenever a `case` label is added to or removed from the parser. Convention: the first backticked token of each row starts with the flag's base name (the text up to the first `:` or `=`); keep the markers.

<!-- cli-flags:start -->
| Flag | Effect |
|---|---|
| `/help` | Prints the help menu. |
| `/run` | Runs a health check. |
| `/show:files` | Opens the output folder in Explorer afterwards (also `/show:report`, `/show:all`). |
| `/days:7` | Report window in days (also `/days:30`, `/days:90`, `/days:12`). |
| `/gui` | Opens the GUI. |
| `/lite` | Implies `/run`; skips the per-job HTML exports. |
| `/import=<path>` | Implies `/run`; builds the report from existing CSVs (`:` also accepted). |
| `/security` | Implies `/run`; security report only. |
| `/remote` | Marks the run as remote (connect to another server). |
| `/scrub:true` | Scrubs identifying data (also `/scrub:false`). |
| `/hotfix` | Runs the hotfix detector. |
| `/clearcreds` | Clears stored credentials during a run. |
| `/silent` | Unattended mode; no prompts. Excludes `/savecreds`. |
| `/savecreds` | One-shot interactive credential seed. |
| `/credfile=<path>` | Reads credentials from a JSON file. |
| `/pdf` | Also exports a PDF. |
| `/pptx` | Also exports a PPTX. |
| `/debug` | Enables debug logging. |
| `/vbr` | Targets VBR (combined with `/vb365` targets both). |
| `/vb365` | Targets VB365. |
| `/path=<path>` | Hotfix-detector path. |
| `/outdir=<path>` | Redirects all output. |
| `/host=<name>` | Targets a named server (a local hostname runs locally). |
<!-- cli-flags:end -->

## How to get to it (user POV)

- Run `VeeamHealthCheck.exe <flags>` in a terminal. Off Windows: `dotnet vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll <flags>`.
- Running with **no arguments** launches the GUI. That cannot start on macOS (see Gotchas).

## Driving it with vhc-verify.sh

Preconditions:

- `vhc-verify.sh doctor` prints `DOCTOR: ready`.
- Use an absolute scratch directory that you created for the run for any `/outdir`.

Two subcommands drive this surface and assert on logs, exit codes and the (absent) output folder:

- `vhc-verify.sh cli-checks <ID>` covers `/help`, `/bogus`, a nonexistent `/import` path, an `/import` dir with no CSVs, and the `/import:<path>` colon form. Ends with `CLI-CHECKS: verified` and `assertions: 19/19 passed`.
- `vhc-verify.sh cli-matrix <ID>` combines `/import` with the other flags and asserts per flag (below). Ends with `CLI-MATRIX: verified` and `assertions: 40/40 passed`.

The bullets below are the underlying recipes, for driving a case by hand:

- **Help.** Run `dotnet vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll /help`. Stdout contains `Veeam Health Check - Command Line Help` and lines beginning `  /run`, `  /import`, `  /scrub:true`, `  /outdir=<path>`. Filter the noise with `grep -v dlopen`.
- **No action.** Run the same binary with `/bogus`. The log contains `WARN No action specified - nothing was collected or reported.` and `Add /run to execute a health check, or /gui to open the interface.`
- **Bad import path.** Run with `/import=/nonexistent-dir /outdir=<abs scratch> /run`. The log contains `ERROR Import path does not exist: /nonexistent-dir` and `Failed to resolve import path. Exiting.`; `<scratch>` contains no `vHC-Report` folder.
- **Good import path.** See [csv-import](./csv-import.md).
- **Proof.** Save the log and `ls` of the output folder showing the absence (bad path) or presence (good path) of `vHC-Report`.

## Flag matrix (`cli-matrix`)

Semantics are from `CArgsParser.ParseAllArgs`. Each case runs `/import=<golden copy> /outdir=<scratch> /run <flag>`.

| Flag | What is asserted |
|---|---|
| (none) | VBR html + json written; header says `7 day report`; `id="jobsummary"` present |
| `/lite` | exit 0; VBR html + json still written (the parser sets `RunFullReport`). Per-job HTML exports do not exist on the import path either way, so `/lite` cannot be told apart from the baseline here; that difference needs a live collection |
| `/security` | `Report_VBR_Security_*.html` written; log has `Starting Security Report generation`; **no** full VBR report and **no** json |
| `/days:7`, `12`, `30`, `90` | log `Days set to N`; report header `N day report` |
| `/debug` | the log has `DEBUG` lines (114 vs 0 for the baseline) |
| `/show:files`, `/show:report`, `/show:all` | exit 0, report written, nothing launched (CLI mode forces open-explorer/open-html off) |
| `/scrub:false` | log `Setting SCRUB = false`; real names still present |
| `/pptx` | `Report_VBR_*.pptx` written and is a valid OOXML zip containing `ppt/presentation.xml` |

Not driven: `/pdf` (DinkToPdf's native library throws an unhandled `NullReferenceException` in a finalizer, exit 134, and no VBR report is written off Windows), `/gui`, `/remote`, `/host=`, `/vbr`, `/vb365` (collection intent, needs Veeam), `/savecreds`, `/credfile=`, `/silent`, `/clearcreds`, `/hotfix`, `/path=`.

## Gotchas

- Exit code is `0` for every case above, including the failed import: `EntryPoint.Main` returns 0 unless an exception escapes. Assert on log lines and on the files in the output folder.
- Argument parsing is covered in depth by unit tests (`vHC/VhcXTests/CArgsParserTEST.cs`, `Startup/`); run them via [build-and-tests](./build-and-tests.md) for flag-combination logic that is not worth driving by hand.
- `/help` on non-Windows prints several `WARN Script unblock failed ... kernel32` blocks before the menu. Ignore them.
- Collection-intent flags (`/vbr`, `/vb365`, `/host=`, `/remote`) imply `/run` and then try to collect, so on macOS they fail on missing Veeam components; do not use them for verification here.
- No-argument launch and `/gui` fail on macOS with an Avalonia/native-library exception in `LaunchUi`. That is the platform limit, not a regression.
