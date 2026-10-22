# Build and tests

A contributor builds the solution and runs the automated suites to prove that code compiles and logic behaves, on a machine that is not Windows.

## Sub-features

- `build-solution` builds `vHC/HC.sln` (Debug) with the .NET 10 SDK off Windows. The solution holds `VeeamHealthCheck`, `VhcXTests`, `VhcGuiTests` and `VhcGuiE2ETests`; `VhcXTests.CrossPlatform` is not in it and is built by `tests`.
- `tests-vhcxtests` runs `vHC/VhcXTests` (1,375 passing, 31 skipped on macOS; Windows-only tests marked `[WindowsOnlyFact]` skip individually).
- `tests-crossplatform` runs `vHC/VhcXTests.CrossPlatform` (121 tests, `net10.0`).
- `tests-gui` runs `vHC/VhcGuiTests` (22 Avalonia.Headless tests; see [gui-and-collection](./gui-and-collection.md#gui-headless)).
- `tests-filter` runs one class or category.

## How to get to it (user POV)

- `dotnet build vHC/HC.sln --configuration Debug`
- `dotnet test vHC/VhcXTests/VhcXTests.csproj`
- `dotnet test vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj`
- `dotnet test vHC/VhcGuiTests/VhcGuiTests.csproj`
- A single class: add `--filter "FullyQualifiedName~CredentialHelperTests"` (see the repo CLAUDE.md).

## Driving it with vhc-verify.sh

Preconditions:

- .NET 10 SDK installed (`dotnet --version`).
- Working tree has no uncommitted edit to `vHC/HC_Reporting/VeeamHealthCheck.csproj` (the helper only reverts a version bump it caused).

- **Build.** Run `.claude/skills/verify-vhc/scripts/vhc-verify.sh build`. Output ends `0 Error(s)` and `build ok: vHC/HC_Reporting/bin/Debug/net10.0-windows7.0/VeeamHealthCheck.dll`. If the csproj was auto-bumped the helper prints `reverted auto-bumped vHC/HC_Reporting/VeeamHealthCheck.csproj`.
- **Tests.** Run `.claude/skills/verify-vhc/scripts/vhc-verify.sh tests <ID>`. Output includes `Passed!  - Failed:     0, Passed:   121 ... VhcXTests.CrossPlatform.dll`, `Passed!  - Failed:     0, Passed:    22 ... VhcGuiTests.dll` and `Passed!  - Failed:     0, Passed:  1375, Skipped:    31 ... VhcXTests.dll`, then `exit codes: crossplatform=0 vhcxtests=0 gui=0`.
- **Proof.** Keep `.verify-vhc/evidence/<ID>/tests-crossplatform.log` and `tests-vhcxtests.log` and `tests-gui.log`.

## Gotchas

- Counts drift as tests are added; assert `Failed: 0` and a non-zero `Passed`, not the exact numbers.
- The skipped tests off Windows are the `[WindowsOnlyFact]` ones (DPAPI credential store, registry reader, the real PDF render). A skip is not a pass; those behaviors need a Windows run. The reverse also exists: `[UnixOnlyFact]` tests (for example in `PasswordNotOnCommandLineTests`) run here and skip on Windows, so a `winhost` run shows them as skips.
- The first build restores NuGet packages and takes a while; later builds are incremental.
- Every build runs `increment_version.ps1` (off Windows: via `pwsh` if installed, else `increment_version.sh`) and edits the csproj version segment. Commit hygiene: `git status` should not show the csproj after the helper runs.
- Build warnings (NETSDK1206, xUnit analyzers) are pre-existing and not a failure.
