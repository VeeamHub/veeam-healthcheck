# Veeam Health Check — Opus Full Code Review

> **Reviewer:** Claude Opus 4.8 (8 parallel subsystem reviewers)
> **Branch:** `dev`
> **Date:** 2026-06-12
> **Scope:** All 260 `.cs` files under `vHC/HC_Reporting/` (production code; tests excluded)
> **Status:** Findings only — nothing committed to git. Each finding is a standalone GitHub-issue-style file.

## How to read this

Each file under `code-review-findings-opus/<category>/ISSUE-<category>-NN.md` is written as if it were a GitHub issue (Summary / Evidence / Impact / Suggested Fix / Labels). This INDEX rolls them up by severity and category. Suppressed analyzers (CA1305, CA1031, CA1822, etc.) were **not** reported as findings unless they cause a real, demonstrable bug.

## Totals

| Severity | Count |
|----------|-------|
| 🔴 Critical | 1 |
| 🟠 High | 20 |
| 🟡 Medium | 45 |
| ⚪ Low | 36 |
| **Total** | **102** |

| Category | Findings | Dir |
|----------|----------|-----|
| Startup / CLI / global state | 22 | [`startup-cli/`](startup-cli/) |
| Collection / data sources | 16 | [`collection-data/`](collection-data/) |
| Credentials / security | 10 | [`collection-security/`](collection-security/) |
| CSV parsing / data types | 10 | [`csv-datatypes/`](csv-datatypes/) |
| VBR report core | 12 | [`reporting-vbr-core/`](reporting-vbr-core/) |
| VBR table renderers | 10 | [`reporting-vbr-tables/`](reporting-vbr-tables/) |
| VB365 reporting | 12 | [`reporting-vb365/`](reporting-vb365/) |
| Analysis / Monitor | 10 | [`analysis-monitor/`](analysis-monitor/) |

---

## Cross-cutting themes (read these first)

These patterns recur across multiple subsystems and are the highest-leverage fixes:

1. **Systemic unescaped-HTML / XSS at the report sink.** The legacy `Shared/CHtmlFormatting.cs` `TableData`/`TableHeader` helpers emit collected values (server/VM/job/repo names, paths, registry values) into HTML with **no encoding**, while a newer encoding-aware path (`CHtmlBuilder`/`CSectionTable` using `WebUtility.HtmlEncode`) exists but is not used by 35+ VBR renderers nor any VB365 renderer. → `reporting-vbr-core-01/02/03/04`, `reporting-vbr-tables-01`, `reporting-vb365-01`. **Fix once at the helper, benefit everywhere.**

2. **Plaintext credentials despite a DPAPI store.** The product carefully DPAPI-encrypts passwords at rest, then decrypts and (a) writes them as cleartext YAML for the monitor agent, (b) passes them as `-Password` on the `powershell.exe` command line, (c) Base64-"encodes" them on the process command line. Base64 ≠ encryption. → `analysis-monitor-01/02`, `collection-security-04/10`, `collection-data-07`, `startup-cli-06/12`.

3. **PowerShell argument injection on the log-collection paths.** Most collection paths use the hardened escaping helper, but the hotfix/log-collection paths (`PSInvoker.RunVbrLogCollect` / `RunServerDump`) interpolate unescaped server/path with `UseShellExecute=true`. → `collection-data-01/02`, `collection-security-01`, `analysis-monitor-06`.

4. **Culture-sensitive parse/format.** CSVs are invariant-formatted but parsed/formatted with current culture in many places — silent wrong values (sizes read as 0, DD/MM vs MM/DD date flips, Turkish-I) on non-US machines. CA1305 is suppressed, but these are **real correctness bugs**, not style. → `csv-datatypes-02/10`, `reporting-vbr-tables-07`, `reporting-vbr-core-09`, `reporting-vb365-07`, `analysis-monitor-05/08`.

5. **Empty/broad catch blocks drop whole sections silently.** Several report sections and collection steps swallow exceptions and emit nothing, so a single bad row hides an entire table/section with no signal. → `csv-datatypes-04`, `reporting-vbr-core-06`, `reporting-vb365-05`, `reporting-vbr-tables-03`, `startup-cli-19`, `collection-data-12`.

6. **Undisposed resources.** `CCsvReader` returns `StreamReader`+`CsvReader` that nothing ever disposes (empty `Dispose()` no-ops), plus undisposed `Process`, DinkToPdf native handles, and an orphaned `WebBrowser`. → `csv-datatypes-01`, `collection-data-09`, `reporting-vbr-core-05/10`.

7. **Static mutable state with no reset / no locking.** `CGlobals`, `CRegReader`, `CredentialStore` cache, and `CScrubHandler` singleton hold mutable static state shared across GUI/CLI threads and across runs/tests. → `startup-cli-08/09/14`, `collection-data-10`.

---

## 🔴 Critical

| ID | Title | File |
|----|-------|------|
| [startup-cli-01](startup-cli/ISSUE-startup-cli-01.md) | VB365 HTML report never generated — compiler disposed immediately after construction | `Startup/CReportModeSelector.cs:65` |

## 🟠 High (20)

| ID | Title | File |
|----|-------|------|
| [startup-cli-02](startup-cli/ISSUE-startup-cli-02.md) | `Main()` always returns 0 on success path — real exit code discarded | `Startup/EntryPoint.cs:26` |
| [startup-cli-03](startup-cli/ISSUE-startup-cli-03.md) | Hotfix detector runs twice (local + remote) when REMOTEEXEC set — missing `else` | `Startup/CArgsParser.cs:334` |
| [startup-cli-04](startup-cli/ISSUE-startup-cli-04.md) | `/silent` with no credential source falls back to interactive prompt | `Startup/CClientFunctions.cs:499` |
| [startup-cli-05](startup-cli/ISSUE-startup-cli-05.md) | GUI `Run()` `Environment.Exit(0)` races `ContinueWith`, swallows task faults | `VhcGui.xaml.cs:248` |
| [collection-data-01](collection-data/ISSUE-collection-data-01.md) | PowerShell arg injection in LogCollectionInfo/ServerDumpInfo (`UseShellExecute=true`) | `Collection/PSCollections/PSInvoker.cs:626` |
| [collection-data-05](collection-data/ISSUE-collection-data-05.md) | `ExecutePsScriptWithFailover` never fails over — return/exit inside version loop | `Collection/PSCollections/PSInvoker.cs:194` |
| [collection-data-07](collection-data/ISSUE-collection-data-07.md) | Password Base64 is not encryption — plaintext recoverable from process cmdline | `Collection/PSCollections/PSInvoker.cs:546` |
| [collection-security-01](collection-security/ISSUE-collection-security-01.md) | PowerShell arg injection via unescaped `-Server`/`-file` in log-collection paths | `Collection/PSCollections/PSInvoker.cs:626,656` |
| [collection-security-10](collection-security/ISSUE-collection-security-10.md) | Cleartext password passed as `-Password` on the powershell.exe command line | `Collection/PSCollections/PSInvoker.cs:269` |
| [csv-datatypes-01](csv-datatypes/ISSUE-csv-datatypes-01.md) | `CsvReader`/`StreamReader` never disposed — file-handle leak on every CSV read | `CsvHandlers/CCsvReader.cs:65-70` |
| [csv-datatypes-02](csv-datatypes/ISSUE-csv-datatypes-02.md) | Culture-sensitive numeric parsing → wrong sizes/counts on non-US machines | `DataTypes/CDataTypesParser.cs:1120-1138` |
| [csv-datatypes-03](csv-datatypes/ISSUE-csv-datatypes-03.md) | `MatchRepoIdToRepo` re-parses Repo+SOBR CSVs from disk on every job row (O(n·m)) | `DataTypes/CDataTypesParser.cs:561-582` |
| [reporting-vbr-core-01](reporting-vbr-core/ISSUE-reporting-vbr-core-01.md) | Collected data emitted into HTML cells without encoding (XSS) | `Shared/CHtmlFormatting.cs:195-219` |
| [reporting-vbr-tables-01](reporting-vbr-tables/ISSUE-reporting-vbr-tables-01.md) | Systemic stored XSS: cell values written without HTML-encoding (35+ renderers) | `Shared/CHtmlFormatting.cs:204` |
| [reporting-vb365-01](reporting-vb365/ISSUE-reporting-vb365-01.md) | Unescaped CSV values into HTML across all VB365 tables (stored XSS) | `CM365Tables.cs:319` |
| [reporting-vb365-02](reporting-vb365/ISSUE-reporting-vb365-02.md) | Repo free-space threshold sets value instead of shade — low-space never flags | `CM365Tables.cs:412` |
| [reporting-vb365-03](reporting-vb365/ISSUE-reporting-vb365-03.md) | `Jobs()` uses `break` on empty Organization — silently drops all remaining jobs | `CM365Tables.cs:1639` |
| [reporting-vb365-04](reporting-vb365/ISSUE-reporting-vb365-04.md) | Unguarded array indexing on split OS/RAM strings crashes Backup Server table | `CM365Tables.cs:826` |
| [analysis-monitor-01](analysis-monitor/ISSUE-analysis-monitor-01.md) | VBR credential password written to disk in cleartext YAML | `Monitor/CVhcMonitorIntegration.cs:83` |
| [analysis-monitor-02](analysis-monitor/ISSUE-analysis-monitor-02.md) | Monitor config hardcodes `verify_ssl: false` — creds over unauthenticated TLS | `Monitor/CVhcMonitorIntegration.cs:86` |

## 🟡 Medium (45)

| ID | Title | File |
|----|-------|------|
| [startup-cli-06](startup-cli/ISSUE-startup-cli-06.md) | Decrypted plaintext passwords passed to monitor install / held in GUI locals | `Startup/CredentialStore.cs:133` |
| [startup-cli-07](startup-cli/ISSUE-startup-cli-07.md) | `/outdir`,`/path`,`/host` args not validated against traversal/quoting | `Startup/CArgsParser.cs:386` |
| [startup-cli-08](startup-cli/ISSUE-startup-cli-08.md) | `CScrubHandler` shared static singleton, non-thread-safe IO-on-every-call | `Common/Scrubber/CXmlHandler.cs:100` |
| [startup-cli-09](startup-cli/ISSUE-startup-cli-09.md) | `CredentialStore` static cache mutated without locking across threads | `Startup/CredentialStore.cs:24` |
| [startup-cli-10](startup-cli/ISSUE-startup-cli-10.md) | `run_Click` calls `VerifyPath()` twice and proceeds after failure | `VhcGui.xaml.cs:236` |
| [startup-cli-11](startup-cli/ISSUE-startup-cli-11.md) | `VbrVersionSupportCheck` indexes `[3]` without bounds check | `Startup/CClientFunctions.cs:115` |
| [startup-cli-12](startup-cli/ISSUE-startup-cli-12.md) | `/credfile=` passwords are Base64 plaintext at rest | `Startup/CArgsParser.cs:555` |
| [startup-cli-14](startup-cli/ISSUE-startup-cli-14.md) | `CGlobals` static state persists across runs/tests — no reset | `Common/CGlobals.cs:47` |
| [startup-cli-17](startup-cli/ISSUE-startup-cli-17.md) | `CHotfixDetector.ClearTargetPath` deletes parent dir inside per-subdir loop | `Startup/CHotfixDetector.cs:223` |
| [startup-cli-19](startup-cli/ISSUE-startup-cli-19.md) | `ExtractLogs` swallows zip failures then deletes source archive — data loss | `Startup/CHotfixDetector.cs:195` |
| [collection-data-02](collection-data/ISSUE-collection-data-02.md) | `RunScript` builds `-Command` from unescaped double-quoted script path | `Collection/PSCollections/PowerShell7Executor.cs:68` |
| [collection-data-03](collection-data/ISSUE-collection-data-03.md) | `CLogParser` CSV writer produces malformed/unquoted CSV | `Collection/LogParser/CLogParser.cs:90` |
| [collection-data-04](collection-data/ISSUE-collection-data-04.md) | `waits.csv` opened per-row under lock during parallel parsing | `Collection/LogParser/CLogParser.cs:86` |
| [collection-data-06](collection-data/ISSUE-collection-data-06.md) | `TestMfa` reads StandardError twice; parse loop never runs, returns true | `Collection/PSCollections/PSInvoker.cs:308` |
| [collection-data-08](collection-data/ISSUE-collection-data-08.md) | `CVmcReader` NRE when no VMC.log; discarded `OrderBy`; null LOGLOCATION | `Collection/LogParser/CVmcReader.cs:62` |
| [collection-data-10](collection-data/ISSUE-collection-data-10.md) | `CRegReader` static mutable fields for instance DB state — cross-thread bleed | `Collection/DB/CRegReader.cs:17` |
| [collection-data-11](collection-data/ISSUE-collection-data-11.md) | Remote registry reads lack null guards / admin-denied handling | `Collection/DB/CRegReader.cs:305` |
| [collection-data-13](collection-data/ISSUE-collection-data-13.md) | `CImpersonation`: broken console prompts; password in immutable string | `Collection/CImpersonation.cs:77` |
| [collection-security-02](collection-security/ISSUE-collection-security-02.md) | NRE dereferencing `creds.Value` in `TestMfa` when credentials absent | `Collection/PSCollections/PSInvoker.cs:256-261` |
| [collection-security-03](collection-security/ISSUE-collection-security-03.md) | DPAPI credential store written with default ACLs, no permission hardening | `Startup/CredentialStore.cs:20-22,179-189` |
| [collection-security-04](collection-security/ISSUE-collection-security-04.md) | Plaintext password round-trips to managed String, never zeroed | `Startup/CredentialStore.cs:140-142` |
| [csv-datatypes-04](csv-datatypes/ISSUE-csv-datatypes-04.md) | Broad catch blocks swallow CSV parse errors, mask malformed data as empty | `DataTypes/CDataTypesParser.cs:496-543` |
| [csv-datatypes-05](csv-datatypes/ISSUE-csv-datatypes-05.md) | `JobSessionInfo` uses `SingleOrDefault` on duplicate job names — throws | `DataTypes/CDataTypesParser.cs:647-659` |
| [csv-datatypes-06](csv-datatypes/ISSUE-csv-datatypes-06.md) | Integer division in RAM/size conversions truncates / divide-by-zero risk | `DataTypes/CDataTypesParser.cs:738-741` |
| [csv-datatypes-07](csv-datatypes/ISSUE-csv-datatypes-07.md) | Static proxy CSV parsers ignore custom import dir — read from default `vbrDir` | `CsvHandlers/CCsvParser.cs:549-567` |
| [csv-datatypes-08](csv-datatypes/ISSUE-csv-datatypes-08.md) | `StreamReader` opened without explicit UTF-8/BOM handling | `CsvHandlers/CCsvReader.cs:67` |
| [csv-datatypes-09](csv-datatypes/ISSUE-csv-datatypes-09.md) | Positional `[Index]` mapping + `MissingFieldFound=null` silently mis-binds | `CsvHandlers/CCsvReader.cs:85` |
| [reporting-vbr-core-02](reporting-vbr-core/ISSUE-reporting-vbr-core-02.md) | License holder name interpolated into header without encoding | `Shared/CHtmlFormatting.cs:540-551` |
| [reporting-vbr-core-03](reporting-vbr-core/ISSUE-reporting-vbr-core-03.md) | Section title/icon/id interpolated raw into section-card markup | `Shared/CHtmlFormatting.cs:101-111` |
| [reporting-vbr-core-04](reporting-vbr-core/ISSUE-reporting-vbr-core-04.md) | `CSectionTable` `&#` heuristic passes raw HTML through, defeating encoding | `Shared/CSectionTable.cs:185-193` |
| [reporting-vbr-core-05](reporting-vbr-core/ISSUE-reporting-vbr-core-05.md) | DinkToPdf converter / PdfTools native resources never released | `Exportables/HtmlToPdfConverter.cs:86-90` |
| [reporting-vbr-core-06](reporting-vbr-core/ISSUE-reporting-vbr-core-06.md) | `SetReportNameAndPath` swallows exceptions, returns null, misleading crash | `CHtmlExporter.cs:227-269` |
| [reporting-vbr-core-07](reporting-vbr-core/ISSUE-reporting-vbr-core-07.md) | O(n²) string concatenation building full VBR report in memory | `VBR/CHtmlBodyHelper.cs:34-421` |
| [reporting-vbr-tables-02](reporting-vbr-tables/ISSUE-reporting-vbr-tables-02.md) | `DomainStatus` computes danger shade but never applies it | `VbrTables/BackupServer/CVbrServerTableHelper.cs:135` |
| [reporting-vbr-tables-03](reporting-vbr-tables/ISSUE-reporting-vbr-tables-03.md) | Malware tables `throw` (drop section); unguarded `DateTime.Parse` | `VbrTables/Security/CMalwareTable.cs:203` |
| [reporting-vbr-tables-05](reporting-vbr-tables/ISSUE-reporting-vbr-tables-05.md) | NRE: `foreach` over unchecked-null list drops table | `VbrTables/SOBR/CSobrExtentTable.cs:57` |
| [reporting-vbr-tables-06](reporting-vbr-tables/ISSUE-reporting-vbr-tables-06.md) | Job Info on-disk column shows GB mislabeled TB; inconsistent thresholds | `VbrTables/Jobs Info/CJobInfoTable.cs:327` |
| [reporting-vbr-tables-09](reporting-vbr-tables/ISSUE-reporting-vbr-tables-09.md) | Proxy JSON export drops a column, risks header/value misalignment vs HTML | `VbrTables/Proxies/CProxyTable.cs:138` |
| [reporting-vb365-05](reporting-vb365/ISSUE-reporting-vb365-05.md) | Empty catch blocks silently drop entire VB365 report sections | `CM365Tables.cs:127` |
| [reporting-vb365-06](reporting-vb365/ISSUE-reporting-vb365-06.md) | Divide-by-zero on license/free-space/size-limit percentages drops sections | `CM365Tables.cs:53` |
| [reporting-vb365-07](reporting-vb365/ISSUE-reporting-vb365-07.md) | Culture-sensitive number/date parsing across all VB365 tables | `CM365Tables.cs:55` |
| [reporting-vb365-12](reporting-vb365/ISSUE-reporting-vb365-12.md) | Positional-index scrubbing brittle — risks emitting identifiers unscrubbed | `CM365Tables.cs:1360` |
| [analysis-monitor-03](analysis-monitor/ISSUE-analysis-monitor-03.md) | `EscapeYaml` insufficient for YAML double-quoted scalars — config corruption/inject | `Monitor/CVhcMonitorIntegration.cs:297` |
| [analysis-monitor-06](analysis-monitor/ISSUE-analysis-monitor-06.md) | Scheduled-task PowerShell built by interpolation with fragile quote escaping | `Monitor/CVhcMonitorIntegration.cs:136` |
| [analysis-monitor-07](analysis-monitor/ISSUE-analysis-monitor-07.md) | Uninstall leaves plaintext-credential config and copied exe on disk | `Monitor/CVhcMonitorIntegration.cs:202` |

## ⚪ Low (36)

| ID | Title | File |
|----|-------|------|
| [startup-cli-13](startup-cli/ISSUE-startup-cli-13.md) | `ModeCheck()` unreachable branch and repeated full process scan | `Startup/CClientFunctions.cs:174` |
| [startup-cli-15](startup-cli/ISSUE-startup-cli-15.md) | Unknown/malformed CLI args silently ignored — no default case | `Startup/CArgsParser.cs:108` |
| [startup-cli-16](startup-cli/ISSUE-startup-cli-16.md) | Import product detection sets flags but selector routes on dir existence | `Startup/CReportModeSelector.cs:38` |
| [startup-cli-18](startup-cli/ISSUE-startup-cli-18.md) | `CHotfixDetector` no-ops on invalid path; null flows into `Run()`; blocks silent | `Startup/CHotfixDetector.cs:24` |
| [startup-cli-20](startup-cli/ISSUE-startup-cli-20.md) | `CLogger` pins output dir at static init — early logs land in default path | `Common/Logging/CLogger.cs:22` |
| [startup-cli-21](startup-cli/ISSUE-startup-cli-21.md) | GUI `HandleThirdState` sets `Scrub=false` — tri-state can disable anonymization | `VhcGui.xaml.cs:361` |
| [startup-cli-22](startup-cli/ISSUE-startup-cli-22.md) | `/vbr` and `/vb365` can silently auto-escalate to Both | `Startup/CArgsParser.cs:215` |
| [collection-data-09](collection-data/ISSUE-collection-data-09.md) | Undisposed `Process` objects in ExecutePsScript/RunVbrLogCollect/etc. | `Collection/PSCollections/PSInvoker.cs:404` |
| [collection-data-12](collection-data/ISSUE-collection-data-12.md) | Swallowed exceptions hide collection failures in log/PATH scanning | `Collection/LogParser/CLogParser.cs:163` |
| [collection-data-14](collection-data/ISSUE-collection-data-14.md) | `CImportPathResolver.FindCsvDirectory` mutates global product flags as side effect | `Collection/CImportPathResolver.cs:88` |
| [collection-data-15](collection-data/ISSUE-collection-data-15.md) | `CLogParser` ctor does registry+FS I/O, can throw from field initializer | `Collection/LogParser/CLogParser.cs:22` |
| [collection-data-16](collection-data/ISSUE-collection-data-16.md) | `CCsvValidator` substring glob over-matches; physical-line record count | `Collection/CCsvValidator.cs:138` |
| [collection-security-05](collection-security/ISSUE-collection-security-05.md) | `EscapeForPowerShellDoubleQuotes` doesn't neutralize newlines | `Collection/Security/CredentialHelper.cs:41-69` |
| [collection-security-06](collection-security/ISSUE-collection-security-06.md) | Username escaping/validation inconsistent; fragile denylist | `Startup/CArgsParser.cs:531-550` |
| [collection-security-07](collection-security/ISSUE-collection-security-07.md) | Dead/insecure `RunImpersonated`/`CImpersonation` accumulate password in string | `Collection/Security/CSecurityInit.cs:45-123` |
| [collection-security-08](collection-security/ISSUE-collection-security-08.md) | Remote registry reads dereference null `RegistryKey`, crash security collection | `Collection/Security/CSecurityInit.cs:145-215` |
| [collection-security-09](collection-security/ISSUE-collection-security-09.md) | Credential store TOCTOU / lost-update; non-atomic write | `Startup/CredentialStore.cs:147-189` |
| [csv-datatypes-10](csv-datatypes/ISSUE-csv-datatypes-10.md) | `TryParseDateTime` fallback uses CurrentCulture — DD/MM vs MM/DD flips | `DataTypes/CDataTypesParser.cs:706-710` |
| [reporting-vbr-core-08](reporting-vbr-core/ISSUE-reporting-vbr-core-08.md) | PPTX export regex-parses HTML, mishandles nested tables / duplicate slides | `Exportables/HtmlToPptxConverter.cs:1460-1576` |
| [reporting-vbr-core-09](reporting-vbr-core/ISSUE-reporting-vbr-core-09.md) | PPTX section-title formatting uses CurrentCulture (Turkish-I) | `Exportables/HtmlToPptxConverter.cs:1588` |
| [reporting-vbr-core-10](reporting-vbr-core/ISSUE-reporting-vbr-core-10.md) | Orphaned `WebBrowser` control and undisposed `Process` in `ExecBrowser` | `CHtmlExporter.cs:318-331` |
| [reporting-vbr-core-11](reporting-vbr-core/ISSUE-reporting-vbr-core-11.md) | `CObjectHelpers.ParseBool` silently returns false for valid truthy values | `Shared/CObjectHelpers.cs:7-37` |
| [reporting-vbr-core-12](reporting-vbr-core/ISSUE-reporting-vbr-core-12.md) | `SetConfigBackupSettings` dereferences `FirstOrDefault()` without null check | `CBackupServerTableHelper.cs:74-97` |
| [reporting-vbr-tables-04](reporting-vbr-tables/ISSUE-reporting-vbr-tables-04.md) | CSV re-parsed twice per render (HTML pass + JSON pass) | `VbrTables/Repositories/CRepoTable.cs:153` |
| [reporting-vbr-tables-07](reporting-vbr-tables/ISSUE-reporting-vbr-tables-07.md) | Culture-sensitive numeric `ToString`/`Convert` wrong in non-US locales | `VbrTables/ProtectedWorkloads/NasSourceInfo.cs:90` |
| [reporting-vbr-tables-08](reporting-vbr-tables/ISSUE-reporting-vbr-tables-08.md) | Tables built by quadratic string `+=` in row loops | `VbrTables/Repositories/CRepoTable.cs:72` |
| [reporting-vbr-tables-10](reporting-vbr-tables/ISSUE-reporting-vbr-tables-10.md) | `FormatDuration` drops hours for compliance scans over 59 minutes | `VbrTables/Security/CComplianceTable.cs:188` |
| [reporting-vb365-08](reporting-vb365/ISSUE-reporting-vb365-08.md) | Proxies scrubbing runs inside column loop, partially scrubs other fields | `CM365Tables.cs:224` |
| [reporting-vb365-09](reporting-vb365/ISSUE-reporting-vb365-09.md) | Security cert date shades use bitwise-or, never rendered (dead coloring) | `CM365Tables.cs:599` |
| [reporting-vb365-10](reporting-vb365/ISSUE-reporting-vb365-10.md) | Quadratic string concatenation building VB365 tables and document | `CM365Tables.cs:1046` |
| [reporting-vb365-11](reporting-vb365/ISSUE-reporting-vb365-11.md) | `ProtStat` user totals can double-count; percentage ignores stale users | `CM365Tables.cs:1513` |
| [analysis-monitor-04](analysis-monitor/ISSUE-analysis-monitor-04.md) | `min_severity` written to YAML without escaping | `Monitor/CVhcMonitorIntegration.cs:104` |
| [analysis-monitor-05](analysis-monitor/ISSUE-analysis-monitor-05.md) | `notifType` lowercased with culture-sensitive `ToLower()` for control flow | `Monitor/CVhcMonitorIntegration.cs:93` |
| [analysis-monitor-08](analysis-monitor/ISSUE-analysis-monitor-08.md) | Monitor state timestamp parsed with culture-sensitive `DateTime.TryParse` | `Monitor/CVhcMonitorIntegration.cs:235` |
| [analysis-monitor-09](analysis-monitor/ISSUE-analysis-monitor-09.md) | `IsTaskRegistered` uses substring match — false positive | `Monitor/CVhcMonitorIntegration.cs:44` |
| [analysis-monitor-10](analysis-monitor/ISSUE-analysis-monitor-10.md) | Dead/stub data models in Analysis/DataModels | `Analysis/DataModels/SOBR.cs:9` |

---

## Suggested triage order

1. **Critical** — `startup-cli-01` (VB365 reports silently never generated) is a functional regression; confirm against a live VB365 run first.
2. **Credential exposure** — `analysis-monitor-01/02`, `collection-security-10`, `collection-data-07`: cleartext creds on disk / cmdline / over `verify_ssl:false`. Highest real-world risk.
3. **PowerShell injection** — `collection-data-01`, `collection-security-01`, `analysis-monitor-06`: the un-hardened collection paths.
4. **The one HTML-encoding fix** — `reporting-vbr-core-01` at `CHtmlFormatting.TableData`/`TableHeader` resolves the bulk of `reporting-vbr-tables-01` and `reporting-vb365-01` in one place.
5. **High-impact correctness** — `startup-cli-02/03/04`, `collection-data-05/06`, `reporting-vb365-02/03/04`, `csv-datatypes-02`.
6. Everything else by severity.

> ⚠️ Several findings overlap intentionally across reviewers (e.g. `PSInvoker.cs` injection appears in both `collection-data` and `collection-security`; the `CHtmlFormatting` XSS appears in core, tables, and VB365). They are kept separate per-subsystem so each can be triaged/assigned independently, but they share a root cause — fix the root once.
