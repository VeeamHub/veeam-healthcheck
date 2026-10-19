# CSV import to health-check output

A user points VHC at a folder of already-collected CSV files and gets a self-contained HTML health check plus a JSON copy, without collecting anything. This is the one end-to-end output path that runs off Windows.

## Sub-features

- `import-resolve` finds the CSV folder from a flat path, a `VBR/<server>/<timestamp>` tree, an `Original/VBR` tree, or a recursive search.
- `import-render-html` writes `vHC-Report/Veeam Health Check Report_VBR_<host>_<timestamp>.html` with sections built from the CSVs.
- `import-render-json` writes the matching `.json` with `VhcVersion` and a `Sections` array.
- `import-no-collect` does not start PowerShell collection or touch the input CSVs.
- `import-vb365-sibling` also writes a `Report_VB365_*.html` (see Gotchas).

## How to get to it (user POV)

- CLI: `VeeamHealthCheck.exe /import=<path> /run` (also `/import:<path>`). On macOS/Linux: `dotnet vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll /import=<abs path> /outdir=<abs path> /run`.
- GUI: the import option in the Avalonia window. **Not drivable here**, see [gui-and-collection](./gui-and-collection.md).

## Driving it with vhc-verify.sh

Preconditions:

- `vhc-verify.sh build` succeeded and `vhc-verify.sh doctor` prints `DOCTOR: ready`.
- The golden CSVs are present under `vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig`, `NasInfo` and `SessionReports`.

- **Run the import.** Run `.claude/skills/verify-vhc/scripts/vhc-verify.sh import-report <ID>`. Output ends with `IMPORT-REPORT: verified`, `exit code: 0`, and `assertions: 11/11 passed`.
- **Output exists.** `ls .verify-vhc/evidence/<ID>/output/vHC-Report/` lists a `Report_VBR_*.html` of roughly 130 KB and a `Report_VBR_*.json`.
- **Content reflects input rows.** The helper greps the HTML for `<title>Veeam HealthCheck Report</title>`, `Production-VM-Backup` and `SQL-Servers-Backup` (from `_Jobs.csv`), `Linux Hardened Repo` (from `_Repositories.csv`), `PROXY01` (from `_Proxies.csv`) and `12.1.2.172` (from `_vbrinfo.csv`). All must be present.
- **Experiments gate.** The helper unsets `VHC_EXPERIMENTS` for the run and asserts the HTML has neither `id="orphanedsupersededbackups"` nor a `#orphanedsupersededbackups` nav link (the Orphaned & Superseded Backups section ships dark). To see the section, run the CLI by hand with `VHC_EXPERIMENTS=1` and expect both.
- **Nav anchors resolve.** The helper statically parses the HTML and requires every in-page `<a href="#id">` (26 in the golden report) to have an element with that `id`. The browser-side equivalent (click and visibility) is `render`, see [render](./render.md).
- **JSON shape.** The helper parses the JSON and requires `VhcVersion` and a non-empty `Sections` array (names such as `backupServer`, `securitySummary`, `jobSummary`).
- **Inputs untouched.** The helper asserts `git diff --quiet -- vHC/HC_Reporting/Tools/GoldenBaselines`.
- **Proof.** Keep `.verify-vhc/evidence/<ID>/{command.txt,stdout.log,exit_code.txt,MANIFEST.sha256,output/}`. The MANIFEST hashes every generated file.

## Folder layouts (`import-layouts`)

`CImportPathResolver.FindCsvDirectory` accepts several folder shapes. `vhc-verify.sh import-layouts <ID>` stages the golden CSVs into each, runs `/import` on the root, and requires a VBR html + json for every layout and that every layout's html (generation date normalised) and json are identical to the flat run:

| Layout name | CSVs live at |
|---|---|
| `flat` | `<root>/` |
| `vbr-direct` | `<root>/VBR/` |
| `vbr-server-ts` | `<root>/VBR/VBR01/2026.10.16.120000/` |
| `original-vbr` | `<root>/Original/VBR/` |
| `original-vbr-server-ts` | `<root>/Original/VBR/VBR01/2026.10.16.120000/` |
| `recursive` | `<root>/archive/2026/customer-a/collected/` (found by the depth-5 recursive search) |

Output ends with `IMPORT-LAYOUTS: verified` and `assertions: 17/17 passed`. VB365 trees (`VB365/<server>/<ts>`) resolve through the same code but are not driven here; there are no VB365 fixtures yet.

## Gotchas

- The golden CSVs are not a complete collection. There is no `_CollectionManifest.csv`, so the log says collector-level error surfacing is unavailable, and some tables are empty. That is expected.
- Every non-Windows run logs `ERROR ... Exception during VBR version detection` from the registry reader and `WARN Script unblock failed ... kernel32`. These are platform noise, not failures.
- The output file names embed the machine's hostname (no `vbrinfo` CSV name matches the lookup, so it falls back to the local host name). Evidence names therefore contain a local hostname; do not paste them into public issues or commits.
- A `Report_VB365_*.html` of about 1.5 MB is written alongside the VBR one even for VBR-only input. Assert on the `VBR` file.
- A stray folder named `Original\Log` (literal backslash in the name) appears under `/outdir`. That is a Windows path separator used verbatim on Linux/macOS and is harmless.
- Exit code is `0` even when import resolution fails; see [cli-arguments](./cli-arguments.md).
- Fixture markers prove the pipeline carried the input rows through. They do not prove the numbers are right for a real environment; for that, compare against a real collection on Windows.
