#Requires -Version 5.1

$Public  = @(Get-ChildItem -Path "$PSScriptRoot\Public\*.ps1"  -ErrorAction SilentlyContinue)
$Private = @(Get-ChildItem -Path "$PSScriptRoot\Private\*.ps1" -ErrorAction SilentlyContinue)

foreach ($import in ($Public + $Private)) {
    try {
        . $import.FullName
    } catch {
        throw "Failed to import function from $($import.FullName): $_"
    }
}

Export-ModuleMember -Function $Public.BaseName
