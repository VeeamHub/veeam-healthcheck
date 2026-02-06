# GitHub Actions Integration Guide

This guide explains how the CSV schema validation system integrates with GitHub Actions for automated CI/CD testing.

## Overview

The validation system automatically runs on every push or pull request that affects CSV handler code or golden baselines. It validates:

1. **CSV Schema Consistency** - CSV files match golden baseline structure
2. **Object Mapping** - CSV columns map correctly to C# properties
3. **Type Compatibility** - CSV data types compatible with C# property types

## Workflow Location

`.github/workflows/validate-csv-schemas.yml`

## Trigger Conditions

The workflow triggers automatically when:

- **Push to main/master** affecting:
  - `vHC/HC_Reporting/Functions/Reporting/CsvHandlers/**`
  - `vHC/HC_Reporting/Tools/GoldenBaselines/**`

- **Pull Request** to main/master affecting:
  - Same paths as above

- **Manual Dispatch** - You can manually run it from GitHub Actions tab

## Workflow Jobs

### Job 1: Validate CSV Schemas (Matrix)

Runs in parallel for three baseline sets:
- VBRConfig (43 CSV files)
- NasInfo (3 CSV files)
- SessionReports (1 CSV file)

**Steps:**
1. Checkout code
2. Setup PowerShell 7.4
3. Run `Compare-GoldenBaseline.ps1` with object mapping validation
4. Generate validation report (markdown)
5. Write job summary to GitHub Actions UI
6. Upload report as artifact

### Job 2: Validate Object Mappings

Validates all object schema JSON files:
- Checks for required fields (csvFile, csharpClass, columns)
- Validates C# file paths exist
- Ensures column definitions are complete
- Generates summary table

### Job 3: Summary

Combines results from all validation jobs:
- Overall pass/fail status
- Links to downloadable reports
- Fails workflow if any validation failed

## Reading Results

### In GitHub Actions UI

1. Go to **Actions** tab in your repository
2. Click on the workflow run
3. Each job shows:
   - ✅ Green check = Passed
   - ❌ Red X = Failed
   - Click job name to see details

### Job Summary

Each job writes a **markdown summary** visible at the bottom of the job:

```
## CSV Schema Validation: VBRConfig

Status: Passed
Baseline Path: vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig

| CSV File | Status | Issues |
|----------|--------|--------|
| _Jobs.csv | ✅ PASS | None |
| _Repositories.csv | ✅ PASS | None |
```

### Download Reports

Validation reports are saved as **artifacts**:

1. Scroll to bottom of workflow run page
2. Under **Artifacts** section
3. Download `validation-report-VBRConfig.md` (or other sets)

## Local Testing (Optional)

While the system is designed for GitHub Actions, you can test locally if needed:

```powershell
# Navigate to project root
cd /Users/adam/code/veeam-healthcheck

# Run validation for VBRConfig
pwsh vHC/HC_Reporting/Tools/GoldenBaselines/Compare-GoldenBaseline.ps1 \
  -ActualPath "vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig" \
  -BaselinePath "vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig" \
  -ValidateObjectMapping \
  -ObjectSchemaPath "vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas" \
  -ShowDiff

# Generate markdown report
pwsh vHC/HC_Reporting/Tools/GoldenBaselines/Compare-GoldenBaseline.ps1 \
  -ActualPath "vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig" \
  -BaselinePath "vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig" \
  -ValidateObjectMapping \
  -ObjectSchemaPath "vHC/HC_Reporting/Tools/GoldenBaselines/ObjectSchemas" \
  -OutputMarkdown > validation-report.md
```

**Note:** GitHub Actions is the primary integration point. Local testing is only for development/debugging.

## Understanding Validation Failures

### Schema Mismatch

```
::error::CSV schema validation failed for VBRConfig
Column count mismatch in _Jobs.csv: Expected 36, Got 35
```

**Fix:** Update golden baseline CSV or fix PowerShell script to match expected schema.

### Object Mapping Failure

```
::error::_Proxies.schema.json: C# file not found: Functions/Reporting/CsvHandlers/CProxyCsvInfos.cs
```

**Fix:** Ensure C# class file exists at specified path, or update schema file.

### Type Incompatibility

```
::warning::Type mismatch in _Jobs.csv column 'OnDiskGB': CSV has 'string', C# expects 'double?'
```

**Fix:** Either:
1. Update C# class to accept string type
2. Update PowerShell script to export as double
3. Update golden baseline to match actual output

## Critical Issues Found

The Algorithm agent identified **14 CSV-to-C# mapping mismatches** that need fixing:

### High Priority Fixes

1. **_Proxies.csv** - Column order completely different from CProxyCsvInfos.cs
2. **_CdpProxy.csv** - Column names and order differ significantly
3. **_HvProxy.csv** - Column order and names differ
4. **_configBackup.csv** - Column order completely different
5. **malware_*.csv** - CSV columns don't match C# properties at all

### Medium Priority Fixes

6. **_Servers.csv** - CPUCount vs CPU name difference
7. **_SOBRExtents.csv** - Typo: IsTemprorary should be IsTemporary
8. **VeeamSessionReport.csv** - Typo: CompressionRation should be CompressionRatio
9. **_vbrinfo.csv** - Missing MFA column (index 9)
10. **_ViProtected.csv** - Missing Type column in CSV

### Recommended Action

Create GitHub issues for each mismatch and fix either:
- **Option A:** Update C# classes to match CSV output (preferred)
- **Option B:** Update PowerShell scripts to match C# classes
- **Option C:** Update object schema files to document the differences

## Workflow Customization

### Manual Dispatch Options

When running manually, you can configure:

- **validate_object_mapping** - Enable/disable object mapping validation (default: true)
- **show_diff** - Show detailed differences in output (default: true)

### Adding New Baseline Sets

Edit `.github/workflows/validate-csv-schemas.yml`:

```yaml
strategy:
  matrix:
    baseline-set:
      - name: VBRConfig
        path: vHC/HC_Reporting/Tools/GoldenBaselines/VBRConfig
      - name: YourNewSet  # Add new set here
        path: vHC/HC_Reporting/Tools/GoldenBaselines/YourNewSet
```

### Changing PowerShell Version

Edit the setup-powershell step:

```yaml
- name: Setup PowerShell
  uses: actions/setup-powershell@v1
  with:
    powershell-version: '7.4'  # Change version here
```

## Next Steps

1. **Push to GitHub** - Commit all new files and push to repository
2. **Create PR** - Make a pull request affecting CSV handler code
3. **Watch Workflow** - Go to Actions tab and watch validation run
4. **Review Results** - Check job summaries and download reports
5. **Fix Issues** - Address any validation failures found

## Files Involved

| File | Purpose |
|------|---------|
| `.github/workflows/validate-csv-schemas.yml` | GitHub Actions workflow definition |
| `Tools/GoldenBaselines/Compare-GoldenBaseline.ps1` | PowerShell validation script |
| `Tools/GoldenBaselines/VBRConfig/*.csv` | Golden baseline CSV files |
| `Tools/GoldenBaselines/ObjectSchemas/*.schema.json` | C# object mapping schemas |
| `Functions/Reporting/CsvHandlers/*.cs` | C# classes for CSV parsing |

## Support

For issues or questions:
1. Check workflow logs in GitHub Actions tab
2. Review downloaded validation reports
3. Examine object schema JSON files for mapping details
4. Run validation locally for debugging

---

**Last Updated:** 2026-02-02
**Workflow Version:** 1.0.0
