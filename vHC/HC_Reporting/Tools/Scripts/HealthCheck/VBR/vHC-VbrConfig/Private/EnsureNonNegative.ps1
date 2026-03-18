#Requires -Version 5.1

function EnsureNonNegative {
    <#
    .Synopsis
        Returns 0 if the input value is negative, otherwise returns the value unchanged.
    .Parameter Value
        Numeric value to clamp.
    .Outputs
        [int] The original value or 0.
    #>
    param (
        [Parameter(Mandatory)] [int] $Value
    )
    if ($Value -lt 0) { return 0 } else { return $Value }
}
