# Phase 01: Quick-Win Bug Fixes

This phase applies three self-contained, well-diagnosed bug fixes from the existing V12 bug fix plan. All fixes are code-only changes that require no Windows environment to apply and can be verified through code review and the cross-platform unit test suite. By the end of this phase, bugs #113 (misleading zero-session log), #114 (VBR version empty without admin), and #108 (success rate over 100%) will be corrected, and the fix branch will be ready for CI validation.

## Tasks

- [ ] Read the full bug fix plan at `Plans/shimmying-exploring-graham.md` to internalize the root causes and fix descriptions for issues #113, #114, and #108 before touching any code.

- [ ] Fix issue #113 — remove dead debug variable in `CDataFormer.cs`:
  - Read `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs` around line 923
  - Delete the dead `v` variable and its log block (lines 923–927) that used `AddDays(+CGlobals.ReportDays)` and produced the misleading "Job Sessions after filter: 0" debug message
  - Confirm the correct count is still logged via the existing line that uses `-CGlobals.ReportDays`
  - Make no other changes to this file

- [ ] Fix issue #114 — add registry fallback for VBR version in `Get-VhcVbrInfo.ps1`:
  - Read `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcVbrInfo.ps1`
  - After the existing registry read that sets `$version`, add a fallback: if `$version` is null or empty, call `(Get-VBRBackupServerInfo).Build` to retrieve the version via the VBR service (no admin rights required)
  - Keep the change minimal — only add the fallback block, touch nothing else

- [ ] Fix issue #108 — correct job session success rate formula in `CJobSessSummary.cs`:
  - Read `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummary.cs` fully before editing
  - Locate the success-rate calculation that includes retries in the numerator: `(sessionCount - fails + retries) / sessionCount * 100`
  - Change the numerator to `(sessionCount - fails)` — retries are recovery attempts, not additional successes, and must not be added
  - Verify the retries value is still surfaced elsewhere in the table as display context (read-only, do not remove it)

- [ ] Build the solution to confirm all three fixes compile cleanly:
  - Run `dotnet build vHC/HC.sln --configuration Debug`
  - Fix any compiler errors introduced by the edits — do not proceed past a broken build
  - Note: tests require Windows; build verification alone is the gate on macOS

- [ ] Run the cross-platform test project to confirm no regressions:
  - Run `dotnet test vHC/VhcXTests.CrossPlatform/ --logger "console;verbosity=normal"` (adjust path if needed — check `vHC/` for the cross-platform project)
  - All previously passing tests must still pass
  - If a test fails, trace it to one of the three edits and fix the root cause surgically
