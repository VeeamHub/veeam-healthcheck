#Requires -Version 5.1

function ConvertTo-GB {
    <#
    .Synopsis
        Converts a value in bytes to whole gigabytes (floor).
    .Parameter InBytes
        Size value in bytes.
    .Outputs
        [int] Size in GB, rounded down.
    #>
    param (
        [Parameter(Mandatory)] $InBytes
    )
    return [math]::Floor($InBytes / 1GB)
}
