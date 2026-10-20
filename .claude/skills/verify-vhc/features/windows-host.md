# Windows host (pre-PR verification)

Everything else in this skill runs on macOS or Linux. `winhost` runs the parts that need Windows, on a Windows machine you can reach over SSH, **against your current working tree, including uncommitted changes**. Nothing needs to be pushed, no PR has to exist, and CI is not involved.

```bash
export VHC_WINHOST=user@winbox        # required; there is no default and no host is ever written into the repo
.claude/skills/verify-vhc/scripts/vhc-verify.sh winhost [ID] [--live] [--gui] [--status] [--no-tests]
```

## Sub-features

- `winhost-build` runs `dotnet build vHC/HC.sln` on the host.
- `winhost-tests` runs the whole `VhcXTests` project there, so the `[WindowsOnlyFact]` tests (DPAPI, registry, native PDF render) run instead of skipping. Counts come from the TRX.
- `winhost-live` (`--live`) publishes the single-file exe the release build produces, runs a real `/run` on the host's local VBR with scrub on and an explicit `/outdir`, then runs the same `Test-VhcLiveRun.ps1` checks the CI live jobs use.
- `winhost-gui` (`--gui`) runs `VhcGuiE2ETests` inside the host's interactive desktop session (see below), not in the SSH session.
- `winhost-status` (`--status`) posts a GitHub commit status on `HEAD`, so the PR verifier can count the run.

## Setup (once per host)

1. **SSH key.** Key-based login that never prompts (`ssh -o BatchMode=yes $VHC_WINHOST 'exit'` must work). The account must be an administrator on the host, because `--gui` registers a short-lived scheduled task. Put per-machine ssh flags (an address family, a port) in `VHC_WINHOST_SSH_OPTS`; its default is `-o BatchMode=yes -o ConnectTimeout=15 -o ServerAliveInterval=30`.
2. **PowerShell 7 as the default SSH shell.** `pwsh` must be what a remote command runs in, and on the PATH. The Windows OpenSSH default is `cmd`; change it with `New-ItemProperty HKLM:\SOFTWARE\OpenSSH -Name DefaultShell -Value 'C:\Program Files\PowerShell\7\pwsh.exe' -PropertyType String -Force`.
3. **Tooling the build needs.** The .NET 10 SDK and `tar` (it ships with Windows 10+). `git` is not needed on the host; the tree arrives as a tarball. NuGet restore runs on the host, so it needs access to nuget.org. The script installs nothing.
4. **For `--live`:** VBR installed and running locally with the Veeam PowerShell module, and an SSH account that is a VBR administrator. VHC on the VBR server uses Windows authentication, so no credential is passed or stored. The step fails unless the collected `_vbrinfo` `DbType` is `PostgreSql` (profile `vbr-13-pg`); for a host on another engine set `VHC_WINHOST_EXPECT_DB` (for example `MsSql`) and `VHC_WINHOST_LAB_PROFILE` (a key of `.github/scripts/live-csv-expectations.json`, or empty for none).
5. **For `--gui`: an interactive desktop session that stays connected.** Sign in over RDP as the same user and leave the session open. **Disconnecting an RDP session locks the desktop, and a locked desktop has no UI Automation tree.** Close the RDP *window* only if your client keeps the session (check `query user` shows `Active`, not `Disc`), or use a console/auto-logon session instead. Also disable the screen lock and the screen saver timeout on that account.

Scratch space is `%LOCALAPPDATA%\vhc-verify\<ID>` on the host, removed at the end of every run.

## What it does

1. Archives `git ls-files --cached --others --exclude-standard` (files that exist now), copies the tarball over `scp`, unpacks it in the scratch folder.
2. Runs `Invoke-WinHostVerify.ps1` there, which builds, tests, publishes, runs and drives the GUI, writing logs, TRX files and `summary.json`.
3. Copies the evidence back to `.verify-vhc/evidence/<ID>/winhost/` (git-ignored, local only), then removes the scratch folder and checks the host is clean.
4. Prints the summary and exits non-zero on any failure.

Evidence includes the host's logs and a window screenshot, so it is for you; do not attach it to a public PR. The raw (unscrubbed) CSVs of the live run stay on the host and are deleted with the scratch folder.

### The GUI step

UI Automation needs a real desktop, and an SSH session is session 0 with none. `--gui` first checks that the SSH user has an `Active`, unlocked interactive session (`query user`, no `LogonUI.exe` in that session). If not, it stops before building, with one message such as `GUI-UNAVAILABLE: ... disconnected ...`.

Otherwise it registers a scheduled task with an **interactive logon for that user and the highest privileges** (no password is stored or needed; it runs only while the user is signed in), starts it, streams the test log back, and unregisters it in a `finally`. The task runs `dotnet test` of `VhcGuiE2ETests` with `VHC_E2E_EXE` pointing at the freshly published exe, so the tests drive the real window on the real desktop. The full-Run test only runs with `--live`; without it that test is skipped, and a status posted from such a run does not count as GUI coverage.

Why elevated: VHC shows an "Administrator Privileges Recommended" confirm to a non-elevated user on a local VBR, and an elevated process gets a classic console host rather than Windows Terminal. The consequence is that the console-hidden assertions describe the classic-conhost case. A non-elevated launch under Windows Terminal leaves a console window visible (see [gui-and-collection](./gui-and-collection.md#gui-e2e)); this tool does not exercise that case.

### Cleanup guarantees

- Every process the script starts is recorded with its PID and start time. A cleanup kills a recorded PID only while its start time still matches, and never kills anything by name.
- The scheduled task is unregistered whether the run passed, failed, timed out or was interrupted (the next run's pre-clean and `winhost`'s `EXIT` trap run the same cleanup).
- The build runs with `-nodeReuse:false -p:UseSharedCompilation=false`, so it leaves no build servers behind.
- One verification runs on a host at a time.
- After cleanup the tool asks the host whether the scratch folder or any `vhc-verify-*` task is left, and fails if so.

## `--status`

`--status` posts a commit status `verify-vhc/windows-host` on the exact `HEAD` SHA with `gh api`. It refuses to start when the working tree is dirty, because the status would then not describe that SHA (the SHA must also already be on GitHub). The description carries counts only, never host identity:

```
build ok; xunit 1353 pass 0 fail 13 skip; live ok 9 checks; gui 4 pass 0 fail 0 skip
```

`pr-surfaces.json` lets a check list `"status": [{ "context": "verify-vhc/windows-host", "step": "xunit" }]`, and `vhc-verify.sh pr` then counts the newest status of that context on the PR head SHA as having exercised that step (`build`, `xunit`, `live` or `gui`). A step the description does not name was not run, a `FAIL` or a non-zero fail count fails the check, and a GUI run with skipped tests does not count. Set `VHC_STATUS_REPO=OWNER/REPO` if `gh` cannot work out the repo, and `VHC_STATUS_DRY_RUN=1` to print the status instead of posting it.

Trust: anyone who can push to the repo can post any commit status, so this is evidence for the reviewer, not a security gate.

## What it proves

| Surface | Step | Proof |
|---|---|---|
| Solution builds on Windows | `build` | `dotnet build vHC/HC.sln` exit 0, 0 errors |
| Full `VhcXTests` including `[WindowsOnlyFact]` (DPAPI, registry, native PDF render) | `tests` | TRX counts: passed, failed, skipped |
| PDF render unit tests, VB365 report compilers, credential stores (coverage matrix rows 11, 15, 19) | `tests` | the same TRX; they run on every PR in `pr-release-prep.yml`, here they run before the PR |
| Live collection against the host's VBR 13 on PostgreSQL, and the report built from it (rows 4-6 shape, row 17) | `live` | `Test-VhcLiveRun.ps1`: `SessionLog`, `CsvContent`, `RowCounts`, `CsvOutputs`, `ReportContent`, `ReportTables`, `Json`, `Scrub` |
| The scrub path on a live run | `live` | `Scrub`: the host name is absent from the HTML and JSON |
| Real window, console hidden, native folder picker, scrubbed GUI Run, screenshot not blank (row 18) | `gui` | the FlaUI TRX |

`Exports` (PDF/PPTX content) is skipped by design: a scrubbed run writes neither. The live step also adds one check of its own, `DbType`, before the shared ones. Raw (unscrubbed) CSVs stay on the host.

## What it does not prove

- **A VBR that is not this host's.** One host, one VBR version, one database engine. v12, SQL Server, remote collection and VB365 are not covered (rows 4, 6, 16, 19 stay partial or none).
- **A clean-room machine.** The host has VBR installed and your sign-in. The build and test run from scratch in a new folder, but the machine is not pristine.
- **Everything a person sees.** Pixel rendering, DPI, themes and dialogs other than the ones the E2E suite drives. Under Windows Terminal as the default terminal the console check can only show that no console-host window appeared; see [gui-and-collection](./gui-and-collection.md#gui-e2e).
- **That the host is stable.** A locked screen, an update reboot, a full disk or a broken NuGet source fails the run; the failure message says which.
- **Anything about code you did not build.** It verifies the tree you ship, not the CI artifact. `ci-cd.yaml` remains the release gate.

## Why this, and not a self-hosted runner on a home machine

Do not register a home or lab machine as a self-hosted runner on this public repository. A workflow in a public repo can run from a fork PR, and a self-hosted runner executes whatever code the workflow runs, on your network, with your credentials, between jobs it does not clean up. GitHub's own guidance is to avoid self-hosted runners on public repositories. A runner is also always on, listening for work from the internet.

`winhost` inverts that: nothing listens, nothing external can start a job, and it runs only the tree *you* have on disk, when *you* run it. The host needs only an SSH key you control. The cost is that a person has to run it, and it is not a merge gate. The `--status` route covers that: the result is attached to the exact SHA and the PR verifier shows it as evidence. If a gate is wanted later, put the lab runner behind a private repository or an org-level runner group that only accepts reviewed branches, not this repo.

## Troubleshooting

| Message | Cause |
|---|---|
| `VHC_WINHOST is not set` | export it as `user@host` |
| `cannot reach the host over SSH` | key auth failed or the host is off; run `ssh -o BatchMode=yes $VHC_WINHOST exit` |
| `GUI-UNAVAILABLE: ... disconnected` | the RDP session is `Disc`; reconnect it and leave it connected |
| `GUI-UNAVAILABLE: ... locked` | `LogonUI.exe` is running in the session; unlock it and disable the lock |
| `another verification is already running` | a run is in progress on that host (the pid is in `%LOCALAPPDATA%\vhc-verify\active.json`) |
| `host after cleanup: DIRTY` | something refused to go away; the message names the scratch folder or the task |
| `--status refuses a dirty working tree` | commit or stash, so the status describes `HEAD` |
