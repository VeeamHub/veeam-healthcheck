# Object Schema Files

This directory contains JSON schema files that map CSV columns to their corresponding C# object properties. These schemas enable automated validation of CSV structure against the C# codebase.

## Purpose

1. **CSV-to-C# Mapping**: Document the relationship between CSV column names and C# property names
2. **Type Validation**: Verify that CSV data types are compatible with C# property types
3. **CI/CD Integration**: Enable automated schema validation in GitHub Actions
4. **Documentation**: Provide a reference for developers working with CSV handlers

## Schema Format

Each `.schema.json` file contains:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Human-readable title",
  "description": "Description of the mapping",
  "csvFile": "_filename.csv",
  "csharpClass": "Full.Namespace.ClassName",
  "csharpFile": "Relative/Path/To/File.cs",
  "version": "1.0.0",
  "vbrVersionRequired": "12.0",  // Optional: minimum VBR version
  "columns": [
    {
      "index": 0,
      "csvName": "ColumnName",
      "csharpProperty": "PropertyName",
      "csharpType": "string",
      "nullable": false,
      "expectedValues": ["Value1", "Value2"],  // Optional
      "nameMapping": "Notes about name differences",  // Optional
      "description": "Column description"
    }
  ],
  "validationRules": {
    "requiredColumns": ["Column1", "Column2"],
    "guidColumns": ["Id", "ParentId"],
    "booleanColumns": ["IsEnabled", "IsDisabled"],
    "numericColumns": ["Count", "Size"],
    "dateTimeColumns": ["CreationTime"]
  }
}
```

## Column Properties

| Property | Required | Description |
|----------|----------|-------------|
| `index` | Yes | Zero-based column position in CSV |
| `csvName` | Yes | Column header name in CSV file |
| `csharpProperty` | Yes | C# property name (may differ from csvName) |
| `csharpType` | Yes | C# type (string, bool, int, double, etc.) |
| `nullable` | Yes | Whether the column can be empty/null |
| `expectedValues` | No | Valid values for enum-like columns |
| `nameMapping` | No | Notes when CSV/C# names differ |
| `optional` | No | Whether column may not exist in some versions |
| `description` | No | Human-readable description |

## Validation Rules

| Rule | Description |
|------|-------------|
| `requiredColumns` | Columns that must be present and non-empty |
| `guidColumns` | Columns expected to contain GUIDs |
| `booleanColumns` | Columns expected to contain True/False |
| `numericColumns` | Columns expected to contain numeric values |
| `dateTimeColumns` | Columns expected to contain ISO 8601 dates |

## Available Schemas

| Schema File | CSV File | C# Class |
|-------------|----------|----------|
| `_Servers.schema.json` | `_Servers.csv` | `CServerCsvInfos` |
| `_Proxies.schema.json` | `_Proxies.csv` | `CProxyCsvInfos` |
| `_HvProxy.schema.json` | `_HvProxy.csv` | `CHvProxyCsvInfo` |
| `_CdpProxy.schema.json` | `_CdpProxy.csv` | `CCdpProxyCsvInfo` |
| `_Repositories.schema.json` | `_Repositories.csv` | `CRepoCsvInfos` |
| `_Jobs.schema.json` | `_Jobs.csv` | `CJobCsvInfos` |
| `_SOBRs.schema.json` | `_SOBRs.csv` | `CSobrCsvInfo` |
| `_configBackup.schema.json` | `_configBackup.csv` | `CConfigBackupCsv` |
| `_SecurityCompliance.schema.json` | `_SecurityCompliance.csv` | `CComplianceCsv` |
| `_entraTenants.schema.json` | `_entraTenants.csv` | `CEntraTenant` |

## Name Mapping Examples

Some CSV columns have different names than their C# properties:

| CSV Column | C# Property | Notes |
|------------|-------------|-------|
| `CPUCount` | `CPU` | Legacy naming in C# |
| `Description` | `description` | Case difference in `CCdpProxyCsvInfo` |

## Usage with Compare-GoldenBaseline.ps1

```powershell
# Validate CSV schema against golden baseline
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping

# Validate with detailed output
./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping -ShowDiff
```

## Adding New Schemas

1. Create a new `.schema.json` file following the format above
2. Ensure `csvFile` matches the baseline CSV filename
3. Ensure `csharpClass` and `csharpFile` match the actual C# implementation
4. Add all columns with correct indices
5. Update this README with the new schema

## Type Mapping

| C# Type | CSV Format | Example |
|---------|------------|---------|
| `string` | Any text | `"Server01"` |
| `bool` | True/False | `"True"` |
| `int` | Integer | `"42"` |
| `int?` | Integer or empty | `"42"` or `""` |
| `double` | Decimal | `"1024.5"` |
| `double?` | Decimal or empty | `"1024.5"` or `""` |
| `Guid` | UUID format | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| `DateTime` | ISO 8601 | `"2024-03-01T22:00:00.0000000"` |
