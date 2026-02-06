<#
.SYNOPSIS
    Compares actual CSV output against golden baseline schemas for CI/CD validation.

.DESCRIPTION
    This script validates that CSV files produced by Veeam Health Check collection scripts
    match the expected schema defined in golden baseline files. It performs:
    - Column name validation (required columns present)
    - Column order validation (optional)
    - Data type inference and validation
    - Extra column detection
    - C# object mapping validation (optional)

.PARAMETER ActualCsv
    Path to a single actual CSV file to validate.

.PARAMETER BaselineCsv
    Path to the corresponding golden baseline CSV file.

.PARAMETER ActualPath
    Path to directory containing actual CSV files to validate.

.PARAMETER BaselinePath
    Path to directory containing golden baseline CSV files.

.PARAMETER StrictOrder
    When specified, validates that column order matches exactly.

.PARAMETER FailOnMismatch
    When specified, exits with error code 1 on any validation failure.

.PARAMETER ShowDiff
    When specified, shows detailed differences between actual and baseline.

.PARAMETER IgnoreExtraColumns
    When specified, does not warn about extra columns in actual output.

.PARAMETER ValidateObjectMapping
    When specified, validates CSV columns against C# object schema definitions.

.PARAMETER ObjectSchemaPath
    Path to directory containing object schema JSON files. Defaults to ObjectSchemas/ relative to script.

.PARAMETER OutputMarkdown
    When specified, outputs results in GitHub-flavored markdown for workflow summaries.

.EXAMPLE
    ./Compare-GoldenBaseline.ps1 -ActualCsv "output/_Servers.csv" -BaselineCsv "GoldenBaselines/VBRConfig/_Servers.csv"

.EXAMPLE
    ./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -FailOnMismatch

.EXAMPLE
    ./Compare-GoldenBaseline.ps1 -ActualPath "output/" -BaselinePath "GoldenBaselines/VBRConfig/" -ValidateObjectMapping

.NOTES
    Version: 2.0.0
    Author: Veeam Health Check Team
#>

[CmdletBinding(DefaultParameterSetName = 'SingleFile')]
param(
    [Parameter(ParameterSetName = 'SingleFile', Mandatory = $true)]
    [string]$ActualCsv,

    [Parameter(ParameterSetName = 'SingleFile', Mandatory = $true)]
    [string]$BaselineCsv,

    [Parameter(ParameterSetName = 'Directory', Mandatory = $true)]
    [string]$ActualPath,

    [Parameter(ParameterSetName = 'Directory', Mandatory = $true)]
    [string]$BaselinePath,

    [Parameter()]
    [switch]$StrictOrder,

    [Parameter()]
    [switch]$FailOnMismatch,

    [Parameter()]
    [switch]$ShowDiff,

    [Parameter()]
    [switch]$IgnoreExtraColumns,

    [Parameter()]
    [switch]$ValidateObjectMapping,

    [Parameter()]
    [string]$ObjectSchemaPath,

    [Parameter()]
    [switch]$OutputMarkdown
)

#region Helper Functions

function Get-CsvSchema {
    <#
    .SYNOPSIS
        Extracts schema information from a CSV file.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return $null
    }

    try {
        # Read first line to get headers
        $headerLine = Get-Content -Path $Path -TotalCount 1 -ErrorAction Stop

        if ([string]::IsNullOrWhiteSpace($headerLine)) {
            return $null
        }

        # Parse CSV header
        $headers = $headerLine -replace '^"', '' -replace '"$', '' -split '","'

        # Read sample data for type inference
        $data = Import-Csv -Path $Path -ErrorAction SilentlyContinue | Select-Object -First 5

        $schema = [PSCustomObject]@{
            Headers = $headers
            HeaderCount = $headers.Count
            SampleData = $data
            RowCount = if ($data) { $data.Count } else { 0 }
            InferredTypes = @{}
        }

        # Infer types from sample data
        if ($data -and $data.Count -gt 0) {
            foreach ($header in $headers) {
                $values = $data | ForEach-Object { $_.$header } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
                $schema.InferredTypes[$header] = Get-InferredType -Values $values
            }
        }

        return $schema
    }
    catch {
        Write-Warning "Failed to read schema from $Path : $_"
        return $null
    }
}

function Get-InferredType {
    <#
    .SYNOPSIS
        Infers the data type from sample values.
    #>
    param(
        [Parameter()]
        [array]$Values
    )

    if (-not $Values -or $Values.Count -eq 0) {
        return 'Unknown'
    }

    $guidPattern = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
    $dateTimePattern = '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}'
    $boolPattern = '^(True|False)$'
    $intPattern = '^\d+$'
    $decimalPattern = '^-?\d+\.\d+$'

    $types = @()
    foreach ($value in $Values) {
        $strValue = $value.ToString()

        if ($strValue -match $guidPattern) {
            $types += 'Guid'
        }
        elseif ($strValue -match $dateTimePattern) {
            $types += 'DateTime'
        }
        elseif ($strValue -match $boolPattern) {
            $types += 'Boolean'
        }
        elseif ($strValue -match $decimalPattern) {
            $types += 'Decimal'
        }
        elseif ($strValue -match $intPattern) {
            $types += 'Integer'
        }
        else {
            $types += 'String'
        }
    }

    # Return most common type
    $grouped = $types | Group-Object | Sort-Object Count -Descending
    return $grouped[0].Name
}

function Get-ObjectSchema {
    <#
    .SYNOPSIS
        Loads object schema JSON file for a CSV.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$CsvFileName,

        [Parameter(Mandatory)]
        [string]$SchemaPath
    )

    $schemaFileName = $CsvFileName -replace '\.csv$', '.schema.json'
    $schemaFile = Join-Path -Path $SchemaPath -ChildPath $schemaFileName

    if (-not (Test-Path $schemaFile)) {
        return $null
    }

    try {
        $schemaContent = Get-Content -Path $schemaFile -Raw -ErrorAction Stop
        $schema = $schemaContent | ConvertFrom-Json -ErrorAction Stop
        return $schema
    }
    catch {
        Write-Warning "Failed to load object schema from $schemaFile : $_"
        return $null
    }
}

function Compare-ObjectMapping {
    <#
    .SYNOPSIS
        Validates CSV columns against C# object schema.
    #>
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$CsvSchema,

        [Parameter(Mandatory)]
        [PSCustomObject]$ObjectSchema
    )

    $results = [PSCustomObject]@{
        IsValid = $true
        MissingProperties = @()
        ExtraColumns = @()
        TypeMismatches = @()
        NameMappings = @()
        Warnings = @()
        Errors = @()
        CSharpClass = $ObjectSchema.csharpClass
        CSharpFile = $ObjectSchema.csharpFile
    }

    # Build lookup of expected columns from schema
    $expectedColumns = @{}
    $csharpPropertyNames = @{}
    foreach ($col in $ObjectSchema.columns) {
        $expectedColumns[$col.csvName] = $col
        $csharpPropertyNames[$col.csharpProperty] = $col
    }

    # Check for missing columns (columns in schema but not in CSV)
    foreach ($col in $ObjectSchema.columns) {
        $isOptional = $col.optional -eq $true -or $col.nullable -eq $true
        if ($col.csvName -notin $CsvSchema.Headers) {
            if (-not $isOptional) {
                $results.MissingProperties += [PSCustomObject]@{
                    CsvName = $col.csvName
                    CSharpProperty = $col.csharpProperty
                    CSharpType = $col.csharpType
                    Index = $col.index
                }
                $results.Errors += "Missing required column '$($col.csvName)' (C# property: $($col.csharpProperty))"
                $results.IsValid = $false
            }
            else {
                $results.Warnings += "Optional column '$($col.csvName)' not present in CSV"
            }
        }
    }

    # Check for extra columns (columns in CSV but not in schema)
    foreach ($header in $CsvSchema.Headers) {
        if (-not $expectedColumns.ContainsKey($header)) {
            $results.ExtraColumns += $header
            $results.Warnings += "Extra column '$header' in CSV not defined in object schema"
        }
    }

    # Validate name mappings
    foreach ($col in $ObjectSchema.columns) {
        if ($col.csvName -ne $col.csharpProperty -and $col.csvName -in $CsvSchema.Headers) {
            $results.NameMappings += [PSCustomObject]@{
                CsvName = $col.csvName
                CSharpProperty = $col.csharpProperty
                Note = if ($col.nameMapping) { $col.nameMapping } else { "Automatic mapping" }
            }
        }
    }

    # Validate type compatibility
    foreach ($header in $CsvSchema.Headers) {
        if ($expectedColumns.ContainsKey($header)) {
            $colDef = $expectedColumns[$header]
            $inferredType = $CsvSchema.InferredTypes[$header]
            $expectedType = $colDef.csharpType

            $isCompatible = Test-TypeCompatibility -InferredType $inferredType -CSharpType $expectedType

            if (-not $isCompatible) {
                $results.TypeMismatches += [PSCustomObject]@{
                    Column = $header
                    InferredType = $inferredType
                    ExpectedCSharpType = $expectedType
                }
                $results.Warnings += "Type mismatch for '$header': CSV inferred '$inferredType', C# expects '$expectedType'"
            }

            # Validate expected values if defined
            if ($colDef.expectedValues -and $CsvSchema.SampleData) {
                $values = $CsvSchema.SampleData | ForEach-Object { $_.$header } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
                foreach ($value in $values) {
                    if ($value -notin $colDef.expectedValues) {
                        $results.Warnings += "Column '$header' contains unexpected value '$value' (expected: $($colDef.expectedValues -join ', '))"
                    }
                }
            }
        }
    }

    return $results
}

function Test-TypeCompatibility {
    <#
    .SYNOPSIS
        Tests if an inferred CSV type is compatible with a C# type.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$InferredType,

        [Parameter(Mandatory)]
        [string]$CSharpType
    )

    # Normalize C# type
    $normalizedCSharp = $CSharpType -replace '\?$', '' # Remove nullable marker

    # Define compatibility matrix
    $compatibilityMap = @{
        'String' = @('string', 'String', 'object')
        'Integer' = @('string', 'String', 'int', 'Int32', 'long', 'Int64', 'double', 'Double', 'decimal', 'Decimal', 'int?', 'Int32?', 'double?')
        'Decimal' = @('string', 'String', 'double', 'Double', 'decimal', 'Decimal', 'float', 'Single', 'double?')
        'Boolean' = @('string', 'String', 'bool', 'Boolean', 'bool?')
        'Guid' = @('string', 'String', 'Guid', 'Guid?')
        'DateTime' = @('string', 'String', 'DateTime', 'DateTime?', 'DateTimeOffset', 'DateTimeOffset?')
        'Unknown' = @('string', 'String', 'object') # Unknown is always compatible with string
    }

    if ($compatibilityMap.ContainsKey($InferredType)) {
        return $normalizedCSharp -in $compatibilityMap[$InferredType]
    }

    # Default: assume string is always compatible
    return $normalizedCSharp -eq 'string' -or $normalizedCSharp -eq 'String'
}

function Compare-CsvSchemas {
    <#
    .SYNOPSIS
        Compares two CSV schemas and returns validation results.
    #>
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Actual,

        [Parameter(Mandatory)]
        [PSCustomObject]$Baseline,

        [Parameter()]
        [switch]$StrictOrder,

        [Parameter()]
        [switch]$IgnoreExtraColumns
    )

    $results = [PSCustomObject]@{
        IsValid = $true
        MissingColumns = @()
        ExtraColumns = @()
        OrderMismatch = $false
        TypeMismatches = @()
        Warnings = @()
        Errors = @()
    }

    # Check for missing columns (columns in baseline but not in actual)
    $missingColumns = $Baseline.Headers | Where-Object { $_ -notin $Actual.Headers }
    if ($missingColumns) {
        $results.MissingColumns = $missingColumns
        $results.Errors += "Missing columns: $($missingColumns -join ', ')"
        $results.IsValid = $false
    }

    # Check for extra columns (columns in actual but not in baseline)
    $extraColumns = $Actual.Headers | Where-Object { $_ -notin $Baseline.Headers }
    if ($extraColumns) {
        $results.ExtraColumns = $extraColumns
        if (-not $IgnoreExtraColumns) {
            $results.Warnings += "Extra columns found: $($extraColumns -join ', ')"
        }
    }

    # Check column order if strict mode
    if ($StrictOrder) {
        $commonColumns = $Baseline.Headers | Where-Object { $_ -in $Actual.Headers }
        $actualOrder = $Actual.Headers | Where-Object { $_ -in $commonColumns }
        $baselineOrder = $Baseline.Headers | Where-Object { $_ -in $commonColumns }

        for ($i = 0; $i -lt $commonColumns.Count; $i++) {
            if ($actualOrder[$i] -ne $baselineOrder[$i]) {
                $results.OrderMismatch = $true
                $results.Errors += "Column order mismatch at position $i : expected '$($baselineOrder[$i])', got '$($actualOrder[$i])'"
                $results.IsValid = $false
                break
            }
        }
    }

    # Check data types for common columns
    $commonColumns = $Baseline.Headers | Where-Object { $_ -in $Actual.Headers }
    foreach ($column in $commonColumns) {
        $baselineType = $Baseline.InferredTypes[$column]
        $actualType = $Actual.InferredTypes[$column]

        if ($baselineType -ne 'Unknown' -and $actualType -ne 'Unknown' -and $baselineType -ne $actualType) {
            # Some type mismatches are acceptable (e.g., Integer vs Decimal)
            $acceptableMismatches = @(
                @('Integer', 'Decimal'),
                @('Integer', 'String'),
                @('Decimal', 'String')
            )

            $isAcceptable = $false
            foreach ($pair in $acceptableMismatches) {
                if (($baselineType -in $pair -and $actualType -in $pair)) {
                    $isAcceptable = $true
                    break
                }
            }

            if (-not $isAcceptable) {
                $results.TypeMismatches += [PSCustomObject]@{
                    Column = $column
                    Expected = $baselineType
                    Actual = $actualType
                }
                $results.Warnings += "Type mismatch for column '$column': expected $baselineType, got $actualType"
            }
        }
    }

    return $results
}

function Write-ValidationResult {
    <#
    .SYNOPSIS
        Writes validation results to console with formatting.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$FileName,

        [Parameter(Mandatory)]
        [PSCustomObject]$Results,

        [Parameter()]
        [switch]$ShowDiff,

        [Parameter()]
        [PSCustomObject]$ObjectMappingResults
    )

    $statusIcon = if ($Results.IsValid) { "[PASS]" } else { "[FAIL]" }
    $statusColor = if ($Results.IsValid) { "Green" } else { "Red" }

    Write-Host "$statusIcon " -ForegroundColor $statusColor -NoNewline
    Write-Host $FileName

    if ($Results.Errors.Count -gt 0) {
        foreach ($err in $Results.Errors) {
            Write-Host "  ERROR: $err" -ForegroundColor Red
        }
    }

    if ($Results.Warnings.Count -gt 0) {
        foreach ($warning in $Results.Warnings) {
            Write-Host "  WARN:  $warning" -ForegroundColor Yellow
        }
    }

    if ($ShowDiff) {
        if ($Results.MissingColumns.Count -gt 0) {
            Write-Host "  Missing Columns:" -ForegroundColor Cyan
            foreach ($col in $Results.MissingColumns) {
                Write-Host "    - $col" -ForegroundColor Red
            }
        }
        if ($Results.ExtraColumns.Count -gt 0) {
            Write-Host "  Extra Columns:" -ForegroundColor Cyan
            foreach ($col in $Results.ExtraColumns) {
                Write-Host "    + $col" -ForegroundColor Yellow
            }
        }
        if ($Results.TypeMismatches.Count -gt 0) {
            Write-Host "  Type Mismatches:" -ForegroundColor Cyan
            foreach ($mismatch in $Results.TypeMismatches) {
                Write-Host "    ~ $($mismatch.Column): $($mismatch.Expected) -> $($mismatch.Actual)" -ForegroundColor Magenta
            }
        }
    }

    # Object mapping results
    if ($ObjectMappingResults) {
        $objStatusIcon = if ($ObjectMappingResults.IsValid) { "[PASS]" } else { "[FAIL]" }
        $objStatusColor = if ($ObjectMappingResults.IsValid) { "Green" } else { "Red" }

        Write-Host "  $objStatusIcon Object Mapping: $($ObjectMappingResults.CSharpClass)" -ForegroundColor $objStatusColor

        if ($ObjectMappingResults.NameMappings.Count -gt 0 -and $ShowDiff) {
            Write-Host "    Name Mappings:" -ForegroundColor Cyan
            foreach ($mapping in $ObjectMappingResults.NameMappings) {
                Write-Host "      $($mapping.CsvName) -> $($mapping.CSharpProperty)" -ForegroundColor DarkGray
            }
        }

        if ($ObjectMappingResults.Errors.Count -gt 0) {
            foreach ($err in $ObjectMappingResults.Errors) {
                Write-Host "    ERROR: $err" -ForegroundColor Red
            }
        }

        if ($ObjectMappingResults.Warnings.Count -gt 0 -and $ShowDiff) {
            foreach ($warning in $ObjectMappingResults.Warnings) {
                Write-Host "    WARN:  $warning" -ForegroundColor Yellow
            }
        }
    }
}

function Write-MarkdownSummary {
    <#
    .SYNOPSIS
        Outputs validation results in GitHub-flavored markdown.
    #>
    param(
        [Parameter(Mandatory)]
        [array]$ValidationResults,

        [Parameter(Mandatory)]
        [int]$TotalFiles,

        [Parameter(Mandatory)]
        [int]$PassedFiles,

        [Parameter(Mandatory)]
        [int]$FailedFiles,

        [Parameter(Mandatory)]
        [int]$SkippedFiles,

        [Parameter()]
        [array]$ObjectMappingResults
    )

    $output = @()
    $output += "# CSV Schema Validation Results"
    $output += ""
    $output += "## Summary"
    $output += ""
    $output += "| Metric | Count |"
    $output += "|--------|-------|"
    $output += "| Total Files | $TotalFiles |"
    $output += "| :white_check_mark: Passed | $PassedFiles |"
    $output += "| :x: Failed | $FailedFiles |"
    $output += "| :warning: Skipped | $SkippedFiles |"
    $output += ""

    if ($FailedFiles -gt 0) {
        $output += "## :x: Failures"
        $output += ""

        foreach ($result in $ValidationResults | Where-Object { -not $_.SchemaResult.IsValid }) {
            $output += "### $($result.FileName)"
            $output += ""

            if ($result.SchemaResult.Errors.Count -gt 0) {
                $output += "**Errors:**"
                foreach ($err in $result.SchemaResult.Errors) {
                    $output += "- $err"
                }
                $output += ""
            }

            if ($result.SchemaResult.MissingColumns.Count -gt 0) {
                $output += "**Missing Columns:** ``$($result.SchemaResult.MissingColumns -join '``, ``')``"
                $output += ""
            }
        }
    }

    if ($ObjectMappingResults -and ($ObjectMappingResults | Where-Object { -not $_.IsValid }).Count -gt 0) {
        $output += "## :x: Object Mapping Failures"
        $output += ""

        foreach ($result in $ObjectMappingResults | Where-Object { -not $_.IsValid }) {
            $output += "### $($result.CSharpClass)"
            $output += ""
            $output += "**File:** ``$($result.CSharpFile)``"
            $output += ""

            if ($result.Errors.Count -gt 0) {
                $output += "**Errors:**"
                foreach ($err in $result.Errors) {
                    $output += "- $err"
                }
                $output += ""
            }
        }
    }

    $output += "## Detailed Results"
    $output += ""
    $output += "| File | Schema | Object Mapping | Warnings |"
    $output += "|------|--------|----------------|----------|"

    foreach ($result in $ValidationResults) {
        $schemaStatus = if ($result.SchemaResult.IsValid) { ":white_check_mark:" } else { ":x:" }
        $objStatus = if ($result.ObjectMappingResult) {
            if ($result.ObjectMappingResult.IsValid) { ":white_check_mark:" } else { ":x:" }
        } else { ":grey_question:" }
        $warningCount = $result.SchemaResult.Warnings.Count
        if ($result.ObjectMappingResult) {
            $warningCount += $result.ObjectMappingResult.Warnings.Count
        }
        $output += "| $($result.FileName) | $schemaStatus | $objStatus | $warningCount |"
    }

    $output += ""

    return $output -join "`n"
}

#endregion

#region Main Execution

$totalFiles = 0
$passedFiles = 0
$failedFiles = 0
$skippedFiles = 0
$validationResults = @()
$objectMappingResultsList = @()

# Determine object schema path
if (-not $ObjectSchemaPath) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ObjectSchemaPath = Join-Path -Path $scriptDir -ChildPath "ObjectSchemas"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Golden Baseline CSV Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($ValidateObjectMapping) {
    Write-Host "Object Mapping Validation: ENABLED" -ForegroundColor Green
    Write-Host "Schema Path: $ObjectSchemaPath" -ForegroundColor DarkGray
    Write-Host ""
}

if ($PSCmdlet.ParameterSetName -eq 'SingleFile') {
    # Single file validation
    $totalFiles = 1

    if (-not (Test-Path $ActualCsv)) {
        Write-Host "[SKIP] Actual file not found: $ActualCsv" -ForegroundColor Yellow
        $skippedFiles++
    }
    elseif (-not (Test-Path $BaselineCsv)) {
        Write-Host "[SKIP] Baseline file not found: $BaselineCsv" -ForegroundColor Yellow
        $skippedFiles++
    }
    else {
        $actualSchema = Get-CsvSchema -Path $ActualCsv
        $baselineSchema = Get-CsvSchema -Path $BaselineCsv

        if ($actualSchema -and $baselineSchema) {
            $results = Compare-CsvSchemas -Actual $actualSchema -Baseline $baselineSchema -StrictOrder:$StrictOrder -IgnoreExtraColumns:$IgnoreExtraColumns

            $objectMappingResult = $null
            if ($ValidateObjectMapping) {
                $csvFileName = Split-Path $BaselineCsv -Leaf
                $objectSchema = Get-ObjectSchema -CsvFileName $csvFileName -SchemaPath $ObjectSchemaPath
                if ($objectSchema) {
                    $objectMappingResult = Compare-ObjectMapping -CsvSchema $actualSchema -ObjectSchema $objectSchema
                    $objectMappingResultsList += $objectMappingResult
                    if (-not $objectMappingResult.IsValid) {
                        $results.IsValid = $false
                    }
                }
            }

            Write-ValidationResult -FileName (Split-Path $ActualCsv -Leaf) -Results $results -ShowDiff:$ShowDiff -ObjectMappingResults $objectMappingResult

            $validationResults += [PSCustomObject]@{
                FileName = Split-Path $ActualCsv -Leaf
                SchemaResult = $results
                ObjectMappingResult = $objectMappingResult
            }

            if ($results.IsValid) {
                $passedFiles++
            }
            else {
                $failedFiles++
            }
        }
        else {
            Write-Host "[FAIL] Could not read schema from one or both files" -ForegroundColor Red
            $failedFiles++
        }
    }
}
else {
    # Directory validation
    if (-not (Test-Path $BaselinePath)) {
        Write-Error "Baseline path not found: $BaselinePath"
        exit 1
    }

    $baselineFiles = Get-ChildItem -Path $BaselinePath -Filter "*.csv" -Recurse
    $totalFiles = $baselineFiles.Count

    Write-Host "Baseline Path: $BaselinePath"
    Write-Host "Actual Path:   $ActualPath"
    Write-Host "Files to check: $totalFiles"
    Write-Host ""

    foreach ($baselineFile in $baselineFiles) {
        # Determine relative path from baseline root
        $relativePath = $baselineFile.FullName.Substring($BaselinePath.Length).TrimStart('\', '/')
        $actualFile = Join-Path -Path $ActualPath -ChildPath $relativePath

        # Also check for file directly in actual path (flat structure)
        if (-not (Test-Path $actualFile)) {
            $actualFile = Join-Path -Path $ActualPath -ChildPath $baselineFile.Name
        }

        if (-not (Test-Path $actualFile)) {
            Write-Host "[SKIP] " -ForegroundColor Yellow -NoNewline
            Write-Host "$relativePath (actual file not found)"
            $skippedFiles++
            continue
        }

        $actualSchema = Get-CsvSchema -Path $actualFile
        $baselineSchema = Get-CsvSchema -Path $baselineFile.FullName

        if ($actualSchema -and $baselineSchema) {
            $results = Compare-CsvSchemas -Actual $actualSchema -Baseline $baselineSchema -StrictOrder:$StrictOrder -IgnoreExtraColumns:$IgnoreExtraColumns

            $objectMappingResult = $null
            if ($ValidateObjectMapping) {
                $objectSchema = Get-ObjectSchema -CsvFileName $baselineFile.Name -SchemaPath $ObjectSchemaPath
                if ($objectSchema) {
                    $objectMappingResult = Compare-ObjectMapping -CsvSchema $actualSchema -ObjectSchema $objectSchema
                    $objectMappingResultsList += $objectMappingResult
                    if (-not $objectMappingResult.IsValid) {
                        $results.IsValid = $false
                    }
                }
            }

            Write-ValidationResult -FileName $relativePath -Results $results -ShowDiff:$ShowDiff -ObjectMappingResults $objectMappingResult

            $validationResults += [PSCustomObject]@{
                FileName = $relativePath
                SchemaResult = $results
                ObjectMappingResult = $objectMappingResult
            }

            if ($results.IsValid) {
                $passedFiles++
            }
            else {
                $failedFiles++
            }
        }
        else {
            Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
            Write-Host "$relativePath (could not read schema)"
            $failedFiles++

            $validationResults += [PSCustomObject]@{
                FileName = $relativePath
                SchemaResult = [PSCustomObject]@{
                    IsValid = $false
                    Errors = @("Could not read schema")
                    Warnings = @()
                }
                ObjectMappingResult = $null
            }
        }
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total:   $totalFiles"
Write-Host "Passed:  $passedFiles" -ForegroundColor Green
Write-Host "Failed:  $failedFiles" -ForegroundColor $(if ($failedFiles -gt 0) { "Red" } else { "Green" })
Write-Host "Skipped: $skippedFiles" -ForegroundColor $(if ($skippedFiles -gt 0) { "Yellow" } else { "Green" })
Write-Host ""

if ($ValidateObjectMapping -and $objectMappingResultsList.Count -gt 0) {
    $objMappingPassed = ($objectMappingResultsList | Where-Object { $_.IsValid }).Count
    $objMappingFailed = ($objectMappingResultsList | Where-Object { -not $_.IsValid }).Count
    Write-Host "Object Mapping:" -ForegroundColor Cyan
    Write-Host "  Validated: $($objectMappingResultsList.Count)"
    Write-Host "  Passed:    $objMappingPassed" -ForegroundColor Green
    Write-Host "  Failed:    $objMappingFailed" -ForegroundColor $(if ($objMappingFailed -gt 0) { "Red" } else { "Green" })
    Write-Host ""
}

# Output markdown if requested
if ($OutputMarkdown) {
    $markdownContent = Write-MarkdownSummary -ValidationResults $validationResults -TotalFiles $totalFiles -PassedFiles $passedFiles -FailedFiles $failedFiles -SkippedFiles $skippedFiles -ObjectMappingResults $objectMappingResultsList
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Markdown Output" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $markdownContent
}

# Exit with appropriate code
if ($FailOnMismatch -and $failedFiles -gt 0) {
    Write-Host "Validation FAILED - exiting with error code 1" -ForegroundColor Red
    exit 1
}
elseif ($failedFiles -eq 0 -and $skippedFiles -eq 0) {
    Write-Host "All validations PASSED" -ForegroundColor Green
    exit 0
}
elseif ($failedFiles -eq 0) {
    Write-Host "Validations completed with skipped files" -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "Validations completed with failures" -ForegroundColor Yellow
    exit 0
}

#endregion
