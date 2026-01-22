# Veeam Health Check: CI/CD Pipeline & Test Enhancement Plan

**Date**: 2026-01-21
**Goal**: 100% automated validation across all VBR versions with content verification

---

## Executive Summary

Your current CI/CD pipeline validates **tool execution** but not **content correctness**. This plan transforms your testing from "did it run?" to "is the output correct?" across all supported VBR versions.

### Current State vs Target State

| Aspect | Current | Target |
|--------|---------|--------|
| **VBR Versions Tested** | 3 (v12, v13-sql, remote) | 6 (v11, v12, v12.1, v12.2, v12.3, v13) |
| **Validation Type** | File existence | Content validation |
| **Test Coverage** | ~100 active / ~200 commented | 400+ active |
| **Code Coverage** | Unknown (not tracked) | 80%+ with reports |
| **Report Validation** | HTML file created | Data accuracy verified |
| **Regression Detection** | None | Automated comparison |

---

## Part 1: Current State Analysis

### 1.1 CI/CD Pipeline Assessment

**What Works Well:**
- ✅ Build & test on GitHub-hosted Windows runner
- ✅ 3 parallel integration tests on self-hosted runners
- ✅ Semgrep SAST scanning
- ✅ Dependency vulnerability scanning
- ✅ VirusTotal malware scanning
- ✅ SBOM generation
- ✅ Automated release creation with checksums

**Critical Gaps:**

| Gap | Impact | Priority |
|-----|--------|----------|
| No content validation | False positives (tool runs but produces garbage) | P0 |
| CodeQL disabled | Missing security vulnerability detection | P1 |
| No code coverage tracking | Unknown test quality | P1 |
| ~200 commented tests | Lost coverage for CSV, HTML, data transformation | P0 |
| No VBR v11, v12.1, v12.2, v12.3 runners | Incomplete version coverage | P1 |
| No regression comparison | Can't detect output changes | P1 |
| No performance baselines | Can't detect slowdowns | P2 |

### 1.2 Test Infrastructure Assessment

**Active Tests (~100-120):**
- Credential security: 17 tests ✅
- CLI argument parsing: 50+ tests ✅
- Path validation: 15+ tests ✅
- PowerShell script syntax: 8 tests ✅
- CSV validation framework: 9 tests ✅
- Static initialization: 3 tests ✅

**Commented/Disabled Tests (~200):**
- CSV parsing pipeline: ~50 tests ❌
- HTML export/generation: ~50 tests ❌
- Data transformation: ~50 tests ❌
- Job session summary: ~50 tests ❌

**ZERO Coverage Areas:**
- Data collection (PowerShell, SQL, Registry, WMI)
- HTML report compilation
- VB365 features
- Export formats (PDF, PPTX)
- Scrubbing/anonymization
- Database interactions

### 1.3 Report Generation Pipeline

```
Collection          Processing           Report Generation
    │                   │                      │
    ▼                   ▼                      ▼
┌─────────┐      ┌────────────┐      ┌──────────────────┐
│ 50+ CSV │  →   │ CCsvParser │  →   │ CHtmlCompiler    │
│  files  │      │ CDataFormer│      │ CHtmlBodyHelper  │
└─────────┘      │ transforms │      │ CHtmlTables      │
    ❌              ❌                      ❌
 No tests        No tests              No tests
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

| Stage | Best Practice | Your Current | Gap |
|-------|--------------|--------------|-----|
| **Build** | Artifact + checksums | ✅ Yes | None |
| **Unit Tests** | 100% pass, tracked | ⚠️ Tests run, ~200 commented | Major |
| **Code Coverage** | 80%+ with trending | ❌ Collected but not reported | Major |
| **SAST** | Multiple tools, all code | ⚠️ Semgrep only, CodeQL disabled | Medium |
| **Dependency Scan** | Block on high/critical | ✅ Fails on moderate | None |
| **Integration Tests** | Contract + behavior | ⚠️ File existence only | Major |
| **E2E Tests** | Content validation | ❌ None | Critical |
| **Regression** | Baseline comparison | ❌ None | Critical |
| **Performance** | Baseline + trending | ⚠️ Metrics extracted, not enforced | Medium |

---

## Part 3: Multi-Version VBR Testing Strategy

### 3.1 Version Matrix Requirements

| VBR Version | Status | Runner Label | Test Focus |
|-------------|--------|--------------|------------|
| v11 | Legacy | `vbr-v11` | Backward compatibility |
| v12 | Current | `vbr-v12` | Core functionality |
| v12.1 | Point release | `vbr-v12-1` | Feature additions |
| v12.2 | Point release | `vbr-v12-2` | Feature additions |
| v12.3 | Point release | `vbr-v12-3` | Feature additions |
| v13 | Latest | `vbr-v13-sql` | New features |

### 3.2 Version-Specific Test Data

Each VBR version produces different CSV schemas and features. You need **golden datasets** per version:

```
test-data/
├── vbr-v11/
│   ├── golden-csvs/           # Expected CSV output structure
│   ├── golden-report.html     # Reference report
│   └── expected-sections.json # Expected report sections
├── vbr-v12/
│   ├── golden-csvs/
│   ├── golden-report.html
│   └── expected-sections.json
├── vbr-v12-1/
│   └── ... (same structure)
├── vbr-v12-2/
│   └── ...
├── vbr-v12-3/
│   └── ...
└── vbr-v13/
    └── ...
```

### 3.3 Self-Hosted Runner Strategy

**Current**: 3 runners (CONSOLE_HOST, vbr-13-sql, vbr-v12)
**Required**: 6 runners (one per version) OR matrix strategy

**Option A: Dedicated Runners (Recommended for Production)**
- 6 Windows VMs, each with specific VBR version
- Pros: Isolation, parallel execution, no version switching
- Cons: Infrastructure cost

**Option B: Dynamic Version Switching**
- Fewer VMs with multiple VBR installations
- Use environment variable to select active version
- Pros: Lower cost
- Cons: Slower (sequential), potential conflicts

**Option C: Hybrid**
- Dedicated runners for v12 (most common) and v13 (latest)
- Single runner for legacy versions (v11, v12.1-v12.3) that switches
- Balance of cost and coverage

---

## Part 4: Content Validation Framework

### 4.1 Validation Layers

```
Layer 1: CSV Validation (Data Collection)
├── File presence (Critical/Warning/Info)
├── Schema validation (expected columns)
├── Record count thresholds
├── Data type validation
└── Cross-file consistency

Layer 2: Transformation Validation (Processing)
├── Calculation accuracy (percentages, aggregations)
├── Deduplication correctness
├── Server role assignment
├── Proxy type mapping
└── Version detection

Layer 3: Report Validation (Output)
├── Section presence
├── Navigation integrity
├── Table structure
├── Data completeness
├── Scrubbing effectiveness

Layer 4: Cross-Version Validation
├── Version-specific features present/absent
├── Schema compatibility
├── Output format consistency
└── Regression detection
```

### 4.2 CSV Content Validation Tests

Create `vHC/VhcXTests/ContentValidation/CsvContentValidationTests.cs`:

```csharp
[Trait("Category", "ContentValidation")]
public class CsvContentValidationTests
{
    [Theory]
    [InlineData("vbrinfo.csv", new[] { "Name", "Version", "SqlServer" })]
    [InlineData("Servers.csv", new[] { "Name", "Type", "ApiVersion" })]
    [InlineData("Proxies.csv", new[] { "Name", "Host", "MaxTasksCount" })]
    public void CsvFile_HasExpectedColumns(string fileName, string[] expectedColumns)
    {
        // Validate CSV schema matches expectations
    }

    [Theory]
    [InlineData("vbrinfo.csv", 1, 1)]      // Exactly 1 record
    [InlineData("LicInfo.csv", 1, 10)]     // 1-10 records
    [InlineData("Servers.csv", 1, 1000)]   // At least 1, up to 1000
    public void CsvFile_HasRecordCountInRange(string fileName, int min, int max)
    {
        // Validate record counts are reasonable
    }

    [Fact]
    public void VbrInfo_VersionFieldMatchesExpectedFormat()
    {
        // "12.0.0.1420" or "13.0.0.xxx" pattern
    }

    [Fact]
    public void Repositories_FreeSpaceDoesNotExceedTotalSpace()
    {
        // Data integrity check
    }

    [Fact]
    public void Servers_AllProxiesHaveCorrespondingServerEntry()
    {
        // Cross-file consistency
    }
}
```

### 4.3 Report Content Validation Tests

Create `vHC/VhcXTests/ContentValidation/ReportContentValidationTests.cs`:

```csharp
[Trait("Category", "ContentValidation")]
public class ReportContentValidationTests
{
    [Fact]
    public void HtmlReport_ContainsAllExpectedSections()
    {
        var expectedSections = new[] {
            "License Information",
            "Backup Server",
            "Security Summary",
            "Proxy Configuration",
            "Repository Configuration",
            "Job Summary"
        };
        // Parse HTML and verify sections exist
    }

    [Fact]
    public void HtmlReport_NavigationLinksAreValid()
    {
        // All href="#section" have corresponding id="section"
    }

    [Fact]
    public void HtmlReport_TablesHaveMatchingHeadersAndRows()
    {
        // Column count in header matches column count in body
    }

    [Fact]
    public void HtmlReport_PercentagesAreWithinValidRange()
    {
        // All percentage values are 0-100
    }

    [Fact]
    public void HtmlReport_ServerCountsAreConsistentAcrossSections()
    {
        // Server summary matches detailed server tables
    }

    [Theory]
    [InlineData("ScrubbedReport.html")]
    public void ScrubbedReport_ContainsNoIpAddresses(string reportFile)
    {
        // Regex scan for IP patterns
    }

    [Theory]
    [InlineData("ScrubbedReport.html")]
    public void ScrubbedReport_ContainsNoServerNames(string reportFile)
    {
        // Compare against original, verify anonymization
    }
}
```

### 4.4 Version-Specific Validation

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
    [InlineData("11", "SecurityCompliance.csv", false)]
    [InlineData("12", "SecurityCompliance.csv", true)]
    [InlineData("13", "SecurityCompliance.csv", true)]
    public void CsvFile_PresentBasedOnVersion(string version, string file, bool expected)
    {
        // Version-specific CSV presence
    }

    [Theory]
    [InlineData("12", 37)]   // v12 has 37 compliance checks
    [InlineData("13", 47)]   // v13 has 47 compliance checks (added Linux security)
    public void ComplianceReport_HasExpectedCheckCount(string version, int count)
    {
        // Version-specific feature counts
    }
}
```

### 4.5 Regression Detection

Create golden baseline comparison:

```csharp
[Trait("Category", "Regression")]
public class RegressionValidationTests
{
    [Theory]
    [InlineData("vbr-v12")]
    [InlineData("vbr-v13")]
    public void Report_StructureMatchesGoldenBaseline(string version)
    {
        var golden = LoadGoldenReport(version);
        var current = GenerateCurrentReport(version);

        var diff = CompareReportStructure(golden, current);

        Assert.Empty(diff.UnexpectedSections);
        Assert.Empty(diff.MissingSections);
        Assert.Empty(diff.ColumnChanges);
    }

    [Theory]
    [InlineData("vbr-v12")]
    [InlineData("vbr-v13")]
    public void Report_DataNotSignificantlyDifferent(string version)
    {
        // Allow some variance (live systems change)
        // But flag >20% change in key metrics as suspicious
    }
}
```

---

## Part 5: Enhanced CI/CD Pipeline Design

### 5.1 Proposed Pipeline Architecture

```yaml
# .github/workflows/ci-cd-enhanced.yaml

name: Enhanced CI/CD Pipeline

on:
  push:
    branches: [master, dev]
  pull_request:
    branches: [master]
  workflow_dispatch:
    inputs:
      run_all_versions:
        description: 'Run tests on all VBR versions'
        required: false
        default: 'false'

jobs:
  # ═══════════════════════════════════════════════════════════════════
  # STAGE 1: BUILD & UNIT TESTS
  # ═══════════════════════════════════════════════════════════════════
  build-and-unit-tests:
    runs-on: windows-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
      coverage: ${{ steps.coverage.outputs.coverage }}
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore vHC/HC.sln

      - name: Build
        run: dotnet build vHC/HC.sln --configuration Release --no-restore

      - name: Run Unit Tests with Coverage
        run: |
          dotnet test vHC/VhcXTests/VhcXTests.csproj \
            --configuration Release \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults \
            --logger "trx;LogFileName=unit-tests.trx"

      - name: Generate Coverage Report
        uses: danielpalme/ReportGenerator-GitHub-Action@5
        with:
          reports: './TestResults/**/coverage.cobertura.xml'
          targetdir: './CoverageReport'
          reporttypes: 'HtmlInline;Cobertura;Badges'

      - name: Publish Coverage to PR
        uses: 5monkeys/cobertura-action@v14
        if: github.event_name == 'pull_request'
        with:
          path: ./TestResults/**/coverage.cobertura.xml
          minimum_coverage: 75
          fail_below_threshold: true

      - name: Upload Coverage Artifact
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./CoverageReport

  # ═══════════════════════════════════════════════════════════════════
  # STAGE 2: STATIC ANALYSIS
  # ═══════════════════════════════════════════════════════════════════
  static-analysis:
    runs-on: ubuntu-latest
    needs: build-and-unit-tests
    steps:
      - uses: actions/checkout@v4

      - name: Semgrep SAST
        uses: returntocorp/semgrep-action@v1
        with:
          config: auto

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp

      - name: Build for CodeQL
        run: dotnet build vHC/HC.sln

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3

  # ═══════════════════════════════════════════════════════════════════
  # STAGE 3: INTEGRATION TESTS (Per VBR Version)
  # ═══════════════════════════════════════════════════════════════════
  integration-tests:
    needs: build-and-unit-tests
    if: github.event_name != 'pull_request'
    strategy:
      fail-fast: false
      matrix:
        include:
          - version: v11
            runner: vbr-v11
            features: [base]
          - version: v12
            runner: vbr-v12
            features: [base, malware]
          - version: v12.1
            runner: vbr-v12-1
            features: [base, malware, immutability]
          - version: v12.2
            runner: vbr-v12-2
            features: [base, malware, immutability]
          - version: v12.3
            runner: vbr-v12-3
            features: [base, malware, immutability, entra]
          - version: v13
            runner: vbr-v13-sql
            features: [base, malware, immutability, entra, linux-security]

    runs-on: [self-hosted, '${{ matrix.runner }}']
    steps:
      - uses: actions/checkout@v4

      - name: Download Build Artifact
        uses: actions/download-artifact@v4
        with:
          name: vhc-release

      - name: Run VHC Collection & Report
        id: run-vhc
        shell: pwsh
        run: |
          $result = ./VeeamHealthCheck.exe /mode=vbr /noopen /silent
          $reportPath = Get-ChildItem -Path "C:\temp\vHC\Reports" -Filter "*.html" |
                        Sort-Object LastWriteTime -Descending |
                        Select-Object -First 1
          echo "report_path=$($reportPath.FullName)" >> $env:GITHUB_OUTPUT

      - name: Validate CSV Collection
        shell: pwsh
        run: |
          $csvPath = "C:\temp\vHC\Original\VBR"
          $criticalFiles = @('vbrinfo.csv', 'Servers.csv', 'Proxies.csv', 'Repositories.csv', '_Jobs.csv')

          foreach ($file in $criticalFiles) {
            $filePath = Get-ChildItem -Path $csvPath -Recurse -Filter $file
            if (-not $filePath) {
              throw "Critical CSV missing: $file"
            }
            $rowCount = (Import-Csv $filePath.FullName).Count
            Write-Host "$file : $rowCount records"
            if ($rowCount -eq 0) {
              throw "Critical CSV is empty: $file"
            }
          }

      - name: Validate Report Content
        shell: pwsh
        run: |
          $report = Get-Content "${{ steps.run-vhc.outputs.report_path }}" -Raw

          # Section validation
          $requiredSections = @(
            'License Information',
            'Backup Server',
            'Security Summary',
            'Proxy Configuration',
            'Repository Configuration'
          )

          foreach ($section in $requiredSections) {
            if ($report -notmatch $section) {
              throw "Missing required section: $section"
            }
          }

          # Version-specific feature validation
          $features = '${{ join(matrix.features, ',') }}'.Split(',')

          if ($features -contains 'malware') {
            if ($report -notmatch 'Malware Detection') {
              throw "Version ${{ matrix.version }} should have Malware Detection section"
            }
          }

          if ($features -contains 'entra') {
            # Entra/Azure AD sections for v12.3+
            Write-Host "Checking Entra features for ${{ matrix.version }}"
          }

          Write-Host "✓ All content validations passed for ${{ matrix.version }}"

      - name: Validate Data Integrity
        shell: pwsh
        run: |
          # Cross-reference validation
          $csvPath = Get-ChildItem "C:\temp\vHC\Original\VBR" -Directory |
                     Sort-Object LastWriteTime -Descending |
                     Select-Object -First 1

          $servers = Import-Csv "$($csvPath.FullName)\Servers.csv"
          $proxies = Import-Csv "$($csvPath.FullName)\Proxies.csv"
          $repos = Import-Csv "$($csvPath.FullName)\Repositories.csv"

          # Validate proxy hosts exist in servers
          foreach ($proxy in $proxies) {
            $hostName = $proxy.Host
            if ($servers.Name -notcontains $hostName) {
              Write-Warning "Proxy host '$hostName' not found in Servers.csv"
            }
          }

          # Validate repository free space doesn't exceed total
          foreach ($repo in $repos) {
            if ([long]$repo.FreeSpace -gt [long]$repo.TotalSpace) {
              throw "Repository '$($repo.Name)' has FreeSpace > TotalSpace"
            }
          }

      - name: Compare Against Golden Baseline
        shell: pwsh
        run: |
          $goldenPath = "./test-data/${{ matrix.version }}/golden-report.html"
          if (Test-Path $goldenPath) {
            # Structure comparison (not exact match - data changes)
            $golden = Get-Content $goldenPath -Raw
            $current = Get-Content "${{ steps.run-vhc.outputs.report_path }}" -Raw

            # Extract and compare section headers
            $goldenSections = [regex]::Matches($golden, '<h[1-6][^>]*>([^<]+)</h[1-6]>')
            $currentSections = [regex]::Matches($current, '<h[1-6][^>]*>([^<]+)</h[1-6]>')

            $missing = $goldenSections.Groups | Where-Object {
              $currentSections.Groups.Value -notcontains $_.Value
            }

            if ($missing.Count -gt 0) {
              Write-Warning "Potential regression - missing sections: $($missing.Value -join ', ')"
            }
          }

      - name: Upload Test Artifacts
        uses: actions/upload-artifact@v4
        with:
          name: integration-${{ matrix.version }}
          path: |
            C:\temp\vHC\Reports\
            C:\temp\vHC\Original\
          retention-days: 14

  # ═══════════════════════════════════════════════════════════════════
  # STAGE 4: CONTENT VALIDATION TESTS
  # ═══════════════════════════════════════════════════════════════════
  content-validation:
    needs: integration-tests
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Download All Integration Artifacts
        uses: actions/download-artifact@v4
        with:
          pattern: integration-*
          path: ./test-artifacts

      - name: Run Content Validation Test Suite
        run: |
          dotnet test vHC/VhcXTests/VhcXTests.csproj \
            --configuration Release \
            --filter "Category=ContentValidation" \
            --logger "trx;LogFileName=content-validation.trx"
        env:
          TEST_ARTIFACTS_PATH: ./test-artifacts

      - name: Run Regression Tests
        run: |
          dotnet test vHC/VhcXTests/VhcXTests.csproj \
            --configuration Release \
            --filter "Category=Regression" \
            --logger "trx;LogFileName=regression.trx"
        env:
          GOLDEN_DATA_PATH: ./test-data

  # ═══════════════════════════════════════════════════════════════════
  # STAGE 5: SECURITY SCAN
  # ═══════════════════════════════════════════════════════════════════
  security-scan:
    needs: [content-validation]
    runs-on: windows-latest
    if: github.ref == 'refs/heads/master'
    steps:
      - name: Download Build Artifact
        uses: actions/download-artifact@v4
        with:
          name: vhc-release

      - name: VirusTotal Scan
        # ... existing VirusTotal logic ...

  # ═══════════════════════════════════════════════════════════════════
  # STAGE 6: RELEASE (only on master, all gates passed)
  # ═══════════════════════════════════════════════════════════════════
  release:
    needs: [build-and-unit-tests, static-analysis, content-validation, security-scan]
    if: github.ref == 'refs/heads/master'
    runs-on: windows-latest
    steps:
      # ... existing release logic ...
```

### 5.2 Quality Gates Summary

| Gate | Threshold | Blocks Release |
|------|-----------|----------------|
| Build | Must succeed | Yes |
| Unit Tests | 100% pass | Yes |
| Code Coverage | ≥75% (target 80%) | Yes |
| CodeQL | No high/critical | Yes |
| Semgrep | No high/critical | Yes |
| Dependencies | No moderate+ vulnerabilities | Yes |
| Integration Tests | 100% pass | Yes |
| Content Validation | 100% pass | Yes |
| Regression Tests | No unexpected changes | Yes (or warning) |
| VirusTotal | <3 detections | Yes |

---

## Part 6: Implementation Roadmap

### Phase 1: Foundation (Week 1-2)

**Goal**: Restore and expand unit test coverage

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| Un-comment and fix CSV parsing tests | P0 | 2 days | High |
| Un-comment and fix HTML export tests | P0 | 2 days | High |
| Un-comment data transformation tests | P0 | 1 day | High |
| Add code coverage reporting to CI | P1 | 0.5 days | Medium |
| Re-enable CodeQL scanning | P1 | 0.5 days | Medium |

**Deliverables**:
- ~300 active tests (vs current ~100)
- Code coverage badge on README
- CodeQL results in Security tab

### Phase 2: Content Validation (Week 2-3)

**Goal**: Validate report content, not just existence

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| Create CSV schema validation tests | P0 | 2 days | High |
| Create HTML section validation tests | P0 | 2 days | High |
| Create data integrity tests | P0 | 1 day | High |
| Create scrubbing validation tests | P1 | 1 day | Medium |
| Update integration test jobs with validation | P0 | 1 day | High |

**Deliverables**:
- ContentValidation test category with 50+ tests
- Integration tests validate content, not just file existence

### Phase 3: Version Matrix (Week 3-4)

**Goal**: Cover all VBR versions systematically

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| Deploy missing self-hosted runners | P1 | 2 days | High |
| Create golden datasets per version | P1 | 3 days | High |
| Implement version-specific validation | P1 | 2 days | High |
| Add matrix strategy to CI workflow | P1 | 1 day | High |

**Deliverables**:
- 6 self-hosted runners (or hybrid approach)
- Version-specific golden baselines
- Matrix integration tests

### Phase 4: Regression Detection (Week 4-5)

**Goal**: Automatically detect unintended changes

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| Implement report structure comparison | P1 | 2 days | High |
| Create baseline update process | P2 | 1 day | Medium |
| Add regression alerts to CI | P1 | 1 day | High |
| Document baseline maintenance | P2 | 0.5 days | Low |

**Deliverables**:
- Automated regression detection
- Clear process for updating baselines
- Alerting for unexpected changes

### Phase 5: Polish & Documentation (Week 5-6)

**Goal**: Production-ready, maintainable system

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| Add performance baseline tracking | P2 | 1 day | Medium |
| Document test architecture | P2 | 1 day | Medium |
| Create runner maintenance guide | P2 | 1 day | Medium |
| Add status badges to README | P3 | 0.5 days | Low |

**Deliverables**:
- Complete documentation
- Performance tracking (optional)
- Fully documented system

---

## Part 7: Test Data Management

### 7.1 Golden Dataset Strategy

For each VBR version, maintain:

```
test-data/
├── vbr-v12/
│   ├── golden-csvs/
│   │   ├── vbrinfo.csv          # Static reference data
│   │   ├── Servers.csv          # Anonymized server list
│   │   ├── Proxies.csv          # Proxy configuration
│   │   └── ...
│   ├── golden-report.html       # Reference HTML report
│   ├── expected-sections.json   # List of required sections
│   ├── expected-columns.json    # CSV schema definitions
│   └── version-features.json    # Feature flags for this version
```

### 7.2 Golden Data Maintenance

**When to Update Golden Data**:
1. After VBR service pack/update that changes CSV schema
2. After intentional report structure changes
3. After adding new report sections

**Update Process**:
```bash
# Generate new golden data from trusted environment
./VeeamHealthCheck.exe /mode=vbr /silent

# Copy to test-data with anonymization
./scripts/generate-golden-data.ps1 -Version v12 -SourcePath C:\temp\vHC
```

### 7.3 Test Data Anonymization

Golden data should be anonymized for repository storage:
- Server names → generic identifiers (Server01, Proxy01)
- IP addresses → 10.0.0.x range
- Paths → C:\Backup\Repository\
- Credentials → removed entirely

---

## Part 8: Self-Hosted Runner Infrastructure

### 8.1 Runner Requirements

| Runner | OS | VBR Version | SQL | Memory | Storage |
|--------|----|-----------  |-----|--------|---------|
| vbr-v11 | Windows Server 2019 | 11.0 | Local | 8 GB | 100 GB |
| vbr-v12 | Windows Server 2022 | 12.0 | Local | 8 GB | 100 GB |
| vbr-v12-1 | Windows Server 2022 | 12.1 | Local | 8 GB | 100 GB |
| vbr-v12-2 | Windows Server 2022 | 12.2 | Local | 8 GB | 100 GB |
| vbr-v12-3 | Windows Server 2022 | 12.3 | Local | 8 GB | 100 GB |
| vbr-v13-sql | Windows Server 2022 | 13.0 | Remote | 16 GB | 200 GB |

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

| Metric | Source | Alert Threshold |
|--------|--------|-----------------|
| Build success rate | GitHub Actions | <95% |
| Test pass rate | Test reports | <100% |
| Code coverage | Coverlet | <75% drop |
| Integration test duration | Job logs | >2x baseline |
| Report generation time | VHC logs | >2x baseline |
| Security findings | CodeQL/Semgrep | Any high/critical |

### 9.2 Dashboard Integration

Consider adding:
- GitHub Actions workflow status badges
- Code coverage trend graphs (Codecov or similar)
- Test result history
- Performance trend tracking

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
| Push to `dev` | Build + Unit Tests + Static Analysis |
| Push to `master` | Full pipeline including release |
| Pull Request | Build + Unit Tests + Coverage check |
| Manual dispatch | Selectable: all versions or specific |
| Weekly schedule | Dependency scan + full integration |

### Golden Data Paths

| Version | Golden CSVs | Golden Report |
|---------|-------------|---------------|
| v11 | `test-data/vbr-v11/golden-csvs/` | `test-data/vbr-v11/golden-report.html` |
| v12 | `test-data/vbr-v12/golden-csvs/` | `test-data/vbr-v12/golden-report.html` |
| v13 | `test-data/vbr-v13/golden-csvs/` | `test-data/vbr-v13/golden-report.html` |

---

## Appendix B: Cost-Benefit Analysis

### Option 1: Full Implementation (Recommended)
- **Cost**: 6 weeks, 6 VMs for runners
- **Benefit**: 100% automated validation, all versions covered
- **Risk**: Low (proven patterns)

### Option 2: Minimal Implementation
- **Cost**: 2 weeks, 3 VMs (existing)
- **Benefit**: Content validation for 3 versions only
- **Risk**: Medium (gaps in version coverage)

### Option 3: Phased Implementation
- **Cost**: 6 weeks spread over quarters, add runners incrementally
- **Benefit**: Lower upfront investment, learn and adjust
- **Risk**: Low (iterative approach)

**Recommendation**: Option 3 (Phased) - Start with Phases 1-2 immediately, add version matrix as runners become available.

---

*Document generated: 2026-01-21*
*Next review: After Phase 2 completion*
