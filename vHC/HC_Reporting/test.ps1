# PowerShell script to increment build number in AssemblyInfo.cs

param (
    
    [Parameter(Mandatory)]
    [string]$assemblyInfoPath# = ".\Properties\AssemblyInfo.cs"
)

if (Test-Path $assemblyInfoPath) {
    # Read the content of AssemblyInfo.cs
    $content = Get-Content $assemblyInfoPath

    # Regex to find the AssemblyVersion and AssemblyFileVersion lines
    $versionRegex = '(\[assembly: (AssemblyVersion|AssemblyFileVersion)\("\d+\.\d+\.)\d+(\.\d+"\)\])'

    $string = $content -match $versionRegex

    # use $versionRegex to get the version number from the $content
    $version = $string[0]
    # use $versionRegex to get the version number into a variable
    $version = $matches[0]

    # Increment the build number
    $updatedContent = $content -replace ($versionRegex, {
        param($match)
        # Split the version numbers
        $parts = $match.Groups[2].Value -split '\.'
        # Increment the build number
        $buildNumber = [int]$parts[2] + 1
        # Reconstruct the version line with the new build number
        return $match.Groups[1].Value + $buildNumber + $match.Groups[3].Value
    })

    # Write the updated content back to AssemblyInfo.cs
    Set-Content -Path $assemblyInfoPath -Value $updatedContent
} else {
    Write-Error "AssemblyInfo.cs not found at path: $assemblyInfoPath"
}
