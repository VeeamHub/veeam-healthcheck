# Golden Baseline CSV Files for CI/CD Testing

## Purpose

This directory contains golden baseline CSV files that represent the expected schema and sample data output from Veeam Health Check PowerShell collection scripts. These baselines serve multiple purposes:

1. **Schema Validation**: Verify that CSV output from scripts matches expected column names and structure
2. **CI/CD Integration**: Automated testing to catch schema changes or regressions
3. **Documentation**: Reference for understanding expected data formats
4. **Test Data**: Realistic sample data for development and testing
5. **Object Mapping Validation**: Verify CSV columns match C# object property definitions

## Directory Structure

```
GoldenBaselines/
├── README.md                    # This file
├── Compare-GoldenBaseline.ps1   # Validation script
├── ObjectSchemas/               # C# object mapping definitions
│   ├── README.md
│   ├── _Servers.schema.json
│   ├── _Proxies.schema.json
│   ├── _Repositories.schema.json
│   ├── _Jobs.schema.json
│   ├── _SOBRs.schema.json
│   ├── _HvProxy.schema.json
│   ├── _CdpProxy.schema.json
│   ├── _configBackup.schema.json
│   ├── _SecurityCompliance.schema.json
│   └── _entraTenants.schema.json
├── VBRConfig/                   # Get-VBRConfig.ps1 outputs
│   ├── _UserRoles.csv
│   ├── _Servers.csv
│   ├── _Proxies.csv
│   ├── _HvProxy.csv
│   ├── _CdpProxy.csv
│   ├── _NasProxy.csv
│   ├── _Repositories.csv
│   ├── _SOBRs.csv
│   ├── _SOBRExtents.csv
│   ├── _capTier.csv
│   ├── _Jobs.csv
│   ├── _nasBackup.csv
│   ├── _nasBCJ.csv
│   ├── _AgentBackupJob.csv
│   ├── _SureBackupJob.csv
│   ├── _TapeJobs.csv
│   ├── _pluginjobs.csv
│   ├── _cdpjobs.csv
│   ├── _configBackup.csv
│   ├── _trafficRules.csv
│   ├── _regkeys.csv
│   ├── _WanAcc.csv
│   ├── _LicInfo.csv
│   ├── _vbrinfo.csv
│   ├── _SecurityCompliance.csv  # VBR 12+ only
│   ├── _ViProtected.csv
│   ├── _ViUnprotected.csv
│   ├── _entraTenants.csv
│   ├── _entraLogJob.csv
│   └── _entraTenantJob.csv
├── NasInfo/                     # Get-NasInfo.ps1 outputs
│   ├── _NasObjectSourceStorageSize.csv
│   ├── _NasFileData.csv
│   └── _NasSharesize.csv
└── SessionReports/              # Get-VeeamSessionReport.ps1 outputs
    └── VeeamSessionReport.csv
```

## VBR Version Compatibility

| CSV File | VBR 10 | VBR 11 | VBR 12 | VBR 12.1+ | Notes |
|----------|--------|--------|--------|-----------|-------|
| _UserRoles.csv | Yes | Yes | Yes | Yes | All versions |
| _Servers.csv | Yes | Yes | Yes | Yes | All versions |
| _Proxies.csv | Yes | Yes | Yes | Yes | All versions |
| _Repositories.csv | Yes | Yes | Yes | Yes | ObjectLockEnabled column added in v12 |
| _SOBRs.csv | Yes | Yes | Yes | Yes | All versions |
| _SOBRExtents.csv | Yes | Yes | Yes | Yes | gatewayHosts, ObjectLockEnabled columns v12+ |
| _Jobs.csv | Yes | Yes | Yes | Yes | GFS columns vary by version |
| _SecurityCompliance.csv | No | No | Yes | Yes | VBR 12+ only, uses different API in v13+ |
| _vbrinfo.csv | Yes | Yes | Yes | Yes | MFA column N/A for pre-v12 |
| _entraTenants.csv | No | No | No | Yes | VBR 12.1+ (Entra ID support) |
| _entraLogJob.csv | No | No | No | Yes | VBR 12.1+ (Entra ID support) |
| _entraTenantJob.csv | No | No | No | Yes | VBR 12.1+ (Entra ID support) |

## CSV Format Specifications

### Encoding
- **Encoding**: UTF-8 with BOM (matches PowerShell `Export-Csv` default)
- **Line Endings**: CRLF (Windows standard)
- **No Type Information**: All CSVs exported with `-NoTypeInformation` flag

### Data Types

| Type | Format | Example |
|------|--------|---------|
| GUID | xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx | a1b2c3d4-e5f6-7890-abcd-ef1234567890 |
| DateTime | ISO 8601 | 2024-03-01T22:00:00.0000000 |
| Boolean | True/False | True |
| Integer | Numeric string | 14 |
| Decimal | Numeric with decimal | 512.45 |
| Size (bytes) | Large integer | 1099511627776 |
| Size (GB) | Decimal | 1024.50 |

### Sample Data Conventions

All sample data uses clearly fake but realistic values:
- Server names: VEEAM-VBR01, PROXY01, REPO-SERVER01, etc.
- IP addresses: 192.168.x.x, 10.0.0.x ranges
- GUIDs: Patterned sequences (11111111-..., 22222222-..., etc.)
- Domains: lab.local, contoso.onmicrosoft.com

## CI/CD Usage

### GitHub Actions Integration

This project includes an automated GitHub Actions workflow that validates CSV schemas on every push and pull request. The workflow:

1. Validates CSV files against golden baselines
2. Validates CSV columns against C# object schema definitions
3. Generates detailed reports as workflow artifacts
4. Fails the build if schemas don't match

See `.github/workflows/validate-csv-schemas.yml` for the workflow definition.

### Basic Schema Validation

Use the provided `Compare-GoldenBaseline.ps1` script:

```powershell
# Validate a single CSV
./Compare-GoldenBaseline.ps1 -ActualCsv "output/_Servers.csv" -BaselineCsv "GoldenBaselines/VBRConfig/_Servers.csv"

# Validate all CSVs in a directory
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/"

# Validate with strict column order checking
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -StrictOrder

# Fail on any mismatch (for CI/CD)
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -FailOnMismatch
```

### Object Mapping Validation

Validate that CSV columns match C# object property definitions:

```powershell
# Enable object mapping validation
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping

# With custom schema path
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping -ObjectSchemaPath "./ObjectSchemas"

# Show detailed differences
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping -ShowDiff

# Output markdown for GitHub Actions summaries
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping -OutputMarkdown
```

### Validation Checks

The validation script performs:

1. **Column Name Match**: All expected columns present
2. **Column Order Match**: Columns in expected sequence (optional with `-StrictOrder`)
3. **Data Type Validation**: Values match expected types where detectable
4. **Extra Column Detection**: Warns on unexpected columns (non-fatal by default)
5. **Object Mapping Validation** (optional):
   - CSV columns map to C# properties
   - Type compatibility between CSV values and C# types
   - Name mapping validation (when CSV/C# names differ)
   - Expected value validation for enum-like columns

## Object Schema Files

Object schema files in `ObjectSchemas/` define the mapping between CSV columns and C# object properties. Each schema file is a JSON document with:

```json
{
  "csvFile": "_Servers.csv",
  "csharpClass": "VeeamHealthCheck.Functions.Reporting.CsvHandlers.CServerCsvInfos",
  "csharpFile": "Functions/Reporting/CsvHandlers/CServerCsvInfos.cs",
  "columns": [
    {
      "index": 0,
      "csvName": "Info",
      "csharpProperty": "Info",
      "csharpType": "string",
      "nullable": false,
      "description": "Server information type identifier"
    }
  ],
  "validationRules": {
    "requiredColumns": ["Info", "ParentId", "Id"],
    "guidColumns": ["ParentId", "Id"],
    "booleanColumns": ["IsUnavailable"],
    "numericColumns": ["Cores", "RAM"]
  }
}
```

### Type Mapping Rules

| C# Type | CSV Format | Compatible Inferred Types |
|---------|------------|---------------------------|
| `string` | Any text | String, Integer, Decimal, Boolean, Guid, DateTime |
| `bool` | True/False | Boolean, String |
| `int`, `long` | Integer | Integer, String |
| `double`, `decimal` | Decimal | Decimal, Integer, String |
| `Guid` | UUID format | Guid, String |
| `DateTime` | ISO 8601 | DateTime, String |

### Name Mapping

Some CSV columns have different names than their C# properties:

| CSV Column | C# Property | C# Class |
|------------|-------------|----------|
| `CPUCount` | `CPU` | `CServerCsvInfos` |
| `Description` | `description` | `CCdpProxyCsvInfo` |

These mappings are documented in the schema files with the `nameMapping` field.

## Updating Baselines

When PowerShell scripts change and produce new CSV schemas:

1. **Identify Changes**: Compare new output against baseline
   ```powershell
   ./Compare-GoldenBaseline.ps1 -ActualCsv "new_output.csv" -BaselineCsv "baseline.csv" -ShowDiff
   ```

2. **Update Baseline**: If changes are intentional
   ```powershell
   # Copy header row from new output
   Get-Content "new_output.csv" -TotalCount 1 | Set-Content "baseline.csv"
   # Then add representative sample data rows
   ```

3. **Update Object Schema**: If column names or types changed
   - Edit the corresponding `.schema.json` file in `ObjectSchemas/`
   - Update column definitions to match new structure

4. **Document Version**: Update compatibility table in this README

5. **Test**: Run full validation suite to ensure changes don't break other tests
   ```powershell
   ./Compare-GoldenBaseline.ps1 -ActualPath "GoldenBaselines/VBRConfig/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping -FailOnMismatch
   ```

## Creating New Baselines

For new CSV exports added to collection scripts:

1. **Extract Schema**: Run script and capture output
2. **Create Baseline File**:
   - Copy header row exactly
   - Add 2-5 rows of realistic sample data
   - Use consistent naming patterns
3. **Add to Directory**: Place in appropriate subdirectory
4. **Create Object Schema**:
   - Create `_filename.schema.json` in `ObjectSchemas/`
   - Define all columns with types and validation rules
5. **Update Documentation**: Add to structure list and compatibility table
6. **Create Tests**: Run validation to verify everything works

## Example: Validation Run

### Successful Validation

```
========================================
 Golden Baseline CSV Validation
========================================

Object Mapping Validation: ENABLED
Schema Path: ObjectSchemas/

Baseline Path: GoldenBaselines/VBRConfig/
Actual Path:   GoldenBaselines/VBRConfig/
Files to check: 30

[PASS] _Servers.csv
  [PASS] Object Mapping: VeeamHealthCheck.Functions.Reporting.CsvHandlers.CServerCsvInfos
[PASS] _Proxies.csv
  [PASS] Object Mapping: VeeamHealthCheck.Functions.Reporting.CsvHandlers.CProxyCsvInfos
[PASS] _Repositories.csv
  [PASS] Object Mapping: VeeamHealthCheck.Functions.Reporting.CsvHandlers.CRepoCsvInfos

========================================
 Summary
========================================
Total:   30
Passed:  30
Failed:  0
Skipped: 0

Object Mapping:
  Validated: 10
  Passed:    10
  Failed:    0

All validations PASSED
```

### Failed Validation

```
========================================
 Golden Baseline CSV Validation
========================================

[FAIL] _Servers.csv
  ERROR: Missing columns: NewColumn
  [FAIL] Object Mapping: VeeamHealthCheck.Functions.Reporting.CsvHandlers.CServerCsvInfos
    ERROR: Missing required column 'NewColumn' (C# property: NewColumn)

========================================
 Summary
========================================
Total:   1
Passed:  0
Failed:  1
Skipped: 0

Validation FAILED - exiting with error code 1
```

## Troubleshooting

### Common Issues

**Encoding Mismatch**
```powershell
# Check file encoding
Get-Content file.csv -Encoding Byte | Select-Object -First 3
# BOM should be: 239 187 191 (UTF-8 with BOM)
```

**Column Order Differences**
- PowerShell `Select-Object` may reorder columns
- Use `-StrictOrder` flag only when order matters

**Missing Columns in Output**
- Check VBR version compatibility
- Some columns are conditional (e.g., based on feature enablement)

**Object Schema Not Found**
- Ensure schema file name matches: `_filename.schema.json`
- Check `ObjectSchemaPath` parameter if using custom location

**Type Mismatch Warnings**
- Some mismatches are acceptable (Integer vs String)
- Review the type compatibility matrix above

## Contributing

When modifying golden baselines:
1. Preserve realistic data patterns
2. Test with actual VBR output when possible
3. Update version compatibility information
4. Update object schema files if columns change
5. Run validation script before committing
6. Ensure GitHub Actions workflow passes
