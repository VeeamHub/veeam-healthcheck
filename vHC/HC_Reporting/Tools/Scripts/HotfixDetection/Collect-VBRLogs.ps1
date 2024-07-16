#Requires -Version 4
#Requires -RunAsAdministrator
<#
.Synopsis
    Simple Veeam report to dump server & job configurations
.Notes
    Version: 0.2
    Original Author: Joe Houghes
    Butchered by: Adam Congdon
    Modified Date: 4 - 24 - 2020
.EXAMPLE
    Get - VBRConfig - VBRServer ausveeambr - ReportPath C:\Temp\VBROutput
    Get-VBRconfig -VBRSERVER localhost -ReportPath C:\HealthCheck\output
#>
param(
# VBRServer
  #[Parameter(Mandatory)]
  #[string]$VBRServer
  [Parameter(Mandatory)]
  [string]$Server,
  [Parameter(Mandatory)]
  [string]$ReportPath,
  [Parameter()]
  [DateTime]$To,
  [Parameter()]
  [DateTime]$From,
  [Parameter()]
  [int]$LastDays
  # [int]$ReportingIntervalDays = -1
)

# version get
$corePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "CorePath"
$depDLLPath = Join-Path -Path $corePath.CorePath -ChildPath "Packages\VeeamDeploymentDll.dll" -Resolve
$file = Get-Item -Path $depDLLPath
$version = $file.VersionInfo.ProductVersion


$s = Get-VBRServer -name $Server

if($version.StartsWith("12")){
    Export-VBRLogs -Server $s -FolderPath $ReportPath -LastDays 1
}
else{
    Export-VBRLogs -Server $s -FolderPath $ReportPath -From (Get-Date).AddDays(-1) -to (get-date)
}
