<#param (
    [bool]$Debug,
    [string]$OutputPath = "",
    [int]$ReportingIntervalDays = -1,
    [string]$VBOServerFqdnOrIp = "",
    [switch]$PersistParams = $false
)#>
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
    [string]VBOServerFqdnOrIp -- dont use/change this unless, you're writing this code remote from VB365 server.
    [string]LogLevel to change verbosity of logging. Log messages with level above or equal to this are output. DEBUG shows minimal debugging & profiling; PROFILE outputs detailed timers & resource usage; TRACE outputs "No Result" warnings & log
#>  

$global:SETTINGS = '{"LogLevel":"INFO","OutputPath":"C:\\temp\\vHC\\Original\\VB365","ReportingIntervalDays":7,"VBOServerFqdnOrIp":"localhost"}'<#,"SkipCollect":false,"ExportJson":false,"ExportXml":false,"DebugInConsole":false,"Watch":false}#> | ConvertFrom-Json
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
$global:SETTINGS | ConvertTo-Json | Out-File -FilePath ($global:SETTINGS.OutputPath + "\CollectorConfig.json") -Force

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
    #$netWatcher = Start-Process powershell.exe -ArgumentList '-NoExit -Command "& { while ($true) { clear-host; write-host; netstat -ano -p TCP | select-string -Pattern $((Get-Process -Name Veeam*).Id -join ''| '') | Select-String -NotMatch -Pattern ''(?:10|192|172|127)\.\d+\.\d+\.\d+:\d+\s+(?:10|192|172|127)\.\d+\.\d+\.\d+:\d+\s+''; sleep -seconds 3} }"' -PassThru
    #Start-Process perfmon.exe -ArgumentList "/res"
}

$process = Get-Process -Id $pid
$process.PriorityClass = 'BelowNormal'
#$process.ProcessorAffinity = 2

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

                    #Write-LogFile -Message $message -LogName Errors
                    Write-LogFile -Message $message -LogName NoResult -LogLevel TRACE
                    Write-LogFile -Message "`tExpanded Expression: $expandedExpression" -LogName NoResult -LogLevel TRACE
                }
            } catch {

                $message = "$expression not valid."
                if ($global:SETTINGS.DebugInConsole) {
                    Write-Warning "$expression not valid."
                    ($Error | Select-Object -Last 1).ToString()
                }

                #Write-LogFile -Message $message -LogLevel ERROR
                Write-LogFile -Message $message -LogName Errors -LogLevel ERROR
                Write-LogFile -Message "`tExpanded Expression: $expandedExpression" -LogName Errors -LogLevel ERROR
                Write-LogFile -Message ($Error | Select-Object -Last 1).ToString() -LogName Errors -LogLevel ERROR

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
        [Parameter(ValueFromPipeline)]$Object
        )
    begin { }
    process {
        $OutputObject = New-Object -TypeName pscustomobject

        $parsedResults = Expand-Expression -BaseObject $Object -Expressions $PropertyNames

        foreach ($result in $parsedResults) {
            
            if (!$result.ColumnName.StartsWith("!")) {
                $OutputObject | Add-Member -MemberType NoteProperty -Name $result.ColumnName -Value $result.Value
            }

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
        

        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Starting...";

        #Org settings:
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting organization..."
        $e.VBOOrganization = Get-VBOOrganization
        Write-ElapsedAndMemUsage -Message "Collected: VBOOrganization" -LogLevel PROFILE
            # we can optionally collect user, site, group,team org info. excluded for now: https://helpcenter.veeam.com/docs/vbo365/powershell/organization_items.html?ver=60
        $e.VBOOrganizationUsers = $e.VBOOrganization | Get-VBOOrganizationUser
        Write-ElapsedAndMemUsage -Message "Collected: VBOOrganizationUsers" -LogLevel PROFILE
        $e.VBOApplication = $e.VBOOrganization | Get-VBOApplication
        Write-ElapsedAndMemUsage -Message "Collected: VBOApplication" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBOBackupApplication = $e.VBOOrganization `
            #Diabled Jun1/22 - Not used #| Where-Object {$_.Office365ExchangeConnectionSettings.AuthenticationType -ne [Veeam.Archiver.PowerShell.Model.Enums.VBOOffice365AuthenticationType]::Basic} `
            #Diabled Jun1/22 - Not used #| Get-VBOBackupApplication
        Write-ResourceUsageToLog -Message "Org collected"

        #infra settings:
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting backup infrastructure..."
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
        #Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting object storage & cloud settings..."
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
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting Job settings..."
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

        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting entity details..."
        #$e.VBOEntityData = [Veeam.Archiver.PowerShell.Cmdlets.DataManagement.VBOEntityDataType].GetEnumNames() `
        #    | ForEach-Object { $e.VBORepository | Get-VBOEntityData -Type $_ -WarningAction SilentlyContinue} | select *,@{n="Repository",} #slow/long-running
        $e.VBOEntityData = $(
            foreach ($repo in $e.VBORepository) {
                foreach ($entityType in @('Mailbox','OneDrive','Group','Site','Team','User')) { #[Veeam.Archiver.PowerShell.Cmdlets.DataManagement.VBOEntityDataType].GetEnumNames()
                    #Didnt work as hoped # $repo | Where-Object {!($_.IsOutOfOrder -and $null -ne $_.ObjectStorageRepository)} | Get-VBOEntityData -Type $entityType -WarningAction SilentlyContinue | Select-Object Email,@{n="DisplayName";e={$_.Organization.DisplayName}},*BackedUpTIme,Is*BackedUp,@{n="Repository";e={@{Id=$repo.Id;Name=$repo.Name}}},@{n="Proxy";e={$proxy=($e.VBOProxy | Where-Object { $_.id -eq $repo.ProxyId}); @{Id=$proxy.Id;Name=$proxy.Hostname}}} #slow/long-running. Testing Jun 24 for collecting specific fields only. Exclude for IsOutOfOrder object repos added Jun 15
                    $repo | Where-Object {!($_.IsOutOfOrder -and $null -ne $_.ObjectStorageRepository)} | Get-VBOEntityData -Type $entityType -WarningAction SilentlyContinue | Select-Object *,@{n="Repository";e={@{Id=$repo.Id;Name=$repo.Name}}},@{n="Proxy";e={$proxy=($e.VBOProxy | Where-Object { $_.id -eq $repo.ProxyId}); @{Id=$proxy.Id;Name=$proxy.Hostname}}} #slow/long-running. Exclude for IsOutOfOrder object repos added Jun 15
                }
            }
        )
        #$e.VBOUserEntityData = Get-VBORepository | Get-VBOEntityData -Type User
        Write-ElapsedAndMemUsage -Message "Collected: VBOEntityData" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "Entities collected"

        #backups
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting restore points..."
        #Diabled Jun1/22 - Not used #$e.VBORestorePoint = Get-VBORestorePoint
        Write-ResourceUsageToLog -Message "RPs collected"
        #Global
        Lap "Collection: Global settings..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting global settings..."
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
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting security settings..."
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
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Collecting sessions & statistics..."
        $e.VBOJobSession = Get-VBOJobSession | Where-Object { $_.EndTime -gt [DateTime]::Now.AddDays(-$global:SETTINGS.ReportingIntervalDays) }
        Write-ElapsedAndMemUsage -Message "Collected: VBOJobSession" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBORestoreSession = Get-VBORestoreSession
        $e.VBOUsageData = $e.VBORepository | Where-Object {!($_.IsOutOfOrder -and $null -ne $_.ObjectStorageRepository)} | ForEach-Object { Get-VBOUsageData -Repository $_ } # added OutOfOrder exclusion for broken repos.
        Write-ElapsedAndMemUsage -Message "Collected: VBOUsageData" -LogLevel PROFILE
        #Diabled Jun1/22 - Not used #$e.VBODataRetrievalSession = Get-VBODataRetrievalSession
        Write-ResourceUsageToLog -Message "Sessions & stats collected"

        #reports
        #Lap "Collection: Generating Reports..."
        #Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Generating reports..."
        #Diabled Jun1/22 - No longer used #Get-ChildItem -Path ($global:SETTINGS.OutputPath + "\Veeam_*Report*.csv") | Remove-Item -Force
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOMailboxProtectionReport -Organization $_ -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOStorageConsumptionReport -StartTime (Get-Date).AddDays(-$global:SETTINGS.ReportingIntervalDays) -EndTime (Get-Date) -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Diabled Jun1/22 - No longer used #$e.VBOOrganization | ForEach-Object { Get-VBOLicenseOverviewReport -StartTime (Get-Date).AddDays(-$global:SETTINGS.ReportingIntervalDays) -EndTime (Get-Date) -Path $global:SETTINGS.OutputPath -Format CSV; Start-Sleep -Seconds 1.1 }
        #Write-ResourceUsageToLog -Message "Reports generated"

        Lap "Collection: Parsing VMC Log..."
        Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Parsing VMC Log..."
        $e.VMCLog = Import-VMCFile
        Write-ElapsedAndMemUsage -Message "Collected: VMCLog" -LogLevel PROFILE
        Write-ResourceUsageToLog -Message "VMC Log Parsed"

        #Lap "Collection: Parse Job Logs..."
        #Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Parsing Job Log..."
        #Diabled Jun1/22 - Needs improvement #$e.PermissionsCheck = Import-PermissionsFromLogs -Jobs $e.VBOJob
        #Write-ResourceUsageToLog -Message "Job Logs Parsed"

        Write-Progress @progressSplat -PercentComplete 100 -CurrentOperation "Done"
        Start-Sleep -Seconds 1
        Write-Progress @progressSplat -PercentComplete 100 -CurrentOperation "Done" -Completed

        return $e
    }
    end { }
}

############## START OF MAIN EXECUTION  ################
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

#check if path exists
if (!(Test-Path $global:SETTINGS.OutputPath)) {
    New-Item -ItemType Directory -Path $global:SETTINGS.OutputPath
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

#Write-Host "VB365 Environment stats collected."
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

            return $false; #if no other return true first, then result to false
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
        $allItems = @()
    }
    process {
        $allItems += $Items;
    }
    end {
        if ([string]::IsNullOrEmpty($Property)) {
            $sortedItems = $allItems | Sort-Object
        } else {
            $sortedItems = $allItems.$Property | Sort-Object
        }

        $result = $sortedItems | Select-Object -First ([math]::Floor(($allItems.Count*$Percentile/100)))

        return $result | Select-Object -Last 1
    }
}

################################ HERE IS WHERE THE PROPERTY MAPPING INTERPRETER STARTS ################################

#USAGE examples:
# 'Name'                                                            :: will populate the column name as "Name", and the value from the passed in object (BaseObject)
# 'Name=>Hostname'                                                  :: will populate the column name as "Name", and the value from "Hostname" property of the passed in object (BaseObject)
# 'Name=>$.Hostname'                                                :: use of "$." is the shorthand for the base object
# 'Listener=>Host + ":" + Port'                                     :: use of an expression that includes '+"' or "+ "' is a simple way to concatenate two or more of the same BaseObject's properties together with a string.
# 'Name=>if (x) { $.Hostname } else { $othervariable.othername }'   :: advanced expression will be evaluated/invoked from the string to a command. You must use the "$." short hand for the BaseObject's properties.
    # This example would set the Name column to either the baseobject's "Hostname" property or to the $othervariable's "othername" property depending on whether X is true.
    # These expressions can be as advanced as you like, as long as they evaluate back to a single result
# NOTE: You can use $self.columnName to refer to a previously created column (must be above where $self is used)
# NOTE: You can add a "!" sign before a column name to have it not include in the returned object

Write-LogFile "Mapping CSV Structures..." -LogLevel INFO
Write-ElapsedAndMemUsage -Message "Start Mapping"
$progress=0
$progressSplat = @{Id=2; Activity="Building maps..."}
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Starting...";

$map = [ordered]@{}
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Management Server...";
$map.Controller = $Global:VBOEnvironment.VMCLog.HostDetails | mde @(
    'VB365 Version=>$Global:VBOEnvironment.VMCLog.ProductDetails.Version'
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
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Global...";
$map.Global = $Global:VBOEnvironment.VBOLicense | mde @(
    'License Status=>Status'
    'License Expiry=>$.ExpirationDate.ToShortDateString()'
    'License Type=>Type'
    'Licensed To=>LicensedTo'
    'License Contact=>ContactPerson'
    'Licensed For=>TotalNumber'
    'Licenses Used=>UsedNumber'
    'Support Expiry=>$.SupportExpirationDate.ToShortDateString()'
    'Global Folder Exclusions=>Join(($Global:VBOEnvironment.VBOFolderExclusions.psobject.Properties | ? { $_.Value -eq $true}).Name)'
    'Global Ret. Exclusions=>Join(($Global:VBOEnvironment.VBOGlobalRetentionExclusion.psobject.Properties | ? { $_.Value -eq $true}).Name)'
    'Log Retention=>if($Global:VBOEnvironment.VBOHistorySettings.KeepAllSessions) { "Keep All" } else {$Global:VBOEnvironment.VBOHistorySettings.KeepOnlyLastXWeeks }'
    'Notification Enabled=>$Global:VBOEnvironment.VBOEmailSettings.EnableNotification'
    'Notifify On=>Join((($Global:VBOEnvironment.VBOEmailSettings | select NotifyOn*).psobject.Properties | ? { $_.Value -eq $false}).Name)'
    'Automatic Updates?=>$Global:VBOEnvironment.VMCLog.SettingsDetails.UpdatesAutoCheckEnabled'
)
Write-ElapsedAndMemUsage -Message "Mapped Global" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Security...";
$map.Security = $null | mde @(
    'Win. Firewall Enabled?=>$v = ((Get-NetConnectionProfile).NetworkCategory -replace "Authenticated","" | % {Get-NetFirewallProfile -Name $_}); Join( $v | % { $_.Name +": " + $_.Enabled } )'
    'Internet proxy?=>$v=$Global:VBOEnvironment.VBOInternetProxySettings; if($v.UseInternetProxy) { $v.Host+":"+$v.Port } else { $false}'
    'Server Cert=>$Global:VBOEnvironment.VBOSecuritySettings.CertificateFriendlyName'
    'Server Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOSecuritySettings.CertificateThumbprint)'
    'Server Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOSecuritySettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Server Cert Self-Signed?=>$Global:VBOEnvironment.VBOSecuritySettings.CertificateIssuedTo -eq $Global:VBOEnvironment.VBOSecuritySettings.CertificateIssuedBy'
    'API Enabled?=>$Global:VBOEnvironment.VBORestAPISettings.IsServiceEnabled'
    'API Port=>$Global:VBOEnvironment.VBORestAPISettings.HTTPSPort'
    'API Cert=>$Global:VBOEnvironment.VBORestAPISettings.CertificateFriendlyName'
    'API Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBORestAPISettings.CertificateThumbprint)'
    'API Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBORestAPISettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'API Cert Self-Signed?=>$Global:VBOEnvironment.VBORestAPISettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBORestAPISettings.CertificateIssuedBy'
    'Tenant Auth Enabled?=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.AuthenticationEnabled'
    'Tenant Auth Cert=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateFriendlyName'
    'Tenant Auth PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateThumbprint)'
    'Tenant Auth Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Tenant Auth Cert Self-Signed?=>$Global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBOTenantAuthenticationSettings.CertificateIssuedBy'
    'Restore Portal Enabled?=>$Global:VBOEnvironment.VBORestorePortalSettings.IsServiceEnabled'
    'Restore Portal Cert=>$Global:VBOEnvironment.VBORestorePortalSettings.CertificateFriendlyName'
    'Restore Portal Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBORestorePortalSettings.CertificateThumbprint)'
    'Restore Portal Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBORestorePortalSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Restore Portal Cert Self-Signed?=>$Global:VBOEnvironment.VBORestorePortalSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBORestorePortalSettings.CertificateIssuedBy'
    'Operator Auth Enabled?=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.AuthenticationEnabled'
    'Operator Auth Cert=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateFriendlyName'
    'Operator Auth Cert PK Exportable?=>Test-CertPKExportable($Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateThumbprint)'
    'Operator Auth Cert Expires=>$expiryDate = $Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateExpirationDate; if ($expiryDate -ne [datetime]::MinValue) { $expiryDate.ToShortDateString() }'
    'Operator Auth Cert Self-Signed?=>$Global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateIssuedTo -eq $global:VBOEnvironment.VBOOperatorAuthenticationSettings.CertificateIssuedBy'
)
Write-ElapsedAndMemUsage -Message "Mapped Security" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "RBACRoles...";
$map.RBACRoles = $Global:VBOEnvironment.VBORbacRole | mde @(
    'Name'
    'Description'
    'Role Type=>RoleType'
    'Operators=>Join($.Operators.DisplayName)'
    'Selected Items=>Join($.SelectedItems.DisplayName)'
    'Excluded Items=>Join($.ExcludedItems.DisplayName)'
)
Write-ElapsedAndMemUsage -Message "Mapped RBAC" -LogLevel PROFILE
<#Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Permissions Check...";
$map.Permissions = $Global:VBOEnvironment.PermissionsCheck.Values | mde @(
    'Type'
    'Organization'
    'API'
    'Permission'
)#>
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Proxies...";
$map.Proxies = $Global:VBOEnvironment.VBOProxy | mde @(
    'Name=>Hostname'
    'Description'
    'Threads=>ThreadsNumber'
    'Throttling?=>if($.ThrottlingValue -gt 0) { $.ThrottlingValue.ToString() + " " + $.ThrottlingUnit } else { "disabled" }'
    'State'
    'Type'
    'Outdated?=>IsOutdated'
    'Internet Proxy=>InternetProxy.UseInternetProxy'
    'Objects Managed=>(($Global:VBOEnvironment.VBOJobSession | ? { $_.JobId -in ($Global:VBOEnvironment.VBOJob | ? { $.Id -eq $_.Repository.ProxyId }).Id} | group JobId | % { $_.Group | Measure-Object -Property Progress -Average} ).Average | measure-object -Sum ).Sum.ToString("0")'
    'OS Version=>($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).OSVersion'
    'RAM=>(($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).RAMTotalSize/1GB).ToString("###0 GB")'
    'CPUs=>($Global:VBOEnvironment.VMCLog.ProxyDetails | ? { $.Id -eq $_.ProxyID }).CPUCount'
)
Write-ElapsedAndMemUsage -Message "Mapped Proxies" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Repositories...";
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
    'Daily Change Rate=>$changesByDay = (($VBOEnvironment.VBOJobSession | ? {($VBOEnvironment.VBOJob | ? { $_.Repository.Id -eq $.Id }).Id -eq $_.JobId })| select @{n="CreationDate"; e={$_.CreationTime.Date}},* | sort CreationDate -Descending | group CreationDate | % { $_.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.000000" | measure -Sum }).Sum;
        #$weeklyChange = ($changesByDay | select -first 7 | measure -Sum).Sum;
        $dailyChangeAvg = ($changesByDay | measure -Average).Average
        #$dailyChange95p = $changesByDay | 95P
        
        $repoUsage = ($Global:VBOEnvironment.VBOUsageData | ? { $_.RepositoryId -eq $.Id})
        $GBUsed = if (($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1GB -gt 0) {($repoUsage.ObjectStorageUsedSpace | measure -Sum).Sum/1GB} else {($repoUsage.UsedSpace | measure -Sum).Sum/1GB}

        $DCR = $dailyChangeAvg/$GBUsed
        $DCR.ToString("##0.000%")'
    'Retention=>($.RetentionPeriod.ToString() -replace "(Years?)(.+)","`$2 `$1" )+", "+$.RetentionType+", Applied "+$.RetentionFrequencyType'
)
Write-ElapsedAndMemUsage -Message "Mapped Local Repos" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "ObjectRepositories...";
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
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Organizations...";
Write-ElapsedAndMemUsage -Message "Mapped Object Storage" -LogLevel PROFILE
<#$map.Organizations2 = $Global:VBOEnvironment.VBOOrganization | ForEach-Object {
    $exoSettings = $_ | mde @(
        'Name'
        'OfficeName'
        'Type'
        'Settings=>Office365ExchangeConnectionSettings'
        'AppCert=>$v = (Get-ChildItem -Recurse cert:\* | Where-Object { $_.Thumbprint -eq $.Office365ExchangeConnectionSettings.ApplicationCertificateThumbprint } | Select-Object -first 1)'

    )
}#>
<#$Global:VBOEnvironment.VBOOrganization | ForEach-Object {
    $certDetails = [pscustomobject]@{}
    $certDetails | Add-Member -MemberType NoteProperty -Name "OrgName" -Value $_.Name
    $certDetails | Add-Member -MemberType NoteProperty -Name "ExchangeAppCert" -Value (Get-ChildItem -Recurse cert:\* | Where-Object { $_.Thumbprint -eq $_.Office365ExchangeConnectionSettings.ApplicationCertificateThumbprint } | Select-Object -first 1)
    $certDetails | Add-Member -MemberType NoteProperty -Name "SharePointAppCert" -Value (Get-ChildItem -Recurse cert:\* | Where-Object { $_.Thumbprint -eq $_.Office365SharePointConnectionSettings.ApplicationCertificateThumbprint } | Select-Object -first 1)
    $i=0
    foreach ($backupApp in $_.BackupApplications) {
        $certDetails | Add-Member -MemberType NoteProperty -Name ("AuxApp"+($i)+"Id") -Value $backupApp.ApplicationId
        $certDetails | Add-Member -MemberType NoteProperty -Name ("AuxApp"+(++$i)+"Cert") -Value (Get-ChildItem -Recurse cert:\* | Where-Object { $_.Thumbprint -eq $backupApp.ApplicationCertificateThumbprint } | Select-Object -first 1)
    }
    $certDetails
} | mde @(
    "Organization Name=>OrgName"
    "Cert=>$.Value.FriendlyName"
    "Expires=>$.Value.NotAfter"
    "Self-Signed=>($.Value.Subject -eq $.Value.Issuer)"
)#> ## This is still in progress.
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
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Jobs...";
$map.Jobs = $Global:VBOEnvironment.VBOJob + $Global:VBOEnvironment.VBOCopyJob | mde @(
    'Organization=>if($null -ne $.Organization) { $.Organization } else { $.BackupJob.Organization }'
    'Name'
    'Description'
    'Job Type=>if ($null -eq $.BackupJob) { "Backup" } else { "Backup Copy" }'
    'Scope Type=>JobBackupType'
    'Processing Options=>@(if ($.SelectedItems.Mailbox) {"Mailbox"}; if ($.SelectedItems.ArchiveMailbox) {"Archive"}; if ($.SelectedItems.OneDrive) {"OneDrive"}; if ($.SelectedItems.Site -or "Site" -in $.SelectedItems.Type) {"Site"}; if ($.SelectedItems.Teams -or "Team" -in $.SelectedItems.Type) {"Teams"}; if ($.SelectedItems.GroupMailbox) {"Group Mailbox"}; if ($.SelectedItems.GroupSite) {"Group Site"}) -join ", "'
    'Selected Items=>$objectCountStr = (($Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobId -eq $.Id } | Sort-Object CreationTime -Descending) | Select-Object -First 1).Log.Title -match "Found (\d+) objects"; [Regex]::Match($objectCountStr,"(?<=Found )(\d+)(?= objects)")' #$.SelectedItems.Count
    'Excluded Items=>$objectCountStr = (($Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobId -eq $.Id } | Sort-Object CreationTime -Descending) | Select-Object -First 1).Log.Title -match "Found (\d+) excluded objects"; [Regex]::Match($objectCountStr,"(?<=Found )(\d+)(?= excluded objects)")' # $.ExcludedItems.Count'
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
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "JobStats...";
$map.JobStats = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name} | Group-Object JobName | mde @(
    'Name'
    'Average Duration (min)=>$totalMin = ($.Group | select *,@{n="Duration";e={($_.EndTime-$_.CreationTime).TotalMinutes}} | ? { $_.Duration -gt 0 } | measure Duration -Average).Average; [DateTime]::MinValue.AddMinutes($totalMin).ToString("HH\:mm\:ss")'
    'Max Duration (min)=>$totalMin = ($.Group | select *,@{n="Duration";e={($_.EndTime-$_.CreationTime).TotalMinutes}} | ? { $_.Duration -gt 0 } | measure Duration -Maximum).Maximum; [DateTime]::MinValue.AddMinutes($totalMin).ToString("HH\:mm\:ss")'
    'Average Data Transferred=>(($.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.0000" | measure -Average).Average.ToString("#,##0.000 GB"))'
    'Max Data Transferred=>(($.Group.Statistics.TransferredData | ConvertData -To "GB" -Format "0.0000" | measure -Maximum).Maximum.ToString("#,##0.000 GB") )'
    'Average Objects (#)=>($.Group.Progress | measure -Average).Average.ToString("0")'
    'Max Objects (#)=>($.Group.Progress | measure -Maximum).Maximum.ToString("0")'
    'Average Items (#)=>($.Group.Statistics | measure ProcessedObjects -Average).Average.ToString("0")'
    'Max Items (#)=>($.Group.Statistics | measure ProcessedObjects -Maximum).Maximum.ToString("0")'
    #'Average Processing Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)\)","`$1" | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
    #'Max Processing Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)\)","`$1" | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
    #'Average Item Proc Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)items/s\)","`$2" | measure -Average).Average.ToString("0.0 items/s"))'
    #'Max Item Proc Rate=>(($.Group.Statistics.ProcessingRate -replace "(\d+.+)\s\((.+)items/s\)","`$2" | measure -Maximum).Maximum.ToString("0.0 items/s") )'
    #'Average Read Rate=>(($.Group.Statistics.ReadRate | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
    #'Max Read Rate=>(($.Group.Statistics.ReadRate | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
    #'Average Write Rate=>(($.Group.Statistics.WriteRate | ConvertData -To "MB" -Format "0.0000" | measure -Average).Average.ToString("0.000 MB/s"))'
    #'Max Write Rate=>(($.Group.Statistics.WriteRate | ConvertData -To "MB" -Format "0.0000" | measure -Maximum).Maximum.ToString("0.000 MB/s") )'
    'Typical Bottleneck=>($.Group.Statistics.Bottleneck | ? { $_ -ne "NA" } | group | sort Count -Descending | select -first 1).Name'
    'Job Avg Throughput=>(($.Group.Statistics.TransferredData | ConvertData -To "MB" -Format "0.0000" | measure -Sum).Sum / ($.Group | select @{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } ).TotalSeconds}} | ? { $_.Duration -gt 0 } | measure Duration -Sum).Sum).ToString("#,##0.000 MB/s")'
    'Job Avg Processing Rate=>(($.Group.Statistics | measure ProcessedObjects -Sum).Sum / ($.Group | select @{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } ).TotalSeconds}} | ? { $_.Duration -gt 0 } | measure Duration -Sum).Sum).ToString("#,##0.000 items/s")'
)
Write-ElapsedAndMemUsage -Message "Mapped Job Stats" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "ProcessingStats...";
$map.ProcessingStats = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name} | Group-Object JobName | mde @(
    '!Vars=>
        $PRIVATE:Vars = @{
            Times = @{
                Startup=@()
                Exclude=@()
                Found=@()
                Processing=@()
                Wrapup=@()
            }
            Stats = @{}
        }
        foreach ($session in $.Group) {
            $ObjectEntries = @()
            foreach ($line in $session.Log) {
                if ($_.Title -imatch "Found \d+ excluded objects") { $ExcludeEntry = $line }
                if ($_.Title -imatch "Found \d+ objects") { $FoundEntry = $line }
                if ($_.Title -imatch "Transferred: \d+") { $SummaryEntry = $line }
                if ($_.Title -imatch "Processing .+:") { $ObjectEntries += $line }
            }
            <#Too Slow rewrote above:
            $ExcludeEntry = ($session.Log | ? { $_.Title -imatch "Found \d+ excluded objects" } )
            $FoundEntry = ($session.Log | ? { $_.Title -imatch "Found \d+ objects" } )
            $SummaryEntry = ($session.Log | ? { $_.Title -imatch "Transferred: \d+" } )
            $ObjectEntries = ($session.Log | ? { $_.Title -imatch "Processing .+:" } )
            #>

            if ($FoundEntry) {
                $PRIVATE:Vars.Times.Startup += ($FoundEntry.EndTime - $session.CreationTime).TotalSeconds
                $PRIVATE:Vars.Times.Found += ($FoundEntry.EndTime - $FoundEntry.CreationTime).TotalSeconds }
            if ($ExcludeEntry) {
                $PRIVATE:Vars.Times.Exclude += ($ExcludeEntry.EndTime - $ExcludeEntry.CreationTime).TotalSeconds }
            if ($SummaryEntry) {
                $PRIVATE:Vars.Times.Wrapup += ($session.EndTime - $SummaryEntry.CreationTime).TotalSeconds }
            foreach ($objEntry in $ObjectEntries) { $PRIVATE:Vars.Times.Processing += ($objEntry.EndTime - $objEntry.CreationTime).TotalSeconds } # grab time for every processed object
        }
        
        #Time Analysis
        foreach ($statkey in $PRIVATE:Vars.Times.Keys) {
            $measurements = $PRIVATE:Vars.Times.$statkey | ? { $_ -gt 0 } | Measure-Object -Average -Minimum -Maximum  #use gt 0 to account for problems with daily ssavings negative values skewing results
            $PRIVATE:Vars.Stats.$statkey = @{}
            $PRIVATE:Vars.Stats.$statkey.Latest = ($PRIVATE:Vars.Times.$statkey)[0]
            $PRIVATE:Vars.Stats.$statkey.Average = $measurements.Average
            $PRIVATE:Vars.Stats.$statkey.Minimum = $measurements.Minimum
            $PRIVATE:Vars.Stats.$statkey.Maximum = $measurements.Maximum
            $PRIVATE:Vars.Stats.$statkey.Median = $PRIVATE:Vars.Times.$statkey | 95p -Percentile 50
            $PRIVATE:Vars.Stats.$statkey.Ninety = $PRIVATE:Vars.Times.$statkey | 95p -Percentile 90
        }'
    'Name'
    'Startup Time (Min)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Startup.Minimum).ToString("HH:mm:ss")'
    'Startup Time (Avg)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Startup.Average).ToString("HH:mm:ss")'
    'Startup Time (Max)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Startup.Maximum).ToString("HH:mm:ss")'
    'Startup Time (Median)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Startup.Median).ToString("HH:mm:ss")'
    'Startup Time (90%)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Startup.Ninety).ToString("HH:mm:ss")'
    'Exclude Time (Min)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Exclude.Minimum).ToString("HH:mm:ss")'
    'Exclude Time (Avg)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Exclude.Average).ToString("HH:mm:ss")'
    'Exclude Time (Max)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Exclude.Maximum).ToString("HH:mm:ss")'
    'Exclude Time (Median)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Exclude.Median).ToString("HH:mm:ss")'
    'Exclude Time (90%)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Exclude.Ninety).ToString("HH:mm:ss")'
    'Found Time (Min)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Found.Minimum).ToString("HH:mm:ss")'
    'Found Time (Avg)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Found.Average).ToString("HH:mm:ss")'
    'Found Time (Max)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Found.Maximum).ToString("HH:mm:ss")'
    'Found Time (Median)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Found.Median).ToString("HH:mm:ss")'
    'Found Time (90%)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Found.Ninety).ToString("HH:mm:ss")'
    'Processing Time (Min)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Processing.Minimum).ToString("HH:mm:ss")'
    'Processing Time (Avg)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Processing.Average).ToString("HH:mm:ss")'
    'Processing Time (Max)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Processing.Maximum).ToString("HH:mm:ss")'
    'Processing Time (Median)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Processing.Median).ToString("HH:mm:ss")'
    'Processing Time (90%)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Processing.Ninety).ToString("HH:mm:ss")'
    'Wrapup Time (Min)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Wrapup.Minimum).ToString("HH:mm:ss")'
    'Wrapup Time (Avg)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Wrapup.Average).ToString("HH:mm:ss")'
    'Wrapup Time (Max)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Wrapup.Maximum).ToString("HH:mm:ss")'
    'Wrapup Time (Median)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Wrapup.Median).ToString("HH:mm:ss")'
    'Wrapup Time (90%)=>[datetime]::MinValue.AddSeconds($PRIVATE:Vars.Stats.Wrapup.Ninety).ToString("HH:mm:ss")'
)
Write-ElapsedAndMemUsage -Message "Mapped Processing/Task Stats" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "JobSessions...";
$map.JobSessions = $Global:VBOEnvironment.VBOJobSession | Where-Object { $_.JobName -in $Global:VBOEnvironment.VBOJob.Name -and $_.CreationTime -gt (Get-Date).AddDays(-$global:SETTINGS.ReportingIntervalDays)} | Sort-Object @{Expression={$_.JobName}; Descending=$false },@{Expression={$_.CreationTime}; Descending=$true } | mde @(
    'Name=>JobName'
    'Status'
    'Start Time=>$.CreationTime.ToString("yyyy/MM/dd HH:mm:ss")'
    'End Time=>$.EndTime.ToString("yyyy/MM/dd HH:mm:ss")'
    'Duration=>$sessWithDuration =  $. | select *,@{n="Duration";e={ $(if ($_.Status -eq "Running") { (get-date)-$_.CreationTime } else { $_.EndTime-$_.CreationTime } )}}; $sessWithDuration.Duration.ToString("hh\:mm\:ss")'
    'Log=>Join -Array $($.Log.Title | ? { !$_.Contains("[Success]") }) -Delimiter "`r`n"'
)
Write-ElapsedAndMemUsage -Message "Mapped Sessions" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Protection Status...";
<#$map.ProtectionStatus = (Get-Item -Path ($global:SETTINGS.OutputPath + "\Veeam_MailboxProtectionReport*.csv")).FullName | Import-Csv | Where-Object { $_."Protection Status" -eq "Unprotected" -or $_."Last Backup Date" -lt (Get-date).AddDays(-$global:SETTINGS.ReportingIntervalDays)} | Sort-Object "Protection Status",User -Descending | mde @(
    'User=>Mailbox'
    'E-mail=>$."E-mail"'
    'Organization'
    'Protection Status=> if ($."Protection Status" -eq "Protected") { "Stale Backup" } else { "Unprotected" }'
    'Last Backup Date=>$."Last Backup Date"'
)#>
$map.ProtectionStatus2 = $Global:VBOEnvironment.VBOOrganizationUsers | Select-Object -ExcludeProperty "IsBackedup" | mde @(
    'Organization=>($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name'
    '!Vars=>
        #THIS IS WAY SLOW #$PRIVATE:Entity = ($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq $self.Name -and $_.Email -eq $.UserName})
        #THIS IS DECENT #$PRIVATE:Entity = ($Global:VBOEnvironment.VBOEntityData | . {Process { if($_.Organization.DisplayName -eq $self.Name -and $_.Email -eq $.UserName) {$_} } })
        $PRIVATE:Entity = foreach ($ent in $Global:VBOEnvironment.VBOEntityData) { if($_.Organization.DisplayName -eq $self.Name -and $_.Email -eq $.UserName) {$_} })
    '
    #'Office ID=>OfficeId'
    #'On-Prem ID=>OnPremisesId'
    'Display Name=>DisplayName'
    'Username=>UserName'
    'Type' # user/sharedmailbox/etc
    'Location=>LocationType'
    #'Has Backup=>!!($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq ($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name -and $_.Email -eq $.UserName -and ($_.IsMailboxBackedUp -or $_.IsArchiveBackedUp -or $_.IsOnedriveBackedUp -or $_.IsPersonalSiteBackedUp) })'
    #'Mail Backed Up=>!!($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq ($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name -and $_.Email -eq $.UserName -and $_.IsMailboxBackedUp })'
    'Mail Backup Date=>$PRIVATE:Entity.MailboxBackedUpTime.DateTime'
    #'Archive Backed Up=>!!($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq ($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name -and $_.Email -eq $.UserName -and $_.IsArchiveBackedUp })'
    'Archive Backup Date=>$PRIVATE:Entity.ArchiveBackedUpTime.DateTime'
    #'Onedrive Backed Up=>!!($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq ($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name -and $_.Email -eq $.UserName -and $_.IsOnedriveBackedUp })'
    'Onedrive Backup Date=>$PRIVATE:Entity.OneDriveBackedUpTime.DateTime'
    #'Site Backed Up=>!!($Global:VBOEnvironment.VBOEntityData | ? { $_.Organization.DisplayName -eq ($Global:VBOEnvironment.VBOOrganization | ? { $_.Id -eq $.OrganizationId }).Name -and $_.Email -eq $.UserName -and $_.IsPersonalSiteBackedUp })'
    'Site Backup Date=>$PRIVATE:Entity.PersonalSiteBackedUpTime.DateTime'
    'Has Backup=>!!(($self."Mail Backup Date", $self."Archive Backup Date", $self."Onedrive Backup Date", $self."Site Backup Date") | measure-Object -Maximum).Maximum'
    'Is Stale=> if ($self."Has Backup") { (($self."Mail Backup Date", $self."Archive Backup Date", $self."Onedrive Backup Date", $self."Site Backup Date") | measure-Object -Maximum).Maximum -lt (Get-date).AddDays(-$global:SETTINGS.ReportingIntervalDays) }'
)
Write-ElapsedAndMemUsage -Message "Mapped Protection Status" -LogLevel PROFILE
Write-Progress @progressSplat -PercentComplete ($progress++) -CurrentOperation "Done." -Completed


######### END MAPS ############


Write-ElapsedAndMemUsage -Message "Done Mapping. Aggregating next." -LogLevel DEBUG
Write-LogFile -Message "Done mapping CSV structures." -LogLevel INFO
Write-LogFile -Message "Aggregating results..." -LogLevel INFO

# Build the objects & sections
$Global:HealthCheckResult = MDE $map.Keys

foreach ($sectionName in $map.Keys) {
    $section = $map.$sectionName

    if ($null -eq $section) {
        Write-Warning "No map found for: "+$section+". Please define."
        Write-LogFile -Message ("No map found for: "+$section+". Please define.") -LogName Errors -LogLevel ERROR
        # used to throw & then return
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
    Write-LogFile -Message "All Errors:" -LogName Errors -LogLevel ERROR
    $Error | ForEach-Object {Write-LogFile -Message ($_.ToString() + "`r`n" + $_.InvocationInfo.Line.ToString() + "`r`n" + $_.ScriptStackTrace.ToString()+ "`r`n" +$_.Exception.StackTrace.ToString()) -LogName Errors -LogLevel ERROR }
}

Write-ResourceUsageToLog -Message "All done"
Write-LogFile -Message "Finished." -LogLevel INFO
Write-LogFile -Message "Time Elapsed: $([datetime]::MinValue.AddSeconds($CollectorTimer.Elapsed.TotalSeconds).ToString("HH:mm:ss"))" -LogLevel INFO
Write-LogFile -Message "Environment: Proxies=$(@($HealthCheckResult.Proxies).Count); Repos=$(@($HealthCheckResult.LocalRepositories).Count); ObjRepos=$(@($HealthCheckResult.ObjectRepositories).Count); Jobs=$(@($HealthCheckResult.Jobs).Count); Sessions=$(@($VBOEnvironment.VBOJobSession).Count); Objects=$(($HealthCheckResult.Proxies | Measure-Object 'Objects Managed' -Sum).Sum); Users=$(@($HealthCheckResult.ProtectionStatus2).Count); Entities=$(@($VBOEnvironment.VBOEntityData).Count)" -LogLevel INFO
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
if ($null -ne $logWatcher) {
    $logWatcher.Kill()
}
if ($null -ne $netWatcher) {
    $netWatcher.Kill()
}
if ($null -ne $taskmgr) {
    $taskmgr.Kill()
}