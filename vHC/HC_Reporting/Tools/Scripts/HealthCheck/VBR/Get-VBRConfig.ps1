#Requires -Version 4
#Requires -RunAsAdministrator

<#
.Synopsis
    Simple Veeam report to dump server & job configurations
.Notes
    Version: 0.2
    Original Author: Joe Houghes
    Butchered by: Adam Congdon
    Modified Date: 4-24-2020
.EXAMPLE
    Get-VBRConfig -VBRServer ausveeambr -ReportPath C:\Temp\VBROutput
    Get-VBRconfig -VBRSERVER localhost -ReportPath C:\HealthCheck\output
#>
param(
    # VBRServer
    [Parameter(Mandatory)]
    [string]$VBRServer,
    [Parameter(Mandatory)]
    [int]$VBRVersion
    # [Parameter(Mandatory)]
    # [string]$ReportPath,
    # [int]$ReportingIntervalDays = -1
)
$ReportPath = 'C:\temp\vHC\Original\VBR'
$logDir = "C:\temp\vHC\Original\Log\"
$logFile = $logDir + "CollectorMain.log"
if (!(Test-Path $logfile)) { New-Item -type Directory $logDir -ErrorAction SilentlyContinue; new-item -type file $logfile }
#functions
enum LogLevel {
    TRACE
    PROFILE
    DEBUG
    INFO
    WARNING
    ERROR
    FATAL
}
function Export-VhcCsv {
    [CmdletBinding()] param (
        [Parameter()] [string] $FileName,
        [Parameter(ValueFromPipeline)] $input
      
    )
    $file = $("$ReportPath\$VBRServer" + $FileName)
    Write-LogFile("Exporting data to file: " + $file)
    $input | Export-Csv -Path $file -NoTypeInformation
  
}
#end functions region


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



#Load the Veeam PSSnapin
if (!(Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
    Add-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue
    Connect-VBRServer -Server $VBRServer -ErrorAction SilentlyContinue
}

else {
    Disconnect-VBRServer
    Connect-VBRServer -Server $VBRServer
}

if (!(Test-Path $ReportPath)) {
    New-Item -Path $ReportPath -ItemType Directory | Out-Null
}

Push-Location -Path $ReportPath
Write-Verbose ("Changing directory to '$ReportPath'")
# COLLECTION Starting
## User Role Collection:
Get-VBRUserRoleAssignment | Export-VhcCsv -FileName "_UserRoles.csv"

#version detection:
#try {
#    $corePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "CorePath"
#    $depDLLPath = Join-Path -Path $corePath.CorePath -ChildPath "Packages\VeeamDeploymentDll.dll" -Resolve
#    $file = Get-Item -Path $depDLLPath
#    $version = $file.VersionInfo.ProductVersion
#
#    Write-LogFile("Detected Version: " + $version)
#}
#catch {
#    Write-LogFile("Error on version detection. ")
#}
#$version = $VBRVersion

# general collection:
try {
    # Agent Jobs
    # do I need this ?? $AgentJobs = Get-VBRComputerBackupJob -WarningAction SilentlyContinue
    # Replica Jobs
    # CDP Job
    # SureBackup?
      
    
    #Agents
    $pg = Get-VBRProtectionGroup
    $pc = Get-VBRDiscoveredComputer

    #Capacity extent grab:

    #Traffic Rules
}
catch {
    Write-LogFile("Error on general info collection. ")
}
Write-LogFile("Executing VBR Config Collection...")
Write-LogFile("VBR Version " + $VBRVersion)
# collection template
try {
    $message = "Collecting ____ info..."
    Write-LogFile($message)

    # work here

    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

# Server Info Collection
try {
    $message = "Collecting server info..."
    Write-LogFile($message)

    $Servers = Get-VBRServer
    $Servers = $Servers | Select-Object -Property "Info", "ParentId", "Id", "Uid", "Name", "Reference", "Description", "IsUnavailable", "Type", "ApiVersion", "PhysHostId", "ProxyServicesCreds", @{name = 'Cores'; expression = { $_.GetPhysicalHost().hardwareinfo.CoresCount } }, @{name = 'CPUCount'; expression = { $_.GetPhysicalHost().hardwareinfo.CPUCount } }, @{name = 'RAM'; expression = { $_.GetPhysicalHost().hardwareinfo.PhysicalRamTotal } }, @{name = 'OSInfo'; expression = { $_.Info.Info } }
    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$Servers | Export-Csv -Path $("$ReportPath\$VBRServer" + '_Servers.csv') -NoTypeInformation



# proxy collection
try {
    $message = "Collecting Proxy info..."
    Write-LogFile($message)

    # work here
    $Proxies = Get-VBRViProxy
    $cdpProxy = Get-VBRCDPProxy                
    # unused - $fileProxy = Get-VBRComputerFileProxyServer 
    $hvProxy = Get-VBRHvProxy                 
    $nasProxy = Get-VBRNASProxyServer    

    $nasProxyOut = $nasProxy | Select-Object -Property "ConcurrentTaskNumber", @{n = "Host"; e = { $_.Server.Name } }, @{n = "HostId"; e = { $_.Server.Id } }
    #$hvProxyOut = $hvProxy | Select-Object -Property "Name", "HostId", @{n=Host}



    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

$Proxies | Export-Csv -Path $("$ReportPath\$VBRServer" + '_Proxies.csv') -NoTypeInformation
$cdpProxy | Export-csv -Path $("$ReportPath\$VBRServer" + '_CdpProxy.csv') -NoTypeInformation
#$fileProxy| Export-csv -Path $("$ReportPath\$VBRServer" + '_FileProxy.csv') -NoTypeInformation
$hvProxy | Export-csv -Path $("$ReportPath\$VBRServer" + '_HvProxy.csv') -NoTypeInformation
$nasProxyOut | Export-csv -Path $("$ReportPath\$VBRServer" + '_NasProxy.csv') -NoTypeInformation

## ENtra ID
try{
    $entraTenant = Get-VBREntraIDTenant



    # Define the custom object with properties tenantName and CacheRepoName
    $entraIDTenant = [PSCustomObject]@{
        tenantName = $entraTenant.Name
        CacheRepoName = $entraTenant.cacherepository.name
    }
    #$entraIDTenant
    
    
    $eIdLogJobs = Get-VBREntraIDLogsBackupJob
    $entraIdLogJobs = [PSCustomObject]@{
        Name = $eIdLogJobs.Name
        Tenant = $eIdLogJobs.BackupObject.Tenant.Name
        shortTermRetType = $eIdLogJobs.Name
        ShortTermRepo = $eIdLogJobs.ShortTermBackupRepository.Name
        ShortTermRepoRetention = $eIdLogJobs.ShortTermRetentionPeriod
    
        CopyModeEnabled = $eIdLogJobs.EnableCopyMode
        SecondaryTarget = $eIdLogJobs.SecondaryTarget
    }
    #$entraIdLogJobs
    
    $eIdTenantBackup = Get-VBREntraIDTenantBackupJob
    
    $entraIdTenantJobs = [PSCustomObject]@{
        Name = $eIdTenantBackup.Tenant.Name
        RetentionPolicy = $eIdTenantBackup.RetentionPolicy
    
    }
   # $entraIdTenantJobs
    
}
catch{
    Write-LogFile("Error on Entra ID collection. ")
}
$entraIdLogJobs | Export-Csv -Path $("$ReportPath\$VBRServer" + '_entraLogJob.csv') -NoTypeInformation
$entraIDTenant | Export-Csv -Path $("$ReportPath\$VBRServer" + '_entraTenants.csv') -NoTypeInformation
$entraIdTenantJobs | Export-Csv -Path $("$ReportPath\$VBRServer" + '_entraTenantJob.csv') -NoTypeInformation

# cap extent grab
try {
    $message = "Collecting capcity tier info..."
    Write-LogFile($message)

    # work here
    $cap = get-vbrbackuprepository -ScaleOut | Get-VBRCapacityExtent
    $capOut = $cap | Select-Object Status, @{n = 'Type'; e = { $_.Repository.Type } }, @{n = 'Immute'; e = { $_.Repository.BackupImmutabilityEnabled } }, @{n = 'immutabilityperiod'; e = { $_.Repository.ImmutabilityPeriod } }, @{n = 'SizeLimitEnabled'; e = { $_.Repository.SizeLimitEnabled } }, @{n = 'SizeLimit'; e = { $_.Repository.SizeLimit } }, @{n = 'RepoId'; e = { $_.Repository.Id } }, parentid


    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$capOut    | Export-csv -Path $("$ReportPath\$VBRServer" + '_capTier.csv') -NoTypeInformation

# traffic config
try {
    $message = "Collecting traffic info..."
    Write-LogFile($message)

    # work here
    $trafficRules = Get-VBRNetworkTrafficRule  


    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$trafficRules | Export-csv -Path $("$ReportPath\$VBRServer" + '_trafficRules.csv') -NoTypeInformation

# registry settings
try {
    $message = "Collecting Registry info..."
    Write-LogFile($message)

    # work here
    $reg = get-item "HKLM:\SOFTWARE\Veeam\Veeam Backup and Replication"


    [System.Collections.ArrayList]$output = @()
    foreach ($r in $reg.Property) {

        $regout2 = [PSCustomObject][ordered] @{
            'KeyName' = $r
            'Value'   = $reg.GetValue($r)

        }
        $null = $output.Add($regout2)
        # $regout2 += $reg | Select-Object @{n="KeyName";e={$r}}, @{n='value';e={$_.GetValue($r)}}
    }

    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$output | Export-csv -Path $("$ReportPath\$VBRServer" + '_regkeys.csv') -NoTypeInformation

# Repos
try {
    $message = "Collecting repositories info..."
    Write-LogFile($message)

    # work here
    $Repositories = Get-VBRBackupRepository
    $SOBRs = Get-VBRBackupRepository -ScaleOut

    [System.Collections.ArrayList]$RepositoryDetails = @()

    foreach ($Repo in $Repositories) {
        $RepoOutput = [pscustomobject][ordered] @{
            'ID'   = $Repo.ID
            'Name' = $Repo.Name
        }
        $null = $RepositoryDetails.Add($RepoOutput)
        Remove-Variable RepoOutput
    }

    foreach ($Repo in $SOBRs) {
        $RepoOutput = [pscustomobject][ordered] @{
            'ID'   = $Repo.ID
            'Name' = $Repo.Name
        }
        $null = $RepositoryDetails.Add($RepoOutput)
        Remove-Variable RepoOutput
    }


    [System.Collections.ArrayList]$AllSOBRExtents = @()

    foreach ($SOBR in $SOBRs) {
        $Extents = Get-VBRRepositoryExtent -Repository $SOBR

        foreach ($Extent in $Extents) {
            if ($VBRVersion -eq 12) {
                #write-host("DEBUG: GATES:")  
                #$Extent.Repository.GetActualGateways().Name
                $ExtentDetails = $Extent.Repository | Select-Object *, @{n = 'SOBR_Name'; e = { $SOBR.Name } }, @{name = 'CachedFreeSpace'; expression = { $_.GetContainer().cachedfreespace.InGigabytes } }, @{name = 'CachedTotalSpace'; expression = { $_.GetContainer().cachedtotalspace.InGigabytes } }, @{name = 'gatewayHosts'; expression = { $_.GetActualGateways().Name } }, @{n = 'ObjectLockEnabled'; e = { $_.ObjectLockEnabled } }
            }
            else {
                $ExtentDetails = $Extent.Repository | Select-Object *, @{n = 'SOBR_Name'; e = { $SOBR.Name } }, @{name = 'CachedFreeSpace'; expression = { $_.GetContainer().cachedfreespace.InGigabytes } }, @{name = 'CachedTotalSpace'; expression = { $_.GetContainer().cachedtotalspace.InGigabytes } }
            }
            
            $AllSOBRExtents.Add($ExtentDetails) | Out-Null
        }
    }
    $SOBROutput = $SOBRs | Select-Object -Property "PolicyType", @{n = "Extents"; e = { $SOBRs.extent.name -as [String] } } , "UsePerVMBackupFiles", "PerformFullWhenExtentOffline", "EnableCapacityTier", "OperationalRestorePeriod", "OverridePolicyEnabled", "OverrideSpaceThreshold", "OffloadWindowOptions", "CapacityExtent", "EncryptionEnabled", "EncryptionKey", "CapacityTierCopyPolicyEnabled", "CapacityTierMovePolicyEnabled", "ArchiveTierEnabled", "ArchiveExtent", "ArchivePeriod", "CostOptimizedArchiveEnabled", "ArchiveFullBackupModeEnabled", "PluginBackupsOffloadEnabled", "CopyAllPluginBackupsEnabled", "CopyAllMachineBackupsEnabled", "Id", "Name", "Description"
    $AllSOBRExtentsOutput = $AllSOBRExtents | Select-Object -property @{name = 'Host'; expression = { $_.host.name } } , "Id", "Name", "HostId", "MountHostId", "Description", "CreationTime", "Path", "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable", "Group", "UseNfsOnMountHost", "VersionOfCreation", "Tag", "IsTemporary", "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId", "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage", "SplitStoragesPerVm", "IsImmutabilitySupported", "SOBR_Name", @{name = 'Options(maxtasks)'; expression = { $_.Options.MaxTaskCount } }, @{name = 'Options(Unlimited Tasks)'; expression = { $_.Options.IsTaskCountUnlim } }, @{name = 'Options(MaxArchiveTaskCount)'; expression = { $_.Options.MaxArchiveTaskCount } }, @{name = 'Options(CombinedDataRateLimit)'; expression = { $_.Options.CombinedDataRateLimit } }, @{name = 'Options(Uncompress)'; expression = { $_.Options.Uncompress } }, @{name = 'Options(OptimizeBlockAlign)'; expression = { $_.Options.OptimizeBlockAlign } }, @{name = 'Options(RemoteAccessLimitation)'; expression = { $_.Options.RemoteAccessLimitation } }, @{name = 'Options(EpEncryptionEnabled)'; expression = { $_.Options.EpEncryptionEnabled } }, @{name = 'Options(OneBackupFilePerVm)'; expression = { $_.Options.OneBackupFilePerVm } }, @{name = 'Options(IsAutoDetectAffinityProxies)'; expression = { $_.Options.IsAutoDetectAffinityProxies } }, @{name = 'Options(NfsRepositoryEncoding)'; expression = { $_.Options.NfsRepositoryEncoding } }, "CachedFreeSpace", "CachedTotalSpace", "gatewayHosts", "ObjectLockEnabled"


    $repoInfo = $Repositories | Select-Object "Id", "Name", "HostId", "Description", "CreationTime", "Path",
    "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable", "Group", "UseNfsOnMountHost",
    "VersionOfCreation", "Tag", "IsTemporary", "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId",
    "Options", "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage", "SplitStoragesPerVm", @{n = "IsImmutabilitySupported"; e = { $_.GetImmutabilitySettings().IsEnabled } },
    @{name = 'Options(maxtasks)'; expression = { $_.Options.MaxTaskCount } },
    @{name = 'Options(Unlimited Tasks)'; expression = { $_.Options.IsTaskCountUnlim } },
    @{name = 'Options(MaxArchiveTaskCount)'; expression = { $_.Options.MaxArchiveTaskCount } },
    @{name = 'Options(CombinedDataRateLimit)'; expression = { $_.Options.CombinedDataRateLimit } },
    @{name = 'Options(Uncompress)'; expression = { $_.Options.Uncompress } },
    @{name = 'Options(OptimizeBlockAlign)'; expression = { $_.Options.OptimizeBlockAlign } },
    @{name = 'Options(RemoteAccessLimitation)'; expression = { $_.Options.RemoteAccessLimitation } },
    @{name = 'Options(EpEncryptionEnabled)'; expression = { $_.Options.EpEncryptionEnabled } },
    @{name = 'Options(OneBackupFilePerVm)'; expression = { $_.Options.OneBackupFilePerVm } },
    @{name = 'Options(IsAutoDetectAffinityProxies)'; expression = { $_.Options.IsAutoDetectAffinityProxies } },
    @{name = 'Options(NfsRepositoryEncoding)'; expression = { $_.Options.NfsRepositoryEncoding } }, 
    @{n = 'CachedTotalSpace'; e = { $_.getcontainer().CachedTotalSpace.ingigabytes } }, 
    @{n = 'CachedFreeSpace'; e = { $_.getcontainer().CachedFreeSpace.ingigabytes } },
    @{name = 'gatewayHosts'; expression = { $_.GetActualGateways().Name } }




    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$repoInfo | Export-Csv -Path $("$ReportPath\$VBRServer" + '_Repositories.csv') -NoTypeInformation
$SOBROutput | Export-Csv -Path $("$ReportPath\$VBRServer" + '_SOBRs.csv') -NoTypeInformation
$AllSOBRExtentsOutput | Export-Csv -Path $("$ReportPath\$VBRServer" + '_SOBRExtents.csv') -NoTypeInformation
# jobs collection
try {
    $message = "Collecting jobs info..."
    Write-LogFile($message)

    try {
        $Jobs = Get-VBRJob -WarningAction SilentlyContinue 
    }
    catch {
        $Jobs = $null
    }
    #JobTypes & conversion
    try {
        $catCopy = Get-VBRCatalystCopyJob

    }
    catch {
        $catCopy = $null
    }
    $catCopy | Export-Csv -Path $("$ReportPath\$VBRServer" + '_catCopyjob.csv') -NoTypeInformation
    try {
        $catJob = Get-VBRCatalystJob
    }
    catch {
        $catJob = $null
    }
    try {
        $vaBcj = Get-VBRComputerBackupCopyJob
    }
    catch {
        $vaBcj = $null
    }
    $catJob | Export-Csv -Path $("$ReportPath\$VBRServer" + '_catalystJob.csv') -NoTypeInformation

    try {
        $vaBJob = Get-VBRComputerBackupJob
    }
    catch {
        $vaBJob = $null
    }
    $vaBJob | Export-Csv -Path $("$ReportPath\$VBRServer" + '_AgentBackupJob.csv') -NoTypeInformation
    try {
        $configBackup = Get-VBRConfigurationBackupJob

    }
    catch {
        $configBackup = $null
    }
    try {
        $epJob = Get-VBREPJob 

    }
    catch {
        $epJob = $null
    }
    $epJob | Export-Csv -Path $("$ReportPath\$VBRServer" + '_EndpointJob.csv') -NoTypeInformation
    
    try {
        $sbJob = Get-VBRSureBackupJob
    }
    catch {
        $sbJob = $null
    }
    $sbJob | Export-Csv -Path $("$ReportPath\$VBRServer" + '_SureBackupJob.csv') -NoTypeInformation
    
    #tape jobs
    try {
        $tapeJob = Get-VBRTapeJob
    }
    catch {
        $tapeJob = $null
    }
    #export tape jobs to csv
    $tapeJob | Export-Csv -Path $("$ReportPath\$VBRServer" + '_TapeJobs.csv') -NoTypeInformation
    #end tape jobs
    try {
        #$nasBackup = Get-VBRNASBackupJob 
        # get all NAS jobs
        $nasBackup = Get-VBRUnstructuredBackupJob
        foreach($job in $nasBackup) {
            #$job.Name
            $onDiskGB = 0
            $sourceGb = 0
            $sessions = Get-VBRBackupSession -Name $job.Name
            #sort sessions by latest first selecting only the latest session
            $sessions = $sessions | Sort-Object CreationTime -Descending | Select-Object -First 1
            foreach($session in $sessions) {
                #$session.sessioninfo.BackupTotalSize
                $onDiskGB = $session.sessioninfo.BackupTotalSize / 1024 / 1024 / 1024
                $sourceGb = $session.SessionInfo.Progress.TotalSize / 1024 / 1024 / 1024
            }
            $job | Add-Member -MemberType NoteProperty -Name JobType -Value "NAS Backup"
            $job | Add-Member -MemberType NoteProperty -Name OnDiskGB -Value $onDiskGB
            $job | Add-Member -MemberType NoteProperty -Name SourceGB -Value $sourceGb
        }
        #$jobs


    }
    catch {
        $nasBackup = $null
    }
    try {
        $nasBCJ = Get-VBRNASBackupCopyJob 

    }
    catch {
        $nasBCJ = $null
    }
    try {
        $piJob = Get-VBRPluginJob

    }
    catch {
        $piJob = $null
    }
    try {
        $cdpJob = Get-VBRCDPPolicy

    }
    catch {
        $cdpJob = $null
    }
    try {
        $vcdJob = Get-VBRvCDReplicaJob

    }
    catch {
        $vcdJob = $null
    }

    $piJob | Export-csv -Path $("$ReportPath\$VBRServer" + '_pluginjobs.csv') -NoTypeInformation

  
    $vcdJob | Add-Member -MemberType NoteProperty -Name JobType -Value "VCD Replica"
    $vcdJob | Export-csv -Path $("$ReportPath\$VBRServer" + '_vcdjobs.csv') -NoTypeInformation
  
    $cdpJob | Add-Member -MemberType NoteProperty -Name JobType -Value "CDP Policy"
    $cdpJob | Export-csv -Path $("$ReportPath\$VBRServer" + '_cdpjobs.csv') -NoTypeInformation
  
    $nasBackup | Add-Member -MemberType NoteProperty -Name JobType -Value "NAS Backup"
    $nasBackup | Export-csv -Path $("$ReportPath\$VBRServer" + '_nasBackup.csv') -NoTypeInformation
    $nasBCJ | export-csv -Path $("$ReportPath\$VBRServer" + '_nasBCJ.csv') -NoTypeInformation
  
    # removing tape jobs from here, exporting independently
    # $tapeJob | Add-Member -MemberType NoteProperty -Name JobType -Value "Tape Backup"
    # $Jobs += $tapeJob
  
  
      
    $vaBcj | Add-Member -MemberType NoteProperty -Name JobType -Value "Physical Backup Copy"
    #$Jobs += $vaBcj //pulled in jobs for now
    try {
        $vaBJob += $epJob 
    }
    catch { write-host("test") }
      
    try {
        $vaBJob | Add-Member -MemberType NoteProperty -Name JobType -Value "Physical Backup" -ErrorAction Ignore
    }
    catch { write-host("test") }
    #$Jobs += $vaBJob //pulled in jobs for now
  
      
    #$configBackup #| Add-Member -MemberType NoteProperty -Name JobType -Value "Config Backup"
    #$Jobs += $configBackup
  
    #$Jobs += $sbJob
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in $Jobs) {
        $JobDetails = $Job | Select-Object -Property 'Name', 'JobType',
        'SheduleEnabledTime', 'ScheduleOptions',
        @{n = 'RestorePoints'; e = { $Job.Options.BackupStorageOptions.RetainCycles } }, 
        @{n = 'RepoName'; e = { $RepositoryDetails | Where-Object { $_.Id -eq $job.Info.TargetRepositoryId.Guid } | Select-Object -ExpandProperty Name } },
        @{n = 'Algorithm'; e = { $Job.Options.BackupTargetOptions.Algorithm } }, 
        @{n = 'FullBackupScheduleKind'; e = { $Job.Options.BackupTargetOptions.FullBackupScheduleKind } }, 
        @{n = 'FullBackupDays'; e = { $Job.Options.BackupTargetOptions.FullBackupDays } }, 
        @{n = 'TransformFullToSyntethic'; e = { $Job.Options.BackupTargetOptions.TransformFullToSyntethic } }, 
        @{n = 'TransformIncrementsToSyntethic'; e = { $Job.Options.BackupTargetOptions.TransformIncrementsToSyntethic } }, 
        @{n = 'TransformToSyntethicDays'; e = { $Job.Options.BackupTargetOptions.TransformToSyntethicDays } }, 
        @{n = 'PwdKeyId'; e = { $_.Info.PwdKeyId } }, 
        @{n = 'OriginalSize'; e = { $_.Info.IncludedSize } },
        @{n = 'RetentionType'; e = { $Job.BackupStorageOptions.RetentionType } },
        @{n = 'RetentionCount'; e = { $Job.BackupStorageOptions.RetainCycles } },
        @{n = 'RetainDaysToKeep'; e = { $Job.BackupStorageOptions.RetainDaysToKeep } },
        @{n = 'DeletedVmRetentionDays'; e = { $Job.BackupStorageOptions.RetainDays } },
        @{n = 'DeletedVmRetention'; e = { $Job.BackupStorageOptions.EnableDeletedVmDataRetention } },
        @{n = 'CompressionLevel'; e = { $Job.BackupStorageOptions.CompressionLevel } },
        @{n = 'Deduplication'; e = { $Job.BackupStorageOptions.EnableDeduplication } },
        @{n = 'BlockSize'; e = { $Job.BackupStorageOptions.StgBlockSize } },
        @{n = 'IntegrityChecks'; e = { $Job.BackupStorageOptions.EnableIntegrityChecks } },
        @{n = 'SpecificStorageEncryption'; e = { $Job.BackupStorageOptions.UseSpecificStorageEncryption } },
        @{n = 'StgEncryptionEnabled'; e = { $Job.BackupStorageOptions.StorageEncryptionEnabled } },
        @{n = 'KeepFirstFullBackup'; e = { $Job.BackupStorageOptions.KeepFirstFullBackup } },
        @{n = 'EnableFullBackup'; e = { $Job.BackupStorageOptions.EnableFullBackup } },
        @{n = 'BackupIsAttached'; e = { $Job.BackupStorageOptions.BackupIsAttached } },
        @{n = 'GfsWeeklyIsEnabled'; e = { $Job.options.gfspolicy.weekly.IsEnabled } },
        @{n = 'GfsWeeklyCount'; e = { $Job.options.gfspolicy.weekly.KeepBackupsForNumberOfWeeks } },
        @{n = 'GfsMonthlyEnabled'; e = { $Job.options.gfspolicy.Monthly.IsEnabled } },
        @{n = 'GfsMonthlyCount'; e = { $Job.options.gfspolicy.Monthly.KeepBackupsForNumberOfMonths } },
        @{n = 'GfsYearlyEnabled'; e = { $Job.options.gfspolicy.yearly.IsEnabled } },
        @{n = 'GfsYearlyCount'; e = { $Job.options.gfspolicy.yearly.KeepBackupsForNumberOfYears } },
        @{n = 'IndexingType'; e = { $Job.VssOptions.GuestFSIndexingType } }
  
        $AllJobs.Add($JobDetails) | Out-Null
    }
  



}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$AllJobs | Export-Csv -Path $("$ReportPath\$VBRServer" + '_Jobs.csv') -NoTypeInformation -ErrorAction SilentlyContinue
$configBackup | Export-Csv -Path $("$ReportPath\$VBRServer" + '_configBackup.csv') -NoTypeInformation
#SOBRS
# try {
#   $message = "Collecting SOBRs info..."
#   Write-LogFile($message)

#   # work here

#   Write-LogFile($message + "DONE")
  
# }
# catch {
#   Write-LogFile($message + "FAILED!")
#   $err = $Error[0].Exception
#   Write-LogFile($err.message)
# }

#WAN 
try {
    $message = "Collecting WAN ACC info..."
    Write-LogFile($message)

    # work here
    $wan = Get-VBRWANAccelerator


    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$wan | Export-csv -Path $("$ReportPath\$VBRServer" + '_WanAcc.csv') -NoTypeInformation
#########################################################################################################
#########################################################################################################
#########################################################################################################
# LICENSE SECTION
try {
    $message = "Collecting License info..."
    Write-LogFile($message)

    # work here
    $lic = Get-VBRInstalledLicense

    $licInfo = $lic | Select-Object "LicensedTo", "Edition", "ExpirationDate", "Type", "SupportId", "SupportExpirationDate", "AutoUpdateEnabled", "FreeAgentInstanceConsumptionEnabled", "CloudConnect",
    @{n = "LicensedSockets"; e = { $_.SocketLicenseSummary.LicensedSocketsNumber } },
    @{n = "UsedSockets"; e = { $_.SocketLicenseSummary.UsedSocketsNumber } },
    @{n = "RemainingSockets"; e = { $_.SocketLicenseSummary.RemainingSocketsNumber } },
    @{n = "LicensedInstances"; e = { $_.InstanceLicenseSummary.LicensedInstancesNumber } },
    @{n = "UsedInstances"; e = { $_.InstanceLicenseSummary.UsedInstancesNumber } },
    @{n = "NewInstances"; e = { $_.InstanceLicenseSummary.NewInstancesNumber } },
    @{n = "RentalInstances"; e = { $_.InstanceLicenseSummary.RentalInstancesNumber } },
    @{n = "LicensedCapacityTB"; e = { $_.CapacityLicenseSummary.LicensedCapacityTb } },
    @{n = "UsedCapacityTb"; e = { $_.CapacityLicenseSummary.UsedCapacityTb } }, "Status"


    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$licInfo | Export-csv -Path $("$ReportPath\$VBRServer" + '_LicInfo.csv') -NoTypeInformation
<# END LICENSE SECTION
#>
#########################################################################################################
#########################################################################################################
#########################################################################################################

<# Malware Detection Section #>
try {
    Get-VBRMalwareDetectionOptions | Export-Csv -Path $("$ReportPath\$VBRServer" + 'malware_settings.csv') -NoTypeInformation
    Get-VBRMalwareDetectionObject | Export-Csv -Path $("$ReportPath\$VBRServer" + 'malware_infectedobject.csv') -NoTypeInformation
    Get-VBRMalwareDetectionEvent | Export-Csv -Path $("$ReportPath\$VBRServer" + 'malware_events.csv') -NoTypeInformation
    Get-VBRMalwareDetectionExclusion | Export-Csv -Path $("$ReportPath\$VBRServer" + 'malware_exclusions.csv') -NoTypeInformation
}
catch {
    Write-LogFile("Failed on Malware Detection")
    Write-LogFile($error[0])
}

<# END Malware Detection Section #>

<# Security Section #>
if($VBRVersion -eq 12){
    try {
    try {
        # Force new scan
        write-LogFile("Starting Security & Compliance scan...")
        Start-VBRSecurityComplianceAnalyzer -ErrorAction SilentlyContinue -WarningAction SilentlyContinue -InformationAction SilentlyContinue
        Start-Sleep -Seconds 15
        # Capture scanner results
        $SecurityCompliances = [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
        write-LogFile("Security & Compliance scan completed.")

    $RuleTypes = @{
        'WindowsScriptHostDisabled'               = 'Windows Script Host is disabled'
        'BackupServicesUnderLocalSystem'          = 'Backup services run under the LocalSystem account'
        'OutdatedSslAndTlsDisabled'               = 'Outdated SSL And TLS are Disabled'
        'ManualLinuxHostAuthentication'           = 'Unknown Linux servers are not trusted automatically'
        'CSmbSigningAndEncryptionEnabled'         = 'SMB v3 signing is enabled'
        'ViProxyTrafficEncrypted'                 = 'Host to proxy traffic encryption should be enabled for the Network transport mode'
        'JobsTargetingCloudRepositoriesEncrypted' = 'Backup jobs to cloud repositories is encrypted'
        'LLMNRDisabled'                           = 'Link-Local Multicast Name Resolution (LLMNR) is disabled'
        'ImmutableOrOfflineMediaPresence'         = 'Immutable or offline media is used'
        'OsBucketsInComplianceMode'               = 'Os Buckets In Compliance Mode'
        'BackupServerUpToDate'                    = 'Backup Server is Up To Date'
        'BackupServerInProductionDomain'          = 'Computer is Workgroup member'
        'ReverseIncrementalInUse'                 = 'Reverse incremental backup mode is not used'
        'ConfigurationBackupEncryptionEnabled'    = 'Configuration backup encryption is enabled'
        'WDigestNotStorePasswordsInMemory'        = 'WDigest credentials caching is disabled'
        'WebProxyAutoDiscoveryDisabled'           = 'Web Proxy Auto-Discovery service (WinHttpAutoProxySvc) is disabled'
        'ContainBackupCopies'                     = 'All backups have at least one copy (the 3-2-1 backup rule)'
        'SMB1ProtocolDisabled'                    = 'SMB 1.0 is disabled'
        'EmailNotificationsEnabled'               = 'Email notifications are enabled'
        'RemoteRegistryDisabled'                  = 'Remote registry service is disabled'
        'PasswordsRotation'                       = 'Credentials and encryption passwords rotates annually'
        'WinRmServiceDisabled'                    = 'Remote powershell is disabled (WinRM service)'
        'MfaEnabledInBackupConsole'               = 'MFA is enabled'
        'HardenedRepositorySshDisabled'           = 'Hardened repositories have SSH disabled'
        'LinuxServersUsingSSHKeys'                = 'Linux servers have password-based authentication disabled'
        'RemoteDesktopServiceDisabled'            = 'Remote desktop protocol is disabled'
        'ConfigurationBackupEnabled'              = 'Configuration backup is enabled'
        'WindowsFirewallEnabled'                  = 'Windows firewall is enabled'
        'ConfigurationBackupEnabledAndEncrypted'  = 'Configuration backup is enabled and use encryption'
        'HardenedRepositoryNotVirtual'            = 'Hardened repositories are not hosted in virtual machines'
        'ConfigurationBackupRepositoryNotLocal'   = 'The configuration backup is not stored on the backup server'
        'PostgreSqlUseRecommendedSettings'        = 'PostgreSQL server uses recommended settings'
        'LossProtectionEnabled'                   = 'Password loss protection is enabled'
        'TrafficEncryptionEnabled'                = 'Encryption network rules added for LAN traffic'
        'NetBiosDisabled'                         = 'NetBIOS protocol should be disabled on all network interfaces'
        'HardenedRepositoryNotContainsNBDProxies' = 'Hardened repository should not be used as proxy'
        'LsassProtectedProcess'                   = 'Local Security Authority Server Service (LSASS) running as protected process'
    }
    $StatusObj = @{
        'Ok'            = "Passed"
        'Violation'     = "Not Implemented"
        'UnableToCheck' = "Unable to detect"
        'Suppressed'    = "Suppressed"
    }
    $OutObj = @()
    foreach ($SecurityCompliance in $SecurityCompliances) {
        try {
            # if (($RuleTypes[$SecurityCompliance.Type.ToString()] -eq "") -or $RuleTypes[$SecurityCompliance.Type.ToString()] -eq $null) {

            #     write-host("missing compliance= " + $SecurityCompliance.Type.ToString())
            # }
            # Write-PscriboMessage -IsWarning "$($SecurityCompliance.Type) = $($RuleTypes[$SecurityCompliance.Type.ToString()])"
            $inObj = [ordered] @{
                'Best Practice' = $RuleTypes[$SecurityCompliance.Type.ToString()]
                'Status'        = $StatusObj[$SecurityCompliance.Status.ToString()]


            }
            $OutObj += [pscustomobject]$inobj
        }
        catch {
            Write-Host "Security & Compliance summary table: $($_.Exception.Message)"
        }
    }
    #dump to csv file
    $OutObj | Export-Csv -Path $("$ReportPath\$VBRServer" + '_SecurityCompliance.csv') -NoTypeInformation
        }
    Catch {
        Write-Host "Security & Compliance summary command: $($_.Exception.Message)"
    }
}
catch {
    Write-Host "Security & Compliance summary section: $($_.Exception.Message)"
}
}


<# END Security Section #>
<#
SECTION: Protected Workloads Collection

This section is where Protected Workload Information is collected and dumped to CSV.

#>
# protected workloads
try {
    $message = "Collecting protected workloads info..."
    Write-LogFile($message)

    try {
        # work here
        ##Protected Workloads Area
        $vmbackups = Get-VBRBackup | ? { $_.TypeToString -eq "VMware Backup" }
        
        try {
            $vmNames = $vmbackups.GetLastOibs($true)
        }
        catch {
            try {
                $vmNames = $vmbackups.GetLastOibs()
            }
            catch {}
        }
        $unprotectedEntityInfo = Find-VBRViEntity | ? { $_.Name -notin $vmNames.Name }
        $protectedEntityInfo = Find-VBRViEntity -Name $vmNames.Name
    }

    catch {
        Write-LogFile("Failed on vmware workloads")
        Write-LogFile($error[0])
    }
    # protected HV Workloads
    try {
        $hvvmbackups = Get-VBRBackup | ? { $_.TypeToString -eq "Hyper-v Backup" }
            
            
        try {
            $hvvmNames = $hvvmbackups.GetLastOibs($true)
        }
        catch {
            try {
                $hvvmNames = $hvvmbackups.GetLastOibs()
            }
            catch {}
        }
            
        $unprotectedHvEntityInfo = Find-VBRHvEntity | ? { $_.Name -notin $hvvmNames.Name }
        if ($hvvmNames.Name -eq $Null) {
            $protectedHvEntityInfo = Find-VBRHvEntity -Name " "    
        }
        else {
            $protectedHvEntityInfo = Find-VBRHvEntity -Name $hvvmNames.Name

        }
        

    }
    catch {
        Write-LogFile("Failed on hyper-V workloads")
        Write-LogFile($error[0])
    }


    #protected physical Loads
    try {
        $phys = Get-VBRDiscoveredComputer

        $physbackups = Get-VBRBackup | ? { $_.TypeToString -like "*Agent*" }
            
        try {
            $pvmNames = $physbackups.GetLastOibs($true)
    
        }
        catch {
            try {
                $pvmNames = $physbackups.GetLastOibs()
            }
            catch {}
    
        }
    
        $notprotected = $phys | ? { $_.Name -notin $pvmNames.Name }
        $protected = $phys | ? { $_.Name -in $pvmNames.Name }
    }
    catch {
        Write-LogFile("Failed on physical workloads")
        Write-LogFile($error[0])
    }


}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
Write-LogFile("Exporting Protected Workloads Files...")
$protected | Export-Csv -Path $("$ReportPath\$VBRServer" + '_PhysProtected.csv') -NoTypeInformation
$notprotected | Export-Csv -Path $("$ReportPath\$VBRServer" + '_PhysNotProtected.csv') -NoTypeInformation
$protectedHvEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path | sort PoweredOn, Path, Name | Export-Csv -Path $("$ReportPath\$VBRServer" + '_HvProtected.csv') -NoTypeInformation
$unprotectedHvEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path, Type | sort Type, PoweredOn, Path, Name | Export-Csv -Path $("$ReportPath\$VBRServer" + '_HvUnprotected.csv') -NoTypeInformation
$protectedEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path | sort PoweredOn, Path, Name | Export-Csv -Path $("$ReportPath\$VBRServer" + '_ViProtected.csv') -NoTypeInformation
$unprotectedEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path, Type | sort Type, PoweredOn, Path, Name | Export-Csv -Path $("$ReportPath\$VBRServer" + '_ViUnprotected.csv') -NoTypeInformation
Write-LogFile("Exporting Protected Workloads Files...OK")
<#
END SECTION
#>
#########################################################################################################
#########################################################################################################
#########################################################################################################
<#
VBR VERSION INFO COLLECTION
#>
try {
    $message = "Collecting VBR Version info..."
    Write-LogFile($message)

    # work here
    #GetVbrVersion:
    $corePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "CorePath"
    $dbPath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlDataBaseName" -ErrorAction SilentlyContinue
    $instancePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlInstanceName" -ErrorAction SilentlyContinue
    $dbServerPath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlServerName" -ErrorAction SilentlyContinue
    $dbType = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations" -Name "SqlActiveConfiguration"
    $pgDbHost = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" -Name "SqlHostName"
    $pgDbDbName = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" -Name "SqlDatabaseName"
    $msDbHost = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlServerName"
    $msDbName = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlDatabaseName"
    if ($dbType.SqlActiveConfiguration -ne "PostgreSql") {
        if ($instancePath -eq "") {
            $instancePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlInstanceName"
        }
        
    }

    $depDLLPath = Join-Path -Path $corePath.CorePath -ChildPath "Packages\VeeamDeploymentDll.dll" -Resolve
    $file = Get-Item -Path $depDLLPath
    $version = $file.VersionInfo.ProductVersion
    $fixes = $file.VersionInfo.Comments
    try {
        # log debug line 
        Write-LogFile("Getting MFA Global Setting")
        $MFAGlobalSetting = [Veeam.Backup.Core.SBackupOptions]::get_GlobalMFA()
    } catch { Out-Null }


    #output VBR Versioning
    $VbrOutput = [pscustomobject][ordered] @{
        'Version'   = $version
        'Fixes'     = $fixes
        'SqlServer' = $dbServerPath.SqlServerName
        'Instance'  = $instancePath.SqlInstanceName
        'PgHost'    = $pgDbHost.SqlHostName
        'PgDb'      = $pgDbDbName.SqlDatabaseName
        'MsHost'    = $msDbHost.SqlServerName
        'MsDb'      = $msDbName.SqlDatabaseName
        'DbType'    = $dbType.SqlActiveConfiguration
        'MFA'       = $MFAGlobalSetting

    }

    #endGetVbrVersion

    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$VbrOutput | Export-Csv -Path $("$ReportPath\$VBRServer" + '_vbrinfo.csv') -NoTypeInformation



Disconnect-VBRServer
Pop-Location

function AddTypeInfo ($object, $jobType) {
    $object | Add-Member -MemberType NoteProperty -Name JobType -Value $jobType

}
#Get-VBRConfig -VBRServer localhost -ReportPath 'C:\temp\vHC\Original\VBR'
#Read-host("Complete! Press ENTER to close...")