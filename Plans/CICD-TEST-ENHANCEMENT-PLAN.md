# Veeam Health Check: CI/CD Pipeline & Test Enhancement Plan

**Date**: 2026-01-21
**Last Updated**: 2026-01-26
**Goal**: 100% automated validation across all VBR versions with content verification

---

## Status Update (2026-01-26)

### Overall Progress: ~60% Complete

| Phase | Status | Completion | Notes |
|-------|--------|------------|-------|
| **Phase 1: Foundation** | ✅ Mostly Complete | 85% | 244 tests active, CodeQL enabled, coverage reporting works |
| **Phase 2: Content Validation** | ⚠️ Partial | 60% | 20 tests exist, CI has inline PowerShell validation |
| **Phase 3: Version Matrix** | ⚠️ Blocked | 30% | 3 of 6 runners exist; **no golden datasets** |
| **Phase 4: Regression Detection** | ❌ Not Started | 0% | Blocked by Phase 3 golden datasets |
| **Phase 5: Polish** | ⚠️ Partial | 25% | Performance metrics exist in CI |

### Key Accomplishments Since Plan Creation
- ✅ **244 active test methods** (was estimated at ~100)
- ✅ **Only 3 commented tests** (was estimated at ~200)
- ✅ CodeQL scanning re-enabled
- ✅ Coverage reporting via ReportGenerator
- ✅ Content validation tests created (CsvContentValidationTests, ReportContentValidationTests)
- ✅ CI pipeline implements inline CSV/HTML validation in PowerShell
- ✅ 3 integration test runners operational (v12, v13-sql, remote)

### Critical Blockers
1. **No `test-data/` directory** - Golden datasets don't exist, blocking regression detection
2. **Missing runners** - v11, v12.1, v12.2, v12.3 runners not deployed

### Recommended Next Actions
1. **Priority 1**: Create `test-data/vbr-v12/` and `test-data/vbr-v13/` golden datasets
2. **Priority 2**: Expand content validation tests from 20 to 50+
3. **Priority 3**: Implement regression comparison tests
4. **Deprioritized**: Legacy runners (v11, v12.x point releases) - focus on v12 and v13 first

---

## Executive Summary

Your current CI/CD pipeline validates **tool execution** but not **content correctness**. This plan transforms your testing from "did it run?" to "is the output correct?" across all supported VBR versions.

### Current State vs Target State

| Aspect | Original Baseline | Current (Jan 26) | Target |
|--------|-------------------|------------------|--------|
| **VBR Versions Tested** | 3 (v12, v13-sql, remote) | 3 (v12, v13-sql, remote) | 4 (v12, v13 primary; v11, v12.3 secondary) |
| **Validation Type** | File existence | File + inline content | Full content + regression |
| **Test Coverage** | ~100 active / ~200 commented | **244 active / 3 commented** | 300+ active |
| **Code Coverage** | Unknown | **Tracked via ReportGenerator** | 80%+ with reports |
| **Report Validation** | HTML file created | **Sections + data validated** | + Golden baseline comparison |
| **Regression Detection** | None | None | Automated comparison |
| **Content Validation Tests** | 0 | **20 (9 CSV + 11 HTML)** | 50+ |

---

## Part 1: Current State Analysis

### 1.1 CI/CD Pipeline Assessment

**What Works Well:**
- ✅ Build & test on GitHub-hosted Windows runner
- ✅ 3 parallel integration tests on self-hosted runners
- ✅ Semgrep SAST scanning
- ✅ **CodeQL scanning (re-enabled)**
- ✅ Dependency vulnerability scanning
- ✅ VirusTotal malware scanning
- ✅ SBOM generation
- ✅ Automated release creation with checksums
- ✅ **Coverage reporting via ReportGenerator**
- ✅ **Inline CSV content validation in integration tests**
- ✅ **Inline HTML section validation in integration tests**

**Remaining Gaps:**

| Gap | Impact | Priority | Status |
|-----|--------|----------|--------|
| No golden datasets | Can't do regression comparison | P0 | ❌ Not started |
| Content validation tests incomplete | Only 20 of 50+ target | P1 | ⚠️ In progress |
| No VBR v11, v12.1, v12.2, v12.3 runners | Incomplete version coverage | P2 | ❌ Deprioritized |
| No regression comparison | Can't detect output changes | P1 | ❌ Blocked |
| No performance baselines | Can't detect slowdowns | P2 | ⚠️ Metrics exist, not enforced |

### 1.2 Test Infrastructure Assessment

**Active Tests (244 total):**
- Credential security: 17 tests ✅
- CLI argument parsing: 50+ tests ✅
- Path validation: 15+ tests ✅
- PowerShell script syntax: 8 tests ✅
- CSV validation framework: 9 tests ✅
- Static initialization: 3 tests ✅
- **CSV content validation: 9 tests ✅** (NEW)
- **HTML report validation: 11 tests ✅** (NEW)
- Integration tests: 15+ tests ✅
- Compliance CSV parsing: 10+ tests ✅

**Commented/Disabled Tests (3 only):**
- EntryPointTEST.cs: 2 tests
- UnitTest1.cs: 1 test

**Areas Still Needing Coverage:**
- Data collection (PowerShell, SQL, Registry, WMI)
- VB365 features
- Export formats (PDF, PPTX)
- Version-specific feature validation
- Regression baseline comparison

### 1.3 Report Generation Pipeline

```
Collection          Processing           Report Generation
    │                   │                      │
    ▼                   ▼                      ▼
┌─────────┐      ┌────────────┐      ┌──────────────────┐
│ 50+ CSV │  →   │ CCsvParser │  →   │ CHtmlCompiler    │
│  files  │      │ CDataFormer│      │ CHtmlBodyHelper  │
└─────────┘      │ transforms │      │ CHtmlTables      │
    ⚠️              ⚠️                      ⚠️
 Schema tests   Some coverage         Section tests
 (9 tests)      exists                (11 tests)
```

---

## Part 2: Best Practices Comparison

### 2.1 Industry Standard CI/CD Pipeline

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        BEST PRACTICE PIPELINE                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐  │
│  │  Build  │ → │  Unit   │ → │ Static  │ → │ Integr. │ → │ E2E     │  │
│  │         │   │  Tests  │   │ Analysis│   │ Tests   │   │ Tests   │  │
│  └─────────┘   └─────────┘   └─────────┘   └─────────┘   └─────────┘  │
│       │             │             │             │             │        │
│       ▼             ▼             ▼             ▼             ▼        │
│  ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐  │
│  │Artifact │   │Coverage │   │Security │   │Contract │   │Content  │  │
│  │Creation │   │Report   │   │Report   │   │Tests    │   │Validate │  │
│  └─────────┘   └─────────┘   └─────────┘   └─────────┘   └─────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                    QUALITY GATES                                   │ │
│  ├───────────────────────────────────────────────────────────────────┤ │
│  │ • Build must succeed                                              │ │
│  │ • Unit tests: 100% pass                                           │ │
│  │ • Code coverage: ≥80%                                             │ │
│  │ • No new security vulnerabilities (critical/high)                 │ │
│  │ • Integration tests: 100% pass                                    │ │
│  │ • E2E content validation: 100% pass                               │ │
│  │ • No performance regression >10%                                  │ │
│  └───────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Your Pipeline vs Best Practices

| Stage | Best Practice | Current Status | Gap |
|-------|--------------|----------------|-----|
| **Build** | Artifact + checksums | ✅ Complete | None |
| **Unit Tests** | 100% pass, tracked | ✅ 244 tests, 100% pass | None |
| **Code Coverage** | 80%+ with trending | ✅ ReportGenerator | Need badge |
| **SAST** | Multiple tools, all code | ✅ Semgrep + CodeQL | None |
| **Dependency Scan** | Block on high/critical | ✅ Fails on moderate | None |
| **Integration Tests** | Contract + behavior | ⚠️ Content validated inline | Needs more tests |
| **E2E Tests** | Content validation | ⚠️ 20 tests exist | Need 50+ |
| **Regression** | Baseline comparison | ❌ No golden data | Critical |
| **Performance** | Baseline + trending | ⚠️ Metrics extracted | Not enforced |

---

## Part 3: Multi-Version VBR Testing Strategy

### 3.1 Version Matrix Requirements (Revised)

**Tier 1 - Active Development (must have):**
| VBR Version | Status | Runner Label | Test Focus |
|-------------|--------|--------------|------------|
| v12 | Current LTS | `vbr-v12` | Core functionality |
| v13 | Latest | `vbr-v13-sql` | New features |

**Tier 2 - Opportunistic (nice to have):**
| VBR Version | Status | Runner Label | Test Focus |
|-------------|--------|--------------|------------|
| v12.3 | Point release | `vbr-v12-3` | Entra ID features |
| v11 | Legacy | `vbr-v11` | Backward compatibility |

**Deprioritized (low value for effort):**
- v12.1, v12.2 - minimal feature differences from v12

### 3.2 Version-Specific Test Data

Each VBR version produces different CSV schemas and features. You need **golden datasets** per version:

```
test-data/                          # ❌ DOES NOT EXIST - CREATE THIS
├── vbr-v12/
│   ├── golden-csvs/               # Expected CSV output structure
│   │   ├── _vbrinfo.csv
│   │   ├── _Servers.csv
│   │   ├── _Proxies.csv
│   │   └── ...
│   ├── golden-report.html         # Reference report
│   ├── expected-sections.json     # Expected report sections
│   └── version-features.json      # Feature flags for this version
└── vbr-v13/
    ├── golden-csvs/
    ├── golden-report.html
    ├── expected-sections.json
    └── version-features.json
```

### 3.3 Self-Hosted Runner Strategy (Revised)

**Current**: 3 runners (CONSOLE_HOST, vbr-13-sql, vbr-v12) ✅
**Target**: 4 runners (add vbr-v12-3 for Entra testing)

**Recommended Approach: Hybrid (Option C)**
- ✅ Dedicated runners for v12 and v13 (existing)
- ⏳ Add v12.3 runner when Entra features need testing
- ❌ Deprioritize v11, v12.1, v12.2 runners

---

## Part 4: Content Validation Framework

### 4.1 Validation Layers

```
Layer 1: CSV Validation (Data Collection)     ← 9 tests exist
├── File presence (Critical/Warning/Info)
├── Schema validation (expected columns)      ✅ CsvContentValidationTests
├── Record count thresholds                   ✅ CsvContentValidationTests
├── Data type validation
└── Cross-file consistency                    ✅ CsvContentValidationTests

Layer 2: Transformation Validation (Processing)
├── Calculation accuracy (percentages, aggregations)
├── Deduplication correctness
├── Server role assignment
├── Proxy type mapping
└── Version detection

Layer 3: Report Validation (Output)           ← 11 tests exist
├── Section presence                          ✅ ReportContentValidationTests
├── Navigation integrity                      ✅ ReportContentValidationTests
├── Table structure                           ✅ ReportContentValidationTests
├── Data completeness
├── Scrubbing effectiveness                   ✅ ReportContentValidationTests

Layer 4: Cross-Version Validation             ← BLOCKED by golden data
├── Version-specific features present/absent
├── Schema compatibility
├── Output format consistency
└── Regression detection
```

### 4.2 CSV Content Validation Tests

**Status**: ✅ Implemented at `vHC/VhcXTests/ContentValidation/CsvContentValidationTests.cs`

Existing tests (9):
- `CsvFile_HasExpectedColumns` - Schema validation
- `CsvFile_HasRecordCountInRange` - Record count validation
- `VbrInfo_VersionFieldMatchesExpectedFormat` - Version format
- `Repositories_FreeSpaceDoesNotExceedTotalSpace` - Data integrity
- `Repositories_FreeSpaceIsNonNegative` - Data integrity
- `Jobs_AllJobsHaveValidType` - Enum validation
- `Servers_AllProxiesHaveCorrespondingServerEntry` - Cross-file
- `VbrInfo_ContainsExactlyOneRecord` - Cardinality
- `ComplianceFiles_PresentBasedOnVbrVersion` - Version-specific (placeholder)

**Needed additions** (to reach 25+ CSV tests):
- VB365-specific CSV schema validation
- SOBR extent validation
- Tape library CSV validation
- Cloud credential CSV validation
- More cross-file consistency checks

### 4.3 Report Content Validation Tests

**Status**: ✅ Implemented at `vHC/VhcXTests/ContentValidation/ReportContentValidationTests.cs`

Existing tests (11):
- `HtmlReport_ContainsAllExpectedSections` - Section presence
- `HtmlReport_ContainsVersionInfo` - Version presence
- `HtmlReport_NavigationLinksAreValid` - Link integrity
- `HtmlReport_NoDeadInternalLinks` - Dead link detection
- `HtmlReport_TablesHaveMatchingHeadersAndRows` - Table structure
- `HtmlReport_TablesAreNotEmpty` - Table content
- `HtmlReport_PercentagesAreWithinValidRange` - Data validation
- `HtmlReport_NoNegativeNumbers_InSizesAndCounts` - Data validation
- `ScrubbedReport_ContainsNoIpAddresses` - Scrubbing
- `ScrubbedReport_ContainsNoCredentials` - Scrubbing
- `HtmlReport_ServerCountsAreConsistentAcrossSections` - Consistency

**Needed additions** (to reach 25+ HTML tests):
- VB365 report section validation
- Export format validation (PDF, PPTX structure)
- Chart/graph data validation
- Compliance section validation
- More consistency checks between summary and detail sections

### 4.4 Version-Specific Validation

**Status**: ⚠️ Placeholder exists, needs golden data to implement properly

Create `vHC/VhcXTests/ContentValidation/VersionSpecificValidationTests.cs`:

```csharp
[Trait("Category", "ContentValidation")]
public class VersionSpecificValidationTests
{
    [Theory]
    [InlineData("11", false)]  // v11 doesn't have malware detection
    [InlineData("12", true)]   // v12+ has malware detection
    [InlineData("13", true)]
    public void Report_MalwareSection_PresentBasedOnVersion(string version, bool expected)
    {
        // Version-appropriate feature validation
    }

    [Theory]
    [InlineData("12.3", true)]  // v12.3+ has Entra ID
    [InlineData("13", true)]
    [InlineData("12", false)]
    public void Report_EntraIdSection_PresentBasedOnVersion(string version, bool expected)
    {
        // Entra ID feature validation
    }
}
```

### 4.5 Regression Detection

**Status**: ❌ Blocked - Requires golden datasets

Once `test-data/` is created:

```csharp
[Trait("Category", "Regression")]
public class RegressionValidationTests
{
    [Theory]
    [InlineData("vbr-v12")]
    [InlineData("vbr-v13")]
    public void Report_StructureMatchesGoldenBaseline(string version)
    {
        var goldenPath = $"test-data/{version}/golden-report.html";
        // Compare structure, not exact content
    }
}
```

---

## Part 5: Enhanced CI/CD Pipeline Design

### 5.1 Current Pipeline Architecture

The current `ci-cd.yaml` already implements much of the proposed design:

**Implemented:**
- ✅ Build & unit tests with coverage
- ✅ Coverage reporting via ReportGenerator
- ✅ 3 integration test jobs (v12, v13-sql, remote)
- ✅ CSV collection validation (inline PowerShell)
- ✅ CSV content quality validation (inline PowerShell)
- ✅ HTML report content validation (inline PowerShell)
- ✅ Session cache validation
- ✅ Performance metrics collection
- ✅ VirusTotal scanning
- ✅ Automated release creation

**Not Yet Implemented:**
- ❌ Golden baseline comparison step
- ❌ Regression detection step
- ❌ Category-filtered xUnit test runs in CI
- ❌ Version matrix for all VBR versions

### 5.2 Proposed Additions

Add these steps to integration test jobs:

```yaml
- name: Compare Against Golden Baseline
  if: hashFiles('test-data/${{ matrix.version }}/golden-report.html') != ''
  shell: pwsh
  run: |
    $goldenPath = "./test-data/${{ matrix.version }}/golden-report.html"
    $currentPath = "${{ steps.find_report.outputs.report_path }}"

    # Extract and compare section headers
    $golden = Get-Content $goldenPath -Raw
    $current = Get-Content $currentPath -Raw

    $goldenSections = [regex]::Matches($golden, '<h[1-6][^>]*>([^<]+)</h[1-6]>')
    $currentSections = [regex]::Matches($current, '<h[1-6][^>]*>([^<]+)</h[1-6]>')

    $missing = $goldenSections | Where-Object {
      $currentSections.Groups.Value -notcontains $_.Groups[1].Value
    }

    if ($missing.Count -gt 0) {
      Write-Error "Regression detected - missing sections: $($missing | ForEach-Object { $_.Groups[1].Value } | Join-String -Separator ', ')"
      exit 1
    }

    Write-Host "✅ Report structure matches golden baseline"
```

### 5.3 Quality Gates Summary

| Gate | Threshold | Current Status | Blocks Release |
|------|-----------|----------------|----------------|
| Build | Must succeed | ✅ Implemented | Yes |
| Unit Tests | 100% pass | ✅ Implemented | Yes |
| Code Coverage | ≥75% (target 80%) | ✅ Reported | No (warning only) |
| CodeQL | No high/critical | ✅ Implemented | Yes |
| Semgrep | No high/critical | ✅ Implemented | Yes |
| Dependencies | No moderate+ vulnerabilities | ✅ Implemented | Yes |
| Integration Tests | 100% pass | ✅ Implemented | Yes |
| CSV Validation | Content checks pass | ✅ Inline PowerShell | Yes |
| HTML Validation | Section checks pass | ✅ Inline PowerShell | Yes |
| Regression Tests | No unexpected changes | ❌ Not implemented | Planned |
| VirusTotal | <3 detections | ✅ Implemented | Yes |

---

## Part 6: Implementation Roadmap (Revised)

### Phase 1: Foundation ✅ MOSTLY COMPLETE

**Goal**: Restore and expand unit test coverage

| Task | Priority | Status | Notes |
|------|----------|--------|-------|
| Un-comment and fix tests | P0 | ✅ Done | Only 3 remain commented |
| Add code coverage reporting to CI | P1 | ✅ Done | ReportGenerator |
| Re-enable CodeQL scanning | P1 | ✅ Done | codeql.yml active |
| Reach 300+ tests | P1 | ⚠️ 244/300 | 56 more needed |

**Deliverables**:
- ~~~300 active tests (vs current ~100)~~ → 244 active tests (81% of target)
- ✅ Code coverage reporting
- ✅ CodeQL results in Security tab

### Phase 2: Content Validation ⚠️ IN PROGRESS

**Goal**: Validate report content, not just existence

| Task | Priority | Status | Notes |
|------|----------|--------|-------|
| Create CSV schema validation tests | P0 | ✅ Done | 9 tests |
| Create HTML section validation tests | P0 | ✅ Done | 11 tests |
| Update integration tests with validation | P0 | ✅ Done | Inline PowerShell |
| Expand to 50+ content validation tests | P1 | ⚠️ 20/50 | 30 more needed |
| Add VB365-specific validation | P2 | ❌ Not started | |

**Deliverables**:
- ✅ ContentValidation test category exists
- ✅ Integration tests validate content
- ⚠️ Need 30 more tests to reach target

### Phase 3: Version Matrix & Golden Data 🚨 PRIORITY

**Goal**: Create golden datasets and enable regression detection

| Task | Priority | Status | Notes |
|------|----------|--------|-------|
| Create `test-data/vbr-v12/` golden data | P0 | ❌ Not started | **BLOCKER** |
| Create `test-data/vbr-v13/` golden data | P0 | ❌ Not started | **BLOCKER** |
| Implement version-specific validation | P1 | ⚠️ Placeholder | Needs golden data |
| Add golden baseline comparison to CI | P1 | ❌ Not started | Needs golden data |

**Revised Deliverables**:
- Focus on v12 and v13 golden data only
- Deprioritize v11, v12.1-v12.3 runners

### Phase 4: Regression Detection ❌ BLOCKED

**Goal**: Automatically detect unintended changes

| Task | Priority | Status | Notes |
|------|----------|--------|-------|
| Implement report structure comparison | P1 | ❌ Blocked | Needs Phase 3 |
| Create baseline update process | P2 | ❌ Blocked | Needs Phase 3 |
| Add regression alerts to CI | P1 | ❌ Blocked | Needs Phase 3 |

### Phase 5: Polish & Documentation ⚠️ PARTIAL

**Goal**: Production-ready, maintainable system

| Task | Priority | Status | Notes |
|------|----------|--------|-------|
| Performance metrics in CI | P2 | ✅ Done | Exists in integration tests |
| Performance baseline enforcement | P2 | ❌ Not started | |
| Document test architecture | P2 | ❌ Not started | |
| Add status badges to README | P3 | ❌ Not started | |

---

## Part 7: Test Data Management

### 7.1 Golden Dataset Strategy

**Priority action**: Create the `test-data/` directory structure:

```bash
# Create directory structure
mkdir -p test-data/vbr-v12/golden-csvs
mkdir -p test-data/vbr-v13/golden-csvs
```

For each VBR version, maintain:

```
test-data/
├── vbr-v12/
│   ├── golden-csvs/
│   │   ├── _vbrinfo.csv          # Static reference data
│   │   ├── _Servers.csv          # Anonymized server list
│   │   ├── _Proxies.csv          # Proxy configuration
│   │   ├── _Repositories.csv     # Repository data
│   │   ├── _Jobs.csv             # Job configuration
│   │   └── _LicInfo.csv          # License info
│   ├── golden-report.html        # Reference HTML report
│   ├── expected-sections.json    # List of required sections
│   ├── expected-columns.json     # CSV schema definitions
│   └── version-features.json     # Feature flags for this version
└── vbr-v13/
    └── ... (same structure)
```

### 7.2 Golden Data Generation Script

Create `scripts/generate-golden-data.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,  # "v12" or "v13"

    [Parameter(Mandatory=$true)]
    [string]$SourcePath  # Path to C:\temp\vHC output
)

$targetDir = "test-data/vbr-$Version"

# Copy and anonymize CSVs
$csvSource = Get-ChildItem "$SourcePath\Original\VBR\*\*" -Directory |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

Get-ChildItem "$($csvSource.FullName)\*.csv" | ForEach-Object {
    $content = Import-Csv $_.FullName
    # Anonymize sensitive data
    # ... anonymization logic ...
    $content | Export-Csv "$targetDir\golden-csvs\$($_.Name)" -NoTypeInformation
}

# Copy and anonymize report
$report = Get-ChildItem "$SourcePath\vHC-Report\*.html" |
          Sort-Object LastWriteTime -Descending |
          Select-Object -First 1
# ... anonymize and copy ...
```

### 7.3 Test Data Anonymization

Golden data should be anonymized for repository storage:
- Server names → generic identifiers (Server01, Proxy01)
- IP addresses → 10.0.0.x range
- Paths → C:\Backup\Repository\
- Credentials → removed entirely
- Customer-specific data → redacted

---

## Part 8: Self-Hosted Runner Infrastructure

### 8.1 Runner Requirements (Revised)

**Active Runners:**
| Runner | OS | VBR Version | SQL | Status |
|--------|----|-----------  |-----|--------|
| vbr-v12 | Windows Server 2022 | 12.0 | Local | ✅ Active |
| vbr-v13-sql | Windows Server 2022 | 13.0 | Remote | ✅ Active |
| CONSOLE_HOST | Windows Server 2022 | 13.0 | Remote | ✅ Active |

**Planned Runners:**
| Runner | OS | VBR Version | SQL | Status |
|--------|----|-----------  |-----|--------|
| vbr-v12-3 | Windows Server 2022 | 12.3 | Local | ⏳ When needed |

**Deprioritized:**
- vbr-v11 (legacy, low ROI)
- vbr-v12-1, vbr-v12-2 (minimal differences from v12)

### 8.2 Runner Security

- Dedicated service account with minimal VBR permissions
- Runners should be ephemeral (reset after each run) if possible
- Secrets stored in GitHub, not on runners
- Network isolation from production

### 8.3 Runner Maintenance

Monthly tasks:
1. Windows updates
2. GitHub Actions runner updates
3. Clear temp files (C:\temp\vHC)
4. Verify VBR services running

---

## Part 9: Metrics & Observability

### 9.1 Key Metrics to Track

| Metric | Source | Current Status | Alert Threshold |
|--------|--------|----------------|-----------------|
| Build success rate | GitHub Actions | ✅ Tracked | <95% |
| Test pass rate | Test reports | ✅ Tracked | <100% |
| Code coverage | ReportGenerator | ✅ Reported | <75% drop |
| Integration test duration | Job logs | ✅ Collected | >2x baseline |
| Report generation time | VHC logs | ✅ Collected | >2x baseline |
| Security findings | CodeQL/Semgrep | ✅ Tracked | Any high/critical |

### 9.2 Dashboard Integration

Consider adding:
- ✅ GitHub Actions workflow status (visible in repo)
- ⏳ Code coverage badge on README
- ⏳ Test result history trending
- ⏳ Performance trend tracking

---

## Appendix A: Quick Reference

### Test Categories

```bash
# Run all tests
dotnet test vHC/VhcXTests/VhcXTests.csproj

# Run by category
dotnet test --filter "Category=Security"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=ContentValidation"
dotnet test --filter "Category=Regression"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### CI/CD Workflow Triggers

| Event | What Runs |
|-------|-----------|
| Push to `dev` | Build + Unit Tests + Coverage |
| Push to `master` | Full pipeline including release |
| Pull Request | Build + Unit Tests (no integration) |
| Manual dispatch | Full pipeline |

### Golden Data Paths

| Version | Golden CSVs | Golden Report | Status |
|---------|-------------|---------------|--------|
| v12 | `test-data/vbr-v12/golden-csvs/` | `test-data/vbr-v12/golden-report.html` | ❌ Not created |
| v13 | `test-data/vbr-v13/golden-csvs/` | `test-data/vbr-v13/golden-report.html` | ❌ Not created |

---

## Appendix B: Cost-Benefit Analysis (Revised)

### Recommended Approach: Focused Implementation

Based on current progress, the recommended path forward:

1. **Immediate (This Week)**
   - Create `test-data/` directory structure
   - Generate golden datasets from existing runners (v12, v13)
   - Add 15 more content validation tests

2. **Short Term (2 Weeks)**
   - Implement regression comparison in CI
   - Reach 50+ content validation tests
   - Add coverage badge to README

3. **Medium Term (1 Month)**
   - Consider v12.3 runner for Entra testing
   - Performance baseline enforcement
   - Documentation updates

4. **Deprioritized**
   - v11 runner (legacy, low value)
   - v12.1, v12.2 runners (minimal differences)
   - Full 6-runner matrix (overkill for current needs)

---

*Document created: 2026-01-21*
*Last updated: 2026-01-26*
*Next review: After golden datasets created*
