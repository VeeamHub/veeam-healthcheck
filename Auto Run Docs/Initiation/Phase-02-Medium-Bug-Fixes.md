# Phase 02: Medium-Complexity Bug Fixes

This phase addresses three medium-complexity bugs from the V12 fix plan: the hotfix detector serverlist crash (requires no test environment — pure path/guard logic), the NAS info parser broken on VBR v13 (regex replacement), and the PDF export timeout hang (#32). These fixes are more involved than Phase 01 but remain code-only changes verifiable through build and unit tests on macOS. By the end of this phase, the serverlist crash will be eliminated, NAS parsing will handle v13's log format, and PDF exports will no longer hang indefinitely.

## Tasks

- [ ] Re-read the relevant fix descriptions in `Plans/shimmying-exploring-graham.md` for items 3 (serverlist crash), 5 (NAS parser), and 7 (PDF hang) before editing anything.

- [ ] Fix the hotfix detector serverlist crash (plan item 3) across three files:
  - Read `vHC/HC_Reporting/Startup/CHotfixDetector.cs` lines 164–178 fully
  - Read `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` lines 598–626 fully
  - Read `vHC/HC_Reporting/Tools/Scripts/HotfixDetection/DumpManagedServerToText.ps1` around line 36
  - In `PSInvoker.cs` `ServerDumpInfo()`: set `WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory` on the `ProcessStartInfo`
  - In `CHotfixDetector.cs` `ServerList()`: construct the full path using `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PSInvoker.SERVERLISTFILE)` and add a `File.Exists()` guard with error logging before opening the `StreamReader`
  - Make no other changes to these files

- [ ] Fix the NAS info parser for VBR v13 (plan item 5):
  - Read `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/Get-NasInfo.ps1` fully
  - Replace the hardcoded `Remove(0,49)` prefix strip with the regex: `$line -replace '^\[[\d.:\s]+\]\s+\d+(?:\s+\[\w+\])?\s+\w+\s+\(\d+\)\s+', ''`
  - Update the parser to capture both parent and child log lines and join them on `ParentServerID` as described in the plan
  - Confirm the fix handles both v12 (49-char) and v13 (56-char) log formats

- [ ] Fix PDF export hang and margin overflow (plan item 7):
  - Read `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs` fully
  - Add a conversion timeout to prevent the DinkToPdf call from hanging indefinitely (wrap with a `Task` and `CancellationTokenSource` with a reasonable timeout — 120 seconds is a safe starting point)
  - Fix the margins: set `Top = 10, Left = 15, Right = 15, Bottom = 10` in the `MarginSettings`
  - Add print-specific CSS to constrain table widths for PDF output — search existing CSS in `Functions/Reporting/Html/` for patterns before adding new styles
  - Do NOT replace DinkToPdf in this phase; add a `// TODO: evaluate DinkToPdf replacement` comment noting it is unmaintained

- [ ] Build and cross-platform test after all three fixes:
  - Run `dotnet build vHC/HC.sln --configuration Debug`
  - Run the cross-platform tests as in Phase 01
  - Fix any build or test failures before marking this phase complete
