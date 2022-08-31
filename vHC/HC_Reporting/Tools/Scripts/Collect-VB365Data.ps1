<#
    .SYNOPSIS
        Data collection and analysis script for Veeam Health Check (vHC) for VB365
    .DESCRIPTION
        This collection script is broken into 3 main phases:
        - The Collection Phase gathers configuration information from the VB365 server
        into a single large collection variable.
        - The Mapping Phase maps those raw data points into column:row formatted custom objects
        using a short-hand code interpretter/translator/expander.
        - Lastly, it Aggregates those maps into structures ready for CSV, at which point they are
        exported to CSV files. These CSVs are then ingested into the main EXE which formats,
        scrubs/anonymizes, and outputs an HTML report for review with a Veeam SE.

        
#>

# Bound Parameters that can be passed in via script execution to override the default or config.json settings:
param (
    [string]$OutputPath = "",
    [int]$ReportingIntervalDays = -1,
    [string]$VBOServerFqdnOrIp = "",
    [switch]$PersistParams = $false,
    [switch]$Debug = $false,
    [switch]$DebugProfiling = $false
)
enum LogLevel {
    TRACE
    PROFILE
    DEBUG
    INFO
    WARNING
    ERROR
    FATAL
}
Clear-Host

<# ### CUSTOM SETTINGS CAN BE APPLIED IN "CollectorConfig.json" ###
    [Bool]SkipCollect if $VBOEnvironment already loaded;
    [bool]ExportJson to export entire raw $VBOEnvironment capture as json;
    [bool]ExportXml to export entire raw $VBOEnvironment capture as xml;
    [bool]DebugInConsole to get output to PS host/console;
    [Bool]Watch to launch logwatcher & resmon
    [string]VBOServerFqdnOrIp -- dont use/change this unless you're writing this code remote, away from VB365 server.
    [string]LogLevel to change verbosity of logging. Log messages with level above or equal to this are output. DEBUG shows minimal debugging & profiling; PROFILE outputs detailed timers & resource usage; TRACE outputs "No Result" warnings & log
#>  

# Default config settings
$global:SETTINGS = '{"LogLevel":"INFO","OutputPath":"C:\\temp\\vHC\\Original\\VB365","ReportingIntervalDays":7,"VBOServerFqdnOrIp":"localhost"}'<#,"SkipCollect":false,"ExportJson":false,"ExportXml":false,"DebugInConsole":false,"Watch":false}#> | ConvertFrom-Json

# if path passed in via argument/param, override to that path for the run.
if ($OutputPath -ne "") { $global:SETTINGS.OutputPath = $OutputPath }

# if config.json exists, pull in the values
if (Test-Path ($global:SETTINGS.OutputPath + "\CollectorConfig.json")) {
    [pscustomobject]$json = Get-Content -Path ($global:SETTINGS.OutputPath + "\CollectorConfig.json") | ConvertFrom-Json
    foreach ($property in $json.PSObject.Properties) {
        if ($null -eq $global:SETTINGS.($property.Name)) {
            $global:SETTINGS | Add-Member -MemberType NoteProperty -Name ($property.Name) -Value $json.($property.Name)
        } else {
            $global:SETTINGS.($property.Name) = $json.($property.Name)
        }   
    }
}
# Write or update config
if (!(Test-Path ($global:SETTINGS.OutputPath))) {
    New-Item -ItemType Directory -Path $global:SETTINGS.OutputPath -Force | Out-Null
}
$global:SETTINGS | ConvertTo-Json | Out-File -FilePath ($global:SETTINGS.OutputPath + "\CollectorConfig.json") -Force

#Override any config.json settings from other args/bound parameters.
if ($ReportingIntervalDays -gt 0 <#default is -1#>) { $global:SETTINGS.ReportingIntervalDays = $ReportingIntervalDays }
if ($VBOServerFqdnOrIp -ne "" <#default is ""#>) { $global:SETTINGS.VBOServerFqdnOrIp = $VBOServerFqdnOrIp }
if ($Debug <#default is -$false#>) {
    $global:SETTINGS | Add-Member -MemberType NoteProperty -Name DebugInConsole -Value $true -Force
    $global:SETTINGS | Add-Member -MemberType NoteProperty -Name Watch -Value $true -Force
    $global:SETTINGS.LogLevel = "DEBUG"
}
if ($DebugProfiling <#default is -$false#>) {
    $global:SETTINGS | Add-Member -MemberType NoteProperty -Name DebugInConsole -Value $true -Force
    $global:SETTINGS | Add-Member -MemberType NoteProperty -Name Watch -Value $true -Force
    $global:SETTINGS.LogLevel = "PROFILE"
}
if ($PersistParams) { $global:SETTINGS | ConvertTo-Json | Out-File -FilePath ($global:SETTINGS.OutputPath + "\CollectorConfig.json") -Force }


$VerbosePreference = "SilentlyContinue"
$DebugPreference = "SilentlyContinue"
$InformationPreference = "SilentlyContinue"
$WarningPreference = "SilentlyContinue"
$ErrorActionPreference = "SilentlyContinue"
if ($global:SETTINGS.DebugInConsole) {
    $Error.Clear()
    switch ([LogLevel]$global:SETTINGS.LogLevel) {
        {$_ -le [LogLevel]::ERROR} { $ErrorActionPreference = "Continue" }
        {$_ -le [LogLevel]::WARNING} { $WarningPreference = "Continue" }
        {$_ -le [LogLevel]::INFO} { $InformationPreference = "Continue" }
        {$_ -le [LogLevel]::DEBUG} { $DebugPreference = "Continue" }
        {$_ -le [LogLevel]::TRACE} { $VerbosePreference = "Continue" }
    }
}

if ($global:SETTINGS.Watch) {
    $logWatcher = Start-Process powershell.exe -ArgumentList '-NoExit -Command "& { start-sleep -seconds 5; while ($true) { clear-host; cat C:\temp\vHC\Original\VB365\CollectorMain.log |select -last 40; sleep -seconds 3} }"' -PassThru
    $taskmgr = Start-Process taskmgr.exe -PassThru

    #Start-Process perfmon.exe -ArgumentList "/res"
}

$process = Get-Process -Id $pid
$process.PriorityClass = 'BelowNormal'

<#
function Function-Template {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]$Object,
        [string]$Prop
        )
    begin { }
    process {
        
    }
    end { }
}
#>
class EndTimeComparer:System.Collections.IComparer {
    [int]Compare($x,$y) {
        return $y.EndTime.CompareTo($x.EndTime)
    }
}
class CreationTimeComparer:System.Collections.IComparer {
    [int]Compare($x,$y) {
        return $y.CreationTime.CompareTo($x.CreationTime)
    }
}
function Write-LogFile {
    [CmdletBinding()]
    param (
        [string]$Message,
        [ValidateSet("Main","Errors","NoResult")][string]$LogName="Main",
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel=[LogLevel]::INFO
        )
    begin {
    }
    process {
        # if message log level is higher/equal to config log level, post it.
        if ([LogLevel]$LogLevel -ge [LogLevel]$global:SETTINGS.loglevel) {
            (get-date).ToString("yyyy-MM-dd hh:mm:ss") + "`t"+$LogLevel+"`t`t" + $Message | Out-File -FilePath ($global:SETTINGS.OutputPath.Trim('\') + "\Collector" + $LogName + ".log") -Append
            
            #write it to console if enabled.
            if ($global:SETTINGS.DebugInConsole) {
                switch ([LogLevel]$LogLevel) {
                    [LogLevel]::WARNING {Write-Warning -Message $message;break; }
                    [LogLevel]::ERROR {Write-Error -Message $message;break; }
                    [LogLevel]::INFO {Write-Information -Message $message;break; }
                    [LogLevel]::DEBUG {Write-Debug -Message $message;break; }
                    [LogLevel]::PROFILE {Write-Debug -Message $message;break; }
                    [LogLevel]::TRACE {Write-Verbose -Message $message;break; }
                }
            }
        }
    }
    end { }
}


function Write-ResourceUsageToLog {
    [CmdletBinding()]
    param (
        [string]$Message,
        [ValidateSet("Main","Errors")][string]$LogName="Main",
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel=[LogLevel]::DEBUG
        )
    begin { }
    process {
        if ([LogLevel]$LogLevel -ge [LogLevel]$global:SETTINGS.loglevel) {
            $resourceUsageAfter = (Get-Process -id $pid) | Select-Object WS,CPU
            $timeAfter = $global:collectorTimer.Elapsed.TotalSeconds
            
            #$previousTotalCpuUsage = ($global:ResourceUsageBefore | Measure-Object CPU -Sum).Sum
            #$currentTotalCpuUsage = ($resourceUsageAfter | Measure-Object CPU -Sum).Sum - $previousTotalCpuUsage

            #$previousPidCpuUsage =($global:ResourceUsageBefore | Where-Object {$_.Id -eq $pid}).CPU
            $currentPidCpuUsage =$resourceUsageAfter.CPU - $global:ResourceUsageBefore.CPU
            $currentTimeUsed = $timeAfter - $global:TimeBefore

            $pctCpuUsage = $currentPidCpuUsage / $currentTimeUsed / ($processor | Measure-Object -Sum NumberOfLogicalProcessors).Sum

            $memUsage = $resourceUsageAfter.WS - $global:ResourceUsageBefore.WS

            $memMessage = ("New memory allocated since last message ($message): " + ($memUsage/1MB).ToString("0.00 MB"))
            $cpuMessage = ("% CPU used since last message ($message): " + ($pctCpuUsage).ToString("0.00 %"))
            
            
            if ($global:SETTINGS.DebugInConsole) {
                Write-Debug "MEM: $memMessage"
                Write-Debug "CPU: $cpuMessage"
            }

            Write-LogFile -Message "MEM: $memMessage" -LogName $LogName -LogLevel $LogLevel
            Write-LogFile -Message "CPU: $cpuMessage" -LogName $LogName -LogLevel $LogLevel

            $global:ResourceUsageBefore = $resourceUsageAfter
            $global:TimeBefore = $timeAfter
        }
    }
    end { }
}

function Lap {
    [CmdletBinding()]
    param (
        [string]$Note,
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel=[LogLevel]::DEBUG
        )
    begin { }
    process {
        if ([LogLevel]$LogLevel -ge [LogLevel]$global:SETTINGS.loglevel) {
            $message = "$($Note): $($stopwatch.Elapsed.TotalSeconds)"
            
            if ($global:SETTINGS.DebugInConsole) {
                Write-Debug "LAP: $message"
            }

            Write-LogFile -Message "LAP: $message" -LogLevel $LogLevel
            $stopwatch.Reset()
            $stopwatch.Start()
        }
    }
    end { }
}

function Write-ElapsedAndMemUsage {
    [CmdletBinding()]
    param (
        [string]$Message,
        [ValidateSet("Main","Errors")][string]$LogName="Main",
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel=[LogLevel]::DEBUG
        )
    begin { }
    process {
        Lap -Note $Message -LogLevel $LogLevel
        Write-ResourceUsageToLog -Message $Message -LogName $LogName -LogLevel $LogLevel
    }
    end { }
}

function Join([string[]]$array, $Delimiter=", ", [switch]$KeepNulls) {
    if ($KeepNulls) {
        return $array -join $Delimiter
    } else {
        return $array.Where({ $null -ne $_ }) -join $Delimiter
    }
}


function Expand-Expression {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]$BaseObject,
        [string[]]$Expressions
        )
    begin { }
    process {
        $results = @()

        foreach ($Expression in $Expressions) {
            $expandedExpression = $Expression
            $result = [pscustomobject]@{ColumnName="";Value=""}
            
            if ($Expression.Contains("=>")) {
                $split = $Expression.Split(@("=>"), [StringSplitOptions]::RemoveEmptyEntries)
                $result.ColumnName = $split[0]
                $expandedExpression = $split[1].Replace('$.','$BaseObject.')
            } else {
                $result.ColumnName = $Expression.Replace('$.','')
            }

            if ($expandedExpression.Replace(" ","").Contains('+"')) {
                $regSplit = [regex]::Split($expandedExpression,'\+\s?".+"\s?\+')

                foreach ($propName in $regSplit) {
                    if ($propName.StartsWith('$.')) {
                        $expandedExpression.Replace('$.','$BaseObject.')
                    } elseif ($propName.Contains('$') -or $propName.Contains('(')) {
                        # do nothing
                    } else {
                        $expandedExpression = $expandedExpression.Replace($propName,'$BaseObject.'+$propName)
                    }
                }
            } else {
                if ($Expression.Contains('$') -or $Expression.Contains('(')) {
                    #complex expression
                    #do nothing.
                } else {
                    #single property
                    $expandedExpression = '$.'+$expandedExpression
                }
            }

            $expandedExpression = $expandedExpression.Replace('$.','$BaseObject.').Replace('$BaseObject. ','$BaseObject ')
            
            if ($expandedExpression.Contains('$self.')) {
                $self = @{}
                $results.ForEach({ $self.($_.ColumnName) = $_.Value }) #create the $self object from current results
            }

            try {
                $result.Value = Invoke-Expression $expandedExpression

                if ($null -eq $result.value -and $null -ne $BaseObject) {
                    
                    $message = "$expression produced no result."
                    if ($global:SETTINGS.DebugInConsole -and $global:SETTINGS.LogLevel -eq [LogLevel]::TRACE) {
                        Write-Host -ForegroundColor DarkYellow $message
                    }

                    Write-LogFile -Message $message -LogName NoResult -LogLevel TRACE
                    Write-LogFile -Message "`tExpanded Expression: $expandedExpression" -LogName NoResult -LogLevel TRACE
                }
            } catch {

                $message = "$expression not valid."
                if ($global:SETTINGS.DebugInConsole) {
                    Write-Warning "$expression not valid."
                    ($Error | Select-Object -Last 1).ToString()
                }

                Write-LogFile -Message $message -LogName Errors -LogLevel ERROR
                Write-LogFile -Message "`tExpanded Expression: $expandedExpression" -LogName Errors -LogLevel ERROR
                Write-LogFile -Message ($Error | Select-Object -Last 1).ToString() -LogName Errors -LogLevel DEBUG

                $result.Value = $null;

            }

            $results += $result
        }

        return $results
    }
    end { }
}

function New-DataTableEntry {
    [CmdletBinding()]
    param (
        [string[]]$PropertyNames,
        [Parameter(ValueFromPipeline)]$Object,
        [switch]$Empty
        )
    begin { }
    process {
        $OutputObject = New-Object -TypeName pscustomobject

        $parsedResults = Expand-Expression -BaseObject $Object -Expressions $PropertyNames

        if ($null -ne $Object -or $Empty.IsPresent) { 
            foreach ($result in $parsedResults) {
                
                if (!$result.ColumnName.StartsWith("!")) {
                    if ($null -eq $Object) {
                        $OutputObject | Add-Member -MemberType NoteProperty -Name $result.ColumnName -Value $null #if the source object is empty, then assume the values returned, including defaults, should be null.
                    } else {
                        $OutputObject | Add-Member -MemberType NoteProperty -Name $result.ColumnName -Value $result.Value
                    }
                }
            }
        } else {
            #do nothing if source object is empty. essentially return empty outputobject
        }

        return $OutputObject
    }
    end { }
}


function Import-VMCFile {
    [CmdletBinding()]
    param ()
    begin {
        $LogPath = Get-Item -Path ($env:ProgramData+'\Veeam\Backup365\Logs')
    }
    process {
        $VmcFiles = $LogPath.GetFiles('*VB*_VMC*')
        $LatestVmcFile = $VmcFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $VmcContents = (Get-Content $LatestVmcFile.FullName -Raw)

        $StartIndex = $VmcContents.LastIndexOf("Product: [Veeam") 
        $LatestDetailsTxt = $VmcContents.Substring($StartIndex,$VmcContents.Length-$StartIndex) -replace '\[\d+\.\d+\.\d+\s\d+\:\d+\:\d+\]\s\<\d+\>\s\w+\s{1,5}(=+[\w\s]+=+)?',''   
        $LatestDetails = $LatestDetailsTxt.Split("`r`n",[System.StringSplitOptions]::RemoveEmptyEntries)

        function Convert-VMCLineToObject($VMCLine) {
            if (!$VMCLine.Trim().StartsWith('{')) {
                $VMCLine = "{ " + $VMCLine + " }"
            }
            return ($VMCLine -replace '([\da-zA-Z]{8}-([\da-zA-Z]{4}-){3}[\da-zA-Z]{12})','"$1"' -replace "(?<!:\s{\s)(:\s)(\[)?(\w)",'$1$2"$3' -replace '(?<![}\]\"])(,\s[A-Z]|\s}|\])','"$0') | ConvertFrom-Json
        }

        $result = [ordered]@{}
        $result.ProductDetails = @()
        $result.LicenseDetails = @()
        $result.HostDetails = @()
        $result.SettingsDetails = @()
        $result.InfraCounts = @()
        $result.ProductDetails = @()
        $result.OrgDetails = @()
        $result.ProxyDetails = @()
        $result.RepoDetails = @()
        $result.ObjectDetails = @()
        $result.JobDetails = @()
        

        foreach ($line in $LatestDetails) {
            switch ($line.Substring(0,$line.IndexOf(':'))) {
                "Product" {$result.ProductDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "License" {$result.LicenseDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "HostID" {
                    $result.HostDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "UpdatesAutoCheckEnabled" {$result.SettingsDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "Backup Infrastructure counts" {$result.InfraCounts += Convert-VMCLineToObject -VMCLine ($line.Replace("Backup Infrastructure counts: ","")); break}
                "OrganizationID" {$result.OrgDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "ProxyID" {
                    $result.ProxyDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "RepositoryID" {$result.RepoDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "ObjStgID" {$result.ObjectDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "JobID" {$result.JobDetails += Convert-VMCLineToObject -VMCLine $line; break}
                "InstallationId" { break }
                Default {
                    Write-LogFile -Message "VMC Log Section uncaptured: $Line" -LogLevel DEBUG -LogName NoResult
                    if ($global:SETTINGS.DebugInConsole) {
                        Write-Host "uncaptured: $Line"
                    }
                }
            }
        }

        return $result
        
    }
    end {
        
    }
}

function Import-PermissionsFromLogs {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)][Veeam.Archiver.PowerShell.Model.VBOJob[]]$Jobs
        )
    begin {
        $LogPath = Get-Item -Path ($env:ProgramData+'\Veeam\Backup365\Logs')
        $Permissions = [ordered]@{}

        function Add-PermissionsFromLog ([string[]]$LogPaths,[ValidateSet("Backup","Restore")]$Prefix) {
            foreach ($logFilePath in $logPaths) {
                $content = Get-Content -Path  $logFilePath -TotalCount 1000
                $hasDoneAtLeastOneOperation = ($content | Select-String "Token found").Count -gt 0

                $orgNames = (($content | Select-String "(.+Counting items in: |.+Tenant: )(.+)(\s\(.+\)\s\(.+\).+|, Auth.+)") -replace "(.+Counting items in: |.+Tenant: )(.+)(\s\(.+\)\s\(.+\).+|, Auth.+)",'$2' | Group-Object).Name

                foreach ($orgName in $orgNames) {
                    $DEFAULT_PERMS = @(
                        "Directory.Read.All"
                        "Group.Read.All"
                        "Sites.Read.All"
                        "TeamSettings.ReadWrite.All"
                        "full_access_as_app"
                        "Sites.FullControl.All"
                        "User.Read.All"
                        "offline_access"
                        "EWS.AccessAsUser.All"
                        "full_access_as_user"
                        "AllSites.FullControl"
                    )
                    #TODO: Make this a subractive report against the above const

                    $permName = $Prefix + " - " + $orgName + ": No operations run"
                    if (!$hasDoneAtLeastOneOperation -and ($Permissions.Keys -match ($Prefix + " - " + $orgName)).Count -eq 0) {
                        $Permissions.$permName = [PSCustomObject]@{Type=$Prefix; Organization=$orgName; API=""; Permission="No backup/restore operations found"}
                    } else {
                        $permissionStrs = (($content | Select-String "Token found with the following permissions") -replace ".+Token found with the following permissions\: (.+)",': $1' -replace "(.+), Microsoft API: (.+)",'$2$1')
                        $impersonationCheck = $null -ne ($content | Select-String "The account does not have permission to impersonate the requested user")

                        if ($null -ne $Permissions.$permName -and $permissionStrs.Count -gt 0) {
                            $Permissions.Remove($permName)
                        }

                        if ($impersonationCheck -eq $true) {
                            $permName = $Prefix + " - " + $orgName + ": ApplicationImpersonation"
                            $Permissions.$permName = [PSCustomObject]@{Type="Restore"; Organization=$orgName; API="Exchange"; Permission="ApplicationImpersonation"}
                        }

                        foreach ($permStr in $permissionStrs) {
                            $API = $permStr.Split(':')[0].Trim()

                            $perms = $permStr.Split(':')[1].Split(",").Trim()

                            foreach ($perm in $perms) {
                                $permName = $Prefix + " - " + $orgName + ": " + $perm
                                $permValue = [PSCustomObject]@{Type=$Prefix; Organization=$orgName; API=$API; Permission=$perm}
                                if ($null -eq $Permissions.$permName) {
                                    $Permissions.$permName = $permValue
                                } else {
                                    $Permissions.$permName.Type = $Prefix
                                    $Permissions.$permName.Organization = $orgName
                                    $Permissions.$permName.API = if ($API -ne "" -and $null -ne $API) { $API } else { $Permissions.$permName.API }
                                    $Permissions.$permName.Permission = $perm
                                }
                            }
                        }
                    }
                }
            }

            $content = ""
            [GC]::Collect()
        }
    }
    process {
        $latestLogs = @{}
        $VEXLogPath = Get-Item ($LogPath.FullName+"\..\..\Backup\ExchangeExplorer\Logs")
        $VESPLogPath = Get-Item ($LogPath.FullName+"\..\..\Backup\SharePointExplorer\Logs")
        $VEODLogPath = Get-Item ($LogPath.FullName+"\..\..\Backup\OneDriveExplorer\Logs")
        $VETLogPath = Get-Item ($LogPath.FullName+"\..\..\Backup\TeamsExplorer\Logs")
        $latestLogs.VEX = Get-ChildItem -Path $VEXLogPath -Recurse -File -Filter "Veeam.Exchange.Explorer_*.log" | Select-Object Name, LastWriteTime,FullName | Sort-Object LastWriteTime -Descending | Select-Object -First 5
        $latestLogs.VESP = Get-ChildItem -Path $VESPLogPath -Recurse -File -Filter "Veeam.SharePoint.Explorer_*.log" | Select-Object Name, LastWriteTime,FullName | Sort-Object LastWriteTime -Descending | Select-Object -First 5
        $latestLogs.VEOD = Get-ChildItem -Path $VEODLogPath -Recurse -File -Filter "Veeam.OneDrive.Explorer_*.log" | Select-Object Name, LastWriteTime,FullName | Sort-Object LastWriteTime -Descending | Select-Object -First 5
        $latestLogs.VET = Get-ChildItem -Path $VETLogPath -Recurse -File -Filter "Veeam.Teams.Explorer_*.log" | Select-Object Name, LastWriteTime,FullName | Sort-Object LastWriteTime -Descending | Select-Object -First 5

        Add-PermissionsFromLog -LogPath $latestLogs.VEX.FullName -Prefix Restore
        Add-PermissionsFromLog -LogPath $latestLogs.VESP.FullName -Prefix Restore
        Add-PermissionsFromLog -LogPath $latestLogs.VEOD.FullName -Prefix Restore
        Add-PermissionsFromLog -LogPath $latestLogs.VET.FullName -Prefix Restore

        foreach ($job in $Jobs) {
            $JobLogPaths = Get-Item ($LogPath.FullName+"\"+$job.Organization.name+"\"+$job.Name),($LogPath.FullName+"\"+$job.Organization.OfficeName+"\"+$job.Name)# -ErrorAction SilentlyContinue

            
            $latestBackupLog = Get-ChildItem -Path $JobLogPaths -Recurse -File -Filter "Job.$($job.Name)_*.log" | Select-Object Name, LastWriteTime,FullName | Sort-Object LastWriteTime -Descending | Select-Object -First 1

            Add-PermissionsFromLog -LogPath $latestBackupLog.FullName -Prefix Backup
        }
    }
    end {
        return $Permissions
    }
}

# Collect one large object w/ all stats
function Get-VBOEnvironment {
    [CmdletBinding()]
    param (
        )
    begin { }
    process {
        $e = [ordered]@{}

        $progress=0
        $progressSplat = @{Id=1; Activity="Collecting VBO Environment`'s stats..."}
        

        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Starting...";

        #Org settings:
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting organization..."
        $e.VBOOrganization = Get-VBOOrganization
        Write-ElapsedAndMemUsage -Message "Collected: VBOOrganization" -LogLevel PROFILE
        $e.VBOOrganizationUsers = $e.VBOOrganization | ForEach-Object { Get-VBOOrganizationUser -Organization $_ }
        Write-ElapsedAndMemUsage -Message "Collected: VBOOrganizationUsers" -LogLevel PROFILE
        $e.VBOApplication = $e.VBOOrganization | Get-VBOApplication
        Write-ElapsedAndMemUsage -Message "Collected: VBOApplication" -LogLevel PROFILE
            #Diabled Jun1/22 - Not used #$e.VBOBackupApplication = $e.VBOOrganization `
                #Diabled Jun1/22 - Not used #| Where-Object {$_.Office365ExchangeConnectionSettings.AuthenticationType -ne [Veeam.Archiver.PowerShell.Model.Enums.VBOOffice365AuthenticationType]::Basic} `
                #Diabled Jun1/22 - Not used #| Get-VBOBackupApplication
        Write-ResourceUsageToLog -Message "Org collected"

        #infra settings:
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting backup infrastructure..."
        $e.VBOServerComponents = Get-VBOServerComponents
        Write-ElapsedAndMemUsage -Message "Collected: VBOServerComponents" -LogLevel PROFILE
        $e.VBORepository = Get-VBORepository
        Write-ElapsedAndMemUsage -Message "Collected: VBORepository" -LogLevel PROFILE
        $e.VBOObjectStorageRepository = Get-VBOObjectStorageRepository
        Write-ElapsedAndMemUsage -Message "Collected: VBOObjectStorageRepository" -LogLevel PROFILE
        $e.VBOProxy = Get-VBOProxy
        Write-ElapsedAndMemUsage -Message "Collected: VBOProxy" -LogLevel PROFILE
        $e.AzureInstance = Invoke-RestMethod -Headers @{"Metadata"="true"} -Method GET -Uri "http://169.254.169.254/metadata/instance?api-version=2021-02-01" -TimeoutSec 5
        Write-ElapsedAndMemUsage -Message "Collected: AzureInstance" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Infra collected"

        #Archiver applainces
        #Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting object storage & cloud settings..."
        #$e.VBOAmazonInstanceType = Get-VBOAmazonInstanceType # these archiver settings need more work to collect https://helpcenter.veeam.com/docs/vbo365/powershell/get-vboamazoninstancetype.html?ver=60
        #$e.VBOAmazonSecurityGroup = Get-VBOAmazonSecurityGroup
        #$e.VBOAmazonSubnet = Get-VBOAmazonSubnet
        #$e.VBOAmazonVPC = Get-VBOAmazonVPC
        #$e.VBOAzureLocation = Get-VBOAzureLocation
        #$e.VBOAzureResourceGroup = Get-VBOAzureResourceGroup
        #$e.VBOAzureSubNet = Get-VBOAzureSubNet
        #$e.VBOAzureVirtualMachineSize = Get-VBOAzureVirtualMachineSize
        #$e.VBOAzureVirtualNetwork = Get-VBOAzureVirtualNetwork
        #Object details
        #Diabled Jun1/22 - Not used #$e.VBOAzureBlobAccount = Get-VBOAzureBlobAccount
        #Diabled Jun1/22 - Not used #$e.VBOAzureServiceAccount = Get-VBOAzureServiceAccount
        #$e.VBOAzureSubscription = Get-VBOAzureSubscription
        #$e.VBOAzureBlobFolder = Get-VBOAzureBlobFolder
        #Diabled Jun1/22 - Not used #$e.VBOAmazonS3Account = Get-VBOAmazonS3Account
        #Diabled Jun1/22 - Not used #$e.VBOAmazonS3CompatibleAccount = Get-VBOAmazonS3CompatibleAccount
        #$e.VBOAmazonS3Bucket = Get-VBOAmazonS3Bucket
        #$e.VBOAmazonS3Folder = Get-VBOAmazonS3Folder
        #Diabled Jun1/22 - Not used #$e.VBOEncryptionKey = Get-VBOEncryptionKey
        #Write-ResourceUsageToLog -Message "Object & cloud collected"

        #jobs
        Lap "Collection: Job settings..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting Job settings..."
        $e.VBOJob = Get-VBOJob
        Write-ElapsedAndMemUsage -Message "Collected: VBOJob " -LogLevel PROFILE
        #Diabled Jun21/22 - Not used#$e.VBOBackupItem = $e.VBOJob | ForEach-Object { Get-VBOBackupItem -Job $_ | Select-Object @{n="Job";e={$_.Name}},*}
            #Write-ElapsedAndMemUsage -Message "Collected: VBOBackupItem" -LogLevel PROFILE
        #Diabled Jun21/22 - Not used#$e.VBOExcludedBackupItem = $e.VBOJob | ForEach-Object { Get-VBOExcludedBackupItem -Job $_ | Select-Object @{n="Job";e={$_.Name}},*}
            #Write-ElapsedAndMemUsage -Message "Collected: VBOExcludedBackupItem" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBOOrganizationRetentionExclusion = $e.VBOOrganization | ForEach-Object { Get-VBOOrganizationRetentionExclusion -Organization $_ | Select-Object @{n="Job";e={$_}},*}
        $e.VBOCopyJob = Get-VBOCopyJob
        Write-ElapsedAndMemUsage -Message "Collected: VBOCopyJob " -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Jobs collected"

        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting entity details..."
        $e.VBOEntityData = $e.VBORepository | Where-Object {!($_.IsOutOfOrder -and $null -ne $_.ObjectStorageRepository)} | Get-VBOEntityData -Type User -WarningAction SilentlyContinue | Select-Object *,@{n="Repository";e={@{Id=$repo.Id;Name=$repo.Name}}},@{n="Proxy";e={$proxy=($e.VBOProxy | Where-Object { $_.id -eq $repo.ProxyId}); @{Id=$proxy.Id;Name=$proxy.Hostname}}} #slow/long-running. Exclude for IsOutOfOrder object repos added Jun 15
        # Build an index (hashtable keyed to email) so that the search in the next function is orders of magnitude faster.
        $e.EntitiesIndex = @{}
        foreach ($org in $e.VBOOrganization) { $e.EntitiesIndex[$org.Name] = @{}}
        foreach ($ent in $e.VBOEntityData) {
            if ($ent.Type -eq "User") {
                if ($ent.Organization.DisplayName -in $e.EntitiesIndex.Keys) { # if the org name is actually needing collection (is in the index keys)
                    if ($ent.Email -notin $e.EntitiesIndex[$ent.Organization.DisplayName].Keys) { # if the email is not already a key in the index, add it.
                        $entityKey = ($ent.DisplayName+"-"+$ent.Email).Replace(" ","")
                        $e.EntitiesIndex[$ent.Organization.DisplayName][$entityKey] = $ent
                    } else {
                        Write-LogFile -Message "Duplicate entity encountered in index: $($ent.Email)" -LogLevel DEBUG
                    }
                }
            }
        }
        Write-ElapsedAndMemUsage -Message "Collected: VBOEntityData" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Entities collected"

        #backups
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting restore points..."
        #Diabled Jun1/22 - Not used #$e.VBORestorePoint = Get-VBORestorePoint
        Write-ResourceUsageToLog -Message "RPs collected"
        #Global
        Lap "Collection: Global settings..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting global settings..."
        $e.VBOLicense = Get-VBOLicense
        Write-ElapsedAndMemUsage -Message "Collected: VBOLicense" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBOLicensedUser = Get-VBOLicensedUser
        $e.VBOFolderExclusions = Get-VBOFolderExclusions
        Write-ElapsedAndMemUsage -Message "Collected: VBOFolderExclusions" -LogLevel PROFILE
        $e.VBOGlobalRetentionExclusion = Get-VBOGlobalRetentionExclusion
        Write-ElapsedAndMemUsage -Message "Collected: VBOEmailSettings" -LogLevel PROFILE
        $e.VBOEmailSettings = Get-VBOEmailSettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOEmailSettings" -LogLevel PROFILE
        $e.VBOHistorySettings = Get-VBOHistorySettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOHistorySettings" -LogLevel PROFILE
        $e.VBOInternetProxySettings = Get-VBOInternetProxySettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOInternetProxySettings" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Global collected"
        #security
        Lap "Collection: Security settings..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting security settings..."
        $e.VBOTenantAuthenticationSettings = Get-VBOTenantAuthenticationSettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOTenantAuthenticationSettings" -LogLevel PROFILE
        $e.VBORestorePortalSettings = Get-VBORestorePortalSettings
        Write-ElapsedAndMemUsage -Message "Collected: VBORestorePortalSettings" -LogLevel PROFILE
        $e.VBOOperatorAuthenticationSettings = Get-VBOOperatorAuthenticationSettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOOperatorAuthenticationSettings" -LogLevel PROFILE
        $e.VBORbacRole = Get-VBORbacRole
        Write-ElapsedAndMemUsage -Message "Collected: VBORbacRole" -LogLevel PROFILE
        $e.VBORestAPISettings = Get-VBORestAPISettings
        Write-ElapsedAndMemUsage -Message "Collected: VBORestAPISettings" -LogLevel PROFILE
        $e.VBOSecuritySettings = Get-VBOSecuritySettings
        Write-ElapsedAndMemUsage -Message "Collected: VBOSecuritySettings" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Security collected"
        
        #stats
        Lap "Collection: Job & session stats..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Collecting sessions & statistics..."
        $e.VBOJobSession = Get-VBOJobSession | Where-Object { $_.EndTime -gt [DateTime]::Now.AddDays(-$global:SETTINGS.ReportingIntervalDays) }   
        #added below session index to enable capturing certain metrics further back than reporting interval, but efficiently.
        [System.Collections.IComparer] $CreationTimeComparer = [CreationTimeComparer]::new()
        $e.VBOJobSession_All = Get-VBOJobSession
        $e.JobSessionIndex = @{}
        foreach ($session in $e.VBOJobSession_All) {
            $jobId = $session.JobId.Guid
            if ($null -eq $e.JobSessionIndex[$jobId]) {
                $e.JobSessionIndex[$jobId] = @{}
                $e.JobSessionIndex[$jobId].All = [System.Collections.ArrayList]@()
                $e.JobSessionIndex[$jobId].Latest = $null
                $e.JobSessionIndex[$jobId].LatestComplete = $null
                $e.JobSessionIndex[$jobId].LastXDays = [System.Collections.ArrayList]@()
                $e.JobSessionIndex[$jobId].LastXSessions = $null
                $e.JobSessionIndex[$jobId].LastXSessionsComplete = $null
            }

            $addResult = $e.JobSessionIndex[$jobId].All.Add($session) #add to "All" index

            if ($session.EndTime -gt [DateTime]::Now.AddDays(-$global:SETTINGS.ReportingIntervalDays)) { #add if < X days
                $addResult = $e.JobSessionIndex[$jobId].LastXDays.Add($session)
            }
        }
        foreach ($key in $e.JobSessionIndex.Keys) {
            $e.JobSessionIndex[$key].All.Sort($CreationTimeComparer)
            $e.JobSessionIndex[$key].LastXDays.Sort($CreationTimeComparer)
            $e.JobSessionIndex[$key].LastXSessions = $e.JobSessionIndex[$key].All | Select-Object -First $global:SETTINGS.ReportingIntervalDays
            $i=1
            $e.JobSessionIndex[$key].LastXSessionsComplete =  foreach($session in $e.JobSessionIndex[$key].All) { if ($session.Status -in @("Success","Warning")) { $session; if($i -ge $global:SETTINGS.ReportingIntervalDays) {break;}; $i++ } }
            $e.JobSessionIndex[$key].Latest = $e.JobSessionIndex[$key].All[0]
            $e.JobSessionIndex[$key].LatestComplete = foreach($session in $e.JobSessionIndex[$key].All) { if ($session.Status -in @("Success","Warning")) { $session;break } }
        }
        Write-ElapsedAndMemUsage -Message "Collected: VBOJobSession" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBORestoreSession = Get-VBORestoreSession
        $e.VBOUsageData = $e.VBORepository | Where-Object {!($_.IsOutOfOrder -and $null -ne $_.ObjectStorageRepository)} | ForEach-Object { Get-VBOUsageData -Repository $_ } # added OutOfOrder exclusion for broken repos.
        Write-ElapsedAndMemUsage -Message "Collected: VBOUsageData" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBODataRetrievalSession = Get-VBODataRetrievalSession
        Write-ResourceUsageToLog -Message "Sessions & stats collected"

        #reports
        #Lap "Collection: Generating Reports..."
        #Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Generating reports..."
        #Diabled Jun1/22 - No longer used #Get-ChildItem -Path ($global:SETTINGS.OutputPath + "\Veeam_*Report*.csv") | Remove-Item -Force
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOMailboxProtectionReport -Organization $_ -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOStorageConsumptionReport -StartTime (Get-Date).AddDays(-$global:SETTINGS.ReportingIntervalDays) -EndTime (Get-Date) -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOLicenseOverviewReport -StartTime (Get-Date).AddDays(-$global:SETTINGS.ReportingIntervalDays) -EndTime (Get-Date) -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Write-ResourceUsageToLog -Message "Reports generated"

        Lap "Collection: Parsing VMC Log..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Parsing VMC Log..."
        $e.VMCLog = Import-VMCFile
        Write-ElapsedAndMemUsage -Message "Collected: VMCLog" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "VMC Log Parsed"

        #Lap "Collection: Parse Job Logs..."
        #Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Parsing Job Log..."
        #Diabled Jun1/22 - Needs improvement #$e.PermissionsCheck = Import-PermissionsFromLogs -Jobs $e.VBOJob
        #Write-ResourceUsageToLog -Message "Job Logs Parsed"

        Write-Progress @progressSplat -PercentComplete 100 -Status "Done"
        Start-Sleep -Seconds 1
        Write-Progress @progressSplat -PercentComplete 100 -Status "Done" -Completed

        return $e
    }
    end { }
}

############## START OF MAIN EXECUTION  ################
#check if output path exists
if (!(Test-Path $global:SETTINGS.OutputPath)) {
    New-Item -ItemType Directory -Path $global:SETTINGS.OutputPath
}

Write-LogFile -Message "" -LogLevel INFO
Write-LogFile -Message "Starting new VB365 data collection session." -LogLevel INFO
Write-LogFile -Message "" -LogLevel INFO

Clear-Host
if ($global:SETTINGS.DebugInConsole) { Disconnect-VBOServer }

# Initial setup
Set-Alias -Name MDE -Value New-DataTableEntry -Force -Option Private -ErrorAction SilentlyContinue
$global:CollectorTimer = [System.Diagnostics.Stopwatch]::StartNew()
$global:stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$Error.Clear()
$global:ResourceUsageBefore = (Get-Process -Id $pid) | Select-Object WS,CPU
$global:TimeBefore = $global:CollectorTimer.Elapsed.TotalSeconds

$processor = (Get-ComputerInfo -Property CsProcessors).CsProcessors
Write-LogFile -LogLevel INFO -Message ("Processor: " + $processor[0].Name + " (" + $processor[0].Architecture +");  " + "Phy Cores: " + ($processor | Measure-Object -Sum NumberOfCores).Sum + ";  " + "Log. Cores: " + ($processor | Measure-Object -Sum NumberOfLogicalProcessors).Sum + ";  " + "Clock Speed: " + ($processor | Measure-Object -Average CurrentClockSpeed).Average + "/" + ($processor | Measure-Object -Average MaxClockSpeed).Average)
$memory =  (Get-ComputerInfo -Property *Memory*)
Write-LogFile -LogLevel INFO -Message ("System Memory: " + [Math]::Round($memory.OsFreePhysicalMemory/1MB,2) + " GB free of " + $memory.CsPhyicallyInstalledMemory/1MB + " GB")

# Import modules & connect to server
Import-Module -Name Veeam.Archiver.PowerShell,Veeam.Exchange.PowerShell,Veeam.SharePoint.PowerShell,Veeam.Teams.PowerShell
if ($global:SETTINGS.VBOServerFqdnOrIp -ne "localhost") {
    if ($null -eq $global:VBO_SERVER_CREDS) {
        $global:VBO_SERVER_CREDS = Get-Credential -Message "Enter authorized VBO credentials for '$($global:SETTINGS.VBOServerFqdnOrIp)':"
    }
    Connect-VBOServer -Server $global:SETTINGS.VBOServerFqdnOrIp -Credential $global:VBO_SERVER_CREDS -ErrorAction Stop;
} else {
    Connect-VBOServer -Server localhost -ErrorAction Stop;
}

Lap "Ready to collect"
Write-ResourceUsageToLog -Message "Start"

if (!$global:SETTINGS.SkipCollect) { 
    $WarningPreference = "SilentlyContinue"
    # Start the data collection
    #Write-Host "Collecting VBO Environment`'s stats..."
    Write-LogFile "Collecting VBO Environment`'s stats..." -LogLevel INFO

    $Global:VBOEnvironment = Get-VBOEnvironment 
    if ($global:SETTINGS.DebugInConsole) { $v = $Global:VBOEnvironment; $v.Keys; }

    Write-ResourceUsageToLog -Message "Done collecting"
    if ($global:SETTINGS.ExportJson) {
        $VBOEnvironment | ConvertTo-Json -Depth 100 | Out-File ($global:SETTINGS.OutputPath.Trim('\')+"\VBOEnvironment.json") -Force
        [GC]::Collect()
        Write-ElapsedAndMemUsage -Message "JSON Exported"
    }
    if ($global:SETTINGS.ExportXml) {
        $VBOEnvironment | Export-Clixml -Depth 100 -Path ($global:SETTINGS.OutputPath.Trim('\')+"\VBOEnvironment.xml") -Force
        [GC]::Collect()
        Write-ElapsedAndMemUsage -Message "XML Exported"
    }

    $WarningPreference = "continue"
 }

Write-LogFile "VB365 Environment stats collected." -LogLevel INFO
Lap "Collection finished. Building maps next."


####### FUNCTIONS THAT SUPPORT MAPPING PROCESS ######
#Start processing the data into columns
function Test-CertPKExportable([string]$thumbprint) {
    $isCertDriveExists = Get-PSDrive -Name Cert
    if ($null -ne $isCertDriveExists) {
        $certEntries = Get-ChildItem -Recurse cert:\* | Where-Object {$_.Thumbprint -eq $thumbprint }

        if ($null -ne $certEntries) {
            foreach ($certEntry in $certEntries) {
                if ($certEntries.PrivateKey.CspKeyContainerInfo.Exportable -eq $true) {
                    return $true
                }
            }

            return $false; #if no other return true first, then result false
        } else {
            return "Failed to find cert."
        }
    }
}
function ConvertData {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)][object[]]$Items,
        [ValidateSet("TB","GB","MB","KB","B","Tb","Gb","Mb","Kb","b")][string]$To,
        [string]$Format="0.0"
    )
    begin { 
        $results = @()
    }
    process {
        foreach ($item in $Items) {
            $result = $item.Replace(" ","")

            $multiplier = if ($Item.IndexOf("B",[StringComparison]::Ordinal) -ge 0 -and $To.IndexOf("b",[StringComparison]::Ordinal) -ge 0) { 1/8 } elseif ($Item.IndexOf("b",[StringComparison]::Ordinal) -ge 0 -and $To.IndexOf("B",[StringComparison]::Ordinal) -ge 0) { 8 } else { 1 }

            $outFormat = $Format
            if ($Format -eq "0.0") {
                $outFormat += " " + $To
                if ($item.Contains("/s")) {
                    $outFormat += "/s"   
                }
            }

            if ($To -ieq "b") { #is destined for Bytes/bits; adjust for the fact that "1B" is meaningless to powershell; "1KB" however is meaningful.
                $To = "KB"
                $multiplier = $multiplier/1024
            }

            if ([regex]::IsMatch($result,"\d+[Bb](?:/s)?$")) { #is in Bytes/bits
                $result = ($result.Replace("B","").Replace("b","").Replace("/s","")/"$multiplier$to")
            } else {
                $result = ($result.Replace("/s","")/"$multiplier$to")
            }

            if ($resul -eq 0) {
                $results += $null
            } else {
                $results += $result.ToString($outFormat)
            }
        }
    }
    end {
        return $results
    }
}
function 95P {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)][object[]]$Items,
        [string]$Property,
        [int]$Percentile=95
        )
    begin {
        $allItems = [System.Collections.ArrayList]@()
    }
    process {
        if ($Items.Count -gt 1) {
            $allItems = $Items
        } else {
            $allItems.Add($Items) | Out-Null
        }
    }
    end {
        if ([string]::IsNullOrEmpty($Property)) {
            $sortedItems = $allItems | Sort-Object
        } else {
            $sortedItems = $allItems.$Property | Sort-Object
        }
        if ($sortedItems.Count -eq 1) {
            return $sortedItems
        } elseif ($sortedItems.Count -eq 2) {
            return $sortedItems[0]
        } else {
            $result = $sortedItems | Select-Object -First ([math]::Floor(($allItems.Count*$Percentile/100)))

            return $result.GetValue($result.Length-1)
        }
    }
}
function GetItemsLeft() {
    $global:CollectorCurrentProcessItems--
    if ($global:CollectorCurrentProcessItems % 1000 -eq 0) {
        Lap -Note "Items left: $global:CollectorCurrentProcessItems. Seconds for previous 1000" -LogLevel PROFILE
    }
}
function ToLongHHmmss {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)][long]$Seconds
        )
    begin { }
    process {
        $time = [datetime]::MinValue.AddSeconds($Seconds);
        $hh = (($time.DayOfYear-1)*24 + $time.TimeOfDay.Hours).ToString("##00")
        $mm = $time.TimeOfDay.Minutes.ToString("00")
        $ss = $time.TimeOfDay.Seconds.ToString("00")

        return "{0}:{1}:{2}" -f $hh,$mm,$ss
    }
    end { }
}

function Append-VB365ProductVersion {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)][string]$BuildVersion
        )
    begin { 
        $ProductVersions = @{
            "11.2.0"=" (v6a)"
            "11.1.0"=" (v6)"
            "10.0.5"=" (v5d)"
            "10.0.4"=" (v5c)"
            "10.0.3"=" (v5b)"
            "10.0.2"=" (v5a)"
            "10.0.1"=" (v4c)"
        }
    }
    process {
        $versionMatch = [regex]::Match($BuildVersion,"(?<prefix>(?<major>\d+)\.(?<minor>\d+)\.(?<update>\d+))\.(?<patch>\d+)")
        $prefix = $versionMatch.Groups["prefix"].Value
        
        return $BuildVersion + $ProductVersions.$prefix
    }
    end { }
}
function GetEndTime {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)][object]$Object,
        [string]$EndTimeParamName="EndTime"
        )
    begin { 
    }
    process {
        if ($Object.GetType().Name -eq "DateTime") {
            $endTime = $Object
        } else {
            $endTime = $Object.$EndTimeParamName
        }

        if ($endTime -gt (get-date) -or $null -eq $endTime) {
            #End time is in future, and thus is likely still running. Return current time instead.
            return (get-date)
        } else {
            #end time is in past; return it
            return $endTime
        }
    }
    end { }
}

################################ HERE IS WHERE THE PROPERTY MAPPING INTERPRETER STARTS ################################
# This started as a way to make things easier to map, and then morphed into a lot.

#USAGE examples:
# 'Name'                                                            :: will populate the column name as "Name", and the value from the passed in object (BaseObject)
# 'Name=>Hostname'                                                  :: will populate the column name as "Name", and the value from "Hostname" property of the passed in object (BaseObject)\
# 'Name=>Hostname.Subproperty'                                      :: will populate the column name as "Name", and the value from subproperty X of the "Hostname" property of the passed in object (BaseObject)
# 'Listener=>Host + ":" + Port'                                     :: use of an expression that includes '+"' or "+ "' is a simple way to concatenate two or more of the same BaseObject's properties together with a string.
# 'Name=>$.Hostname'                                                :: use of "$." is the shorthand for the $BaseObject (object passed in). "$." is implied in the above examples; use it if doing something more advanced.
# 'Name=>if (x) { $.Hostname } else { $othervariable.othername }'   :: advanced expression will be evaluated/invoked from the string to a command. You must use the "$." short hand for the BaseObject's properties.
    # This example would set the Name column to either the baseobject's "Hostname" property or to the $othervariable's "othername" property depending on whether X is true.
    # These expressions can be as advanced as you like, as long as they evaluate back to a single result
# NOTE: You can use $self.columnName to refer to a previously created column (must be above where $self is used)
# NOTE: You can add a "!" sign before a column name to have it not include in the returned object. Useful for defining variables or doing something before/after other entries are invoked.

Write-LogFile "Mapping CSV Structures..." -LogLevel INFO
Write-ElapsedAndMemUsage -Message "Start Mapping"
$progress=0
$progressSplat = @{Id=2; Activity="Building maps..."}
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Starting...";

$map = [ordered]@{}

Write-LogFile -Message "Mapping Controllers..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Management Server...";
$map.Controller = $Global:VBOEnvironment.VMCLog.HostDetails | mde @(
    'VB365 Version=>$Global:VBOEnvironment.VMCLog.ProductDetails.Version | Append-VB365ProductVersion'
    'OS Version=>OSVersion'
    'RAM=>($.RAMTotalSize/1GB).ToString("#,##0 GB")'
    'CPUs=>CPUCount'
    'Proxies Managed=>$VBOEnvironment.VMCLog.InfraCounts.BackupProxiesCount'
    'Repos Managed=>[int]$VBOEnvironment.VMCLog.InfraCounts.BackupRepositoriesCount ' #just count true repo definitions -- old: + [int]$VBOEnvironment.VMCLog.InfraCounts.ObjStgCount'
    'Orgs Managed=>$VBOEnvironment.VMCLog.InfraCounts.OrganizationsCount'
    'Jobs Managed=>$VBOEnvironment.VMCLog.InfraCounts.BackupJobsCount'
    'PowerShell Installed?=>IsPowerShellServiceInstalled'
    'Proxy Installed?=>if ($.IsProxyServiceInstalled -or $.Type -eq "Proxy") { $true } else { $false }'
    'REST Installed?=>IsRestServiceInstalled'
    'Console Installed?=>IsShellServiceInstalled'
    'VM Name=>$Global:VBOEnvironment.AzureInstance.compute.name'
    'VM Location=>if ($null -ne $Global:VBOEnvironment.AzureInstance.compute.location) { $Global:VBOEnvironment.AzureInstance.compute.location + " (Zone " + $Global:VBOEnvironment.AzureInstance.compute.zone + ")" }'
    'VM SKU=>$Global:VBOEnvironment.AzureInstance.compute.sku'
    'VM Size=>$Global:VBOEnvironment.AzureInstance.compute.vmSize'
)
Write-ElapsedAndMemUsage -Message "Mapped Controller" -LogLevel PROFILE

Write-LogFile -Message "Mapping Controller Drives..." -LogLevel DEBUG
$map.ControllerDrives = Get-PhysicalDisk | mde @(
    'Friendly Name=>FriendlyName'
    'DeviceId'
    'Bus Type=>BusType'
    'Media Type=>MediaType'
    'Manufacturer'
    'Model'
    'Size=>($.Size/1TB).ToString("#,##0.00") + " TB"'
    'Allocated Size=>($.AllocatedSize/1TB).ToString("#,##0.00") + " TB"'
    'Operational Status=>OperationalStatus'
    'Health Status=>HealthStatus'
    'Boot Drive=>(get-disk -Number $.DeviceId).IsBoot'
)
Write-ElapsedAndMemUsage -Message "Mapped Controller drives" -LogLevel PROFILE

Write-LogFile -Message "Mapping Global..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Global...";
$map.Global = $Global:VBOEnvironment.VBOLicense | mde @(
    'License Status=>Status'
    'License Expiry=>$.ExpirationDate.ToShortDateString()'
    'Support Expiry=>$.SupportExpirationDate.ToShortDateString()'
    'License Type=>Type'
    'Licensed To=>LicensedTo'
    'License Contact=>ContactPerson'
    'Licensed For=>TotalNumber'
    'Licenses Used=>UsedNumber'
    'Global Folder Exclusions=>Join(($Global:VBOEnvironment.VBOFolderExclusions.psobject.Properties | ? { $_.Value -eq $true}).Name)'
    'Global Ret. Exclusions=>Join(($Global:VBOEnvironment.VBOGlobalRetentionExclusion.psobject.Properties | ? { $_.Value -eq $true}).Name)'
    'Log Retention=>if($Global:VBOEnvironment.VBOHistorySettings.KeepAllSessions) { "Keep All" } else {$Global:VBOEnvironment.VBOHistorySettings.KeepOnlyLastXWeeks }'
    'Notification Enabled=>$Global:VBOEnvironment.VBOEmailSettings.EnableNotification'
    #'Notifify On=>Join((($Global:VBOEnvironment.VBOEmailSettings | select NotifyOn*).psobject.Properties | ? { $_.Value -eq $false}).Name)'
    <#fixed Jul 21#>'Notify On=>Join((($Global:VBOEnvironment.VBOEmailSettings | select NotifyOn*,Supress*).psobject.Properties | ? { $_.Value -eq $true}).Name.Replace("NotifyOn",""))'
    'Automatic Updates?=>$Global:VBOEnvironment.VMCLog.SettingsDetails.UpdatesAutoCheckEnabled'
)
Write-ElapsedAndMemUsage -Message "Mapped Global" -LogLevel PROFILE

Write-LogFile -Message "Mapping Security..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Security...";
$map.Security = 1 | mde @(
    'Win. Firewall Enabled?=>$v = ((Get-NetConnectionProfile).NetworkCategory -replace "Authenticated","" | % {Get-NetFirewallProfile -Name $_}); Join( $v | % { $_.Name +": " + $_.Enabled } )'
    'Internet proxy?=>$v=$Global:VBOEnvironment.VBOInternetProxySettings; if($v.UseInternetProxy) { $v.Host+":"+$v.Port } else { $false}'
    'Server Cert=>$Global:VBOEnvironment.VBOSecuritySettings.CertificateFriendlyName'
    #'Server Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOSecuritySettings.CertificateThumbprint)'
    'Server Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOSecuritySettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Server Cert Self-Signed?=>$Global:VBOEnvironment.VBOSecuritySettings.CertificateIssuedTo -eq $Global:VBOEnvironment.VBOSecuritySettings.CertificateIssuedBy'
    'API Enabled?=>$Global:VBOEnvironment.VBORestAPISettings.IsServiceEnabled'
    'API Port=>$Global:VBOEnvironment.VBORestAPISettings.HTTPSPort'
    'API Cert=>$Global:VBOEnvironment.VBORestAPISettings.CertificateFriendlyName'
    #'API Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBORestAPISettings.CertificateThumbprint)'
    'API Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBORestAPISettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'API Cert Self-Signed?=>$Global:VBOEnvironment.VBORestAPISettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBORestAPISettings.CertificateIssuedBy'
    'Tenant Auth Enabled?=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.AuthenticationEnabled'
    'Tenant Auth Cert=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateFriendlyName'
    #'Tenant Auth PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateThumbprint)'
    'Tenant Auth Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Tenant Auth Cert Self-Signed?=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateIssuedBy'
    'Restore Portal Enabled?=>$Global:VBOEnvironment.VBORestorePortalSettings.IsServiceEnabled'
    'Restore Portal Cert=>$Global:VBOEnvironment.VBORestorePortalSettings.CertificateFriendlyName'
    #'Restore Portal Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBORestorePortalSettings.CertificateThumbprint)'
    'Restore Portal Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBORestorePortalSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Restore Portal Cert Self-Signed?=>$Global:VBOEnvironment.VBORestorePortalSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBORestorePortalSettings.CertificateIssuedBy'
    'Operator Auth Enabled?=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.AuthenticationEnabled'
    'Operator Auth Cert=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateFriendlyName'
    #'Operator Auth Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateThumbprint)'
    'Operator Auth Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Operator Auth Cert Self-Signed?=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateIssuedBy'
)
Write-ElapsedAndMemUsage -Message "Mapped Security" -LogLevel PROFILE

Write-LogFile -Message "Mapping RBAC Usage..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "RBACRoles...";
$map.RBACRoles = $Global:VBOEnvironment.VBORbacRole | mde @(
    'Name'
    'Description'
    'Role Type=>RoleType'
    'Operators=>Join($.Operators.DisplayName)'
    'Selected Items=>Join($.SelectedItems.DisplayName)'
    'Excluded Items=>Join($.ExcludedItems.DisplayName)'
)
Write-ElapsedAndMemUsage -Message "Mapped RBAC" -LogLevel PROFILE

<#
Write-LogFile -Message "Mapping Permissions Check..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Permissions Check...";
$map.Permissions = $Global:VBOEnvironment.PermissionsCheck.Values | mde @(
    'Type'
    'Organization'
    'API'
    'Permission'
)#>
Write-LogFile -Message "Mapping Proxies..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Proxies...";
$map.Proxies = $Global:VBOEnvironment.VBOProxy | mde @(
    'Name=>Hostname'
    'Description'
    'Threads=>ThreadsNumber'
    'Throttling?=>if($.ThrottlingValue -gt 0) { $.ThrottlingValue.ToString() + " " + $.ThrottlingUnit } else { "disabled" }'
    'State'
    'Type'
    'Outdated?=>IsOutdated'
    'Internet Proxy=>InternetProxy.UseInternetProxy'
    #replaced Aug 11/22 with below, more efficient & accurate # 'Objects Managed_Old=>(($Global:VBOEnvironment.VBOJobSession | ? { $_.JobId -in ($Global:VBOEnvironment.VBOJob | ? { $.Id -eq $_.Repository.ProxyId }).Id} | group JobId | % { $_.Group | Measure-Object -Property Progress -Average} ).Average | measure-object -Sum ).Sum.ToString("0")'
    'Objects Managed=>(($Global:VBOEnvironment.VBOJob | ? { $.Id -eq $_.Repository.ProxyId }).Id.Guid | % {$Global:VBOEnvironment.JobSessionIndex[$_].LatestComplete.Progress} | Measure-Object -Sum).Sum'
    'OS Version=>($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).OSVersion'
    'RAM=>(($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).RAMTotalSize/1GB).ToString("###0 GB")'
    'CPUs=>($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).CPUCount'
    'Extended Logging?=>($VBOEnvironment.VBOServerComponents | ? { $.Id -eq $_.Id -and $_.Name -eq "Proxy" }).ExtendedLoggingEnabled'
)
Write-ElapsedAndMemUsage -Message "Mapped Proxies" -LogLevel PROFILE

Write-LogFile -Message "Mapping Repositories..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Repositories...";
$map.LocalRepositories = $Global:VBOEnvironment.VBORepository | mde @(
    'Bound Proxy=>($Global:VBOEnvironment.VBOProxy | ? { $_.id -eq $.ProxyId }).Hostname'
    'Name'
    'Description'
    'Type=>if($.IsLongTerm) {"Archive"} else {"Primary"}'
    'Path'
    'Object Repo=>ObjectStorageRepository'
    'Encryption?=>EnableObjectStorageEncryption'
    #Dropped Jun 17 #'Out of Sync?=>IsOutOfSync'
    #Dropped Jun 17 #'Outdated?=>IsOutdated'
    'State=>$states = Join($(if ($.IsOutOfSync) { "Out of Sync" }),$(if ($.IsOutdated) { "Out of Date" }),$(if ($.IsOutOfOrder) { "Invalid: " + $.OutOfOrderReason })); if ($states -eq "") {"Healthy"} else { $states }'
    'Capacity=>($.Capacity/1TB).ToString("#,##0.000 TB")'
    'Free=>($.FreeSpace/1TB).ToString("#,##0.000 TB")'
    'Data Stored=>$repoUsage = ($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id}); $SpaceUsed = if (($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1TB -gt 0) {($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1TB} else {($repoUsage.UsedSpace | measure -Sum).Sum/1TB}; $SpaceUsed.ToString("#,##0.000 TB")'
    'Cache Space Used=>((($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id}).LocalCacheUsedSpace | measure -Sum).Sum/1TB).ToString("#,##0.000 TB")'
    #Dropped Jun 17 #'Local Space Used=>((($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id}).UsedSpace | measure -Sum).Sum/1TB).ToString("#,##0.000 TB")'
    #Dropped Jun 17 #'Object Space Used=>((($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id}).ObjectStorageUsedSpace | measure -Sum).Sum/1TB).ToString("#,##0.000 TB")'
    #the below isnt perfect, as its based on creationtime not a true per day amount; its also based on repo change not dataset change....
    #replaced below with change to a GB rate rather than %. Then it reflects just the change in the data transferred and not against the repo storage.
    '!Calcs=>$changesByDay = ((($Global:VBOEnvironment.VBOJob | ? { $.Id -eq $_.Repository.Id }).Id.Guid | % {$Global:VBOEnvironment.JobSessionIndex[$_].LastXSessionsComplete})| select @{n="CreationDate"; e={$_.CreationTime.Date}},* | sort CreationDate -Descending | group CreationDate | % { $_.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.000000" | measure -Sum }).Sum;
        $weeklyChange = ($changesByDay | select -first 7 | measure -Sum).Sum;
        $dailyChangeAvg = ($changesByDay | measure -Average).Average;
        $dailyChangeMed = $changesByDay | 95P -Percentile 50;
        $dailyChange90p = $changesByDay | 95P -Percentile 90;'
    'Daily Change Rate=>"AVG: " + $dailyChangeAvg.ToString("#,##0.000 GB") + ";<br/>MED: " + $dailyChangeMed.ToString("#,##0.000 GB") + ";<br/>90th: " + $dailyChange90p.ToString("#,##0.000 GB")' #uses $weeklyChange from object above.
    <#replaced aug 11/22. See above #
    '!OLD Daily Change Rate=>$changesByDay = (($VBOEnvironment.VBOJobSession | ? {($VBOEnvironment.VBOJob | ? { $_.Repository.Id -eq $.Id }).Id -eq $_.JobId })| select @{n="CreationDate"; e={$_.CreationTime.Date}},* | sort CreationDate -Descending | group CreationDate | % { $_.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.000000" | measure -Sum }).Sum;
        $weeklyChange = ($changesByDay | select -first 7 | measure -Sum).Sum;
        $dailyChangeAvg = ($changesByDay | measure -Average).Average
        #$dailyChange95p = $changesByDay | 95P
        
        $repoUsage = ($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id})
        $isObject = $null -ne $.ObjectStorageRepository #($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1GB -gt 0
        $GBUsed = if ($isObject) {($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1GB} else {($repoUsage.UsedSpace | measure -Sum).Sum/1GB}

        if ($GBUsed -gt 0) {
            $DCR = $dailyChangeAvg/$GBUsed * $(if ($isObject) {0.5} else {0.9}) ################## HARD CODED COMPRESSION RATES ARE AN APPROXIMATION ##############
            $DCR.ToString("~##0.000%")
        }'
    'Daily Change Rate=>($weeklyChange/7).ToString("#,##0.000 GB")' #uses $weeklyChange from object above.
    #>
    #'!OLD_Retention=>($.RetentionPeriod.ToString() -replace "(Years?)(.+)","`$2 `$1" )+", "+$.RetentionType+", Applied "+$.RetentionFrequencyType'
    <#fixed Jul 21#>'Retention=>if ($.CustomRetentionPeriodType -eq "Months") {
        $period = ($.RetentionPeriod.value__/12);
        if ($period -le 25) {
            $retPeriod = ($.RetentionPeriod.value__/12).ToString() + " Years"
        } else {
            $retPeriod = "Forever"
        }
    } else {
        $retPeriod = $.RetentionPeriod.value__.ToString() + " Days"
    };
    return $retPeriod + ", " + $.RetentionType + ", Applied " + $.RetentionFrequencyType'
)
Write-ElapsedAndMemUsage -Message "Mapped Local Repos" -LogLevel PROFILE

Write-LogFile -Message "Mapping Object Repositories..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "ObjectRepositories...";
$map.ObjectRepositories = $Global:VBOEnvironment.VBOObjectStorageRepository | mde @(
    'Name'
    'Description'
    'Cloud=>Type'
    'Type=>if($.IsLongTerm) {"Archive"} else {"Primary"}'
    'Bucket/Container=>if($null -ne $.Folder.Container) { $.Folder.Container } else { $.Folder.Bucket }'
    'Path=>$.Folder.Path'
    'Size Limit=>if($.EnableSizeLimit) { ($.SizeLimit/1024).ToString("#,##0.00 TB")} else { "Unlimited" }'
    'Used Space=>($.UsedSpace/1TB).ToString("#,##0.00 TB")'
    'Free Space=>if($.EnableSizeLimit) { ($.FreeSpace/1TB).ToString("#,##0.00 TB") } else { "Unlimited" }'
    'Bound Repo=>($Global:VBOEnvironment.VBORepository | Where-Object {$.id -in $_.ObjectStorageRepository.Id }).Name'
)

Write-ElapsedAndMemUsage -Message "Mapped Object Storage" -LogLevel PROFILE

Write-LogFile -Message "Mapping Organizations..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Organizations...";
$map.Organizations = $Global:VBOEnvironment.VBOOrganization | mde @(
    'Friendly Name=>Name'
    'Real Name=>OfficeName'
    'Type'
    'Protected Apps=>Join($.BackupParts -replace "Office365","")'
    'EXO Settings=>$.Office365ExchangeConnectionSettings.AuthenticationType.ToString() + " (App: " + ($VBOEnvironment.VBOApplication | ? { $.Office365ExchangeConnectionSettings.ApplicationId -eq $_.Id}).DisplayName + " / User: " + ($.Office365ExchangeConnectionSettings.ImpersonationAccountName,$.Office365ExchangeConnectionSettings.UserName) +")"'
    'EXO App Cert=>$v = Get-ChildItem -Recurse cert:\* | Where-Object {$_.Thumbprint -eq $.Office365ExchangeConnectionSettings.ApplicationCertificateThumbprint}; $v.FriendlyName + " (Self-signed?: " + ($v.Subject -eq $v.Issuer) + ")"'
    'SPO Settings=>$.Office365SharePointConnectionSettings.AuthenticationType.ToString() + " (App: " + ($VBOEnvironment.VBOApplication | ? { $.Office365SharePointConnectionSettings.ApplicationId -eq $_.Id}).DisplayName + " / User: " + ($.Office365SharePointConnectionSettings.ImpersonationAccountName,$.Office365SharePointConnectionSettings.UserName) +")"'
    'SPO App Cert=>$v = Get-ChildItem -Recurse cert:\* | Where-Object {$_.Thumbprint -eq $.Office365SharePointConnectionSettings.ApplicationCertificateThumbprint}; $v.FriendlyName + " (Self-signed?: " + ($v.Subject -eq $v.Issuer) + ")"'
    'On-Prem Exch Settings=>if ($null -ne $.OnPremExchangeConnectionSettings) { ("Server: " + $.OnPremExchangeConnectionSettings.ServerName + " (" + $.OnPremExchangeConnectionSettings.UserName + ")") }'
    'On-Prem SP Settings=>if ($null -ne $.OnPremSharePointConnectionSettings) { ("Server: " + $.OnPremSharePointConnectionSettings.ServerName + " (" + $.OnPremSharePointConnectionSettings.UserName + ")") }'
    'Licensed Users=>$.LicensingOptions.LicensedUsersCount'
    'Grant SC Admin=>GrantAccessToSiteCollections'
    'Aux Accounts/Apps=>[math]::Max($.BackupAccounts.Count,$.BackupApplications.Count)'
)
Write-ElapsedAndMemUsage -Message "Mapped Orgs" -LogLevel PROFILE

Write-LogFile -Message "Mapping Jobs..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Jobs...";
$map.Jobs = @($Global:VBOEnvironment.VBOJob) + @($Global:VBOEnvironment.VBOCopyJob) | mde @(
    'Organization=>if($null -ne $.Organization) { $.Organization } else { $.BackupJob.Organization }'
    'Name'
    'Description'
    'Job Type=>if ($null -eq $.BackupJob) { "Backup" } else { "Backup Copy" }'
    'Scope Type=>JobBackupType'
    <#Fixed jul 21#>'Processing Options=>@("Mailbox","ArchiveMailbox","OneDrive","Sites","Teams","GroupMailbox","GroupSite" | Where-Object { $.SelectedItems.$_ -eq $true -or $_.Replace("Teams","Team").Replace("Sites","Site")  -in $.SelectedItems.Type }).Replace("ArchiveMailbox","Archive").Replace("GroupMailbox","Group Mailbox").Replace("GroupSite","Group Site") -join ", "'
    #'Processing Options=>@(if ($.SelectedItems.Mailbox.Count -gt 0) {"Mailbox"}; if ($.SelectedItems.ArchiveMailbox.Count -gt 0) {"Archive"}; if ($.SelectedItems.OneDrive.Count -gt 0) {"OneDrive"}; if ($.SelectedItems.Sites.Count -gt 0 -or "Site" -in $.SelectedItems.Type) {"Site"}; if ($.SelectedItems.Teams.Count -gt 0 -or "Team" -in $.SelectedItems.Type) {"Teams"}; if ($.SelectedItems.GroupMailbox.Count -gt 0) {"Group Mailbox"}; if ($.SelectedItems.GroupSite.Count -gt 0) {"Group Site"}) -join ", "'
    'Selected Objects=>$objectCountStr = $Global:VBOEnvironment.JobSessionIndex[$($.Id.Guid)].LatestComplete; [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= objects)").Value'
    'Excluded Objects=>$objectCountStr = $Global:VBOEnvironment.JobSessionIndex[$($.Id.Guid)].LatestComplete; [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= excluded objects)").Value'
    #replaced aug 11/22 with above # <#Fixed jul 21#>'Selected Objects=>$objectCountStr = ($Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobId -eq $.Id } | Sort-Object CreationTime -Descending | Select-Object -First 1); [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= objects)").Value' #OLD: used to look at defined things selected. now looks at actual resolved. #$.SelectedItems.Count
    #replaced aug 11/22 with above # #<#Fixed jul 21#>'Excluded Objects=>$objectCountStr = ($Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobId -eq $.Id } | Sort-Object CreationTime -Descending | Select-Object -First 1); [Regex]::Match($objectCountStr.Log.Title,"(?<=Found )(\d+)(?= excluded objects)").Value' #OLD: used to look at defined things selected. now looks at actual resolved. #$.ExcludedItems.Count'
    'Repository'
    'Bound Proxy=>($Global:VBOEnvironment.VBOProxy | ? { $_.id -eq $.Repository.ProxyId }).Hostname'
    'Enabled?=>IsEnabled'
    'Schedule=>if ($.SchedulePolicy.EnableSchedule -or $.SchedulePolicy.Type.ToString() -eq "Immediate") {
        if ($.SchedulePolicy.Type.ToString() -eq "Immediate") {
            "Immediate"
        } elseif ($.SchedulePolicy.Type.ToString() -eq "Daily") {
            $.SchedulePolicy.DailyType.ToString() + " @ " + $.SchedulePolicy.DailyTime.ToString()
        } else {
            $.SchedulePolicy.Type.ToString() + " every " + ($.SchedulePolicy.PeriodicallyEvery.ToString() -replace "(\w+?)(\d+)","`$2 `$1") + " (Window?: " + (($.SchedulePolicy.PeriodicallyWindowSettings.BackupWindow -eq $false).Count -gt 0) + ")"
        }
    } else { "Not scheduled" }'
    'Related Job=>if ($null -eq  $.BackupJob) { $Global:VBOEnvironment.VBOCopyJob.Name | ? { $.Name -in $Global:VBOEnvironment.VBOCopyJob.BackupJob.Name} } else { $.BackupJob.Name }'
)
Write-ElapsedAndMemUsage -Message "Mapped Jobs" -LogLevel PROFILE

Write-LogFile -Message "Mapping Job Stats..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "JobStats...";
#replaced aug 11/22 with below # $map.JobStats = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name} | Group-Object JobName | mde @(
$map.JobStats = ($Global:VBOEnvironment.VBOJob).Id.Guid | % {$Global:VBOEnvironment.JobSessionIndex[$_].LastXSessionsComplete } | Group-Object JobName | mde @(
    'Name'
    'Average Duration (min)=>$totalMin = ($.Group | select *,@{n="Duration";e={(($_.EndTime | GetEndTime)-$_.CreationTime).TotalMinutes}} | ? { $_.Duration -gt 0 } | measure Duration -Average).Average; $totalMin*60 | ToLongHHmmss'
    'Max Duration (min)=>$totalMin = ($.Group | select *,@{n="Duration";e={(($_.EndTime | GetEndTime)-$_.CreationTime).TotalMinutes}} | ? { $_.Duration -gt 0 } | measure Duration -Maximum).Maximum; $totalMin*60 | ToLongHHmmss'
    'Average Data Transferred=>(($.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.0000" | measure -Average).Average.ToString("#,##0.000 GB"))'
    'Max Data Transferred=>(($.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.0000" | measure -Maximum).Maximum.ToString("#,##0.000 GB") )'
    'Average Objects (#)=>($.Group.Progress | measure -Average).Average.ToString("0")'
    'Max Objects (#)=>($.Group.Progress | measure -Maximum).Maximum.ToString("0")'
    'Average Items (#)=>($.Group.Statistics | measure ProcessedObjects -Average).Average.ToString("0")'
    'Max Items (#)=>($.Group.Statistics | measure ProcessedObjects -Maximum).Maximum.ToString("0")'
    <# The columns below removed as they are using the in-built stats, which are point-in-time, and thus not very informative for this. Excluded...
        'Average Processing Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)\)","`$1" | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
        'Max Processing Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)\)","`$1" | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
        'Average Item Proc Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)items/s\)","`$2" | measure -Average).Average.ToString("0.0 items/s"))'
        'Max Item Proc Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)items/s\)","`$2" | measure -Maximum).Maximum.ToString("0.0 items/s") )'
        'Average Read Rate=>(($.Group.Statistics.ReadRate | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
        'Max Read Rate=>(($.Group.Statistics.ReadRate | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
        'Average Write Rate=>(($.Group.Statistics.WriteRate | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
        'Max Write Rate=>(($.Group.Statistics.WriteRate | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
    #>
    'Typical Bottleneck=>($.Group.Statistics.Bottleneck | group | sort Count -Descending | select -first 1).Name.Replace("NA","None")'
    'Job Avg Throughput=>(($.Group.Statistics.TransferredData | ConvertData -To "MB" -Format "0.0000" | measure -Sum).Sum / ($.Group | select @{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } ).TotalSeconds}} | ? { $_.Duration -gt 0 } | measure Duration -Sum).Sum).ToString("#,##0.000 MB/s")'
    'Job Avg Processing Rate=>(($.Group.Statistics | measure ProcessedObjects -Sum).Sum / ($.Group | select @{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } ).TotalSeconds}} | ? { $_.Duration -gt 0 } | measure Duration -Sum).Sum).ToString("#,##0.000 items/s")'
)
Write-ElapsedAndMemUsage -Message "Mapped Job Stats" -LogLevel PROFILE

Write-LogFile -Message "Mapping Processing/Task Stats..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "ProcessingStats...";
$global:CollectorCurrentProcessItems = $Global:VBOEnvironment.VBOJobSession.Log.Count
# replaced aug 11/22 w/ below # $map.ProcessingStats = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name} | Group-Object JobName | mde @(
$map.ProcessingStats = ($Global:VBOEnvironment.VBOJob).Id.Guid | % {$Global:VBOEnvironment.JobSessionIndex[$_].LastXSessionsComplete } | Group-Object JobName | mde @(   
    '!Vars=>
        $PRIVATE:Vars = @{
            Times = @{
                Startup=[System.Collections.ArrayList]@()
                Exclude=[System.Collections.ArrayList]@()
                Found=[System.Collections.ArrayList]@()
                "Processing per Object"=[System.Collections.ArrayList]@()
                "Processing per Session"=[System.Collections.ArrayList]@()
                Wrapup=[System.Collections.ArrayList]@()
            }
            Stats = @{}
        }
        
        foreach ($session in $.Group) {
            $ObjectEntries = @()

            $ExcludeEntry = $session.Log -imatch "Found \d+ excluded objects"
            $FoundEntry = $session.Log -imatch "Found \d+ objects"
            $SummaryEntry = $session.Log -imatch "Transferred: \d+"
            $ObjectEntries = $session.Log -imatch "(?<!\[Running\].+)Processing .+:"

            if ($FoundEntry.Count -gt 0) {
                #there should always be a found entry, unless a running job, thus we should only add stats if its !$null.
                
                $startupSeconds = (($FoundEntry.EndTime | GetEndTime) - $session.CreationTime).TotalSeconds
                if ($startupSeconds -gt 0) { $PRIVATE:Vars.Times.Startup.Add($startupSeconds)}
                $foundSeconds = (($FoundEntry.EndTime | GetEndTime) - $FoundEntry.CreationTime).TotalSeconds
                if ($foundSeconds -gt 0) {$PRIVATE:Vars.Times.Found.Add($foundSeconds)}
            }
                
            if ($ExcludeEntry.Count -gt 0) {
                $excludeSeconds = (($ExcludeEntry.EndTime | GetEndTime) - $ExcludeEntry.CreationTime).TotalSeconds 
                if ($excludeSeconds -gt 0) { $PRIVATE:Vars.Times.Exclude.Add($excludeSeconds)} }
            if ($SummaryEntry.Count -gt 0) {
                $wrapSeconds = (($session.EndTime | GetEndTime) - $SummaryEntry.CreationTime).TotalSeconds
                if ($wrapSeconds -gt 0) { $PRIVATE:Vars.Times.Wrapup.Add($wrapSeconds)} }
            
            foreach ($objEntry in $ObjectEntries) {
                GetItemsLeft
                $taskSeconds = (($objEntry.EndTime | GetEndTime) - $objEntry.CreationTime).TotalSeconds
                if ($taskSeconds -gt 0) { $PRIVATE:Vars.Times."Processing per Object".Add($taskSeconds) }
            } # grab time for every processed object
            
            $firstObjEntry = $ObjectEntries | Sort-Object CreationTime | Select-Object -First 1
            $lastObjEntry = $ObjectEntries | Sort-Object CreationTime | Select-Object -Last 1

            $totalSessionTime = (($lastObjEntry.EndTime | GetEndTime)-$firstObjEntry.CreationTime).TotalSeconds
            $PRIVATE:Vars.Times."Processing per Session".Add($totalSessionTime)

            #Deprecated with the above fix # $PRIVATE:Vars.Times."Processing per Session".Add(($PRIVATE:Vars.Times."Processing per Object" | Measure-Object -Sum).Sum)
        }
        #Time Analysis
        foreach ($statkey in $PRIVATE:Vars.Times.Keys) {
            $measurements = $PRIVATE:Vars.Times.$statkey | Measure-Object -Average -Minimum -Maximum  #use gt 0 to account for problems with daylight ssavings negative values skewing results
            
            $PRIVATE:Vars.Stats.$statkey = @{}
            $PRIVATE:Vars.Stats.$statkey.Latest = ($PRIVATE:Vars.Times.$statkey) | select-object -first 1
            $PRIVATE:Vars.Stats.$statkey.Average = $measurements.Average
            $PRIVATE:Vars.Stats.$statkey.Minimum = $measurements.Minimum
            $PRIVATE:Vars.Stats.$statkey.Maximum = $measurements.Maximum
            $PRIVATE:Vars.Stats.$statkey.Median = ,$PRIVATE:Vars.Times.$statkey | 95p -Percentile 50
            $PRIVATE:Vars.Stats.$statkey.Ninety = ,$PRIVATE:Vars.Times.$statkey | 95p -Percentile 90
        }'
    'Name'
    'Startup Time (Latest)=>($PRIVATE:Vars.Stats.Startup.Latest) | ToLongHHmmss'
    'Startup Time (Median)=>($PRIVATE:Vars.Stats.Startup.Median) | ToLongHHmmss'
    'Startup Time (Min)=>($PRIVATE:Vars.Stats.Startup.Minimum) | ToLongHHmmss'
    'Startup Time (Avg)=>($PRIVATE:Vars.Stats.Startup.Average) | ToLongHHmmss'
    'Startup Time (Max)=>($PRIVATE:Vars.Stats.Startup.Maximum) | ToLongHHmmss'
    'Startup Time (90%)=>($PRIVATE:Vars.Stats.Startup.Ninety) | ToLongHHmmss'
    'Exclude Time (Latest)=>($PRIVATE:Vars.Stats.Exclude.Latest) | ToLongHHmmss'
    'Exclude Time (Median)=>($PRIVATE:Vars.Stats.Exclude.Median) | ToLongHHmmss'
    'Exclude Time (Min)=>($PRIVATE:Vars.Stats.Exclude.Minimum) | ToLongHHmmss'
    'Exclude Time (Avg)=>($PRIVATE:Vars.Stats.Exclude.Average) | ToLongHHmmss'
    'Exclude Time (Max)=>($PRIVATE:Vars.Stats.Exclude.Maximum) | ToLongHHmmss'
    'Exclude Time (90%)=>($PRIVATE:Vars.Stats.Exclude.Ninety) | ToLongHHmmss'
    'Found Time (Latest)=>($PRIVATE:Vars.Stats.Found.Latest) | ToLongHHmmss'
    'Found Time (Median)=>($PRIVATE:Vars.Stats.Found.Median) | ToLongHHmmss'
    'Found Time (Min)=>($PRIVATE:Vars.Stats.Found.Minimum) | ToLongHHmmss'
    'Found Time (Avg)=>($PRIVATE:Vars.Stats.Found.Average) | ToLongHHmmss'
    'Found Time (Max)=>($PRIVATE:Vars.Stats.Found.Maximum) | ToLongHHmmss'
    'Found Time (90%)=>($PRIVATE:Vars.Stats.Found.Ninety) | ToLongHHmmss'
    'Processing per Object Time (Latest)=>($PRIVATE:Vars.Stats."Processing per Object".Latest) | ToLongHHmmss'
    'Processing per Object Time (Median)=>($PRIVATE:Vars.Stats."Processing per Object".Median) | ToLongHHmmss'
    'Processing per Object Time (Min)=>($PRIVATE:Vars.Stats."Processing per Object".Minimum) | ToLongHHmmss'
    'Processing per Object Time (Avg)=>($PRIVATE:Vars.Stats."Processing per Object".Average) | ToLongHHmmss'
    'Processing per Object Time (Max)=>($PRIVATE:Vars.Stats."Processing per Object".Maximum) | ToLongHHmmss'
    'Processing per Object Time (90%)=>($PRIVATE:Vars.Stats."Processing per Object".Ninety) | ToLongHHmmss'
    'Processing per Session Time (Latest)=>($PRIVATE:Vars.Stats."Processing per Session".Latest) | ToLongHHmmss'
    'Processing per Session Time (Median)=>($PRIVATE:Vars.Stats."Processing per Session".Median) | ToLongHHmmss'
    'Processing per Session Time (Min)=>($PRIVATE:Vars.Stats."Processing per Session".Minimum) | ToLongHHmmss'
    'Processing per Session Time (Avg)=>($PRIVATE:Vars.Stats."Processing per Session".Average) | ToLongHHmmss'
    'Processing per Session Time (Max)=>($PRIVATE:Vars.Stats."Processing per Session".Maximum) | ToLongHHmmss'
    'Processing per Session Time (90%)=>($PRIVATE:Vars.Stats."Processing per Session".Ninety) | ToLongHHmmss'
    'Wrapup Time (Latest)=>($PRIVATE:Vars.Stats.Wrapup.Latest) | ToLongHHmmss'
    'Wrapup Time (Median)=>($PRIVATE:Vars.Stats.Wrapup.Median) | ToLongHHmmss'
    'Wrapup Time (Min)=>($PRIVATE:Vars.Stats.Wrapup.Minimum) | ToLongHHmmss'
    'Wrapup Time (Avg)=>($PRIVATE:Vars.Stats.Wrapup.Average) | ToLongHHmmss'
    'Wrapup Time (Max)=>($PRIVATE:Vars.Stats.Wrapup.Maximum) | ToLongHHmmss'
    'Wrapup Time (90%)=>($PRIVATE:Vars.Stats.Wrapup.Ninety) | ToLongHHmmss'
)
#transform the output for CSV
$map.ProcessingStats = ($map.ProcessingStats | ForEach-Object { foreach ($op in @("Startup","Exclude","Found","Processing per Object","Processing per Session","Wrapup")) { $_ | Select-Object Name,@{n="Operation";e={$op}},*$op* }} | ConvertTo-Json) -replace "Startup\s|Exclude\s|Found\s|Processing per Object\s|Processing per Session\s|Wrapup\s","" | convertfrom-json
Write-ElapsedAndMemUsage -Message "Mapped Processing/Task Stats" -LogLevel PROFILE
    
Write-LogFile -Message "Mapping Job Sessions..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "JobSessions...";
$map.JobSessions = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name} | Sort-Object @{Expression={$_.JobName}; Descending=$false },@{Expression={$_.CreationTime}; Descending=$true } | mde @(
    'Name=>JobName'
    'Status'
    'Start Time=>$.CreationTime.ToString("yyyy/MM/dd HH:mm:ss")'
    'End Time=>$.EndTime.ToString("yyyy/MM/dd HH:mm:ss")'
    'Duration=>$sessWithDuration =  $. | select *,@{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } )}}; $sessWithDuration.Duration.TotalSeconds | ToLongHHmmss'
    'Log=>Join -Array $($.Log.Title | ? { !$_.Contains("[Success]") }) -Delimiter "`r`n"'
)
Write-ElapsedAndMemUsage -Message "Mapped Sessions" -LogLevel PROFILE

Write-LogFile -Message "Mapping Protection Status..." -LogLevel DEBUG
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Protection Status...";
<#$map.ProtectionStatus = (Get-Item -Path ($global:SETTINGS.OutputPath + "\Veeam_MailboxProtectionReport*.csv")).FullName | Import-Csv | Where-Object { $_."Protection Status" -eq "Unprotected" -or $_."Last Backup Date" -lt (Get-date).AddDays(-$global:SETTINGS.ReportingIntervalDays)} | Sort-Object "Protection Status",User -Descending | mde @(
    'User=>Mailbox'
    'E-mail=>$."E-mail"'
    'Organization'
    'Protection Status=> if ($."Protection Status" -eq "Protected") { "Stale Backup" } else { "Unprotected" }'
    'Last Backup Date=>$."Last Backup Date"'
)#>

$global:CollectorCurrentProcessItems = $Global:VBOEnvironment.VBOOrganizationUsers.Count
$map.ProtectionStatus = $Global:VBOEnvironment.VBOOrganizationUsers | Select-Object -ExcludeProperty "IsBackedup" | mde @(
    '!Profiling=>$(GetItemsLeft)'
    'Organization=>($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name'
    '!Vars=>
        $entityKey = ($.DisplayName+"-"+$.UserName).Replace(" ","")
        $PRIVATE:Entity = $Global:VBOEnvironment.EntitiesIndex[$self.Organization][$entityKey]
    '
    #'Office ID=>OfficeId'
    #'On-Prem ID=>OnPremisesId'
    'Display Name=>DisplayName'
    'Username=>UserName'
    'Type' # user/sharedmailbox/etc
    'Location=>LocationType'
    'Mail Backup Date=>$PRIVATE:Entity.MailboxBackedUpTime.DateTime'
    'Archive Backup Date=>$PRIVATE:Entity.ArchiveBackedUpTime.DateTime'
    'Onedrive Backup Date=>$PRIVATE:Entity.OneDriveBackedUpTime.DateTime'
    'Site Backup Date=>$PRIVATE:Entity.PersonalSiteBackedUpTime.DateTime'
    'Has Backup=>!!(($PRIVATE:Entity.MailboxBackedUpTime, $PRIVATE:Entity.ArchiveBackedUpTime, $PRIVATE:Entity.OneDriveBackedUpTime, $PRIVATE:Entity.PersonalSiteBackedUpTime) | measure-Object -Maximum).Maximum'
    'Is Stale=> if ($self."Has Backup") { (($PRIVATE:Entity.MailboxBackedUpTime, $PRIVATE:Entity.ArchiveBackedUpTime, $PRIVATE:Entity.OneDriveBackedUpTime, $PRIVATE:Entity.PersonalSiteBackedUpTime) | measure-Object -Maximum).Maximum -lt (Get-date).AddDays(-$global:SETTINGS.ReportingIntervalDays) }'
)
Write-ElapsedAndMemUsage -Message "Mapped Protection Status" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -Status "Done." -Completed


######### END MAPS ############


Write-ElapsedAndMemUsage -Message "Done Mapping. Aggregating next." -LogLevel DEBUG
Write-LogFile -Message "Done mapping CSV structures." -LogLevel INFO
Write-LogFile -Message "Aggregating results..." -LogLevel INFO

# Build the objects & sections
$Global:HealthCheckResult = MDE $map.Keys -Empty

foreach ($sectionName in $map.Keys) {
    $section = $map.$sectionName

    if ($null -eq $section) {
        Write-Warning "No map found for: "+$sectionName+". Please define."
        Write-LogFile -Message ("No map found for: "+$sectionName+". Please define.") -LogName Errors -LogLevel ERROR
    } else {
        if ($section.GetType().Name -eq "PSCustomObject" -or $section.GetType().Name -eq "Object[]") {
            $Global:HealthCheckResult.$sectionName = $section
        } else {
            $Global:HealthCheckResult.$sectionName = MDE $section
        }

        if ($global:SETTINGS.DebugInConsole) {
            Write-Host -ForegroundColor Green "SECTION: $($sectionName.ToUpper())"
            $Global:HealthCheckResult.$sectionName | Format-Table *
        }
    }
}


Write-ElapsedAndMemUsage -Message "Done Aggregating. Exporting Next." -LogLevel DEBUG
Write-LogFile -Message "Done Aggregating." -LogLevel INFO
Write-LogFile -Message "Exporting to CSVs..." -LogLevel INFO

$Global:HealthCheckResult.psobject.Properties.Name | ForEach-Object { $Global:HealthCheckResult.$_ | ConvertTo-Csv -NoTypeInformation | Out-File $($global:SETTINGS.OutputPath.Trim('\') + "\" +$_+".csv") -Force }

Write-ElapsedAndMemUsage -Message "Done export" -LogLevel DEBUG
Write-LogFile -Message "Done Exporting to CSVs." -LogLevel INFO

if ($Error.Count -gt 0) {
    Write-LogFile -Message "Some errors were encountered. See 'CollectorErrors.log'" -LogLevel INFO
    Write-LogFile -Message "All Errors:" -LogName Errors -LogLevel DEBUG
    $Error | ForEach-Object {Write-LogFile -Message ($_.ToString() + "`r`n" + $_.InvocationInfo.Line.ToString() + "`r`n" + $_.ScriptStackTrace.ToString()+ "`r`n" +$_.Exception.StackTrace.ToString()) -LogName Errors -LogLevel DEBUG }
}

Write-ResourceUsageToLog -Message "All done"
Write-LogFile -Message "Finished." -LogLevel INFO
Write-LogFile -Message "Time Elapsed: $($CollectorTimer.Elapsed.TotalSeconds | ToLongHHmmss)" -LogLevel INFO
Write-LogFile -Message "Environment: Proxies=$(@($HealthCheckResult.Proxies).Count); Repos=$(@($HealthCheckResult.LocalRepositories).Count); ObjRepos=$(@($HealthCheckResult.ObjectRepositories).Count); Jobs=$(@($HealthCheckResult.Jobs).Count); Sessions=$(@($VBOEnvironment.VBOJobSession).Count); Objects=$(($HealthCheckResult.Proxies | Measure-Object 'Objects Managed' -Sum).Sum); Users=$(@($HealthCheckResult.ProtectionStatus).Count); Entities=$(@($VBOEnvironment.VBOEntityData).Count)" -LogLevel INFO
Write-LogFile -Message $($proc = (get-process -Id $PID); "CPU (s): " + $proc.CPU.ToString() + " / Private Mem (MB): " +$proc.PM/1MB + " / Working Set (MB): " + $proc.WorkingSet/1MB) -LogLevel INFO
Write-LogFile -Message ("CPU Average: " + ($proc.CPU / $CollectorTimer.Elapsed.TotalSeconds / ($processor | Measure-Object -Sum NumberOfLogicalProcessors).Sum).ToString("0.00 %")) -LogLevel INFO
$memory2 =  (Get-ComputerInfo -Property *Memory*)
Write-LogFile -LogLevel INFO -Message ("System Memory: " + [Math]::Round($memory2.OsFreePhysicalMemory/1MB,2) + " GB free of " + $memory2.CsPhyicallyInstalledMemory/1MB + " GB; Delta=" + [math]::Round(($memory2.OsFreePhysicalMemory-$memory.OsFreePhysicalMemory)/1MB,2) + "GB")

$stopwatch.Stop()
$CollectorTimer.Stop()

[GC]::Collect()

if ([LogLevel]$global:SETTINGS.LogLevel -le [LogLevel]::DEBUG ) {
    Compress-Archive -Path ($Global:SETTINGS.OutputPath+"\..\..\") -DestinationPath ($Global:SETTINGS.OutputPath+"\..\..\DebugCollection.zip") -CompressionLevel Optimal -Force
}

Start-Sleep -Seconds 10
if ($null -ne $logWatcher -and !$logWatcher.HasExited) {
    $logWatcher.Kill()
}
if ($null -ne $taskmgr -and !$taskmgr.HasExited) {
    $taskmgr.Kill()
}