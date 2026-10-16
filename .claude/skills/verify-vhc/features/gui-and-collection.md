# GUI and live collection

A field engineer runs VHC on (or against) a Veeam server: the GUI or `/run` collects configuration with PowerShell, SQL, registry, WMI and log parsing, then builds the report and optional PDF/PowerPoint exports. **Collection and the real GUI window cannot be driven on macOS or Linux, and this skill does not claim to verify them.** The one exception is the headless GUI suite below, which proves GUI logic without a real window.

## Sub-features

- `gui-headless` runs the Avalonia.Headless xUnit suite against the real windows. **Drivable here**; see the section below.
- `gui-launch` opens a real Avalonia window (`/gui` or no arguments).
- `collect-vbr` runs the VBR PowerShell collection scripts under `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/`.
- `collect-vb365` runs the VB365 scripts.
- `collect-remote` uses `/remote /host=<name>` with stored or file-supplied credentials (`/savecreds`, `/credfile=`, `/silent`).
- `export-pdf` produces a PDF with `/pdf` (DinkToPdf native library, Windows).
- `export-pptx` produces a PowerPoint with `/pptx` (not available with `/scrub:true`).
- `credentials-dpapi` stores credentials with Windows DPAPI.

## How to get to it (user POV)

- Double-click `VeeamHealthCheck.exe` on Windows, or run it with `/gui`.
- `VeeamHealthCheck.exe /run` on a VBR/VB365 server, or `/run /remote /host=<name>` from a workstation with the Veeam PowerShell module.

## Driving it with vhc-verify.sh

Preconditions that are **not met on macOS/Linux**:

- A Windows host (Windows 10 1607+ or Server 2012+).
- For collection: Veeam Backup & Replication or VB365 reachable, `pwsh` 7 and the Veeam PowerShell module on the local machine, and a Veeam Backup Administrator account.

What was attempted and what happened off Windows:

- **No arguments or `/gui`.** The process reaches `LaunchUi` and throws from `Avalonia.AppBuilder.SetupWithLifetime`; no window opens. Unverified here.
- **`/run` without `/import`.** Mode detection looks for `Veeam.Backup.Service` / `Veeam.Archiver.Service` processes and finds none; with `/host=` it would try to run PowerShell against a server that does not exist. Unverified here.
- **`/pdf`.** With `/import ... /pdf` the log shows `Exporting HTML to PDF...` then `ERROR Failed to export PDF: COM Interop is not supported on this platform.` (DinkToPdf); no PDF is written. The HTML is still produced.
- **DPAPI credential tests.** Skipped by `[WindowsOnlyFact]` in the xUnit run.

What a Windows verification should do (for whoever has the host): run `VeeamHealthCheck.exe /run /outdir=<scratch>` on a lab VBR server, then compare the produced `vHC-Report` HTML and `Original\VBR\<server>\<timestamp>\*.csv` against the same CSVs fed back through `/import` (see [csv-import](./csv-import.md)); that closes the collection-to-report loop that this skill cannot.

## gui-headless

`vHC/VhcGuiTests` has 22 Avalonia.Headless xUnit tests that construct the real windows (main window, ManageServers, credential prompt, About disclaimer, `AvaloniaUiNotifier`) with no display, and drive them by name.

Recipe: `.claude/skills/verify-vhc/scripts/vhc-verify.sh tests <ID>` (runs it with `CI=true`, which skips a local SMB post-build copy). Expect `Passed!  - Failed: 0, Passed: 22 ... VhcGuiTests.dll` and `gui=0` on the exit-codes line. Evidence: `.verify-vhc/evidence/<ID>/tests-gui.log`.

Proves:

- The windows construct and the named controls exist and bind to state.
- Click and type flows: terms accept/decline, Run enable/disable, period radios, scrub and PDF toggles, path box, product selector, theme toggle (persisted).
- Dialog flows: add, cancel and remove servers (including stored credentials), credential prompt, About, confirm-yes/no.
- State changes land in `CGlobals` and the settings store, which the tests isolate and restore.
- Clicking Run calls the `RunAction` seam, which the tests replace, so no collection ever runs.

Does not prove:

- Real Win32 window and console hide on Run.
- DPAPI credential storage (the tests isolate the stores and never exercise Windows DPAPI).
- The native folder picker.
- How it renders on real Windows (fonts, DPI, theme fidelity).
- Live collection, or what `Run` itself does once invoked.

## Gotchas

- Without `CI=true` the GUI test project's post-build step tries a local SMB copy; the helper sets `CI=true` to skip it.

- A passing macOS `import-report` run says nothing about the collection scripts. Do not cite it as proof of collection or GUI behavior.
- `CGlobals.REMOTEEXEC` does not mean the scripts run on the remote server; the PowerShell module always runs locally and connects out. Preflight checks must run regardless (repo CLAUDE.md).
- Never put real credentials, customer CSVs or a real VBR hostname in this repo, in evidence you commit, or in an issue. Use the synthetic golden CSVs.
- `/silent` and `/savecreds` are mutually exclusive (exit 2); `/credfile=` errors exit 6. These exit-code paths are unit-tested in `CArgsParserCredFileTests` and `SilentModeTests`, which do run off Windows via [build-and-tests](./build-and-tests.md).
