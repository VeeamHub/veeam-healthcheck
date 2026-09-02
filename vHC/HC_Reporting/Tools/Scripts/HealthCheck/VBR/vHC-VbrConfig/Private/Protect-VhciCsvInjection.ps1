#Requires -Version 5.1

function ConvertTo-VhciCsvSafeValue {
    <#
    .Synopsis
        Neutralizes a single value against spreadsheet (CSV) formula/command
        injection. Returns the value unchanged unless it is a string that a
        spreadsheet would treat as the start of a formula or command -- a leading
        '=', '+', '-', '@', TAB, or CR -- in which case a single quote is
        prefixed. Legitimate numbers (e.g. '-5', '+3.2') are preserved so report
        data is never corrupted. Non-string and empty values pass through.
    #>
    [CmdletBinding()]
    param([Parameter()] $Value)

    if ($Value -isnot [string] -or [string]::IsNullOrEmpty($Value)) {
        return $Value
    }

    $first = $Value[0]
    $isDangerous = ($first -eq '=' -or $first -eq '+' -or $first -eq '-' -or
                    $first -eq '@' -or $first -eq [char]9 -or $first -eq [char]13)
    if (-not $isDangerous) {
        return $Value
    }

    # Preserve legitimate numbers so negative/positive numerics are not corrupted
    # (e.g. change rates, deltas). Only genuinely formula-shaped strings are quoted.
    $parsed = [double]0
    if ([double]::TryParse(
            $Value,
            [System.Globalization.NumberStyles]::Any,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref] $parsed)) {
        return $Value
    }

    return "'" + $Value
}

function Protect-VhciCsvInjection {
    <#
    .Synopsis
        Neutralizes spreadsheet (CSV) formula/command injection in object fields
        before they are written to a CSV an operator may open in Excel/Sheets.
    .Description
        For each input object, returns a shallow copy in which every string-typed
        property value is passed through ConvertTo-VhciCsvSafeValue. Non-string
        values pass through unchanged. Property order is preserved so Export-Csv
        emits the same columns in the same order. Strings/primitives piped in
        directly are returned unchanged (they carry no CSV columns).
    .Parameter InputObject
        Object(s) to sanitize. Accepts pipeline input.
    #>
    [CmdletBinding()]
    param([Parameter(ValueFromPipeline)] $InputObject)

    process {
        if ($null -eq $InputObject) { return }

        # Primitives/strings have no meaningful CSV columns here; pass through.
        if ($InputObject -is [string] -or $InputObject -is [System.ValueType]) {
            return $InputObject
        }

        $safe = [ordered]@{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $safe[$prop.Name] = ConvertTo-VhciCsvSafeValue -Value $prop.Value
        }
        [pscustomobject] $safe
    }
}
