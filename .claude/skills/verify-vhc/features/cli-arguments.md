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

There is no wrapper subcommand; run the binary directly and capture output with `> .verify-vhc/evidence/<ID>/cli.log 2>&1`.

- **Help.** Run `dotnet vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll /help`. Stdout contains `Veeam Health Check - Command Line Help` and lines beginning `  /run`, `  /import`, `  /scrub:true`, `  /outdir=<path>`. Filter the noise with `grep -v dlopen`.
- **No action.** Run the same binary with `/bogus`. The log contains `WARN No action specified - nothing was collected or reported.` and `Add /run to execute a health check, or /gui to open the interface.`
- **Bad import path.** Run with `/import=/nonexistent-dir /outdir=<abs scratch> /run`. The log contains `ERROR Import path does not exist: /nonexistent-dir` and `Failed to resolve import path. Exiting.`; `<scratch>` contains no `vHC-Report` folder.
- **Good import path.** See [csv-import](./csv-import.md).
- **Proof.** Save the log and `ls` of the output folder showing the absence (bad path) or presence (good path) of `vHC-Report`.

## Gotchas

- Exit code is `0` for every case above, including the failed import: `EntryPoint.Main` returns 0 unless an exception escapes. Assert on log lines and on the files in the output folder.
- Argument parsing is covered in depth by unit tests (`vHC/VhcXTests/CArgsParserTEST.cs`, `Startup/`); run them via [build-and-tests](./build-and-tests.md) for flag-combination logic that is not worth driving by hand.
- `/help` on non-Windows prints several `WARN Script unblock failed ... kernel32` blocks before the menu. Ignore them.
- Collection-intent flags (`/vbr`, `/vb365`, `/host=`, `/remote`) imply `/run` and then try to collect, so on macOS they fail on missing Veeam components; do not use them for verification here.
- No-argument launch and `/gui` fail on macOS with an Avalonia/native-library exception in `LaunchUi`. That is the platform limit, not a regression.
