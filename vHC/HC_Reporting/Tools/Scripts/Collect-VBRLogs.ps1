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
  [string]$ReportPath
  # [int]$ReportingIntervalDays = -1
)


# Load the Veeam PSSnapin
#if (!(Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
#  Add-PSSnapin -Name VeeamPSSnapIn
#  Connect-VBRServer -Server $VBRServer
#}
#
#else {
#  Disconnect-VBRServer
#  Connect-VBRServer -Server $VBRServer
#}
#
#if (!(Test-Path $ReportPath)) {
#  New-Item -Path $ReportPath -ItemType Directory | Out-Null
#}

Push-Location -Path $ReportPath
# COLLECTION Starting
 $servers = Get-VBRServer | Where-Object {$_.Info -notmatch "^This server.*" }
Export-VBRLogs -Server $servers -FolderPath $ReportPath -From (Get-Date).AddDays(-1) -to (get-date)