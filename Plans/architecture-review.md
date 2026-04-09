# Veeam Health Check - Architecture Review

> **STATUS: REVIEW COMPLETE — FINDINGS NOT ACTIONED** | 2 Critical, 7 High, 12 Medium findings documented. No implementation work done yet. Recommendations in Phase 1-3 action plan at bottom of file.

**Date:** 2026-03-15
**Reviewer:** Architecture Agent (Serena Blackwood)
**Codebase:** veeam-healthcheck @ commit f00de23 (dev branch)
**Scope:** Read-only review of .NET 8.0 WPF application

---

## Executive Summary

The Veeam Health Check is a well-structured Windows utility that follows a clear three-phase pipeline (Collection, Processing, Report Generation). The codebase demonstrates solid domain knowledge and has evolved organically over time. However, several architectural patterns present maintainability and reliability risks at scale. The most critical concerns are: pervasive static mutable state via `CGlobals`, resource leaks in CSV readers, and inconsistent error handling including swallowed exceptions across the data pipeline.

---

## 1. Project Structure and Layering

### 1.1 Overall Organization

The project follows a reasonable physical layout:

```
Startup/          - Entry point, CLI parsing, mode selection, client functions
Common/           - CGlobals, CLogger, CCsvsInMemory, CScrubHandler
Functions/
  Collection/     - PowerShell invocation, SQL, registry, log parsing
  Reporting/
    CsvHandlers/  - CSV reading and parsing
    DataTypes/    - Typed data models and parsing
    Html/         - HTML compilation, data forming, table generation
      VBR/        - VBR-specific HTML
      VB365/      - VB365-specific HTML
      Shared/     - Shared HTML formatting
```

**Finding 1.1a: Layer violations in Startup layer** [Medium]
- `CClientFunctions.cs` in `Startup/` directly references `Functions.Collection.DB.CRegReader` and `Functions.Collection.CCollections`, creating a tight coupling between the orchestration layer and collection internals. It also imports `Functions.Reporting.Html.VBR.VbrTables.Security` namespace.
- File: `vHC/HC_Reporting/Startup/CClientFunctions.cs:10-11`

**Finding 1.1b: Report generation triggered in constructors** [Medium]
- `CVb365HtmlCompiler` executes its entire report generation pipeline inside the constructor (line 26: `this.RunCompiler()`). This means instantiation has a major side effect -- the VB365 report is fully generated and exported by the time the constructor returns. The calling code at `CReportModeSelector:68-69` instantiates and immediately disposes, relying on this constructor side effect.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:25-27`
- File: `vHC/HC_Reporting/Startup/CReportModeSelector.cs:66-69`

**Finding 1.1c: VBR/VB365 directory separation is fragile** [Medium]
- `CReportModeSelector.FileChecker()` determines which reports to run by checking whether `CVariables.vbrDir` and `CVariables.vb365dir` directories exist (lines 40, 45). These directory paths are computed dynamically with side effects (server name, timestamp). If the import path resolver sets `ResolvedImportPath`, both `vbrDir` and `vb365dir` return the same value, which could cause both reports to attempt generation against the same data.
- File: `vHC/HC_Reporting/Startup/CVariables.cs:33-73`

### 1.2 Namespace Consistency

**Finding 1.2a: Inconsistent namespace hierarchy** [Low]
- `CHtmlTables` is in namespace `VeeamHealthCheck.Html.VBR` (missing `Functions.Reporting` segment), while most other reporting classes use `VeeamHealthCheck.Functions.Reporting.Html.VBR`.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs:33`

---

## 2. Static State and Coupling (CGlobals)

### 2.1 Scale of Dependency

`CGlobals.cs` is the most deeply coupled component in the codebase. It is referenced in **66 files with 704 total usages**. It serves simultaneously as:
- Application configuration store (report days, paths, scrub mode)
- Runtime state container (VBR version, DB info, server info)
- Shared data cache (`DtParser`, `ServerInfo`, `CsvValidationResults`)
- Service locator (`Logger`, `Scrubber`)

**Finding 2.1a: CGlobals is an untestable God Object** [Critical]
- `CGlobals` holds 40+ public static mutable fields/properties spanning configuration, runtime state, data caches, and shared services. This makes unit testing nearly impossible without carefully managing global state between tests, creates hidden coupling between components, and prevents any form of parallel execution.
- File: `vHC/HC_Reporting/Common/CGlobals.cs:12-174` (entire file)

**Finding 2.1b: Mutable static state creates ordering dependencies** [High]
- Multiple fields must be set in specific order during initialization: `VBRMAJORVERSION` must be set before `PowerShellVersion` can be derived; `VBRServerName` must be set before `CVariables.vbrDir` is computed; `IMPORT` flag changes the behavior of `CVariables.vbrDir` and `vb365dir` getters. There is no compile-time or runtime enforcement of these ordering constraints.
- File: `vHC/HC_Reporting/Common/CGlobals.cs:33-35` (version fields)
- File: `vHC/HC_Reporting/Startup/CVariables.cs:54-73` (conditional path logic)

**Finding 2.1c: DtParser stored as global mutable singleton** [High]
- `CGlobals.DtParser` is assigned during `CHtmlBodyHelper.PopulateCsvToMemory()` and then read by `CDataFormer` and `CHtmlTables`. This global assignment creates a temporal coupling: any code that accesses `DtParser` before `PopulateCsvToMemory()` runs will get null.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs:89`
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:223` (reads `CGlobals.DtParser.ServerSummaryInfo`)

**Finding 2.1d: Thread-safety concerns with static mutable fields** [Medium]
- Fields like `REMOTEHOST`, `VBRServerName`, `IsVbr`, `IsVb365` are public static with no synchronization. While the application is currently single-threaded, the multi-server support infrastructure (`SelectedServers`, `MaxParallelServers`) suggests future parallelism which would be unsafe.
- File: `vHC/HC_Reporting/Common/CGlobals.cs:45-49`

### 2.2 Recommendations for CGlobals

1. **Short-term:** Extract CGlobals into logical groups -- `AppConfig` (immutable after startup), `RuntimeState` (mutable during execution), `SharedServices` (Logger, Scrubber). Make AppConfig fields `readonly` after initialization.
2. **Medium-term:** Introduce a `HealthCheckContext` object that is passed through the pipeline, replacing global state access. This enables testability and eventually parallel execution.
3. **Long-term:** Use dependency injection to provide services (logger, scrubber, csv parser) to components that need them.

---

## 3. Data Flow Pipeline

### 3.1 Collection Layer

**Finding 3.1a: PowerShell execution lacks structured output validation** [High]
- PowerShell scripts produce CSV files as side effects. The C# code has no schema validation beyond CsvHelper's header matching. If a PowerShell script changes column names or adds/removes columns, failures manifest as silent empty data (via `MissingFieldFound = null` in CSV config) rather than explicit errors.
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:85` (`MissingFieldFound = null`)

**Finding 3.1b: Hardcoded PowerShell paths** [Medium]
- Multiple locations hardcode `C:\Program Files\PowerShell\7\pwsh.exe` as the PS7 path, though `PSInvoker` also searches `PATH`. The `Ps7Executor` class (separate from `PSInvoker`) independently hardcodes this path in 4 locations without any fallback.
- File: `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs:32,60,161`
- File: `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:269`

**Finding 3.1c: Duplicate credential handling code** [Medium]
- The credential encoding pattern (Base64 password encoding, `CredsHandler` instantiation, safe logging) is duplicated across `VbrConfigStartInfo()`, `ConfigStartInfo()`, and `MfaTestPassed()` in PSInvoker.cs. Each duplication is a maintenance risk and potential source of inconsistency.
- File: `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:478-489,669-681,290-294`

### 3.2 CSV Processing Layer

**Finding 3.2a: CsvReader resources are never disposed** [Critical]
- `CCsvReader.CReader()` creates a `TextReader` (StreamReader) and wraps it in a `CsvReader`, but neither is ever disposed. The `CsvReader` objects are returned to callers who also don't dispose them. `CCsvParser.Dispose()` is an empty method. Since `CsvReader` implements `IDisposable` and holds open file handles, this creates resource leaks that accumulate across the ~43 CSV file types processed.
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs:66-69` (creates StreamReader, never disposed)
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:1009` (empty Dispose)

**Finding 3.2b: Inconsistent null vs empty return patterns** [High]
- `CCsvParser` has two different patterns for missing data: the `VbrGetDynamicCsvRecs` methods return `Enumerable.Empty<dynamic>()` when CSV is missing (line 157), while the typed parser methods (e.g., `SessionCsvParser`, `BnrCsvParser`, `SobrCsvParser`) return `null` (43 occurrences). Callers must handle both patterns, and several callers call `.ToList()` or LINQ methods directly on the return value without null checks, risking `NullReferenceException`.
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:147-158` (dynamic returns empty)
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:514-526` (typed returns null)
- Example caller risk: `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:205` (`csv.GetDynamicVboGlobal().ToList()` -- will throw if null)

**Finding 3.2c: CCsvsInMemory uses naive CSV parsing** [Medium]
- `CCsvsInMemory.LoadCsv()` splits on commas without handling quoted fields, escaped commas, or multi-line values. This is a separate CSV parser from CsvHelper, introduced for in-memory caching. If any CSV value contains a comma (e.g., server descriptions, paths), rows will be silently corrupted with "mismatched columns" warnings.
- File: `vHC/HC_Reporting/Common/CCsvsInMemory.cs:33,38` (naive `Split(',')`)

**Finding 3.2d: Two independent CSV parsing paths** [Medium]
- The codebase has two CSV parsing mechanisms: `CCsvReader`/`CCsvParser` (using CsvHelper library) and `CCsvsInMemory` (custom naive parser). `CHtmlCompiler.LoadCsvToMemory()` uses `CCsvsInMemory` to read the vbrinfo CSV for version detection, while `CDataFormer` and all typed parsers use CsvHelper. This dual-path creates inconsistency risk if the same file is read differently by each path.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:384-448`
- File: `vHC/HC_Reporting/Common/CCsvsInMemory.cs:1-92`

### 3.3 Data Transformation Layer

**Finding 3.3a: CDataFormer is a 1400+ line God Class** [High]
- `CDataFormer.cs` is 67KB and handles transformation logic for every VBR data type (servers, proxies, repositories, SOBR, jobs, sessions, workloads, malware, compliance, NAS, tape, cloud connect, etc.). It has public mutable fields (`viDupes`, `vmProtectedByPhys`, etc. at lines 366-377) that are set as side effects of calling `ProtectedWorkloadsToXml()` and then read by other methods.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs` (67KB, 1400+ lines)

**Finding 3.3b: Heavy use of `dynamic` type bypasses compile-time safety** [High]
- Most CSV data flows through `IEnumerable<dynamic>` from the parser. Property access on dynamic objects (e.g., `x.mfa`, `x.foureyes`, `x.encryptionenabled`) has zero compile-time checking. A typo in a property name or a CSV header change will cause `RuntimeBinderException` at runtime with no static analysis catching it.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:92-93` (examples: `x.mfa`, `x.foureyes`)
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:147-158` (dynamic return pattern)

### 3.4 Report Generation Layer

**Finding 3.4a: HTML built via string concatenation** [Medium]
- The entire HTML report is built by concatenating strings (39 `htmldoc +=` operations in VBR compiler, 30 in VB365 compiler). For large reports, this creates O(n^2) string allocation behavior. A `StringBuilder` or template engine would be significantly more efficient and maintainable.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:563-567` (AddToHtml method)
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:43-93` (30 concatenations)

**Finding 3.4b: Duplicated server name resolution logic** [Medium]
- Both `CHtmlCompiler.GetServerName()` and `CVb365HtmlCompiler.GetServerName()` contain nearly identical 3-priority fallback logic (remote host, CSV extraction, DNS hostname). These should be unified.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs:121-182`
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs:155-200`

---

## 4. Error Handling Patterns

### 4.1 Swallowed Exceptions

**Finding 4.1a: Empty catch blocks hide failures** [Critical]
- At least 7 locations in the codebase have completely empty catch blocks (`catch (Exception) { }`) that silently swallow errors. These are found in critical paths including log parsing, data type parsing, and job session summary generation. When these silently fail, the resulting report will have missing or incorrect data with no indication of the problem.
- File: `vHC/HC_Reporting/Startup/CHotfixDetector.cs:194,213,222`
- File: `vHC/HC_Reporting/Functions/Collection/LogParser/CLogParser.cs:163,257,258`
- File: `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs:567`
- File: `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummaryHelper.cs:249`

**Finding 4.1b: Exception handler that logs but eats the return value** [High]
- In `CDataFormer.SecSummary()`, multiple catch blocks handle exceptions by logging but then set security properties to `false`. A parsing failure that prevents reading security configuration is reported as "security feature not enabled" rather than "unknown." This could cause a health check report to falsely indicate security features are disabled.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:115-117` (catch swallows, sets MFA to default)
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:148-153` (immutability defaults to false on error)

**Finding 4.1c: ProtectedWorkloadsToXml returns error code silently** [Medium]
- `CDataFormer.ProtectedWorkloadsToXml()` catches all exceptions and returns `1` as an error code (line 361), but the calling code in `CHtmlTables` does not check this return value. A failure in workload calculation silently produces empty/zero data in the report.
- File: `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs:359-363`

### 4.2 Hard Process Exits

**Finding 4.2a: Environment.Exit calls bypass cleanup** [High]
- There are 8 active `Environment.Exit()` calls scattered across the codebase (CArgsParser, CClientFunctions, CCollections, PSInvoker). These immediately terminate the process without allowing dispose methods, finally blocks, or WPF shutdown handlers to run. This can leave temporary files, open file handles, and PowerShell processes orphaned.
- File: `vHC/HC_Reporting/Startup/CArgsParser.cs:265,288`
- File: `vHC/HC_Reporting/Startup/CClientFunctions.cs:77,100,130`
- File: `vHC/HC_Reporting/Functions/Collection/CCollections.cs:234`
- File: `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs:197`

### 4.3 Logging

**Finding 4.3a: Logger has no log level filtering** [Low]
- `CLogger` writes all levels to file. The `Debug` method checks `CGlobals.DEBUG` for console output but still writes to the log file, and there is no way to set minimum log level. The `silent` parameter on most methods controls console output, not file output, which is counterintuitive.
- File: `vHC/HC_Reporting/Common/Logging/CLogger.cs:66-81`

**Finding 4.3b: Logger file writes can silently fail** [Low]
- The retry-once pattern for file writes (lines 111-127) is reasonable, but the final catch swallows all exceptions silently. In a scenario where the log directory becomes inaccessible, all diagnostic information is lost.
- File: `vHC/HC_Reporting/Common/Logging/CLogger.cs:122-127`

---

## 5. Extensibility

### 5.1 Adding a New Product Type

**Finding 5.1a: Product detection is tightly coupled to process scanning** [High]
- Adding a new product (e.g., Veeam Backup for Kubernetes) would require changes in at minimum:
  1. `CClientFunctions.ModeCheck()` -- new process name detection
  2. `CGlobals` -- new `IsVbK` boolean flag
  3. `CVariables` -- new directory path property
  4. `CCollections.Run()` -- new collection branch
  5. `PSInvoker` -- new script paths and invocation methods
  6. `CReportModeSelector.FileChecker()` -- new report start method
  7. New HTML compiler class
  8. New CSV parser methods in `CCsvParser`
  9. New data type models
- There is no plugin architecture, interface-based product abstraction, or strategy pattern that would allow adding a product without touching the core pipeline. Each product type is wired in via conditionals on global boolean flags.
- File: `vHC/HC_Reporting/Startup/CClientFunctions.cs:135-196` (process-based detection)

### 5.2 Adding a New Report Format

**Finding 5.2a: Report formats partially decoupled** [Medium]
- HTML is the primary format with PDF and PPTX as secondary exports. The HTML-to-PDF and HTML-to-PPTX converters exist as separate classes, which is good. However, the report data is deeply intertwined with HTML generation (string concatenation of HTML tags in `CHtmlTables`, `CDataFormer`, `CHtmlBodyHelper`). Adding a new format (e.g., JSON, Markdown) would require either: (a) parsing the generated HTML, or (b) extracting the data model from HTML generation logic. Neither is straightforward.
- The `CFullReportJson` object in `CGlobals` suggests JSON export was considered but the data flow still runs through HTML first.
- File: `vHC/HC_Reporting/Common/CGlobals.cs:106` (`CFullReportJson` field)

### 5.3 Adding a New Data Source

**Finding 5.3a: Collection sources are hardwired** [Medium]
- Adding a new data source (e.g., REST API for Veeam ONE integration) would require adding:
  1. A new collection class in `Functions/Collection/`
  2. Wiring it into `CCollections.Run()` with appropriate conditional logic
  3. New CSV output (to match the existing pipeline) OR new parser paths
- The pipeline assumes CSV as the intermediate format. A non-CSV source would need to either produce CSV files to fit the existing pipeline, or introduce a parallel data path that bypasses `CCsvReader`/`CCsvParser`.
- File: `vHC/HC_Reporting/Functions/Collection/CCollections.cs:36-69`

---

## 6. Additional Findings

### 6.1 Fake Dispose Pattern

**Finding 6.1a: IDisposable implemented with empty Dispose methods** [Medium]
- At least 8 classes implement `Dispose()` as empty methods or implement `IDisposable` without actually disposing anything: `CReportModeSelector`, `CClientFunctions`, `CCsvParser`, `CJobSessionInfo`, `CVb365HtmlCompiler`, `CDataTypesParser`, `CHtmlCompiler`, `HtmlToPptxConverter`. This is misleading -- callers may wrap these in `using` statements expecting resources to be cleaned up.
- File: `vHC/HC_Reporting/Startup/CReportModeSelector.cs:24-26`
- File: `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs:1009`

### 6.2 Suppressed Code Analysis

**Finding 6.2a: Multiple code analysis rules suppressed project-wide** [Medium]
- The project suppresses CA1031 (catch general exceptions), CA1806 (ignoring return values), CA1822 (members could be static), and several others. While pragmatic for a utility tool, CA1031 suppression masks the swallowed exception problem identified in section 4.
- File: `vHC/HC_Reporting/VeeamHealthCheck.csproj:30`

### 6.3 Dependency Concerns

**Finding 6.3a: Unnecessary Node.js and npm packages** [Low]
- The project includes `Node.js` (5.3.0) and `Npm` (3.5.2) NuGet packages. Neither appears to be used in any C# code -- no JavaScript build step or Node invocation was found. These add unnecessary size to the deployment.
- File: `vHC/HC_Reporting/VeeamHealthCheck.csproj:125-126`

### 6.4 CLI Argument Parsing

**Finding 6.4a: Days parameter accepts only specific hardcoded values** [Low]
- The CLI parser handles `/days:7`, `/days:30`, `/days:90`, `/days:12` as individual switch cases rather than parsing the numeric value. Any other day value (e.g., `/days:14`) is silently ignored.
- File: `vHC/HC_Reporting/Startup/CArgsParser.cs:125-140`

---

## Summary of Findings by Severity

### Critical (2)
| # | Finding | Location |
|---|---------|----------|
| 2.1a | CGlobals is an untestable God Object with 40+ mutable static fields | CGlobals.cs (entire file) |
| 3.2a | CsvReader/StreamReader resources are never disposed (file handle leaks) | CCsvReader.cs:66-69 |
| 4.1a | 7+ empty catch blocks silently swallow errors in critical paths | Multiple files |

### High (7)
| # | Finding | Location |
|---|---------|----------|
| 2.1b | Mutable static state creates undocumented ordering dependencies | CGlobals.cs, CVariables.cs |
| 2.1c | DtParser stored as global mutable singleton with temporal coupling | CHtmlBodyHelper.cs:89 |
| 3.1a | PowerShell CSV output has no schema validation; MissingFieldFound = null | CCsvReader.cs:85 |
| 3.2b | Inconsistent null vs empty return patterns in CCsvParser (43 null returns) | CCsvParser.cs |
| 3.3a | CDataFormer is a 67KB God Class with mutable public fields | CDataFormer.cs |
| 3.3b | Heavy use of `dynamic` type bypasses all compile-time safety | CDataFormer.cs, CCsvParser.cs |
| 4.2a | 8 Environment.Exit() calls bypass all cleanup | Multiple files |
| 4.1b | Security properties default to "disabled" on parse error (false negatives) | CDataFormer.cs:115-210 |
| 5.1a | No abstraction for product types; adding a product requires 9+ file changes | CClientFunctions.cs, CCollections.cs |

### Medium (12)
| # | Finding | Location |
|---|---------|----------|
| 1.1a | Layer violations in Startup referencing deep collection internals | CClientFunctions.cs:10-11 |
| 1.1b | Report generation executed as constructor side effect | CVb365HtmlCompiler.cs:25-27 |
| 1.1c | Import path resolver makes vbrDir and vb365dir return same value | CVariables.cs:33-73 |
| 2.1d | Thread-safety concerns with static mutable fields | CGlobals.cs:45-49 |
| 3.1b | Hardcoded PowerShell 7 path in multiple locations | PSInvoker.cs, Ps7Executor.cs |
| 3.1c | Duplicate credential handling code across 3 methods | PSInvoker.cs |
| 3.2c | CCsvsInMemory uses naive CSV parsing (no quote handling) | CCsvsInMemory.cs:33,38 |
| 3.2d | Two independent CSV parsing paths for same data | CHtmlCompiler.cs, CCsvsInMemory.cs |
| 3.4a | HTML built via O(n^2) string concatenation | CHtmlCompiler.cs, CVb365HtmlCompiler.cs |
| 3.4b | Duplicated server name resolution in VBR and VB365 compilers | CHtmlCompiler.cs, CVb365HtmlCompiler.cs |
| 5.2a | Report data intertwined with HTML; no format-agnostic data model | Multiple files |
| 6.1a | 8 classes implement empty Dispose methods | Multiple files |
| 6.2a | CA1031 suppressed project-wide, masking swallowed exceptions | VeeamHealthCheck.csproj:30 |

### Low (4)
| # | Finding | Location |
|---|---------|----------|
| 1.2a | Inconsistent namespace hierarchy | CHtmlTables.cs:33 |
| 4.3a | Logger has no log level filtering | CLogger.cs:66-81 |
| 4.3b | Logger file writes can silently fail | CLogger.cs:122-127 |
| 6.3a | Unnecessary Node.js/npm NuGet packages | VeeamHealthCheck.csproj:125-126 |
| 6.4a | Days parameter only accepts 4 hardcoded values | CArgsParser.cs:125-140 |

---

## Recommended Action Plan

### Phase 1: Safety and Reliability (Immediate)
1. **Fix CsvReader resource leaks** -- Wrap `StreamReader` and `CsvReader` in `using` statements or make `CCsvReader` implement `IDisposable` properly. This is a resource leak that worsens with every CSV file processed.
2. **Replace empty catch blocks** -- At minimum, add logging to all empty catch blocks. Ideally, let exceptions propagate or handle them specifically.
3. **Standardize CCsvParser return values** -- Choose one pattern (prefer `Enumerable.Empty<T>()` over `null`) and apply it consistently. Add null guards at call sites.
4. **Replace Environment.Exit with proper shutdown** -- Use return codes through the call stack instead of hard process termination.

### Phase 2: Structural Improvements (Short-term)
5. **Split CGlobals** into `AppConfig` (immutable after init), `RuntimeState` (mutable), and `SharedServices` (Logger/Scrubber). Make config fields readonly.
6. **Extract CDataFormer** into per-domain classes (SecurityDataFormer, ProxyDataFormer, SobrDataFormer, etc.). Each should be under 200 lines.
7. **Remove CCsvsInMemory** or replace its naive CSV parser with CsvHelper to prevent data corruption on comma-containing values.
8. **Use StringBuilder** for HTML generation instead of string concatenation.

### Phase 3: Architecture Evolution (Medium-term)
9. **Introduce a product abstraction** -- Create an `IProduct` interface with `Detect()`, `Collect()`, `GenerateReport()` methods. VBR and VB365 become implementations.
10. **Separate data model from HTML rendering** -- Create a format-agnostic report data model that can be rendered to HTML, JSON, or any other format.
11. **Replace `dynamic` CSV access** with strongly-typed models throughout the pipeline. This trades flexibility for compile-time safety.
12. **Introduce dependency injection** -- Start with the Logger and Scrubber, then expand to CSV parsers and data formers. This enables unit testing.

---

## Files Examined

| File | Purpose | Lines Read |
|------|---------|------------|
| `vHC/HC_Reporting/Startup/EntryPoint.cs` | Application entry point | Full |
| `vHC/HC_Reporting/Common/CGlobals.cs` | Global static state | Full |
| `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs` | CSV file reading | Full |
| `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs` | CSV parsing facade (1055 lines) | Full |
| `vHC/HC_Reporting/Startup/CReportModeSelector.cs` | Report routing | Full |
| `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs` | Data transformation (67KB) | 500 lines |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs` | VBR HTML report compiler | Full |
| `vHC/HC_Reporting/Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs` | VB365 HTML report compiler | Full |
| `vHC/HC_Reporting/Startup/CArgsParser.cs` | CLI argument parsing | Full |
| `vHC/HC_Reporting/Functions/Collection/CCollections.cs` | Collection orchestrator | Full |
| `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs` | PowerShell script invoker | Full |
| `vHC/HC_Reporting/Functions/Collection/PSCollections/PowerShell7Executor.cs` | PS7 execution | Full |
| `vHC/HC_Reporting/Startup/CVariables.cs` | Path configuration | Full |
| `vHC/HC_Reporting/Common/Logging/CLogger.cs` | Logging implementation | Full |
| `vHC/HC_Reporting/Startup/CClientFunctions.cs` | Client orchestration | Full |
| `vHC/HC_Reporting/Functions/Reporting/DataTypes/CDataTypesParser.cs` | Data type parsing | 100 lines |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlBodyHelper.cs` | VBR HTML body builder | 100 lines |
| `vHC/HC_Reporting/Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` | VBR HTML table generation | 100 lines |
| `vHC/HC_Reporting/Common/CCsvsInMemory.cs` | In-memory CSV cache | Full |
| `vHC/HC_Reporting/VeeamHealthCheck.csproj` | Project configuration | Full |
