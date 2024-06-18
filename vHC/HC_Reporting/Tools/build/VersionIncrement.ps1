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

    $versionParts = $string -split '\.'
    $build = $versionParts[7].replace('")', '')
    $build = $build.replace(']', '')
    $buildNumber = [int]$build
    $buildNumber += 1
    $finalVersionString = $versionParts[0] + '.' + $versionParts[1] + '.' + $versionParts[2] + '.' + $buildNumber + '")]'# + $versionParts[4] + '.' + $versionParts[5] + '.' + $versionParts[6] + '.' + $buildNumber + '")'
    $finalAssemblyString = $versionParts[4] + '.' + $versionParts[5] + '.' + $versionParts[2] + '.' + $buildNumber + '")]'
    # Increment the build number

    $updatedContent = $content -replace ('(\[assembly: AssemblyFileVersion\(")(\d+\.\d+\.\d+\.\d+)("\)\])', $finalVersionString)
    $updatedContent= $updatedContent -replace ('(\[assembly: AssemblyVersion\(")(\d+\.\d+\.\d+\.\d+)("\)\])',  $finalAssemblyString )

    # Write the updated content back to AssemblyInfo.cs
    Set-Content -Path $assemblyInfoPath -Value $updatedContent
} else {
    Write-Error "AssemblyInfo.cs not found at path: $assemblyInfoPath"
}
