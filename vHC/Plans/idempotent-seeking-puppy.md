---
prd: true
id: PRD-20260304-report-redesign-loop
status: PLANNED
mode: interactive
effort_level: Advanced
created: 2026-03-04
updated: 2026-03-04
iteration: 0
maxIterations: 128
loopStatus: null
last_phase: PLAN
failing_criteria: [ISC-C1, ISC-C2, ISC-C3, ISC-C4, ISC-C5, ISC-C6, ISC-C7, ISC-C8, ISC-C9]
verification_summary: "0/9"
parent: null
children: []
---

# VBR Report Redesign — Designer→Architect→Engineer Loop to Completion

> Complete the HTML report visual redesign by running a Designer→Architect→Engineer loop against real data from zbook until the Designer signs off with no critical/high gaps remaining. No functionality lost. All tests pass. No tests deleted.

## STATUS

| What | State |
|------|-------|
| Progress | 0/9 criteria passing |
| Phase | PLAN complete — awaiting user approval to BEGIN |
| Next action | Engineer: implement remaining visual gaps + architecture migration |
| Blocked by | Nothing |

---

## CONTEXT

### Problem Space

The `feature/report-redesign-executive-dashboard` branch has a modern CSS/JS foundation (css.css, ReportScript.js) but the C# code that generates HTML was still emitting legacy markup. Significant work has already been done:

- **Already complete**: KPI row, toolbar, progress bars, collapsible JS fix, CSS classes for cell coloring, god-class extraction (4 tables extracted), `WriteTupleListToHtml` self-contained, `AddRegKeysTable` HTML fixed, `AddBkpSrvTable` workaround removed, `CSectionTable<T>` typed builder used by 4 tables, `bgcolor=` replaced with CSS classes.
- **Remaining gaps (from Architect + Designer analysis)**: Badges on status fields, heatmap HTML emission, initial active nav link, two-col layout sections, bold first-column pattern, and cleanup of remaining legacy patterns.
- **Key insight**: All CSS already exists in `css.css` (heatmap, badges, two-col, cell-danger/warning/success). Gaps are 100% C# not emitting those classes — no CSS work remains.

### Key Files

| File | Role |
|------|------|
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` | God class (~3,350 lines) — primary target for remaining gap fixes |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs` | Orchestrates table calls, KPI row, toolbar |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs` | Top-level compiler, navigation, sidebar |
| `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs` | Primitive HTML helpers (SectionStart, TableHeader, Toolbar, etc.) |
| `vHC/HC_Reporting/css.css` | Already complete — all needed CSS classes exist |
| `vHC/HC_Reporting/ReportScript.js` | Already complete — collapsible, sort, scroll-spy |
| `vHC/HC_Reporting/HC_Reporting.csproj` | Has `EnableWindowsTargeting=true` — macOS can cross-compile win-x64 |
| `vHC/VhcXTests/` | Windows-only xUnit tests (WPF dependency) |
| `vHC/VhcXTests.CrossPlatform/` | Cross-platform tests (net10.0) |

### Constraints

- **No functionality lost**: VB365, data pipeline, CSV handlers, export pipeline (PDF, PPT) must not be modified.
- **Tests**: ≥282 total tests must pass. Zero test deletions. New tests may be added.
- **PowerShell 5.1 syntax**: Any `.ps1` scripts must remain PS 5.1 compatible (CI validates this).
- **Import mode path**: `C:\temp\vHC\Original\VBR\vbr-v13-rtm.home.lab\20260219_142501\`
- **Zbook deploy**: SCP to `cac89@zbook.home.lab`, run as `pwsh -NonInteractive -File C:\temp\script.ps1` pattern or direct exe execution.
- **Designer sign-off threshold**: No Critical or High gaps. Medium/Low acceptable to defer.

### Remaining Visual Gaps (from `/tmp/vhc-redesign-plan-v2.md`)

| # | Gap | CSS Available | C# Change Needed |
|---|-----|--------------|-----------------|
| 4 | Status badges (Success/Warning/Danger/Info) | `.badge-success/warning/danger/info` | Emit `<span class="badge badge-X">` in status columns |
| 5 | Job schedule heatmap | `.heatmap-grid`, `.heatmap-cell`, `.heat-0..5` | Emit heatmap grid HTML from schedule data |
| 6 | Initial active nav link | `.nav-link.active` | Set first nav link active in CHtmlCompiler |
| 7 | Two-col layout for paired sections | `.two-col` | Wrap paired section-cards in `<div class="two-col">` |
| 10 | Bold first column in data tables | `font-weight: 600` inline or th-like td | Add `style="font-weight:600"` to first td in key tables |
| 11 | Cleanup: remove inline style="" remnants | — | Strip legacy `style="..."` from generated HTML |
| 12 | Cleanup: remove empty title="" attributes | — | Remove `title=""` from `<td title="">` |
| 13 | Cleanup: `<br>` → CSS spacing | — | Replace `<br>` with `<p>` or margin CSS |
| 14 | Security grid section | `.security-grid`, `.security-item` | Emit security compliance as grid not table |

---

## PLAN

### Phase A — Engineer: Complete Visual Gaps + Architecture Migration

**Agent**: Engineer (worktree isolated from `feature/report-redesign-executive-dashboard`)

**Scope**: All gaps #4, #5, #6, #7, #10, #11-14 plus architecture migration phases 2-3 (extract 5+ more large methods from CHtmlTables.cs, migrate 6+ tables to CSectionTable<T>).

**Implementation order** (dependency-sorted):

1. **Gap #6** (initial active nav link) — `CHtmlCompiler.cs`: When building the sidebar nav, find the first `nav-link` anchor and add class `active`. One-line fix.

2. **Gap #4** (status badges) — `CHtmlTables.cs` + extracted table files: Identify all columns that currently emit text like "Success", "Warning", "Failed", "OK". Replace with:
   ```csharp
   private static string Badge(string text, string variant) =>
       $"<span class=\"badge badge-{variant}\">{text}</span>";
   ```
   Map: "Success"/"OK" → `success`, "Warning" → `warning`, "Failed"/"Error" → `danger`, others → `info`/`neutral`.

3. **Gap #7** (two-col layout) — `CHtmlBodyHelper.cs`: Identify logically paired sections (e.g., Proxy Config + Backup Repositories, Tape Servers + Tape Vaults). Wrap adjacent pairs:
   ```csharp
   HTMLSTRING += "<div class=\"two-col\">";
   HTMLSTRING += AddProxyTable(...);
   HTMLSTRING += AddRepoTable(...);
   HTMLSTRING += "</div>";
   ```

4. **Gap #10** (bold first column) — Key lookup tables in CHtmlTables.cs: Add `class="font-bold"` or inline style to first `<td>` in tables where first column is a label (Registry Keys, Config Backup, etc.).

5. **Gap #5** (job schedule heatmap) — Complex. Parse job schedule data from CSV objects to build 7×24 grid. Emit:
   ```html
   <div class="heatmap-grid">
     <div class="hour-label"> </div>
     <div class="hour-label">0</div>...<div class="hour-label">23</div>
     <div class="day-label">Mon</div>
     <div class="heatmap-cell heat-3" title="Monday 02:00 - 3 jobs"></div>
     ...
   </div>
   ```
   If schedule data is not reliably parsed, skip heatmap and emit a placeholder — do not fabricate data.

6. **Gap #14** (security grid) — `CHtmlTables.cs` security section: Replace table with security-grid:
   ```html
   <div class="security-grid">
     <div class="security-item">
       <div class="status-dot green"></div>
       <div class="label">Encryption Enabled</div>
     </div>
     ...
   </div>
   ```

7. **Cleanup #11-13**: In all C#-generated HTML, eliminate `style=""` (empty), `title=""` (empty), stray `<br>` tags that can be replaced with CSS. Search via Grep first to scope impact.

8. **Architecture Phase 2** (extract 5+ more methods from CHtmlTables.cs, target < 2,000 lines):
   - Extract `AddSobrExtTable` → `CSobrExtentTable.cs`
   - Extract `AddJobSumTable` → `CJobSummaryTable.cs`
   - Extract `AddTapeJobsTable` → `CTapeJobsTable.cs`
   - Extract `AddSobrTable` → `CSobrTable.cs`
   - Extract `AddWanAccTable` → `CWanAcceleratorTable.cs`

9. **Architecture Phase 3** (migrate 6+ large tables to `CSectionTable<T>`):
   - Targets: whichever of the Phase 2 extractions are amenable to the typed pattern
   - Follow existing pattern in `CTapeServersTable.cs`, `CCredentialsTable.cs`

**Engineer agent constraints**:
- Branch from `feature/report-redesign-executive-dashboard` (worktree)
- Run `dotnet build vHC/HC.sln` after each logical group — must remain green
- Do NOT touch any VB365 files, CSV handlers, export pipeline, or test files (except adding new tests)
- Commit each logical group separately

---

### Phase B — Build + Deploy to Zbook

**Executor**: Main agent (not subagent — requires SSH coordination)

```bash
# 1. Cross-compile from macOS
cd /Users/adam/code/veeam-healthcheck
dotnet publish vHC/HC_Reporting/HC_Reporting.csproj \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -c Release \
  -o ./publish/

# 2. SCP to zbook
scp ./publish/HC_Reporting.exe cac89@zbook.home.lab:"C:/temp/vhc_build.exe"

# 3. Run in import mode (SSH)
ssh cac89@zbook.home.lab 'cmd /c "C:\temp\vhc_build.exe /import:C:\temp\vHC\Original\VBR\vbr-v13-rtm.home.lab\20260219_142501\ /html"'

# 4. Find and SCP HTML output back
ssh cac89@zbook.home.lab 'cmd /c "dir C:\temp\vHC\Report\ /b /o-d"'
# Then SCP the most recent .html file
scp cac89@zbook.home.lab:"C:/temp/vHC/Report/[latest].html" ./local_report.html

# 5. Open locally for Designer review
open ./local_report.html
```

---

### Phase C — Designer→Architect→Engineer Loop

**Trigger**: After HTML is retrieved from zbook.

**Loop iteration**:

1. **Designer agent** reads the HTML file (`local_report.html`) and produces structured gap analysis:
   - Categorizes each gap: Critical / High / Medium / Low
   - References specific HTML element/section for each gap
   - States expected output vs actual output

2. **Termination check**: If Designer reports zero Critical + High gaps → loop exits → proceed to Phase D.

3. **Architect agent** takes Designer gap analysis and produces:
   - File-level implementation plan for each gap
   - Method/line-level change descriptions
   - Notes ordering constraints between fixes

4. **Engineer agent** implements Architect plan:
   - Branch from current feature branch (worktree)
   - Implement each fix
   - Run `dotnet build` — must stay green
   - Commit

5. **Rebuild and re-deploy** (Phase B steps 1-4 again).

6. **Repeat** from step 1.

**Maximum iterations**: 5 (if not signed off by iteration 5, pause and escalate to Adam for direction).

---

### Phase D — Final Verification Gate

```bash
# Test gate — must pass all tests, zero deletions
dotnet test vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj
# Count [Fact]/[Theory] methods — must be ≥282
grep -r "\[Fact\]\|\[Theory\]" vHC/VhcXTests/ vHC/VhcXTests.CrossPlatform/ | wc -l

# No VB365/pipeline files changed
git diff --name-only origin/master..HEAD | grep -E "VB365|CVb365|CsvHandler|CPdf|CPowerPoint|CExcel"
# Expected: empty output

# No test methods deleted
git diff origin/master..HEAD -- "**/*Tests*" | grep "^\-.*\[Fact\]"
# Expected: empty output
```

---

## IDEAL STATE CRITERIA (Verification Criteria)

- [ ] ISC-C1: All 6 remaining visual gaps emit correct CSS-class HTML | Verify: Read: inspect generated HTML for badge/heatmap/two-col/bold/security-grid patterns
- [ ] ISC-C2: Architecture phases 2-3 complete, CHtmlTables.cs under 2,000 lines | Verify: CLI: `wc -l vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs`
- [ ] ISC-C3: Win-x64 binary builds and deploys to zbook successfully | Verify: CLI: `dotnet publish -r win-x64` exits 0; SSH ls confirms exe present
- [ ] ISC-C4: HTML report retrieved from zbook and opens locally without errors | Verify: CLI: `open ./local_report.html` + file size > 100KB
- [ ] ISC-C5: Designer produces structured gap analysis of real HTML output | Verify: Custom: Designer agent returns categorized gap list with Critical/High/Medium/Low
- [ ] ISC-C6: All designer-identified Critical+High gaps have architect implementation plans | Verify: Custom: Architect agent returns file-level plan covering every Critical/High gap
- [ ] ISC-C7: Engineer implements all architect-planned fixes and build remains green | Verify: CLI: `dotnet build vHC/HC.sln` exits 0 after engineer commits
- [ ] ISC-C8: Designer signs off — zero Critical or High gaps remain in real HTML | Verify: Custom: Designer agent reports 0 Critical + 0 High gaps
- [ ] ISC-C9: All existing tests pass, zero deleted, new tests added where appropriate | Verify: CLI: `dotnet test` passes; grep [Fact] count ≥282; git diff shows no removed [Fact] lines
- [ ] ISC-A1: No VB365, CSV pipeline, or export pipeline files modified | Verify: CLI: `git diff --name-only` shows no VB365/CsvHandler/CPdf/CPowerPoint matches
- [ ] ISC-A2: No existing test methods deleted from any test project | Verify: CLI: `git diff -- "**/*Tests*" | grep "^\-.*\[Fact\]"` returns empty

## DECISIONS

- macOS cross-compile via `EnableWindowsTargeting=true` already in csproj — confirmed viable
- Designer sign-off threshold: zero Critical + High gaps (Medium/Low deferrable)
- Loop max iterations: 5 before human escalation
- Architecture extraction target: CHtmlTables.cs < 2,000 lines (from current ~3,350)
- Badge mapping: Success/OK=success, Warning=warning, Failed/Error=danger, others=neutral/info

## LOG

### Iteration 0 — 2026-03-04
- Phase reached: PLAN
- Criteria progress: 0/9 (0/11 including anti-criteria)
- Work done: Plan written. All previous session fixes committed (bc6934e, 31636d6, 82ba587). CSS complete. JS fixed. KPI row, toolbar, progress bars, cell coloring, 4 tables extracted.
- Failing: All ISC-C criteria (work not started yet for this plan)
- Context for next iteration: Begin with Phase A (Engineer agent implementing visual gaps). CSS is complete — only C# changes needed. Import mode path: C:\temp\vHC\Original\VBR\vbr-v13-rtm.home.lab\20260219_142501\
