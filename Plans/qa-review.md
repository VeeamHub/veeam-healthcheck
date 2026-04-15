# QA Review: veeam-healthcheck

> **STATUS: REVIEW COMPLETE — PARTIALLY ACTIONED** | Test count grew 244 → 315 (Phase 1 target 300 exceeded). Placeholder tests, empty catches, CReportModeSelector/CScrubHandler gaps still unaddressed.

**Date:** 2026-03-15
**Reviewer:** QA Tester Agent (Opus 4.6)
**Branch:** dev
**Scope:** Comprehensive code-level QA review -- test coverage gaps, test quality, error handling, code quality, cross-platform concerns

---

## Executive Summary

The veeam-healthcheck project has a growing test suite (~20 test files) covering credential handling, CSV parsing, CLI arguments, path validation, static initialization safety, and HTML export basics. However, significant coverage gaps remain in the report compilation pipeline, export formats (PDF/PPTX), data transformation logic, collection layer, scrubbing/anonymization, and the majority of HTML table generators. Many existing tests contain placeholder assertions (`Assert.True(true)`) that pass unconditionally. Error handling across the production code relies heavily on catch-and-swallow patterns that silently hide failures.

**Overall Assessment:** The test foundation is solid in areas it covers (credentials, CSV parsing, path validation). The critical gaps are in the report generation pipeline and data transformation layers -- the highest-risk areas for end-user-facing regressions.

---

## 1. Test Coverage Gap Matrix

### Legend
- **TESTED** = Has dedicated test file(s) with meaningful assertions
- **PARTIAL** = Has some test coverage but significant gaps remain
- **STUB** = Test file exists but contains only placeholders or commented-out code
- **NONE** = No test coverage whatsoever

| Source Module | File(s) | Coverage | Notes |
|---|---|---|---|
| **Startup / Entry** | | | |
| EntryPoint | `Startup/EntryPoint.cs` | STUB | `EntryPointTEST.cs` is entirely commented out (lines 1-30) |
| CArgsParser | `Startup/CArgsParser.cs` | PARTIAL | Good ParsePath tests, but 14 switch tests are placeholders with `Assert.True(true)` |
| CClientFunctions | `Startup/CClientFunctions.cs` | PARTIAL | PathValidation tests cover VerifyPath; ModeCheck, RunVbr, RunVb365 untested |
| CReportModeSelector | `Startup/CReportModeSelector.cs` | NONE | Zero tests for report routing logic |
| CHostNameHelper | `Startup/CHostNameHelper.cs` | TESTED | Good coverage in CArgsParserTEST.cs including edge cases |
| CredentialStore | `Startup/CredentialStore.cs` | TESTED | Comprehensive security tests including encryption roundtrip |
| CAdminCheck | `Startup/CAdminCheck.cs` | NONE | No tests |
| CHotfixDetector | `Startup/CHotfixDetector.cs` | NONE | No tests |
| CVariables | `Startup/CVariables.cs` | PARTIAL | StaticInitializationTEST covers circular dependency; properties untested |
| **Common** | | | |
| CGlobals | `Common/CGlobals.cs` | PARTIAL | CsvValidationResults property tested; most static state untested |
| CLogger | `Common/Logging/CLogger.cs` | STUB | `CLogger_TEST.cs` is empty class with no test methods |
| CVersionSetter | `Common/CVersionSetter.cs` | NONE | No tests |
| CCsvsInMemory | `Common/CCsvsInMemory.cs` | NONE | No tests |
| CScrubHandler | `Common/Scrubber/CXmlHandler.cs` | NONE | Zero tests for anonymization/scrubbing logic |
| CMessages | `Common/CMessages.cs` | PARTIAL | Help menu content validated in CArgsParserTEST |
| **Collection Layer** | | | |
| CCollections | `Functions/Collection/CCollections.cs` | NONE | No tests for orchestration |
| PSInvoker | `Functions/Collection/PSCollections/PSInvoker.cs` | PARTIAL | Script unblocking tests exist; actual invocation untested |
| PowerShell7Executor | `Functions/Collection/PSCollections/PowerShell7Executor.cs` | NONE | No tests |
| CScripts | `Functions/Collection/PSCollections/CScripts.cs` | NONE | No tests |
| CDbAccessor | `Functions/Collection/DB/CDbAccessor.cs` | NONE | No tests |
| CSqlExecutor | `Functions/Collection/DB/CSqlExecutor.cs` | NONE | No tests |
| CRegReader | `Functions/Collection/DB/CRegReader.cs` | NONE | No tests |
| CImpersonation | `Functions/Collection/CImpersonation.cs` | NONE | No tests |
| CImportPathResolver | `Functions/Collection/CImportPathResolver.cs` | TESTED | Good coverage with directory structures |
| CCsvValidator | `Functions/Collection/CCsvValidator.cs` | TESTED | Good coverage |
| CLogParser | `Functions/Collection/LogParser/CLogParser.cs` | NONE | No tests |
| CVmcReader | `Functions/Collection/LogParser/CVmcReader.cs` | NONE | No tests |
| CredentialHelper | `Functions/Collection/Security/CredentialHelper.cs` | TESTED | Good coverage |
| CSecurityInit | `Functions/Collection/Security/CSecurityInit.cs` | NONE | No tests |
| **CSV Handlers (Reporting)** | | | |
| CCsvParser | `Functions/Reporting/CsvHandlers/CCsvParser.cs` | TESTED | Good coverage for many parser methods |
| CCsvReader | `Functions/Reporting/CsvHandlers/CCsvReader.cs` | PARTIAL | FileFinder tested; VbrCsvReader, VboCsvReader untested |
| CComplianceCsv | `Functions/Reporting/CsvHandlers/CComplianceCsv.cs` | TESTED | Compliance parsing well covered |
| CEntraObjects | `Functions/Reporting/CsvHandlers/CEntraObjects.cs` | TESTED | Empty value handling tested |
| All other CSV types | 25+ CSV handler files | NONE | CBjobCsv, CJobCsvInfos, CServerCsvInfos, CRepoCsvInfos, CSobrCsvInfo, CProxyCsvInfos, etc. -- none have dedicated tests |
| **Data Transformation** | | | |
| CDataFormer | `Functions/Reporting/Html/CDataFormer.cs` | PARTIAL | CapacityTier/ArchiveTier methods tested; SecSummary barely tested; most methods untested |
| CDataTypesParser | `Functions/Reporting/DataTypes/CDataTypesParser.cs` | PARTIAL | DateTime parsing tested; most data type parsing untested |
| CJobTypesParser | `Functions/Reporting/Html/DataFormers/CJobTypesParser.cs` | NONE | No tests |
| CMultiRoleServer | `Functions/Reporting/Html/DataFormers/CMultiRoleServer.cs` | NONE | No tests |
| **HTML Report Generation** | | | |
| CHtmlCompiler | `Functions/Reporting/Html/VBR/CHtmlCompiler.cs` | NONE | Zero tests for VBR report compilation |
| CVb365HtmlCompiler | `Functions/Reporting/Html/VB365/CVb365HtmlCompiler.cs` | NONE | Zero tests |
| CHtmlExporter | `Functions/Reporting/Html/CHtmlExporter.cs` | TESTED | Constructor, export file creation tested |
| CHtmlFormatting | `Functions/Reporting/Html/Shared/CHtmlFormatting.cs` | NONE | No tests |
| CObjectHelpers | `Functions/Reporting/Html/Shared/CObjectHelpers.cs` | TESTED | ParseBool, ParseInt well covered |
| CBackupServerTableHelper | `Functions/Reporting/Html/CBackupServerTableHelper.cs` | TESTED | Null/empty server handling tested |
| CHtmlTables | `Functions/Reporting/Html/VBR/VbrTables/CHtmlTables.cs` | NONE | No tests for main table generator |
| CJobSessSummary | `Functions/Reporting/Html/VBR/VbrTables/Job Session Summary/CJobSessSummary.cs` | NONE | Related helpers partially tested via CJobSessSummaryTEST |
| All VBR table classes | 30+ table generator files | NONE | CSecuritySummaryTable, CComplianceTable, CRepository, CCredentialsTable, etc. |
| All VB365 classes | CM365Tables, CM365Summaries | NONE | Zero VB365 reporting tests |
| **Export Formats** | | | |
| HtmlToPdfConverter | `Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs` | NONE | Zero tests |
| HtmlToPptxConverter | `Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs` | NONE | Zero tests |

### Coverage Summary

| Category | Files | Tested | Partial | Stub | None | Coverage % |
|---|---|---|---|---|---|---|
| Startup | 9 | 3 | 3 | 1 | 2 | ~39% |
| Common | 7 | 0 | 2 | 1 | 4 | ~14% |
| Collection | 14 | 3 | 1 | 0 | 10 | ~25% |
| CSV Handlers | 31 | 3 | 1 | 0 | 27 | ~13% |
| Data Transformation | 4 | 0 | 2 | 0 | 2 | ~25% |
| HTML Reporting | 35+ | 3 | 0 | 0 | 32+ | ~9% |
| Export Formats | 2 | 0 | 0 | 0 | 2 | 0% |
| **OVERALL** | **~102** | **12** | **9** | **2** | **~79** | **~21%** |

---

## 2. Test Quality Assessment

### 2A. Placeholder Tests (Critical)

**Severity: HIGH**

The CArgsParser test file contains **17 tests** that are pure placeholders -- they call `Assert.True(true)` with a comment "Placeholder for future implementation." These tests always pass and validate nothing.

**File:** `vHC/VhcXTests/CArgsParserTEST.cs`
**Lines:** 196-302, 530-542

Affected test methods:
- `ParseAllArgs_RunSwitch_SetsGlobalFlags` (line 196)
- `ParseAllArgs_GuiSwitch_SetsGlobalFlags` (line 208)
- `ParseAllArgs_ShowFilesSwitch_SetsOpenExplorer` (line 216)
- `ParseAllArgs_ShowReportSwitch_SetsOpenHtml` (line 224)
- `ParseAllArgs_LiteSwitch_DisablesIndividualJobHtmls` (line 232)
- `ParseAllArgs_ImportSwitch_SetsImportFlag` (line 240)
- `ParseAllArgs_SecuritySwitch_SetsSecurityReportFlag` (line 248)
- `ParseAllArgs_RemoteSwitch_SetsRemoteExecFlag` (line 256)
- `ParseAllArgs_ClearCredsSwitch_SetsClearStoredCredsFlag` (line 264)
- `ParseAllArgs_PdfSwitch_SetsPdfExportFlag` (line 272)
- `ParseAllArgs_PptxSwitch_SetsPptxExportFlag` (line 280)
- `ParseAllArgs_DebugSwitch_SetsDebugFlag` (line 288)
- `ParseAllArgs_HotfixSwitch_SetsRunHfdFlag` (line 296)
- `ParseAllArgs_RemoteWithoutHost_ShouldLogWarning` (line 530)
- `ParseAllArgs_NoArgumentsProvided_ShouldLaunchGui` (line 538)

Additionally, the `ParseAllArgs_RunSwitch_SetsCGlobalsRunFullReport` test (line 98) asserts `Assert.Contains("/run", new string[] { "/run" })` which tests the array literal, not the code.

### 2B. Commented-Out Tests

**Severity: MEDIUM**

- `vHC/VhcXTests/UnitTest1.cs` -- Entire file is commented out (lines 1-11). Should be deleted.
- `vHC/VhcXTests/EntryPointTEST.cs` -- All test methods commented out (lines 12-29). Empty class with no active tests.

### 2C. Weak Assertions in Error Handling Tests

**Severity: MEDIUM**

Several tests catch exceptions and assert `Assert.True(true)`, which provides no useful signal:

- `CCsvParser_Compliance_TEST.cs:245` -- `ComplianceCsv_MalformedCsv_HandlesGracefully` catches any exception and passes
- `CCsvParser_Compliance_TEST.cs:269` -- `ComplianceCsv_MissingColumns_HandlesGracefully` same pattern
- `CArgsParserTEST.cs:402` -- `ParsePath_NullInput_ThrowsOrReturnsNull` accepts both null return and NullReferenceException

These patterns mean the tests cannot distinguish between "handled gracefully" and "threw an unexpected error."

### 2D. Shallow Combination Tests

**Severity: LOW**

The "Argument Combination Tests" region in CArgsParserTEST.cs (lines 408-458) only validates that string arrays contain expected strings. They never instantiate CArgsParser with those arguments or verify CGlobals state changes. Example:

```csharp
// This test validates the array contains "/run", not that the parser works
var args = new[] { "/run", "/days:30", "/lite" };
Assert.Contains("/run", args);
```

### 2E. Naming Convention Compliance

The project convention is `[MethodUnderTest]_[Scenario]_[ExpectedBehavior]`. Most tests follow this pattern well:
- `EscapePasswordForPowerShell_ShouldEscapeSingleQuotes` -- follows convention
- `VerifyPath_NullPath_ReturnsFalse` -- follows convention
- `CVariables_InitializationCompletes_WithoutStackOverflow` -- follows convention

Exceptions:
- `Test1` (UnitTest1.cs) -- commented out, but violates naming
- `CsvParser_DefaultConstructor_UsesGlobalPath` -- missing method prefix ("new" or "Constructor")

### 2F. Test Data Quality

**Positive findings:**
- `VbrCsvSampleGenerator.cs` provides realistic test data with proper CSV structures
- `ComplianceCsvSampleGenerator.cs` generates version-specific compliance samples (VBR v12 vs v13)
- Credential tests use realistic special characters and edge cases

**Gaps:**
- No test data for malformed CSV with encoding issues (BOM, UTF-16, mixed line endings)
- No test data for extremely large CSV files (stress testing)
- No test data for CSV files with special characters in field values (Unicode, CJK)

---

## 3. Error Handling Edge Cases

### 3A. Empty Catch Blocks (Critical)

**Severity: CRITICAL**

The codebase has **13+ empty catch blocks** that silently swallow exceptions with no logging. These are the most dangerous pattern because failures become invisible.

| File | Line | Context |
|---|---|---|
| `PSInvoker.cs` | 90 | `catch { }` -- Swallows file system errors when searching PATH |
| `PSInvoker.cs` | 374 | `catch { }` -- Swallows process kill failure |
| `CHotfixDetector.cs` | 194 | `catch (Exception) { }` -- Swallows exception during hotfix detection |
| `CHotfixDetector.cs` | 213 | `catch (Exception) { }` -- Swallows exception during directory operations |
| `CHotfixDetector.cs` | 222 | `catch (Exception) { }` -- Swallows exception during file operations |
| `CDataTypesParser.cs` | 567 | `catch (Exception) { }` -- Swallows data parsing errors |
| `CLogParser.cs` | 163 | `catch (Exception) { }` -- Swallows log parsing errors |
| `CLogParser.cs` | 258 | `catch (Exception) { }` -- Swallows log parsing errors |
| `CJobSessSummaryHelper.cs` | 249 | `catch (Exception) { }` -- Swallows session summary errors |
| `CHtmlTables.cs` | 1101 | `catch { }` -- Swallows table rendering errors |
| `CHtmlTables.cs` | 1268 | `catch { }` -- Swallows table rendering errors |
| `CHtmlTables.cs` | 1281 | `catch { }` -- Swallows table rendering errors |

### 3B. Broad Exception Catching

**Severity: HIGH**

Many methods catch `Exception` rather than specific exception types:

- `CBackupServerTableHelper.cs` lines 92, 160, 204 -- catches `Exception` for server data lookups
- `CDataTypesParser.cs` lines 95, 110, 136, 236, 378, 678, 701 -- catches `Exception` throughout data parsing
- `CDataFormer.cs` lines 115, 149, 167 -- catches `Exception` in data formation

This makes it impossible to distinguish between expected failures (file not found) and unexpected bugs (null reference, index out of range).

### 3C. Missing Null Checks on External Data

**Severity: HIGH**

- `CCsvReader.cs:43` -- `Directory.GetFiles(outpath, ...)` will throw if `outpath` is null, but the catch block returns null without distinguishing the cause
- `CDataFormer.cs:69-72` -- Constructor calls static CSV parser methods that could return null; defensive `.ToList()` is good but inner properties accessed later may still be null
- `CScrubHandler.cs:100` -- `matchDictionary.ContainsKey(item)` has a null check for `item` at line 65, but the `ScrubItem(string, ScrubItemType)` overload at line 117 passes `type.ToString()` which could technically produce unexpected results

### 3D. File Path Validation Gaps

**Severity: MEDIUM**

- `HtmlToPdfConverter.cs:42` -- `File.WriteAllBytes(outputPath, pdf)` has no validation of outputPath; no try/catch around file write
- `CScrubHandler.cs:31` -- `doc.Save(matchListPath)` saves XML to a hardcoded path derived from `CVariables.unsafeDir` with no error handling for directory-not-found
- `CScrubHandler.cs:55` -- `File.WriteAllText(filePath, jsonString)` is the only file operation with a try/catch in the scrubber

---

## 4. Code Quality Issues

### 4A. Dead Code

**Severity: LOW**

- `vHC/VhcXTests/UnitTest1.cs` -- Entirely commented out. Should be deleted.
- `vHC/VhcXTests/EntryPointTEST.cs` -- Empty class with all methods commented out.
- `vHC/HC_Reporting/Common/CGlobals.cs:79-81` -- Commented-out properties (`isConsoleLocal`, `_isRdpEnabled`, `_isDomainJoined`)
- `CCsvParser.cs:12-13` -- Commented-out using statements
- `CDataFormer.cs:23-24, 27-28, 34` -- Commented-out using statements
- `CScrubHandler.cs:76-98` -- Empty switch cases in ScrubItem that do nothing (all cases have `break` only)

### 4B. Unused Import (Minor)

- `CCsvParser.cs:10` -- `using System.Reflection.PortableExecutable;` appears unused based on the visible portion of the file

### 4C. Magic Strings and Numbers

**Severity: MEDIUM**

- `CGlobals.cs:24` -- `backupServerId = "6745a759-2205-4cd2-b172-8ec8f7e60ef8"` is a magic GUID with no documentation about what it represents or how it was determined
- `CVariables.cs:11` -- `outDir = @"C:\temp\vHC"` hardcoded default path (necessary for Windows app but should be documented)
- `HtmlToPptxConverter.cs:17-32` -- Numerous magic numbers for layout constants (these ARE named constants, which is good practice)
- `PSInvoker.cs:97` -- Fallback path `@"C:\Program Files\PowerShell\7\pwsh.exe"` hardcoded
- `PowerShell7Executor.cs:32, 60, 160` -- Same hardcoded pwsh.exe path repeated 3 times (should be a constant)

### 4D. Copy-Paste Duplication

**Severity: MEDIUM**

- `PowerShell7Executor.cs` -- The string `@"C:\Program Files\PowerShell\7\pwsh.exe"` appears at lines 32, 60, and 160. This should be extracted to a constant.
- `CredentialStoreSecurityTests.cs` -- The credential store path construction (`Path.Combine(Environment.GetFolderPath(...), "VeeamHealthCheck", "creds.json")`) is repeated in nearly every test. Should be extracted to a helper constant/method.
- `CJobSessSummaryTEST.cs` -- The save/restore pattern for CGlobals state appears in multiple test fixtures with slight variations. A shared test helper base class would reduce duplication.

### 4E. Static Mutable State (Architecture Concern)

**Severity: HIGH**

`CGlobals` is a static class with ~50+ mutable static fields that serve as global configuration. This creates:

1. **Test isolation problems** -- Tests must save/restore global state manually (seen in `CJobSessSummaryTEST.cs`, `CHtmlExporterTEST.cs`, `CBackupServerTableHelperTEST.cs`). Missing restoration causes test interference.
2. **Thread safety** -- No synchronization on any mutable static field.
3. **Testability** -- Any class that reads CGlobals is implicitly coupled to every other class that writes it.

Multiple test files demonstrate the problem with elaborate save/restore patterns in constructors and Dispose methods.

### 4F. Dispose Pattern Issues

**Severity: LOW**

- `HtmlToPdfConverter.cs:47-49` -- `Dispose()` method sets `converter = null` but the class does not implement `IDisposable`. This method is misleading.
- `CReportModeSelector.cs:25-26` -- `Dispose()` method is empty and the class does not implement `IDisposable`.
- `CClientFunctions.cs:27` -- `Dispose()` is empty but the class implements `IDisposable`.

### 4G. Typos in Production Code

**Severity: LOW**

- `CReportModeSelector.cs:67` -- "genration" should be "generation" in log message
- `CCsvParser.cs` -- Method name `GetDynamincConfigBackup` and `GetDynamincNetRules` contain typo "Dynaminc" instead of "Dynamic"

---

## 5. Cross-Platform Compatibility

### 5A. Hardcoded Windows Paths

**Severity: INFO (acceptable for Windows-only target)**

Since the project targets `net8.0-windows7.0`, Windows path assumptions are expected. However, documenting them is useful for potential future cross-platform work.

| File | Line | Hardcoded Path |
|---|---|---|
| `CVariables.cs` | 11 | `@"C:\temp\vHC"` -- Default output directory |
| `PSInvoker.cs` | 28-38 | Backslash paths in script field initializations |
| `PSInvoker.cs` | 97 | `@"C:\Program Files\PowerShell\7\pwsh.exe"` |
| `PowerShell7Executor.cs` | 32, 60, 160 | `@"C:\Program Files\PowerShell\7\pwsh.exe"` |
| `CRegReader.cs` | 82 | `@"C:\Program Files\Veeam\..."` |
| `CVmcReader.cs` | 18 | `@"C:\ProgramData\Veeam\Backup365\Logs\"` |
| `CCollections.cs` | 269, 422 | `@"C:\Program Files\PowerShell\7\pwsh.exe"` |
| `CDefaultRegOptions.cs` | 21-29 | Multiple Veeam installation paths |

### 5B. Path Separator Usage

The codebase uses `Path.Combine()` in most places (good), but also uses hardcoded backslash in:
- `PSInvoker.cs` field initializers (e.g., `@"Tools\Scripts\HealthCheck\VBR\Get-VBRConfig.ps1"`)
- `CScrubHandler.cs:15` -- `CVariables.unsafeDir + @"\vHC_KeyFile.xml"` (string concatenation instead of Path.Combine)
- `CScrubHandler.cs:44` -- `CVariables.unsafeDir + @"\vHC_KeyFile.json"`

### 5C. Windows API Dependencies

Beyond WPF (documented requirement), the code depends on:
- `System.Management.Automation` -- PowerShell SDK (Windows only)
- `Microsoft.Management.Infrastructure` -- WMI/CIM (Windows only)
- `DinkToPdf` -- Uses wkhtmltopdf native library (platform-specific)
- `System.Drawing` -- Referenced in HtmlToPdfConverter (Windows-specific in older .NET)

All of these are expected for a Windows-targeted application.

---

## 6. Prioritized Recommendations

### Critical Priority (Immediate)

1. **Fix empty catch blocks** -- Add logging to all 13 empty catch blocks. At minimum: `log.Warning("Operation X failed: " + ex.Message)`. The `PSInvoker.cs:90`, `CHtmlTables.cs:1101/1268/1281`, and `CDataTypesParser.cs:567` catches are the highest risk because they can hide data loss or silent report corruption.

2. **Delete or implement placeholder tests** -- The 17 `Assert.True(true)` tests in `CArgsParserTEST.cs` inflate the perceived test count while testing nothing. Either implement them properly or delete them and track as tech debt. Current state gives a false sense of coverage.

3. **Add tests for CReportModeSelector** -- This is the routing logic that determines which report gets generated. It has zero tests despite being a critical decision point.

### High Priority (Next Sprint)

4. **Add tests for CHtmlCompiler.RunFullVbrReport()** -- This is the core report generation method. Even a smoke test that verifies it does not throw when given valid CSV test data would be valuable.

5. **Add tests for CScrubHandler** -- The scrubbing/anonymization logic has zero tests. Given that scrubbed reports are shared with Veeam support (containing customer data), ensuring scrubbing works correctly is security-critical. Test cases needed:
   - IP addresses are replaced
   - Server names are replaced
   - The same input always maps to the same obfuscated output
   - Paths are properly anonymized

6. **Add tests for HtmlToPdfConverter and HtmlToPptxConverter** -- These export formats are user-facing features with zero test coverage. At minimum, verify they do not throw on valid HTML input and produce non-empty output files.

7. **Narrow exception catching** -- Replace `catch (Exception)` with specific exception types (`IOException`, `FileNotFoundException`, `CsvMissingFieldException`, etc.) in `CDataTypesParser.cs` and `CBackupServerTableHelper.cs`.

### Medium Priority (Next Quarter)

8. **Extract CGlobals state into injectable configuration** -- The static mutable state in CGlobals is the root cause of test isolation difficulties. Refactoring to an interface/class that can be injected would dramatically improve testability.

9. **Add CSV edge case tests** -- Missing tests for:
   - CSV files with BOM (byte order mark)
   - CSV files with mixed line endings (CRLF/LF)
   - CSV files with Unicode characters in field values
   - CSV files with extremely long field values
   - CSV files with mismatched quote delimiters

10. **Consolidate hardcoded PowerShell paths** -- Extract `@"C:\Program Files\PowerShell\7\pwsh.exe"` into a single constant (appears 5+ times across 3 files).

11. **Add VB365 report tests** -- The VB365 reporting path has zero tests for both `CVb365HtmlCompiler` and `CM365Tables`/`CM365Summaries`.

12. **Add CDataFormer method-level tests** -- `CDataFormer` contains ~30+ methods for converting CSV data to report objects. Most are untested. Priority methods to test:
    - `ProxyXml()` / `GetProxyData()`
    - `ServerXml()` / `GetServerData()`
    - `JobXml()` / `GetJobData()`
    - `RepoXml()` / `GetRepoData()`

### Low Priority (Backlog)

13. **Delete dead code** -- Remove `UnitTest1.cs`, empty `EntryPointTEST.cs`, commented-out using statements, and the empty `CLogger_TEST.cs` class.

14. **Fix typos** -- Rename `GetDynamincConfigBackup` to `GetDynamicConfigBackup` and `GetDynamincNetRules` to `GetDynamicNetRules`. Fix "genration" -> "generation" in CReportModeSelector.

15. **Add integration test for full pipeline** -- End-to-end test that feeds sample CSV data through the complete pipeline (parse -> transform -> compile -> export) and validates the output HTML contains expected sections.

16. **Implement IDisposable correctly** -- Either implement the interface properly (with `using` support) or remove the empty `Dispose()` methods from `HtmlToPdfConverter`, `CReportModeSelector`.

---

## 7. Quick Wins for Improving Test Coverage

These changes would provide the highest coverage gain for the least effort:

### Quick Win 1: Implement CArgsParser placeholder tests (~2 hours)
Convert the 14 placeholder tests in `CArgsParserTEST.cs` to real tests that:
1. Instantiate `CArgsParser` with the target switch
2. Verify `CGlobals` state changed appropriately
3. Restore CGlobals state in cleanup

### Quick Win 2: Delete dead test files (~5 minutes)
Delete `UnitTest1.cs` and `EntryPointTEST.cs` (or add `[Fact]` tests to EntryPointTEST).

### Quick Win 3: CScrubHandler basic tests (~1 hour)
Add tests for `ScrubItem()`:
- Same input produces same output
- Different inputs produce different outputs
- Null/empty input returns empty string
- Already-scrubbed items pass through unchanged

### Quick Win 4: CReportModeSelector routing test (~1 hour)
Test that `FileChecker()` routes correctly based on CGlobals state:
- When `RunSecReport = true`, security report starts
- When VBR directory exists and `RunFullReport = true`, VBR report starts
- When VB365 directory exists and `RunFullReport = true`, M365 report starts

### Quick Win 5: Add CLogger tests (~30 minutes)
The `CLogger_TEST.cs` file exists but is empty. Add basic tests:
- Logger initialization succeeds
- Info/Warning/Error methods do not throw
- Log file is created

---

## 8. Risk Assessment

| Risk Area | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Silent data loss in catch blocks | High | High | Add logging to all empty catches |
| Incorrect scrubbing leaks customer data | Medium | Critical | Add scrubber tests |
| Report routing fails silently | Medium | High | Add CReportModeSelector tests |
| PDF/PPTX export produces corrupted output | Low | Medium | Add export format tests |
| CGlobals state leaks between tests | High | Medium | Refactor to injectable config |
| CSV parsing fails on international formats | Medium | Medium | Add i18n CSV test data |

---

## Appendix: Files Examined

### Test Files (20 files read)
- `vHC/VhcXTests/UnitTest1.cs`
- `vHC/VhcXTests/CredentialHelperTests.cs`
- `vHC/VhcXTests/CredentialStoreSecurityTests.cs`
- `vHC/VhcXTests/EntryPointTEST.cs`
- `vHC/VhcXTests/CArgsParserTEST.cs`
- `vHC/VhcXTests/PSScriptUnblockingTests.cs`
- `vHC/VhcXTests/PathValidationTests.cs`
- `vHC/VhcXTests/StaticInitializationTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/CsvHandlers/CCsvParser_TEST.cs`
- `vHC/VhcXTests/Functions/Reporting/CsvHandlers/CCsvParser_Compliance_TEST.cs`
- `vHC/VhcXTests/Functions/Reporting/CsvHandlers/CEntraObjectsTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/Html/CHtmlExporterTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/Html/Shared/CObjectHelpersTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/Html/CBackupServerTableHelperTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/Html/VBR/VbrTables/CJobSessSummaryTEST.cs`
- `vHC/VhcXTests/Functions/Reporting/DataTypes/CDataTypesParserTEST.cs`
- `vHC/VhcXTests/Functions/Collection/CsvValidationTests.cs`
- `vHC/VhcXTests/Functions/Collection/CImportPathResolverTests.cs`
- `vHC/VhcXTests/Integration/CsvStructureIntegrationTests.cs`
- `vHC/VhcXTests/Integration/PSScriptIntegrationTests.cs`
- `vHC/VhcXTests/Functions/Collection/PSScripts/PowerShell51CompatibilityTests.cs`
- `vHC/VhcXTests/ContentValidation/CsvContentValidationTests.cs`
- `vHC/VhcXTests/ContentValidation/ReportContentValidationTests.cs`
- `vHC/VhcXTests/Common/Logging/CLogger_TEST.cs`

### Source Files (15+ files read)
- `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvParser.cs`
- `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/CCsvReader.cs`
- `vHC/HC_Reporting/Common/CGlobals.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/CDataFormer.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPdfConverter.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/Exportables/HtmlToPptxConverter.cs`
- `vHC/HC_Reporting/Startup/CReportModeSelector.cs`
- `vHC/HC_Reporting/Functions/Collection/PSCollections/PSInvoker.cs`
- `vHC/HC_Reporting/Functions/Reporting/Html/VBR/CHtmlCompiler.cs`
- `vHC/HC_Reporting/Common/Scrubber/CXmlHandler.cs`
- `vHC/HC_Reporting/Functions/Collection/CCollections.cs`
- `vHC/HC_Reporting/Startup/CClientFunctions.cs`

### Pattern Searches
- Empty catch blocks: 13 instances found
- `Assert.True(true)` placeholder assertions: 19 instances found
- Hardcoded Windows paths: 18+ instances cataloged
- Broad `catch (Exception)` patterns: 40+ instances found
