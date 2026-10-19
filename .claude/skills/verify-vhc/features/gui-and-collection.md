# GUI and live collection

A field engineer runs VHC on (or against) a Veeam server: the GUI or `/run` collects configuration with PowerShell, SQL, registry, WMI and log parsing, then builds the report and optional PDF/PowerPoint exports. **Collection and the real GUI window cannot be driven on macOS or Linux, and this skill does not claim to verify them.** The one exception is the headless GUI suite below, which proves GUI logic without a real window.

## Sub-features

- `gui-headless` runs the Avalonia.Headless xUnit suite against the real windows. **Drivable here**; see the section below.
- `gui-e2e` drives the real published exe with FlaUI on the lab runner (push or dispatch only). **Not drivable here**; see the section below.
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

Recipe: `.claude/skills/verify-vhc/scripts/vhc-verify.sh tests <ID>`. Expect `Passed!  - Failed: 0, Passed: 22 ... VhcGuiTests.dll` and `gui=0` on the exit-codes line. Evidence: `.verify-vhc/evidence/<ID>/tests-gui.log`.

Proves:

- The windows construct and the named controls exist and bind to state.
- Click and type flows: terms accept/decline, Run enable/disable, period radios, scrub and PDF toggles, path box, product selector, theme toggle (persisted).
- Dialog flows: add, cancel and remove servers (including stored credentials), credential prompt, About, confirm-yes/no.
- State changes land in `CGlobals` and the settings store, which the tests isolate and restore.
- Clicking Run calls the `RunAction` seam, which the tests replace, so no collection ever runs.

Does not prove:

- Real Win32 window and console hide on Run (see [gui-e2e](#gui-e2e)).
- DPAPI credential storage (the tests isolate the stores and never exercise Windows DPAPI).
- The native folder picker (see [gui-e2e](#gui-e2e)).
- How it renders on real Windows (fonts, DPI, theme fidelity).
- Live collection, or what `Run` itself does once invoked.

## gui-e2e

`vHC/VhcGuiE2ETests` (`net10.0-windows`, xUnit, FlaUI.UIA3) launches the real published `VeeamHealthCheck.exe` and drives it through UI Automation. CI job: `ci-cd.yaml#gui-e2e` on the `[self-hosted, vbr-13-sql]` runner, after `integration-test-vbr-13-sql`. It runs on push to dev/master or a dispatch, never on a pull_request. Status: pending its first green run, so treat it as unproven until then.

Controls are found by AutomationId. Avalonia's `ControlAutomationPeer.GetAutomationIdCore` returns `AutomationProperties.AutomationId ?? Owner.Name` (checked in 11.3.20), so the `x:Name` values the headless tests use are the ids; no XAML change.

Proves:

- The real main window opens, is a visible Win32 window, has the title `Veeam Health Check` (or `... - Remote Mode`), and the named controls exist.
- The app's console is hidden, checked from outside via `AttachConsole` + `GetConsoleWindow` + `IsWindowVisible`, once after launch and again after clicking Run. The hide is done at startup (`CArgsParser.HideConsoleIfOwned`), not by the Run click, so both moments are asserted. The test starts the exe on its own new console, like a double-click.
- The browse button opens the native folder picker, accepts a path through UIA, and the path box shows it.
- A full Run on the local VBR in scrub mode, started from the terms prompt, exits 0 and writes `vHC-AnonymousReport/*.html` under the chosen folder with the core section ids and without the machine name.
- A window screenshot is taken and is not blank. It is never uploaded (it shows the host name and paths); the job logs only its name and dimensions.

Does not prove:

- Console visibility under Windows Terminal as the default terminal: the console window is then a hidden pseudo window, so the test can only assert that no conhost or Windows Terminal window appeared. Deterministic only with classic conhost.
- Pixel-level rendering, DPI and theme fidelity (the screenshot is checked for non-blank only).
- Dialogs other than the terms prompt, and credential or Manage Servers flows (headless covers those).
- PDF and PPTX (scrub mode skips them), unscrubbed reports, remote hosts, VBR 12, VB365.

Run it (Windows desktop session, VBR installed for the Run test):

```powershell
$env:VHC_E2E_EXE = 'C:\path\to\VeeamHealthCheck.exe'   # without it every test is skipped with that reason
$env:VHC_E2E_LIVE = '1'                                  # enables the full Run; without it that test is skipped
dotnet test vHC/VhcGuiE2ETests/VhcGuiE2ETests.csproj
```

Optional: `VHC_E2E_OUT` (output and screenshots folder, default `%TEMP%\vhc-e2e`), `VHC_E2E_RUN_TIMEOUT_MIN` (default 30). `dotnet test vHC/HC.sln` skips these tests, so build-and-test and pr-release-prep are unaffected. Each test kills only the PID it started.

Session 0 requirement: UI Automation needs an interactive desktop. A GitHub runner installed as a Windows service is in session 0 and sees no windows. The job's first step prints the session id and `Environment.UserInteractive` and fails with a single message if it is session 0 or the desktop is locked. Fix: start the runner from an interactive logon (`run.cmd` at logon), enable auto-logon and disable the screen lock.

## Gotchas


- A passing macOS `import-report` run says nothing about the collection scripts. Do not cite it as proof of collection or GUI behavior.
- `CGlobals.REMOTEEXEC` does not mean the scripts run on the remote server; the PowerShell module always runs locally and connects out. Preflight checks must run regardless (repo CLAUDE.md).
- Never put real credentials, customer CSVs or a real VBR hostname in this repo, in evidence you commit, or in an issue. Use the synthetic golden CSVs.
- `/silent` and `/savecreds` are mutually exclusive (exit 2); `/credfile=` errors exit 6. These exit-code paths are unit-tested in `CArgsParserCredFileTests` and `SilentModeTests`, which do run off Windows via [build-and-tests](./build-and-tests.md).
