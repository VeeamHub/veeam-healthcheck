# VBR Report Redesign — Resume Plan
**Branch**: `feature/report-redesign-executive-dashboard`
**Last commit**: `c72d69e`
**Resume session**: check `/Users/adam/.claude/projects/-Users-adam-code-veeam-healthcheck/` for latest `.jsonl`

---

## Execution Sequence

```
[DONE] #39 — No VB365 files modified          ✅ git diff confirmed empty
[DONE] #40 — No test files deleted             ✅ git diff confirmed empty

[READY] #38 — Run test suites                  run immediately, no code changes
[BLOCKED] #53 — Backup Server cleanup          Designer spec complete, ready for Engineer

[DEFERRED] #30/#31 — Arch migration Phase 2-3  separate Algorithm session
```

---

## #53 — Backup Server Cleanup (Engineer-ready)

### Files to change

| File | What changes |
|------|-------------|
| `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs` | Add `Subsection(string text)` helper near `header3()` (line ~284) |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` | Replace all `header3` + `Table()` calls in Backup Server scope |

### CHtmlFormatting.cs — add helper

```csharp
public string Subsection(string text)
{
    return $"<div class=\"subsection\"><h4>{text}</h4></div>";
}
```

### CHtmlTables.cs — exact changes

| Location | Current | Replace with |
|----------|---------|-------------|
| `WriteTupleListToHtml()` line ~426 | `"<table><thead><tr>"` | `"<table class=\"content-table\"><thead><tr>"` |
| `AddBkpSrvTable()` line ~557 | `this.form.header3("Config Backup Info")` | `this.form.Subsection("Config Backup Info")` |
| `ConfigDbTable()` line ~455 | `this.form.header3("Config DB Info")` | `this.form.Subsection("Config DB Info")` |
| `ConfigDbTable()` line ~456 | `this.form.Table()` | `"<table class=\"content-table\">"` |
| `AddDbCoresRam()` line ~524 | `TableData(string.Empty, dbCoresToolTip)` | `TableData("<span class=\"text-muted\">&mdash;</span>", dbCoresToolTip)` |
| `AddDbCoresRam()` line ~533 | `TableData(string.Empty, dbRamToolTip)` | `TableData("<span class=\"text-muted\">&mdash;</span>", dbRamToolTip)` |
| `AddTable(string, string)` line ~781 | `this.form.header3(title)` | `this.form.Subsection(title)` |
| `AddTable(string, string)` line ~782 | `this.form.Table()` | `"<table class=\"content-table\">"` |
| `AddTable(string, List<string>)` line ~792 | `this.form.header3(title)` | `this.form.Subsection(title)` |
| `AddTable(string, List<string>)` line ~793 | `this.form.Table()` | `"<table class=\"content-table\">"` |

**No CSS changes needed.** `.subsection`, `.subsection h4`, and `.text-muted` already exist.

**After implementing:** build → deploy → open report → verify Backup Server section shows clean subsection headers with no underline decoration.

---

## #38 — Test Suite Validation

### CrossPlatform tests (run locally on macOS — do this first)

```bash
cd /Users/adam/code/veeam-healthcheck
dotnet test vHC/VhcXTests.CrossPlatform/VhcXTests.CrossPlatform.csproj \
  --logger "console;verbosity=detailed"
```

### Windows-only tests (run on zbook via SSH)

```bash
# Ensure repo is present on zbook at C:\repos\veeam-healthcheck (or adjust path)
ssh -i ~/.ssh/id_ed25519 -o IdentitiesOnly=yes cac89@192.168.20.237 \
  "cmd /c \"cd C:\\repos\\veeam-healthcheck && dotnet test vHC/VhcXTests/VhcXTests.csproj --logger console;verbose 2>&1\""
```

### Pass criteria
- Both suites: exit code 0, output `Test Run Successful`, zero `FAILED` lines
- Test count on branch ≥ test count on master (no tests deleted)

---

## #30/#31 — Architecture Migration (Deferred — next session)

**Decision:** Separate Algorithm run. CHtmlTables.cs is 2,074 lines; 13 methods remain inline.

### Phase 2 extraction list (priority order)

| Method | Lines | Target class |
|--------|-------|-------------|
| `AddSrvSummaryTable` | ~40 | `CInfraChipSection.cs` |
| `AddMissingJobsTable` | ~61 | `CMissingJobsTable.cs` |
| `AddBkpSrvTable` + private helpers | ~80 | `CBackupServerSection.cs` |
| `AddSecSummaryTable` | ~73 | `CSecuritySummarySection.cs` |
| `AddMultiRoleTable` | ~66 | `CMultiRoleTable.cs` |
| `AddJobConTable` + `AddTaskConTable` | ~116 | `Concurrency Tables/` |
| `AddSecurityReportSecuritySummaryTable` | ~50 | `CSecurityReportTable.cs` |
| `AddSobrExtTable` | ~153 | `CSobrExtTable.cs` |
| `LicTable` | ~106 | `CLicenseTable.cs` |
| `AddProtectedWorkLoadsTable` | ~284 | `CProtectedWorkloadsSection.cs` |

**Phase 3** (SectionStart/SectionEnd via base class contract) blocked on Phase 2 completion.

---

## Deploy Pattern

```bash
dotnet publish vHC/HC_Reporting/VeeamHealthCheck.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o /tmp/vhc-build

scp -i ~/.ssh/id_ed25519 -o IdentitiesOnly=yes \
  /tmp/vhc-build/VeeamHealthCheck.exe \
  cac89@192.168.20.237:C:/temp/VeeamHealthCheck.exe

ssh -i ~/.ssh/id_ed25519 -o IdentitiesOnly=yes cac89@192.168.20.237 \
  "cmd /c \"start /wait C:\\temp\\VeeamHealthCheck.exe \
  /import:C:\\temp\\vHC\\Original\\VBR\\localhost\\20260305_100108 /html && echo DONE\""

# SCP latest from C:\temp\vHC\vHC-Report\*.html
```
