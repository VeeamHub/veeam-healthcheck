# Veeam Health Check — Full Code Review (dev branch)

> Reviewer: **Fable 5** (8 parallel subsystem reviewers) · Branch: `dev` · Date: 2026-06-11
> Scope: 260 C# files / ~48k LOC across the whole `HC_Reporting` project.
> These are findings formatted as GitHub issues. **Nothing has been committed or pushed to git.**

## Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 27 |
| Medium | 55 |
| Low | 25 |
| **Total** | **107** |

### By domain

| Domain | Findings |
|---|---|
| `collection-data` | 20 |
| `reporting-vbr-core` | 18 |
| `reporting-vb365` | 15 |
| `startup-cli` | 14 |
| `reporting-vbr-tables` | 12 |
| `csv-datatypes` | 12 |
| `analysis-monitor` | 11 |
| `collection-security` | 5 |

### Cross-cutting themes (appear in multiple domains)

- **HTML injection / un-encoded output** — collected CSV data is interpolated into report HTML without HTML-encoding across VBR tables, VBR core, and VB365. (vbr-tables-01, vbr-core-01, vb365-01)
- **Culture-sensitive parse/format** — `Parse`/`ToString` without `InvariantCulture` on machine-locale CSVs yields wrong numbers/dates. (csv-datatypes-01, vbr-tables-04, vbr-core-16, vb365-08)
- **Swallowed exceptions → silent failure** — empty/bare catch blocks mark runs successful while writing partial or no output. (collection-data, vbr-core, vb365-02/09, csv-datatypes-04/05, startup-cli)
- **Undisposed CsvReader/StreamReader & process/runspace handles** — leaked file handles per compile. (csv-datatypes-02, vb365-11, collection-data-02)
- **Positional CSV `[Index]` mapping** — silently corrupts when collector column order changes. (csv-datatypes-03, vb365-12)
- **Exit-code / failover correctness** — failures masked as exit 0; PS5 failover and MFA tests don't behave as intended. (startup-cli-01/02/03, collection-data-01/04/06/07)

## All findings (by severity)

### High

| ID | Domain | Title | Conf | First location |
|---|---|---|---|---|
| [`analysis-monitor-01`](analysis-monitor/ISSUE-analysis-monitor-01.md) | analysis-monitor | Stop writing DPAPI-protected credentials as plaintext to vhc-monitor.yaml | High | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:84` |
| [`collection-data-01`](collection-data/ISSUE-collection-data-01.md) | collection-data | Fix ExecutePsScriptWithFailover so it actually fails over to PowerShell 5 | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:194` |
| [`collection-data-02`](collection-data/ISSUE-collection-data-02.md) | collection-data | Eliminate WaitForExit-before-ReadToEnd and sequential stream reads that can deadlock child PowerShell processes | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:160` |
| [`collection-data-04`](collection-data/ISSUE-collection-data-04.md) | collection-data | TestMfa error-parsing loop is dead code; method always returns true | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:297` |
| [`collection-data-05`](collection-data/ISSUE-collection-data-05.md) | collection-data | TestMfa passes plaintext password on the PowerShell command line | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:269` |
| [`collection-data-06`](collection-data/ISSUE-collection-data-06.md) | collection-data | TestMfaVB365 return semantics are inverted relative to ExecutePsScriptWithFailover | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:368` |
| [`collection-data-07`](collection-data/ISSUE-collection-data-07.md) | collection-data | VB365 collection ignores exit code and stderr; SCRIPTSUCCESS set true unconditionally | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:865` |
| [`collection-data-08`](collection-data/ISSUE-collection-data-08.md) | collection-data | FindExecutableInPath returns hardcoded pwsh.exe default without existence check, poisoning PowerShell selection | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:95` |
| [`collection-security-01`](collection-security/ISSUE-collection-security-01.md) | collection-security | Escape PowerShell -Server/-file args in LogCollectionInfo and ServerDumpInfo (hardening commit missed them) | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626` |
| [`csv-datatypes-01`](csv-datatypes/ISSUE-csv-datatypes-01.md) | csv-datatypes | Use InvariantCulture consistently for numeric CSV parsing (ParseToInt/ParseToDouble use machine locale) | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:1124` |
| [`csv-datatypes-03`](csv-datatypes/ISSUE-csv-datatypes-03.md) | csv-datatypes | Replace positional [Index] CSV mapping with header-name mapping — 27 classes silently corrupt on column reorder | High | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CJobCsvInfos.cs:11` |
| [`csv-datatypes-04`](csv-datatypes/ISSUE-csv-datatypes-04.md) | csv-datatypes | Move JobInfo() CSV materialization inside try and stop swallowing row errors with empty catch | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:500` |
| [`csv-datatypes-05`](csv-datatypes/ISSUE-csv-datatypes-05.md) | csv-datatypes | Isolate per-section failures in Init() — one parser error silently empties the whole report, and JobSessionInfo's null return guarantees it | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:61` |
| [`csv-datatypes-06`](csv-datatypes/ISSUE-csv-datatypes-06.md) | csv-datatypes | Add VBR v13+ branch to CProxyDataFormer.CalcProxyTasks — proxies on v13 get wrong provisioning verdicts | Medium | `vHC/HC_Reporting/Functions/Reporting/DataTypes/ProxyData/CProxyDataFormer.cs:19` |
| [`reporting-vb365-01`](reporting-vb365/ISSUE-reporting-vb365-01.md) | reporting-vb365 | HTML-encode collected VB365 data before interpolating into report HTML | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:319` |
| [`reporting-vb365-02`](reporting-vb365/ISSUE-reporting-vb365-02.md) | reporting-vb365 | Don't swallow exceptions in FormVb365Body — failure skips ExportHtml and no report is written | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:96` |
| [`reporting-vb365-03`](reporting-vb365/ISSUE-reporting-vb365-03.md) | reporting-vb365 | Guard against decimal divide-by-zero in Globals() license usage percentage | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:53` |
| [`reporting-vb365-04`](reporting-vb365/ISSUE-reporting-vb365-04.md) | reporting-vb365 | Repository free-space warning never fires — parses '1.234 TB (12.34 %)' strings as plain doubles | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:401` |
| [`reporting-vb365-06`](reporting-vb365/ISSUE-reporting-vb365-06.md) | reporting-vb365 | Jobs(): 'break' on empty Organization silently truncates all remaining job rows | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1639` |
| [`reporting-vb365-07`](reporting-vb365/ISSUE-reporting-vb365-07.md) | reporting-vb365 | Vb365Controllers(): unguarded OS-version string indexing can blank the whole Backup Server section | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:826` |
| [`reporting-vbr-core-01`](reporting-vbr-core/ISSUE-reporting-vbr-core-01.md) | reporting-vbr-core | HTML-encode all data flowing through CHtmlFormatting table/cell helpers | High | `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:200` |
| [`reporting-vbr-tables-01`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-01.md) | reporting-vbr-tables | HTML-encode all collected data before interpolating into VBR report tables | High | `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:201` |
| [`reporting-vbr-tables-02`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-02.md) | reporting-vbr-tables | Replace direct dynamic CSV member access with TryGetValue pattern in 22 table renderers | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CloudConnect/CCloudTenantsTable.cs:1` |
| [`reporting-vbr-tables-03`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-03.md) | reporting-vbr-tables | Fix License Utilization KPI: per-row TryParse overwrites totals instead of summing | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:1624` |
| [`startup-cli-01`](startup-cli/ISSUE-startup-cli-01.md) | startup-cli | EntryPoint.Main discards InitializeProgram's exit code and always returns 0 | High | `vHC/HC_Reporting/Startup/EntryPoint.cs:26` |
| [`startup-cli-02`](startup-cli/ISSUE-startup-cli-02.md) | startup-cli | /run /host=<local machine name> skips ModeCheck, collects nothing, and exits 0 | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:250` |
| [`startup-cli-03`](startup-cli/ISSUE-startup-cli-03.md) | startup-cli | Error paths call Environment.Exit(0), masking failure; SilentExit.NoProductDetected (7) is never used | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:353` |

### Medium

| ID | Domain | Title | Conf | First location |
|---|---|---|---|---|
| [`analysis-monitor-02`](analysis-monitor/ISSUE-analysis-monitor-02.md) | analysis-monitor | Fix truncated process output: WaitForExit(timeout) returns before async stdout/stderr drain | High | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:286` |
| [`analysis-monitor-03`](analysis-monitor/ISSUE-analysis-monitor-03.md) | analysis-monitor | Harden EscapeYaml against newlines/control characters and escape vbrServer in URL (YAML injection) | High | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:297` |
| [`analysis-monitor-04`](analysis-monitor/ISSUE-analysis-monitor-04.md) | analysis-monitor | Scheduled task registered without principal/logon type — monitor likely stops when user logs off | Medium | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:136` |
| [`analysis-monitor-05`](analysis-monitor/ISSUE-analysis-monitor-05.md) | analysis-monitor | Guard RunNow/TestConnection against missing exe — Process.Start exception escapes into Task.Run and strands the GUI | High | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:176` |
| [`analysis-monitor-06`](analysis-monitor/ISSUE-analysis-monitor-06.md) | analysis-monitor | CLogger.LogLine only retries IOException — other exceptions from the logging path can crash the app | High | `vHC/HC_Reporting/Common/Logging/CLogger.cs:111` |
| [`analysis-monitor-09`](analysis-monitor/ISSUE-analysis-monitor-09.md) | analysis-monitor | Localization helpers have no missing-key fallback and a single-point-of-failure static initializer | High | `vHC/HC_Reporting/Resources/Localization/VbrLocalizationHelper.cs:6` |
| [`analysis-monitor-11`](analysis-monitor/ISSUE-analysis-monitor-11.md) | analysis-monitor | IsTaskRegistered spawns PowerShell synchronously (up to 30s) and is called on the WPF UI thread | High | `vHC/HC_Reporting/Functions/Monitor/CVhcMonitorIntegration.cs:38` |
| [`collection-data-03`](collection-data/ISSUE-collection-data-03.md) | collection-data | Reorder ExecutePsScript timeout handling: stream tasks are awaited before the timeout/kill branch can run | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:417` |
| [`collection-data-09`](collection-data/ISSUE-collection-data-09.md) | collection-data | CRegReader.DefaultLogDir returns null instead of the default path when the Veeam registry key is missing | High | `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:557` |
| [`collection-data-10`](collection-data/ISSUE-collection-data-10.md) | collection-data | CLogParser silently swallows per-file errors and writes unvalidated parse results into waits.csv | High | `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:163` |
| [`collection-data-11`](collection-data/ISSUE-collection-data-11.md) | collection-data | waits.csv written with unescaped fields and culture-sensitive DateTime formatting | High | `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:90` |
| [`collection-data-12`](collection-data/ISSUE-collection-data-12.md) | collection-data | CVmcReader.GetLogDir: discarded OrderBy, NullReferenceException on missing VMC.log, and unprotected call path | High | `vHC/HC_Reporting/Functions/Collection/LogParser/CVmcReader.cs:61` |
| [`collection-data-13`](collection-data/ISSUE-collection-data-13.md) | collection-data | Unquoted -Server and path arguments in LogCollectionInfo/ServerDumpInfo break on spaces and allow argument injection | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:626` |
| [`collection-data-15`](collection-data/ISSUE-collection-data-15.md) | collection-data | CImportPathResolver.ValidateCsvFiles declares import valid with a single critical file and misvalidates unknown product type | High | `vHC/HC_Reporting/Functions/Collection/CImportPathResolver.cs:343` |
| [`collection-security-02`](collection-security/ISSUE-collection-security-02.md) | collection-security | Get-VhcSessionReport.ps1 exports session data without Protect-VhciCsvInjection | High | `vHC/HC_Reporting/Tools/Scripts/HealthCheck/VBR/vHC-VbrConfig/Public/Get-VhcSessionReport.ps1:186` |
| [`collection-security-03`](collection-security/ISSUE-collection-security-03.md) | collection-security | CImpersonation leaks the LogonUser access token and holds the password in a managed string | High | `vHC/HC_Reporting/Functions/Collection/CImpersonation.cs:50` |
| [`collection-security-05`](collection-security/ISSUE-collection-security-05.md) | collection-security | EscapeForPowerShellDoubleQuotes mixes Win32-argv and PowerShell escaping models | Low | `vHC/HC_Reporting/Functions/Collection/Security/CredentialHelper.cs:41` |
| [`csv-datatypes-02`](csv-datatypes/ISSUE-csv-datatypes-02.md) | csv-datatypes | Dispose CsvReader/StreamReader instances — every CSV parse leaks an open file handle | High | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65` |
| [`csv-datatypes-07`](csv-datatypes/ISSUE-csv-datatypes-07.md) | csv-datatypes | MissingFieldFound/HeaderValidated disabled globally — missing columns become silent nulls for typed records and runtime binder crashes for dynamic records | High | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:85` |
| [`csv-datatypes-08`](csv-datatypes/ISSUE-csv-datatypes-08.md) | csv-datatypes | FileFinder picks an arbitrary CSV via recursive FirstOrDefault — wrong dataset in multi-collection import folders | Medium | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:43` |
| [`csv-datatypes-09`](csv-datatypes/ISSUE-csv-datatypes-09.md) | csv-datatypes | MatchRepoIdToRepo re-parses SOBR and Repo CSVs from disk for every plugin job row | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:561` |
| [`csv-datatypes-10`](csv-datatypes/ISSUE-csv-datatypes-10.md) | csv-datatypes | CNetTrafficRulesCsv index map contradicts its own documented column layout — EncryptionEnabled may read the wrong column | Medium | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CNetTrafficRulesCsv.cs:11` |
| [`reporting-vb365-05`](reporting-vb365/ISSUE-reporting-vb365-05.md) | reporting-vb365 | Fix wrong-variable assignment: '<5% free' branch sets freeSpace instead of freeShade | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:410` |
| [`reporting-vb365-08`](reporting-vb365/ISSUE-reporting-vb365-08.md) | reporting-vb365 | Culture-sensitive date parsing falsely flags license/certs as expired when cultures differ | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:55` |
| [`reporting-vb365-09`](reporting-vb365/ISSUE-reporting-vb365-09.md) | reporting-vb365 | Replace empty catch blocks in every CM365Tables section with logged, row-scoped handling | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:127` |
| [`reporting-vb365-10`](reporting-vb365/ISSUE-reporting-vb365-10.md) | reporting-vb365 | GetServerName(): dynamic '.Name' never matches lowercased CSV key — Priority-2 lookup always fails | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:177` |
| [`reporting-vb365-11`](reporting-vb365/ISSUE-reporting-vb365-11.md) | reporting-vb365 | Dispose CsvReader/StreamReader — VB365 report leaks ~15 open file handles per compile | High | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:65` |
| [`reporting-vb365-12`](reporting-vb365/ISSUE-reporting-vb365-12.md) | reporting-vb365 | Index-based CSV mapping in VB365 POCOs silently misaligns if collector columns change | Medium | `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/VB365/CSecurityCsv.cs:10` |
| [`reporting-vb365-13`](reporting-vb365/ISSUE-reporting-vb365-13.md) | reporting-vb365 | Vb365ProtStat(): inconsistent denominators between 'Total Users' and unprotected-percentage shading | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:1498` |
| [`reporting-vbr-core-02`](reporting-vbr-core/ISSUE-reporting-vbr-core-02.md) | reporting-vbr-core | Fix duplicated document header in security report assembly | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:343` |
| [`reporting-vbr-core-03`](reporting-vbr-core/ISSUE-reporting-vbr-core-03.md) | reporting-vbr-core | ExecBrowser crashes with NullReferenceException in CLI mode (/show:report) | High | `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:318` |
| [`reporting-vbr-core-04`](reporting-vbr-core/ISSUE-reporting-vbr-core-04.md) | reporting-vbr-core | PDF export failure marks the whole HTML export as failed; converter never disposed on throw | High | `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:104` |
| [`reporting-vbr-core-05`](reporting-vbr-core/ISSUE-reporting-vbr-core-05.md) | reporting-vbr-core | HtmlToPdfConverter: timed-out conversion leaves a foreground thread that blocks process exit; Dispose is a no-op | High | `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs:57` |
| [`reporting-vbr-core-06`](reporting-vbr-core/ISSUE-reporting-vbr-core-06.md) | reporting-vbr-core | SetReportNameAndPath swallows exceptions and returns null, causing opaque downstream failures | High | `vHC/HC_Reporting/Functions/Reporting/Html/CHtmlExporter.cs:251` |
| [`reporting-vbr-core-07`](reporting-vbr-core/ISSUE-reporting-vbr-core-07.md) | reporting-vbr-core | ProtectedWorkloadsToXml swallows all exceptions without logging; ineffective null guard uses || before dereferencing both collections | High | `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:393` |
| [`reporting-vbr-core-08`](reporting-vbr-core/ISSUE-reporting-vbr-core-08.md) | reporting-vbr-core | Null CSV fields used as Dictionary keys crash report generation (PreCalculations, JobSummaryInfoToXml) | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:1187` |
| [`reporting-vbr-core-09`](reporting-vbr-core/ISSUE-reporting-vbr-core-09.md) | reporting-vbr-core | VM-to-server matching by StartsWith/Contains miscounts protected workloads | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:841` |
| [`reporting-vbr-core-10`](reporting-vbr-core/ISSUE-reporting-vbr-core-10.md) | reporting-vbr-core | Section summaries are built then silently discarded (LicSum returns null; SectionEnd ignores its parameter) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CVbrSummaries.cs:41` |
| [`reporting-vbr-core-11`](reporting-vbr-core/ISSUE-reporting-vbr-core-11.md) | reporting-vbr-core | PPTX export: fragile signature-based dedup can drop distinct tables; failure leaves a corrupt partial .pptx | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs:1490` |
| [`reporting-vbr-tables-04`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-04.md) | reporting-vbr-tables | Use InvariantCulture consistently when parsing numbers/dates from collected CSVs | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/ProtectedWorkloads/NasSourceInfo.cs:90` |
| [`reporting-vbr-tables-05`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-05.md) | reporting-vbr-tables | Fix inconsistent unit ladder in NasSourceInfo.CalculateStorageString (off-by-1024 fallback) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/ProtectedWorkloads/NasSourceInfo.cs:86` |
| [`reporting-vbr-tables-06`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-06.md) | reporting-vbr-tables | Fix concurrency heatmap window, phantom default rows, and midnight overflow in CConcurrencyHelper | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Concurrency Tables/CConcurrencyHelper.cs:292` |
| [`reporting-vbr-tables-07`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-07.md) | reporting-vbr-tables | Eliminate double data-load: every legacy renderer loads its dataset twice (HTML pass + JSON pass) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobSessionSummaryTable.cs:88` |
| [`reporting-vbr-tables-08`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-08.md) | reporting-vbr-tables | Remove O(jobs x sessions) re-parsing inside aggregation loops (Waits CSV, session list, NAS CSV) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummaryHelper.cs:57` |
| [`reporting-vbr-tables-09`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-09.md) | reporting-vbr-tables | Harden CMalwareTable: unguarded DateTime.Parse plus rethrow drops all malware tables on one bad row | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Security/CMalwareTable.cs:203` |
| [`reporting-vbr-tables-10`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-10.md) | reporting-vbr-tables | Fix On-Disk totals cell in CJobInfoTable: GB value rendered with TB/MB tooltip, conversions dead | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Jobs Info/CJobInfoTable.cs:327` |
| [`startup-cli-04`](startup-cli/ISSUE-startup-cli-04.md) | startup-cli | Hotfix detector runs twice in remote mode (missing else), and the remoteServer parameter is ignored anyway | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:334` |
| [`startup-cli-05`](startup-cli/ISSUE-startup-cli-05.md) | startup-cli | VhcGui.Run swallows background-task exceptions (silent failure) and force-exits with code 0 | High | `vHC/HC_Reporting/VhcGui.xaml.cs:248` |
| [`startup-cli-06`](startup-cli/ISSUE-startup-cli-06.md) | startup-cli | Circular static initialization makes CGlobals.desiredPath default to C:\temp\vHC\Original (not C:\temp\vHC) | High | `vHC/HC_Reporting/Common/CGlobals.cs:73` |
| [`startup-cli-07`](startup-cli/ISSUE-startup-cli-07.md) | startup-cli | CHotfixDetector.ClearTargetPath deletes the parent directory instead of each subdirectory; ExtractLogs deletes zips even when extraction fails | High | `vHC/HC_Reporting/Startup/CHotfixDetector.cs:229` |
| [`startup-cli-08`](startup-cli/ISSUE-startup-cli-08.md) | startup-cli | /path= argument matching is case-fragile, unanchored, and logs the wrong variable | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:229` |
| [`startup-cli-09`](startup-cli/ISSUE-startup-cli-09.md) | startup-cli | Unknown CLI arguments are silently ignored (no default case); /days only accepts 4 hardcoded literals | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:108` |
| [`startup-cli-10`](startup-cli/ISSUE-startup-cli-10.md) | startup-cli | ApplyOutDir swaps CGlobals.mainlog mid-run, but components hold the old CLogger in readonly fields | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:643` |
| [`startup-cli-11`](startup-cli/ISSUE-startup-cli-11.md) | startup-cli | CHotfixDetector constructor silently ignores an invalid path, then Run() proceeds with null path | High | `vHC/HC_Reporting/Startup/CHotfixDetector.cs:24` |
| [`startup-cli-14`](startup-cli/ISSUE-startup-cli-14.md) | startup-cli | /import:<bad-path> logs an error but continues the run against the default path | High | `vHC/HC_Reporting/Startup/CArgsParser.cs:158` |

### Low

| ID | Domain | Title | Conf | First location |
|---|---|---|---|---|
| [`analysis-monitor-07`](analysis-monitor/ISSUE-analysis-monitor-07.md) | analysis-monitor | CLogger.Debug ignores its silent parameter | High | `vHC/HC_Reporting/Common/Logging/CLogger.cs:70` |
| [`analysis-monitor-08`](analysis-monitor/ISSUE-analysis-monitor-08.md) | analysis-monitor | Unbounded log accumulation and per-line file open/close in CLogger | High | `vHC/HC_Reporting/Common/Logging/CLogger.cs:17` |
| [`analysis-monitor-10`](analysis-monitor/ISSUE-analysis-monitor-10.md) | analysis-monitor | Remove dead Analysis data models: empty SOBR class and unused Repository class | High | `vHC/HC_Reporting/Functions/Analysis/DataModels/SOBR.cs:9` |
| [`collection-data-14`](collection-data/ISSUE-collection-data-14.md) | collection-data | Ps7Executor.IsModuleInstalled always returns true; RunScript invokes script path via -Command | High | `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:39` |
| [`collection-data-16`](collection-data/ISSUE-collection-data-16.md) | collection-data | CCsvValidator wildcard substring matching can validate the wrong file as present | Medium | `vHC/HC_Reporting/Functions/Collection/CCsvValidator.cs:138` |
| [`collection-data-17`](collection-data/ISSUE-collection-data-17.md) | collection-data | CRegReader stores per-instance registry results in static fields | High | `vHC/HC_Reporting/Functions/Collection/DB/CRegReader.cs:17` |
| [`collection-data-18`](collection-data/ISSUE-collection-data-18.md) | collection-data | CCsvsInMemory reports load errors to Console instead of the logger and silently drops bad CSV data | High | `vHC/HC_Reporting/Common/CCsvsInMemory.cs:31` |
| [`collection-data-19`](collection-data/ISSUE-collection-data-19.md) | collection-data | TryModuleLoad redirects streams it never reads, risking false dynamic-fallback timeouts | Medium | `vHC/HC_Reporting/Functions/Collection/CCollections.cs:637` |
| [`collection-data-20`](collection-data/ISSUE-collection-data-20.md) | collection-data | CQueries parses @@version with fragile fixed-index token slicing | High | `vHC/HC_Reporting/Functions/Collection/DB/CQueries.cs:75` |
| [`collection-security-04`](collection-security/ISSUE-collection-security-04.md) | collection-security | REMOTEHOST is used unsanitized as a filesystem path component (output-dir traversal) | Medium | `vHC/HC_Reporting/Startup/CVariables.cs:84` |
| [`csv-datatypes-11`](csv-datatypes/ISSUE-csv-datatypes-11.md) | csv-datatypes | MemoryTasksCount truncates via integer division before rounding; availableMem computed but unused | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:738` |
| [`csv-datatypes-12`](csv-datatypes/ISSUE-csv-datatypes-12.md) | csv-datatypes | Clean up CCsvParser/CDataTypesParser API smells: null ConfigBackup, '' from ParseBool, unused vboReader, duplicate typo methods | High | `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:493` |
| [`reporting-vb365-14`](reporting-vb365/ISSUE-reporting-vb365-14.md) | reporting-vb365 | Vb365Security(): malformed/duplicated HTML structure (headers rebuilt per row, stray tags) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CM365Tables.cs:660` |
| [`reporting-vb365-15`](reporting-vb365/ISSUE-reporting-vb365-15.md) | reporting-vb365 | CVb365HtmlCompiler emits duplicate <body> tag and never closes body/html | High | `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:47` |
| [`reporting-vbr-core-12`](reporting-vbr-core/ISSUE-reporting-vbr-core-12.md) | reporting-vbr-core | SecSummary: bare empty catch silently defaults MFA/Four-Eyes security flags | High | `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:134` |
| [`reporting-vbr-core-13`](reporting-vbr-core/ISSUE-reporting-vbr-core-13.md) | reporting-vbr-core | CSectionTable swallows extractor exceptions per cell, rendering silent blanks | High | `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CSectionTable.cs:178` |
| [`reporting-vbr-core-14`](reporting-vbr-core/ISSUE-reporting-vbr-core-14.md) | reporting-vbr-core | CHtmlFormatting.cs bloated to 3 MB by two 1.5 MB commented-out base64 lines | High | `vHC/HC_Reporting/Functions/Reporting/Html/Shared/CHtmlFormatting.cs:433` |
| [`reporting-vbr-core-15`](reporting-vbr-core/ISSUE-reporting-vbr-core-15.md) | reporting-vbr-core | Dead code cluster in VBR report core: broken DivIdClass format string, no-op AddToHtml overload, unused proxy CSV loads, duplicated LoadCsvToMemory, empty classes | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:612` |
| [`reporting-vbr-core-16`](reporting-vbr-core/ISSUE-reporting-vbr-core-16.md) | reporting-vbr-core | Culture-sensitive decimal ToString leaks locale decimal separators into report and JSON export | Medium | `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:659` |
| [`reporting-vbr-core-17`](reporting-vbr-core/ISSUE-reporting-vbr-core-17.md) | reporting-vbr-core | GetEmbeddedCssContent throws ArgumentNullException (not a clear error) when an embedded resource is missing; helper duplicated | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:190` |
| [`reporting-vbr-core-18`](reporting-vbr-core/ISSUE-reporting-vbr-core-18.md) | reporting-vbr-core | SetConfigBackupSettings relies on NullReferenceException for empty config-backup CSV | High | `vHC/HC_Reporting/Functions/Reporting/Html/CBackupServerTableHelper.cs:81` |
| [`reporting-vbr-tables-11`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-11.md) | reporting-vbr-tables | Remove dead/no-op immutability rendering and empty 'Immutability' subsection in security report | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTablesHelper.cs:43` |
| [`reporting-vbr-tables-12`](reporting-vbr-tables/ISSUE-reporting-vbr-tables-12.md) | reporting-vbr-tables | Consolidate copy-pasted helpers drifting across table classes (SetSection, progress bar, BoolCell, totals block) | High | `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:1600` |
| [`startup-cli-12`](startup-cli/ISSUE-startup-cli-12.md) | startup-cli | Dead/unreachable code cluster in startup path (ModeCheck, VbrVersionSupportCheck, duplicate dispatch branches, redundant null check) | High | `vHC/HC_Reporting/Startup/CClientFunctions.cs:174` |
| [`startup-cli-13`](startup-cli/ISSUE-startup-cli-13.md) | startup-cli | CMessages.PsVbrFunctionDone is built from the wrong constant (copy-paste error) | High | `vHC/HC_Reporting/Common/CMessages.cs:125` |

---
*Each row links to a standalone issue file with Summary / Impact / Evidence (file:line) / Suggested fix. Ready to paste into GitHub issues — none were filed automatically.*
