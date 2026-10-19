# Veeam Health Check — Merged & Deduplicated Code Review Index

> Merge of two independent full reviews of the `dev` branch (260 `.cs` files):
> - **Fable** = `code-review-findings/` (Fable 5, 2026-06-11, 107 findings)
> - **Opus** = `code-review-findings-opus/` (Claude Opus 4.8, 2026-06-12, 102 findings)
>
> Matched by **file:line + symptom** (the per-review IDs do **not** align). Each row is one distinct work item.
> `Source` column: **both** = independently found by both reviews · **fable** / **opus** = found by only that review.
> Where both flagged the same method but caught *different* sub-bugs, the row is tagged **both** and the title notes both defects — treat as one location, read both reviews.

## Summary

| | Count |
|---|---|
| **Distinct issues (merged)** | **~150** |
| Found by both | 55 |
| Unique to Fable | 51 |
| Unique to Opus | 44 |
| 🔴 Critical (Opus-only) | 1 |

### Severity is the higher of the two reviews where they overlap.

### Top of the triage list (cross-validated or critical)
1. 🔴 **`opus startup-cli-01`** — VB365 HTML report **never generated** (compiler disposed right after construction). Only Opus caught it; it's a live functional regression. Confirm against a real VB365 run first.
2. **Credential exposure cluster** — cleartext YAML (both `am-01`), `verify_ssl:false` (opus `am-02`), `-Password` on cmdline (both), Base64≠encryption (opus `cd-07`/`startup-12`), Scrub=false tri-state bypass (opus `startup-21`).
3. **PowerShell injection** on log-collection paths (both `cd` + `cs-01`).
4. **The one HTML-encoding fix** at `CHtmlFormatting.TableData/TableHeader` (both, every subsystem) — fix once, fixes the bulk of the XSS findings.

---

## Collection — data sources & security (`collection-data` + `collection-security`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | cd-01 | cd-05 | `ExecutePsScriptWithFailover` never fails over to PS5 (return/exit inside version loop) | `PSInvoker.cs:194` |
| High | both | cd-13 / cs-01 | cd-01 / cs-01 | PS arg injection: unescaped `-Server`/`-file` in LogCollection/ServerDump (`UseShellExecute=true`) | `PSInvoker.cs:626` |
| High | both | cd-04 | cd-06 | `TestMfa` dead parse loop → always returns true (StdErr read twice) | `PSInvoker.cs:297` |
| High | both | cd-05 | cs-10 | Cleartext password passed as `-Password` on powershell.exe cmdline | `PSInvoker.cs:269` |
| High | both | cd-12 | cd-08 | `CVmcReader.GetLogDir`: discarded `OrderBy`, NRE on missing VMC.log, null `LOGLOCATION` | `CVmcReader.cs:61` |
| High | both | cd-11 | cd-03 | `waits.csv` written with unescaped/malformed fields (+ culture DateTime, Fable) | `CLogParser.cs:90` |
| High | both | cd-10 | cd-12 | `CLogParser` swallows per-file errors, writes unvalidated rows | `CLogParser.cs:163` |
| High | both | cd-17 | cd-10 | `CRegReader` stores per-instance DB state in static fields (cross-thread bleed) | `CRegReader.cs:17` |
| Med | both | cs-03 | cd-13 / cs-07 | `CImpersonation` holds password in managed String (+ leaks LogonUser token, Fable) | `CImpersonation.cs:50` |
| Med | both | cd-14¹ | cd-02 | `RunScript` builds `-Command` from unescaped double-quoted script path | `PowerShell7Executor.cs:68` |
| Med | both | cd-16 | cd-16 | `CCsvValidator` wildcard substring over-matches → wrong file validated present | `CCsvValidator.cs:138` |
| Low | both | cs-05 | cs-05 | `EscapeForPowerShellDoubleQuotes` incomplete (newlines / mixed argv+PS models) | `CredentialHelper.cs:41` |
| High | fable | cd-02 | — | `WaitForExit`-before-`ReadToEnd` + sequential stream reads can **deadlock** child PS | `PSInvoker.cs:160` |
| High | fable | cd-03 | — | `ExecutePsScript` timeout/kill branch unreachable (stream tasks awaited first) | `PSInvoker.cs:417` |
| High | fable | cd-06 | — | `TestMfaVB365` return semantics inverted vs failover convention | `PSInvoker.cs:368` |
| High | fable | cd-07 | — | VB365 collection ignores exit code/stderr; `SCRIPTSUCCESS` set true unconditionally | `PSInvoker.cs:865` |
| High | fable | cd-08 | — | `FindExecutableInPath` returns hardcoded `pwsh.exe` w/o existence check | `PSInvoker.cs:95` |
| High | fable | cd-09 | — | `CRegReader.DefaultLogDir` returns null instead of default path when key missing | `CRegReader.cs:557` |
| High | fable | cd-15 | — | `ValidateCsvFiles` declares import valid w/ one critical file; misvalidates unknown product | `CImportPathResolver.cs:343` |
| High | fable | cs-02 | — | **`Get-VhcSessionReport.ps1` exports without `Protect-VhciCsvInjection`** (only Fable reviewed PS1) | `Get-VhcSessionReport.ps1:186` |
| Med | fable | cd-19 | — | `TryModuleLoad` redirects streams it never reads → false dynamic-fallback timeouts | `CCollections.cs:637` |
| Low | fable | cd-14 | — | `Ps7Executor.IsModuleInstalled` always returns true | `PowerShell7Executor.cs:39` |
| Low | fable | cd-18 | — | `CCsvsInMemory` logs to Console not logger; silently drops bad CSV data | `CCsvsInMemory.cs:31` |
| Low | fable | cd-20 | — | `CQueries` parses `@@version` with fragile fixed-index token slicing | `CQueries.cs:75` |
| Low | fable | cs-04 | — | `REMOTEHOST` used unsanitized as filesystem path component (traversal) | `CVariables.cs:84` |
| High | opus | — | cd-07 | **Password Base64 ≠ encryption** — plaintext recoverable from process cmdline | `PSInvoker.cs:546` |
| Med | opus | — | cd-04 | `waits.csv` opened per-row under lock during parallel parse (perf) | `CLogParser.cs:86` |
| Med | opus | — | cd-11 | Remote registry reads lack null guards / admin-denied handling | `CRegReader.cs:305` |
| Med | opus | — | cs-02 | NRE dereferencing `creds.Value` in `TestMfa` when credentials absent | `PSInvoker.cs:256` |
| Med | opus | — | cs-03 | DPAPI credential store written with default ACLs, no permission hardening | `CredentialStore.cs:20` |
| Med | opus | — | cs-04 | Plaintext password round-trips to managed String, never zeroed | `CredentialStore.cs:140` |
| Low | opus | — | cd-09 | Undisposed `Process` objects in ExecutePsScript/RunVbrLogCollect | `PSInvoker.cs:404` |
| Low | opus | — | cd-14 | `CImportPathResolver.FindCsvDirectory` mutates global product flags as side effect | `CImportPathResolver.cs:88` |
| Low | opus | — | cd-15 | `CLogParser` ctor does registry+FS I/O → can throw from field initializer | `CLogParser.cs:22` |
| Low | opus | — | cs-06 | Username escaping/validation inconsistent; fragile denylist | `CArgsParser.cs:531` |
| Low | opus | — | cs-07 | Dead/insecure `RunImpersonated`/`CSecurityInit` accumulate password in string | `CSecurityInit.cs:45` |
| Low | opus | — | cs-08 | Remote registry reads deref null `RegistryKey` → crash security collection | `CSecurityInit.cs:145` |
| Low | opus | — | cs-09 | Credential store TOCTOU / lost-update; non-atomic write | `CredentialStore.cs:147` |

¹ Fable `cd-14` combines the `-Command` quoting issue with the `IsModuleInstalled` always-true bug.

---

## CSV parsing & data types (`csv-datatypes`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | csv-01 | csv-02 | Numeric CSV parsing uses machine locale, not `InvariantCulture` → wrong sizes/counts | `CDataTypesParser.cs:1124` |
| High | both | csv-02 | csv-01 | `CsvReader`/`StreamReader` never disposed → file-handle leak per read | `CCsvReader.cs:65` |
| High | both | csv-03 | csv-09 | Positional `[Index]` CSV mapping silently corrupts on column reorder (27 classes) | `CCsvReader.cs:85` |
| High | both | csv-04 | csv-04 | Broad/empty catch swallows CSV parse errors → malformed data shown as empty | `CDataTypesParser.cs:500` |
| High | both | csv-09 | csv-03 | `MatchRepoIdToRepo` re-parses Repo+SOBR CSVs from disk per job row (O(n·m)) | `CDataTypesParser.cs:561` |
| High | both | csv-07 | csv-09 | `MissingFieldFound`/`HeaderValidated` disabled globally → silent nulls / binder crashes | `CCsvReader.cs:85` |
| High | both | csv-11 | csv-06 | Integer division in RAM/size conversions truncates / divide-by-zero risk | `CDataTypesParser.cs:738` |
| High | fable | csv-05 | — | `Init()` one parser error empties whole report; `JobSessionInfo` null return guarantees it | `CDataTypesParser.cs:61` |
| Med | fable | csv-06 | — | `CProxyDataFormer.CalcProxyTasks` missing VBR v13+ branch → wrong proxy verdicts | `CProxyDataFormer.cs:19` |
| Med | fable | csv-08 | — | `FileFinder` picks arbitrary CSV via recursive `FirstOrDefault` (multi-collection import) | `CCsvReader.cs:43` |
| Med | fable | csv-10 | — | `CNetTrafficRulesCsv` index map contradicts documented layout → wrong column | `CNetTrafficRulesCsv.cs:11` |
| Low | fable | csv-12 | — | API smells: null ConfigBackup, `''` from ParseBool, unused vboReader, dup typo methods | `CDataTypesParser.cs:493` |
| Med | opus | — | csv-05 | `JobSessionInfo` uses `SingleOrDefault` on duplicate job names → throws | `CDataTypesParser.cs:647` |
| Med | opus | — | csv-07 | Static proxy CSV parsers ignore custom import dir — read from default `vbrDir` | `CCsvParser.cs:549` |
| Med | opus | — | csv-08 | `StreamReader` opened without explicit UTF-8/BOM handling | `CCsvReader.cs:67` |
| Low | opus | — | csv-10 | `TryParseDateTime` fallback uses `CurrentCulture` → DD/MM vs MM/DD flips | `CDataTypesParser.cs:706` |

---

## VBR report core (`reporting-vbr-core`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | vbr-core-01 | vbr-core-01/02/03 | Collected data interpolated into HTML w/o encoding (XSS). Opus adds license-holder header + section title/icon/id | `CHtmlFormatting.cs:200` |
| High | both | vbr-core-06 | vbr-core-06 | `SetReportNameAndPath` swallows exceptions, returns null → opaque downstream crash | `CHtmlExporter.cs:251` |
| High | both | vbr-core-04/05 | vbr-core-05 | DinkToPdf converter/native handles never disposed; timed-out thread blocks exit | `HtmlToPdfConverter.cs:57` |
| Med | both | vbr-core-11 | vbr-core-08 | PPTX export: fragile signature dedup drops tables / regex mishandles nested tables → corrupt partial | `HtmlToPptxConverter.cs:1490` |
| Med | both² | vbr-core-13 | vbr-core-04 | `CSectionTable`: Fable=per-cell swallow→blanks; Opus=`&#` heuristic passes raw HTML | `CSectionTable.cs:178` |
| Med | both² | vbr-core-03 | vbr-core-10 | `ExecBrowser`: Fable=NRE in CLI mode; Opus=orphaned `WebBrowser` + undisposed `Process` | `CHtmlExporter.cs:318` |
| High | both | vbr-core-18 | vbr-core-12 | `SetConfigBackupSettings` relies on NRE / derefs `FirstOrDefault` for empty config-backup CSV | `CBackupServerTableHelper.cs:81` |
| High | fable | vbr-core-02 | — | Duplicated document header in security report assembly | `CHtmlCompiler.cs:343` |
| High | fable | vbr-core-07 | — | `ProtectedWorkloadsToXml` swallows all exceptions; broken `||` null guard | `CDataFormer.cs:393` |
| High | fable | vbr-core-10 | — | Section summaries built then silently discarded (`LicSum` null; `SectionEnd` ignores param) | `CVbrSummaries.cs:41` |
| High | fable | vbr-core-12 | — | `SecSummary` bare empty catch silently defaults MFA/Four-Eyes security flags | `CDataFormer.cs:134` |
| High | fable | vbr-core-14 | — | `CHtmlFormatting.cs` bloated to 3 MB by two commented-out base64 lines | `CHtmlFormatting.cs:433` |
| High | fable | vbr-core-15 | — | Dead-code cluster (broken DivIdClass fmt, no-op AddToHtml, unused proxy loads, dup LoadCsv) | `CHtmlCompiler.cs:612` |
| High | fable | vbr-core-17 | — | `GetEmbeddedCssContent` throws `ArgumentNullException` on missing resource; helper duplicated | `CHtmlCompiler.cs:190` |
| Med | fable | vbr-core-08 | — | Null CSV fields used as Dictionary keys crash report (PreCalculations, JobSummaryInfoToXml) | `CDataFormer.cs:1187` |
| Med | fable | vbr-core-09 | — | VM-to-server matching by `StartsWith`/`Contains` miscounts protected workloads | `CDataFormer.cs:841` |
| Med | fable | vbr-core-16 | — | Culture-sensitive decimal `ToString` leaks locale separators into report + JSON export | `CDataFormer.cs:659` |
| Med | opus | — | vbr-core-07 | O(n²) string concatenation building full VBR report in memory | `CHtmlBodyHelper.cs:34` |
| Low | opus | — | vbr-core-09 | PPTX section-title formatting uses `CurrentCulture` (Turkish-I) | `HtmlToPptxConverter.cs:1588` |
| Low | opus | — | vbr-core-11 | `CObjectHelpers.ParseBool` silently returns false for valid truthy values | `CObjectHelpers.cs:7` |

² Same class/method flagged by both, but each caught a distinct defect — read both reviews for the full set.

---

## VBR table renderers (`reporting-vbr-tables`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | vbr-tables-01 | vbr-tables-01 | Systemic stored XSS: cell values written without HTML-encoding (35+ renderers) | `CHtmlFormatting.cs:201` |
| High | both | vbr-tables-09 | vbr-tables-03 | `CMalwareTable`: unguarded `DateTime.Parse` + rethrow drops all malware tables on one bad row | `CMalwareTable.cs:203` |
| High | both | vbr-tables-07 | vbr-tables-04 | Every legacy renderer loads its dataset twice (HTML pass + JSON pass) | `CJobSessionSummaryTable.cs:88` |
| High | both | vbr-tables-04 | vbr-tables-07 | Culture-sensitive numeric parse/`ToString` in NasSourceInfo → wrong in non-US locales | `NasSourceInfo.cs:90` |
| High | both | vbr-tables-10 | vbr-tables-06 | On-Disk totals cell: GB value rendered with TB/MB tooltip; conversions dead | `CJobInfoTable.cs:327` |
| High | both | vbr-tables-08 | vbr-tables-08 | O(jobs×sessions) re-parsing / quadratic `+=` in aggregation loops | `CJobSessSummaryHelper.cs:57` |
| High | fable | vbr-tables-02 | — | Replace direct dynamic CSV member access with `TryGetValue` in 22 renderers | `CCloudTenantsTable.cs:1` |
| High | fable | vbr-tables-03 | — | License Utilization KPI: per-row `TryParse` overwrites totals instead of summing | `CHtmlTables.cs:1624` |
| High | fable | vbr-tables-05 | — | Inconsistent unit ladder in `NasSourceInfo.CalculateStorageString` (off-by-1024) | `NasSourceInfo.cs:86` |
| High | fable | vbr-tables-06 | — | Concurrency heatmap window, phantom default rows, midnight overflow in `CConcurrencyHelper` | `CConcurrencyHelper.cs:292` |
| Low | fable | vbr-tables-11 | — | Dead/no-op immutability rendering + empty 'Immutability' subsection | `CHtmlTablesHelper.cs:43` |
| Low | fable | vbr-tables-12 | — | Consolidate copy-pasted helpers drifting across table classes | `CHtmlTables.cs:1600` |
| Med | opus | — | vbr-tables-02 | `DomainStatus` computes danger shade but never applies it | `CVbrServerTableHelper.cs:135` |
| Med | opus | — | vbr-tables-05 | NRE: `foreach` over unchecked-null list drops table | `CSobrExtentTable.cs:57` |
| Med | opus | — | vbr-tables-09 | Proxy JSON export drops a column → header/value misalignment vs HTML | `CProxyTable.cs:138` |
| Low | opus | — | vbr-tables-10 | `FormatDuration` drops hours for compliance scans over 59 minutes | `CComplianceTable.cs:188` |

---

## VB365 reporting (`reporting-vb365`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | vb365-01 | vb365-01 | Unescaped collected values into HTML across all VB365 tables (stored XSS) | `CM365Tables.cs:319` |
| High | both | vb365-05 | vb365-02 | Repo free-space threshold sets value instead of shade → low-space never flags | `CM365Tables.cs:410` |
| High | both | vb365-06 | vb365-03 | `Jobs()` uses `break` on empty Organization → silently drops all remaining jobs | `CM365Tables.cs:1639` |
| High | both | vb365-07 | vb365-04 | Unguarded OS/RAM split-string indexing can blank the Backup Server section | `CM365Tables.cs:826` |
| High | both | vb365-09 | vb365-05 | Empty catch blocks silently drop entire VB365 report sections | `CM365Tables.cs:127` |
| High | both | vb365-03 | vb365-06 | Divide-by-zero on license/free-space/size-limit percentages drops sections | `CM365Tables.cs:53` |
| High | both | vb365-08 | vb365-07 | Culture-sensitive number/date parsing across all VB365 tables (false expiry flags) | `CM365Tables.cs:55` |
| Med | both | vb365-13 | vb365-11 | `ProtStat`: inconsistent denominators / double-counts users; stale-user percentage | `CM365Tables.cs:1498` |
| High | fable | vb365-02 | — | `FormVb365Body` swallows exceptions → `ExportHtml` skipped, **no report written** | `CVb365HtmlCompiler.cs:96` |
| High | fable | vb365-04 | — | Repo free-space warning never fires — parses `'1.234 TB (12.34 %)'` as plain double | `CM365Tables.cs:401` |
| High | fable | vb365-11 | — | Dispose CsvReader/StreamReader — VB365 report leaks ~15 handles/compile³ | `CCsvReader.cs:65` |
| Med | fable | vb365-10 | — | `GetServerName`: dynamic `.Name` never matches lowercased CSV key → Priority-2 lookup fails | `CVb365HtmlCompiler.cs:177` |
| Med | fable | vb365-12 | — | Index-based CSV mapping in VB365 POCOs misaligns if collector columns change | `CSecurityCsv.cs:10` |
| Low | fable | vb365-14 | — | `Vb365Security()`: malformed/duplicated HTML (headers rebuilt per row, stray tags) | `CM365Tables.cs:660` |
| Low | fable | vb365-15 | — | `CVb365HtmlCompiler` emits duplicate `<body>` and never closes body/html | `CVb365HtmlCompiler.cs:47` |
| Med | opus | — | vb365-12 | Positional-index scrubbing brittle → risks emitting identifiers **unscrubbed** | `CM365Tables.cs:1360` |
| Low | opus | — | vb365-08 | Proxies scrubbing runs inside column loop → partially scrubs other fields | `CM365Tables.cs:224` |
| Low | opus | — | vb365-09 | Security cert date shades use bitwise-or, never rendered (dead coloring) | `CM365Tables.cs:599` |
| Low | opus | — | vb365-10 | Quadratic string concatenation building VB365 tables and document | `CM365Tables.cs:1046` |

³ Opus covers VB365 dispose under the global `csv-datatypes-01`; Fable filed it separately for VB365.

---

## Analysis & Monitor (`analysis-monitor`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| High | both | am-01 | am-01 | VBR credential password written to disk as cleartext YAML | `CVhcMonitorIntegration.cs:84` |
| High | both | am-03 | am-03 | `EscapeYaml` insufficient (newlines/control chars, double-quoted scalars) → injection/corruption | `CVhcMonitorIntegration.cs:297` |
| Med | both² | am-04 | am-06 | Scheduled task: Fable=no principal/logon (stops on logoff); Opus=fragile PS quote escaping | `CVhcMonitorIntegration.cs:136` |
| Med | both² | am-11 | am-09 | `IsTaskRegistered`: Fable=sync PS ≤30s on WPF UI thread; Opus=substring false positive | `CVhcMonitorIntegration.cs:38` |
| Low | both | am-10 | am-10 | Dead/stub Analysis data models (empty SOBR, unused Repository) | `Analysis/DataModels/SOBR.cs:9` |
| High | fable | am-02 | — | Truncated process output: `WaitForExit(timeout)` returns before async stdout/stderr drain | `CVhcMonitorIntegration.cs:286` |
| High | fable | am-05 | — | `RunNow`/`TestConnection` `Process.Start` exception escapes into `Task.Run`, strands GUI | `CVhcMonitorIntegration.cs:176` |
| High | fable | am-06 | — | `CLogger.LogLine` only retries `IOException` — other exceptions can crash the app | `CLogger.cs:111` |
| High | fable | am-09 | — | Localization helpers: no missing-key fallback + single-point-of-failure static init | `VbrLocalizationHelper.cs:6` |
| Low | fable | am-07 | — | `CLogger.Debug` ignores its `silent` parameter | `CLogger.cs:70` |
| Low | fable | am-08 | — | Unbounded log accumulation + per-line file open/close in `CLogger` | `CLogger.cs:17` |
| High | opus | — | am-02 | Monitor config hardcodes `verify_ssl: false` → creds over unauthenticated TLS | `CVhcMonitorIntegration.cs:86` |
| Med | opus | — | am-07 | Uninstall leaves plaintext-credential config + copied exe on disk | `CVhcMonitorIntegration.cs:202` |
| Low | opus | — | am-04 | `min_severity` written to YAML without escaping | `CVhcMonitorIntegration.cs:104` |
| Low | opus | — | am-05 | `notifType` lowercased with culture-sensitive `ToLower()` for control flow | `CVhcMonitorIntegration.cs:93` |
| Low | opus | — | am-08 | Monitor state timestamp parsed with culture-sensitive `DateTime.TryParse` | `CVhcMonitorIntegration.cs:235` |

---

## Startup / CLI / global state (`startup-cli`)

| Sev | Source | Fable | Opus | Issue | Location |
|---|---|---|---|---|---|
| 🔴 Critical | opus | — | startup-cli-01 | **VB365 HTML report never generated — compiler disposed immediately after construction** | `CReportModeSelector.cs:65` |
| High | both | startup-cli-01 | startup-cli-02 | `EntryPoint.Main` discards `InitializeProgram`'s exit code, always returns 0 | `EntryPoint.cs:26` |
| High | both | startup-cli-05 | startup-cli-05 | GUI `Run()` swallows background-task exceptions and force-exits with code 0 | `VhcGui.xaml.cs:248` |
| High | both | startup-cli-04 | startup-cli-03 | Hotfix detector runs twice (local + remote) — missing `else` | `CArgsParser.cs:334` |
| High | both | startup-cli-07 | startup-cli-17/19 | `ClearTargetPath` deletes parent dir; `ExtractLogs` deletes zips even when extraction fails | `CHotfixDetector.cs:229` |
| High | both | startup-cli-11 | startup-cli-18 | `CHotfixDetector` ctor ignores invalid path → null flows into `Run()` | `CHotfixDetector.cs:24` |
| High | both | startup-cli-12 | startup-cli-13 | `ModeCheck` unreachable/dead branch + repeated full process scan | `CClientFunctions.cs:174` |
| High | both | startup-cli-09 | startup-cli-15 | Unknown/malformed CLI args silently ignored (no default case); `/days` 4 literals only | `CArgsParser.cs:108` |
| High | both² | startup-cli-06 | startup-cli-14 | `CGlobals` static state: Fable=circular init → wrong `desiredPath` default; Opus=no reset across runs/tests | `CGlobals.cs:47` |
| High | both² | startup-cli-08 / cs-04 | startup-cli-07 | Path args (`/path`,`/outdir`,`/host`): case-fragile matching + no traversal/quoting validation | `CArgsParser.cs:229` |
| High | both² | startup-cli-10 | startup-cli-20 | Output-dir/logger: Fable=`ApplyOutDir` swaps mainlog under readonly holders; Opus=`CLogger` pins dir at static init | `CArgsParser.cs:643` |
| High | fable | startup-cli-02 | — | `/run /host=<local machine name>` skips `ModeCheck`, collects nothing, exits 0 | `CArgsParser.cs:250` |
| High | fable | startup-cli-03 | — | Error paths call `Environment.Exit(0)`, masking failure; `SilentExit.NoProductDetected (7)` never used | `CArgsParser.cs:353` |
| High | fable | startup-cli-14 | — | `/import:<bad-path>` logs an error but continues the run against the default path | `CArgsParser.cs:158` |
| Low | fable | startup-cli-13 | — | `CMessages.PsVbrFunctionDone` built from the wrong constant (copy-paste error) | `CMessages.cs:125` |
| High | opus | — | startup-cli-04 | `/silent` with no credential source falls back to interactive prompt | `CClientFunctions.cs:499` |
| Med | opus | — | startup-cli-06 | Decrypted plaintext passwords passed to monitor install / held in GUI locals | `CredentialStore.cs:133` |
| Med | opus | — | startup-cli-08 | `CScrubHandler` shared static singleton, non-thread-safe IO-on-every-call | `CXmlHandler.cs:100` |
| Med | opus | — | startup-cli-09 | `CredentialStore` static cache mutated without locking across threads | `CredentialStore.cs:24` |
| Med | opus | — | startup-cli-10 | `run_Click` calls `VerifyPath()` twice and proceeds after failure | `VhcGui.xaml.cs:236` |
| Med | opus | — | startup-cli-11 | `VbrVersionSupportCheck` indexes `[3]` without bounds check | `CClientFunctions.cs:115` |
| Med | opus | — | startup-cli-12 | `/credfile=` passwords are Base64 plaintext at rest | `CArgsParser.cs:555` |
| Low | opus | — | startup-cli-16 | Import product detection sets flags but selector routes on dir existence | `CReportModeSelector.cs:38` |
| Low | opus | — | startup-cli-21 | GUI `HandleThirdState` sets `Scrub=false` — tri-state can **disable anonymization** | `VhcGui.xaml.cs:361` |
| Low | opus | — | startup-cli-22 | `/vbr` and `/vb365` can silently auto-escalate to Both | `CArgsParser.cs:215` |

---

## Where the two reviews diverge (meta)

- **Opus skews security & robustness**: the lone Critical, `verify_ssl:false`, Base64-creds, the Scrub=false anonymization bypass, thread-safety of static caches (`CScrubHandler`/`CredentialStore`/`CGlobals`), credential lifecycle (ACLs, zeroing, TOCTOU), and O(n²) perf.
- **Fable skews functional correctness & cleanup**: inverted return semantics, deadlocks/timeout ordering, the PS1 collection scripts (CSV injection), `CLogger` robustness, dead-code removal, and a deeper VB365/VBR-tables correctness pass.
- **Neither is a superset.** A complete backlog needs both. 55 of ~150 issues were independently confirmed by both reviewers — those are the highest-confidence fixes.
