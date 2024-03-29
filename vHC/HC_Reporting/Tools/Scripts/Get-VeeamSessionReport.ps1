#Requires -Version 4
#Requires -RunAsAdministrator
<#
.Synopsis
  Simple Veeam report to give details of task sessions for VMware backup jobs
.Notes
  Version: 1.0
  Author: Joe Houghes
  Modified Date: 4-21-20
.EXAMPLE
  Get-VeeamSessionReport | Format-Table
.EXAMPLE
  Get-VeeamSessionReport -VBRServer ausveeambr | Export-Csv D:\Temp\VeeamSessionReport.csv -NoTypeInformation
  .EXAMPLE
  Get-VeeamSessionReport -VBRServer ausveeambr -RemoveDuplicates | Export-Csv D:\Temp\VeeamSessionReport_NoDupes.csv -NoTypeInformation

#>
param(
  [Parameter (Mandatory)]
  [string]$VBRServer,
  [Parameter (Mandatory)]
  [string]$ReportInterval,
  [Parameter ()]
  [string[]]$JobName,
  [Parameter ()]
  [ValidateSet("Backup", "BackupCopy", "VBRServerInstall", "VBRConsoleInstall", "VBRExplorersInstall", "VEMPrereqCheck", "VEMPrereqInstall", "VEMServerInstall", "VCCPortal", "All")]
  [string]$JobType,
  [Parameter ()]
  [switch]$RemoveDuplicates
)


#Functions:
$global:SETTINGS = '{"LogLevel":"INFO","OutputPath":"C:\\temp\\vHC\\Original\\Log","ReportingIntervalDays":7,"VBOServerFqdnOrIp":"localhost"}'<#,"SkipCollect":false,"ExportJson":false,"ExportXml":false,"DebugInConsole":false,"Watch":false}#> | ConvertFrom-Json
if (Test-Path ($global:SETTINGS.OutputPath + "\CollectorConfig.json")) {
  [pscustomobject]$json = Get-Content -Path ($global:SETTINGS.OutputPath + "\CollectorConfig.json") | ConvertFrom-Json
  foreach ($property in $json.PSObject.Properties) {
    if ($null -eq $global:SETTINGS.($property.Name)) {
      $global:SETTINGS | Add-Member -MemberType NoteProperty -Name ($property.Name) -Value $json.($property.Name)
    }
    else {
      $global:SETTINGS.($property.Name) = $json.($property.Name)
    }   
  }
}
function Write-LogFile {
  [CmdletBinding()]
  param (
    [string]$Message,
    [ValidateSet("Main", "Errors", "NoResult")][string]$LogName = "Main",
    [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel = [LogLevel]::INFO
  )
  begin {
  }
  process {
    # if message log level is higher/equal to config log level, post it.
    if ([LogLevel]$LogLevel -ge [LogLevel]$global:SETTINGS.loglevel) {
            (get-date).ToString("yyyy-MM-dd hh:mm:ss") + "`t" + $LogLevel + "`t`t" + $Message | Out-File -FilePath ($global:SETTINGS.OutputPath.Trim('\') + "\Collector" + $LogName + ".log") -Append
            
      #write it to console if enabled.
      if ($global:SETTINGS.DebugInConsole) {
        switch ([LogLevel]$LogLevel) {
          [LogLevel]::WARNING { Write-Warning -Message $message; break; }
          [LogLevel]::ERROR { Write-Error -Message $message; break; }
          [LogLevel]::INFO { Write-Information -Message $message; break; }
          [LogLevel]::DEBUG { Write-Debug -Message $message; break; }
          [LogLevel]::PROFILE { Write-Debug -Message $message; break; }
          [LogLevel]::TRACE { Write-Verbose -Message $message; break; }
        }
      }
    }
  }
  end { }
}

# end functions

#[CmdletBinding()]


#Load the Veeam PSSnapin
if (!(Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
  Add-PSSnapin -Name VeeamPSSnapIn
  Connect-VBRServer -Server $VBRServer
}

Disconnect-VBRServer
Connect-VBRServer -Server $VBRServer



$AllJobs = Get-VBRJob -WarningAction SilentlyContinue

$VMwareBackupJobIDs = $AllJobs | <#Where-Object { ($_.JobType -eq 'Backup') -AND ($_.BackupPlatform.Platform -eq 'EVmware') } |#> Select-Object -ExpandProperty ID

$jobs = $null
$jobTypes = [Veeam.Backup.Model.EDbJobType].GetEnumNames()
foreach ($jt in $jobTypes) {
  $jobs += [Veeam.Backup.Core.CBackupSession]::GetByTypeAndTimeInterval($jt, [datetime]::Now.AddDays(-$ReportInterval), [datetime]::Now)# | select @{Name="JobName";Expression={$_.Name -replace "\((Incremental|[A-Za-z\s]*Full|Retry\s\d+)\)"}} #| group JobName
}
#$jobs.count

#$AllBackupSessions = [Veeam.Backup.Core.CBackupSession]::GetAll()
$AllBackupSessions = $jobs
$SelectBackupSessions = $AllBackupSessions #| Where-Object { $_.JobId -in $VMwareBackupJobIDs }

$SelectTaskSessions = $SelectBackupSessions.GetTaskSessions()

$SelectTaskSessions = $SelectTaskSessions #| Select-Object -First 500

[System.Collections.ArrayList]$AllTasksOutput = @()

foreach ($TaskSession in $SelectTaskSessions) {

  $LogRegex = [regex]'\bUsing \b.+\s(\[[^\]]*\])'
  $BottleneckRegex = [regex]'^Busy: (\S+ \d+% > \S+ \d+% > \S+ \d+% > \S+ \d+%)'
  $PrimaryBottleneckRegex = [regex]'^Primary bottleneck: (\S+)'

  $ProcessingLogMatches = $TaskSession.Logger.GetLog().UpdatedRecords | Where-Object Title -match $LogRegex
  $ProcessingLogMatchTitles = $(($ProcessingLogMatches.Title -replace '\bUsing \b.+\s\[', '') -replace ']', '')
  $ProcessingMode = $($ProcessingLogMatchTitles | Select-Object -Unique) -join ';'

  $BottleneckLogMatch = $TaskSession.Logger.GetLog().UpdatedRecords | Where-Object Title -match $BottleneckRegex
  $BottleneckDetails = $BottleneckLogMatch.Title -replace 'Busy: ', ''

  $PrimaryBottleneckLogMatch = $TaskSession.Logger.GetLog().UpdatedRecords | Where-Object Title -match $PrimaryBottleneckRegex
  $PrimaryBottleneckDetails = $PrimaryBottleneckLogMatch.Title -replace 'Primary bottleneck: '

  try {
    $JobSessionDuration = $TaskSession.JobSess.SessionInfo.Progress.Duration.ToString()
  }
  catch {
    $JobSessionDuration = ''
  }

  try {
    $TaskSessionDuration = $TaskSession.WorkDetails.WorkDuration.ToString()
  }
  catch {
    $TaskSessionDuration = ''
  }

  $TaskOutputResult = [pscustomobject][ordered] @{

    'JobName'           = $TaskSession.JobName
    'VMName'            = $TaskSession.Name
    'Status'            = $TaskSession.Status
    'IsRetry'           = $TaskSession.JobSess.IsRetryMode
    'ProcessingMode'    = $ProcessingMode
    'JobDuration'       = $($JobSessionDuration)
    'TaskDuration'      = $($TaskSessionDuration)
    'TaskAlgorithm'     = $TaskSession.WorkDetails.TaskAlgorithm
    'CreationTime'      = $TaskSession.JobSess.CreationTime
    'BackupSize(GB)'    = [math]::Round(($TaskSession.JobSess.BackupStats.BackupSize / 1GB), 4)
    'DataSize(GB)'      = [math]::Round(($TaskSession.JobSess.BackupStats.DataSize / 1GB), 4)
    'DedupRatio'        = $TaskSession.JobSess.BackupStats.DedupRatio
    'CompressRatio'     = $TaskSession.JobSess.BackupStats.CompressRatio
    'BottleneckDetails' = $BottleneckDetails
    'PrimaryBottleneck' = $PrimaryBottleneckDetails
    'JobType'           = $TaskSession.ObjectPlatform.Platform

  } #end TaskOutputResult object

  if ($TaskOutputResult) {
    $null = $AllTasksOutput.Add($TaskOutputResult)
    Remove-Variable TaskOutputResult -ErrorAction SilentlyContinue
  }#end if

} #end foreach TaskSession



if ($RemoveDuplicates) {

  $UniqueTaskOutput = $AllTasksOutput | Select-Object JobName, VMName, Status, IsRetry, ProcessingMode, WorkDuration, TaskAlgorithm, CreationTime, BackupSize, DataSize, DedupRatio, CompressRatio -Unique
  #Write-Output $UniqueTaskOutput
  $UniqueTaskOutput | Export-Csv 'C:\Temp\vHC\Original\VBR\VeeamSessionReport.csv' -NoTypeInformation
}

else {
  #Write-Output $AllTasksOutput
  $AllTasksOutput | Export-Csv 'C:\Temp\vHC\Original\VBR\VeeamSessionReport.csv' -NoTypeInformation
}



#$TaskOutputResult | Export-Csv 'C:\Temp\vHC\Original\VBR\VeeamSessionReport.csv' -NoTypeInformation

#Get-VeeamSessionReport -VBRServer localhost | Export-Csv 'C:\Temp\vHC\Original\VBR\VeeamSessionReport.csv' -NoTypeInformation
#Read-host("Complete! Press ENTER to close...")
Disconnect-VBRServer