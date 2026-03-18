#Requires -Version 5.1

function SafeValue {
    <#
    .Synopsis
        Returns 0 if the input is null, otherwise returns the value unchanged.
        Used to guard against null arithmetic in concurrency calculations.
    .Parameter Value
        Any value that may be null.
    .Outputs
        The original value, or 0 if null.
    #>
    param (
        $Value
    )
    if ($null -eq $Value) { return 0 } else { return $Value }
}
