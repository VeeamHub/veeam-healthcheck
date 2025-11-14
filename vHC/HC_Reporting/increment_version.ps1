#!/usr/bin/env pwsh
# PowerShell script to increment AssemblyVersion and FileVersion
# Works on Windows, macOS, and Linux

$csprojPath = Join-Path $PSScriptRoot "VeeamHealthCheck.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Error "VeeamHealthCheck.csproj not found at: $csprojPath"
    exit 1
}

# Load the .csproj file as XML
[xml]$csproj = Get-Content $csprojPath

# Find the PropertyGroup with version info (the one without a Condition attribute at the top)
$versionGroup = $csproj.Project.PropertyGroup | Where-Object { $_.AssemblyVersion -and -not $_.Condition } | Select-Object -First 1

if (-not $versionGroup) {
    Write-Error "Could not find AssemblyVersion in .csproj file"
    exit 1
}

$currentAssemblyVersion = $versionGroup.AssemblyVersion
$currentFileVersion = $versionGroup.FileVersion

Write-Host "Current AssemblyVersion: $currentAssemblyVersion"
Write-Host "Current FileVersion: $currentFileVersion"

# Use AssemblyVersion as the source for increment
$versionParts = $currentAssemblyVersion.Split('.')

# Increment the last segment (build/revision)
$lastIndex = $versionParts.Length - 1
$versionParts[$lastIndex] = [int]$versionParts[$lastIndex] + 1

$newVersion = $versionParts -join '.'

# Update both AssemblyVersion and FileVersion
$versionGroup.AssemblyVersion = $newVersion
$versionGroup.FileVersion = $newVersion

# Save with proper formatting
$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.IndentChars = "`t"
$settings.NewLineChars = "`n"
$settings.Encoding = [System.Text.UTF8Encoding]::new($false) # UTF-8 without BOM

$writer = [System.Xml.XmlWriter]::Create($csprojPath, $settings)
try {
    $csproj.Save($writer)
    Write-Host "Version updated to $newVersion (AssemblyVersion and FileVersion)" -ForegroundColor Green
}
finally {
    $writer.Close()
}
