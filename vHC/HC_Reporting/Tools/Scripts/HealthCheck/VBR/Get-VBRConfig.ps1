#Requires -Version 4
##Requires -RunAsAdministrator

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
    [int]$VBRVersion,
    [Parameter(Mandatory = $false)]
    [string]$User = "",
    [Parameter(Mandatory = $false)]
    [string]$Password = "",
    [Parameter(Mandatory = $false)]
    [string]$PasswordBase64 = "",
    [Parameter(Mandatory = $false)]
    [bool]$RemoteExecution = $false,
    [Parameter(Mandatory = $false)]
    [string]$ReportPath = "",
    [Parameter(Mandatory = $false)]
    [int]$ReportInterval = 14
)
# If ReportPath not provided, use default with server name and timestamp structure
if ([string]::IsNullOrEmpty($ReportPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = "C:\temp\vHC\Original\VBR\$VBRServer\$timestamp"
}
$logDir = "C:\temp\vHC\Original\Log\"
$logFile = $logDir + "VBRConfigScript.log"
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
    $input | Export-Csv -Path $file -NoTypeInformation  -ErrorAction SilentlyContinue
  
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
        [ValidateSet("Main", "Errors", "NoResult")][string]$LogName = "VeeamConfigScript",
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][String]$LogLevel = [LogLevel]::INFO
    )
    begin {}
    process {
        if (-not $PSBoundParameters.ContainsKey('Message') -or [string]::IsNullOrWhiteSpace($Message)) {
            Write-Warning "Write-LogFile called with no message. Skipping log write."
            return
        }
        if ([LogLevel]$LogLevel -ge [LogLevel]$global:SETTINGS.loglevel) {
            $outPath = $global:SETTINGS.OutputPath.Trim('\') + "\Collector" + $LogName + ".log"
            $dir = Split-Path -Path $outPath -Parent
            if (!(Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
            $line = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") + "`t" + $LogLevel + "`t`t" + $Message
            Add-Content -Path $outPath -Value $line -Encoding UTF8
            # Always echo to console for diagnostics
            Write-Host "[LOG:$LogLevel] $Message"
            if ($global:SETTINGS.DebugInConsole) {
                switch ([LogLevel]$LogLevel) {
                    [LogLevel]::WARNING { Write-Warning -Message $Message; break; }
                    [LogLevel]::ERROR { Write-Error -Message $Message; break; }
                    [LogLevel]::INFO { Write-Information -Message $Message; break; }
                    [LogLevel]::DEBUG { Write-Debug -Message $Message; break; }
                    [LogLevel]::PROFILE { Write-Debug -Message $Message; break; }
                    [LogLevel]::TRACE { Write-Verbose -Message $Message; break; }
                }
            }
        }
    }
    end {}
}


#log start of script
Write-LogFile("[Script Start]`tStarting VBR Config Collection...")

# Ensure Veeam module or snap-in is loaded
# PowerShell 7+ only supports modules, not PSSnapins
if ($PSVersionTable.PSVersion.Major -ge 6) {
    # PowerShell 6+ (Core/7+) - use module only
    if (!(Get-Module -Name Veeam.Backup.PowerShell -ErrorAction SilentlyContinue)) {
        Import-Module -Name Veeam.Backup.Powershell -ErrorAction Stop
    }
}
else {
    # Windows PowerShell 5.1 - try PSSnapin first, then module
    if (!(Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
        try {
            Add-PSSnapin -Name VeeamPSSnapIn -ErrorAction Stop
        }
        catch {
            # If PSSnapin fails, try module
            if (!(Get-Module -Name Veeam.Backup.PowerShell -ErrorAction SilentlyContinue)) {
                Import-Module -Name Veeam.Backup.Powershell -ErrorAction Stop
            }
        }
    }
}

# Always disconnect first (ignore errors)
try { Disconnect-VBRServer -ErrorAction SilentlyContinue } catch {}

# Determine if credentials are valid (non-empty, non-whitespace)
$useCreds = ($User -and $PasswordBase64 -and -not [string]::IsNullOrWhiteSpace($User) -and -not [string]::IsNullOrWhiteSpace($PasswordBase64))

if ($useCreds) {
    Write-LogFile("Attempting connection to VBR Server $VBRServer with credentials for user '$User' ...")
}
else {
    Write-LogFile("Attempting connection to VBR Server $VBRServer without credentials ...")
}

try {
    if ($useCreds) {
        # Decode Base64 password
        $passwordBytes = [System.Convert]::FromBase64String($PasswordBase64)
        $password = [System.Text.Encoding]::UTF8.GetString($passwordBytes)
        
        # Convert to SecureString and use PSCredential
        $securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential($User, $securePassword)
        Connect-VBRServer -Server $VBRServer -Credential $credential -ErrorAction Continue
    }
    else {
        Connect-VBRServer -Server $VBRServer -ErrorAction Stop
    }
    Write-LogFile("Connected to VBR Server: $VBRServer")
}
catch {
    $errMsg = ""
    try {
        $errMsg = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
    }
    catch {
        $errMsg = "Unable to get error details"
    }
    Write-LogFile("Failed to connect to VBR Server: " + $VBRServer + ". Error: " + $errMsg, "Errors", "ERROR")
    exit 1
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
if ($VBRVersion -ne 13) {
    try {
        $corePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "CorePath"
        $depDLLPath = Join-Path -Path $corePath.CorePath -ChildPath "Veeam.Backup.Core.dll" -Resolve
        $file = Get-Item -Path $depDLLPath
        $version = $file.VersionInfo.ProductVersion
        Write-LogFile("Detected Version: " + $version)
        $majorVersion = $version.Split('.')[0]
        Write-LogFile("Major Version: " + $majorVersion)

    }
    catch {
        Write-LogFile("Error on version detection. ")
    }
}
if ($VBRVersion -eq 0) {
    if ($majorVersion -eq 12) {
        $VBRVersion = 12
    }
    elseif ($majorVersion -eq 11) {
        $VBRVersion = 11
    }
    elseif ($majorVersion -eq 10) {
        $VBRVersion = 10
    }
    else {
        Write-LogFile("Unknown VBR Version: " + $majorVersion)
        $VBRVersion = 0
    }
}

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
$Servers | Export-VhcCsv -FileName '_Servers.csv'



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

$Proxies | Export-VhcCsv -FileName '_Proxies.csv'
$cdpProxy | Export-VhcCsv -FileName '_CdpProxy.csv'
#$fileProxy| Export-csv -Path $("$ReportPath\$VBRServer" + '_FileProxy.csv') -NoTypeInformation  -ErrorAction SilentlyContinue
$hvProxy | Export-VhcCsv -FileName '_HvProxy.csv'
$nasProxyOut | Export-VhcCsv -FileName '_NasProxy.csv'

## ENtra ID
try {
    $entraTenant = Get-VBREntraIDTenant



    # Define the custom object with properties tenantName and CacheRepoName
    $entraIDTenant = [PSCustomObject]@{
        tenantName    = $entraTenant.Name
        CacheRepoName = $entraTenant.cacherepository.name
    }
    #$entraIDTenant
    
    
    $eIdLogJobs = Get-VBREntraIDLogsBackupJob
    $entraIdLogJobs = [PSCustomObject]@{
        Name                   = $eIdLogJobs.Name
        Tenant                 = $eIdLogJobs.BackupObject.Tenant.Name
        shortTermRetType       = $eIdLogJobs.Name
        ShortTermRepo          = $eIdLogJobs.ShortTermBackupRepository.Name
        ShortTermRepoRetention = $eIdLogJobs.ShortTermRetentionPeriod
    
        CopyModeEnabled        = $eIdLogJobs.EnableCopyMode
        SecondaryTarget        = $eIdLogJobs.SecondaryTarget
    }
    #$entraIdLogJobs
    
    $eIdTenantBackup = Get-VBREntraIDTenantBackupJob
    
    $entraIdTenantJobs = [PSCustomObject]@{
        Name            = $eIdTenantBackup.Tenant.Name
        RetentionPolicy = $eIdTenantBackup.RetentionPolicy
    
    }
    # $entraIdTenantJobs
    
}
catch {
    Write-LogFile("Error on Entra ID collection. ")
}
$entraIdLogJobs | Export-VhcCsv -FileName '_entraLogJob.csv'
$entraIDTenant | Export-VhcCsv -FileName '_entraTenants.csv'
$entraIdTenantJobs | Export-VhcCsv -FileName '_entraTenantJob.csv'

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
$capOut | Export-VhcCsv -FileName '_capTier.csv'

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
$trafficRules | Export-VhcCsv -FileName '_trafficRules.csv'

# registry settings
if ($RemoteExecution -eq $false) {
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
}
$output | Export-VhcCsv -FileName '_regkeys.csv'

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
$repoInfo | Export-VhcCsv -FileName '_Repositories.csv'
$SOBROutput | Export-VhcCsv -FileName '_SOBRs.csv'
$AllSOBRExtentsOutput | Export-VhcCsv -FileName '_SOBRExtents.csv'
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
    $catCopy | Export-VhcCsv -FileName '_catCopyjob.csv'
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
    $catJob | Export-VhcCsv -FileName '_catalystJob.csv'

    try {
        $vaBJob = Get-VBRComputerBackupJob
    }
    catch {
        $vaBJob = $null
    }
    $vaBJob | Export-VhcCsv -FileName '_AgentBackupJob.csv'
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
    $epJob | Export-VhcCsv -FileName '_EndpointJob.csv'
    
    try {
        $sbJob = Get-VBRSureBackupJob
    }
    catch {
        $sbJob = $null
    }
    $sbJob | Export-VhcCsv -FileName '_SureBackupJob.csv'
    
    #tape jobs
    try {
        $tapeJob = Get-VBRTapeJob
    }
    catch {
        $tapeJob = $null
    }
    #export tape jobs to csv
    $tapeJob | Export-VhcCsv -FileName '_TapeJobs.csv'
    #end tape jobs

    # NAS Jobs
    Write-LogFile("Starting NAS Jobs collection...")
    try {
        # Get all NAS jobs
        Write-LogFile("Calling Get-VBRUnstructuredBackupJob...")
        $nasBackup = Get-VBRUnstructuredBackupJob
        Write-LogFile("Found " + $nasBackup.Count + " NAS backup jobs")
        
        # Process NAS jobs - use NAS-specific session cmdlet
        if ($nasBackup.Count -gt 0) {
            Write-LogFile("Fetching NAS backup sessions for the last " + $ReportInterval + " days...")
            $cutoffDate = (Get-Date).AddDays(-$ReportInterval)

            # Build hashtable of latest NAS session per job ID for O(1) lookup
            $nasSessionLookup = @{}
            try {
                # OPTIMIZED: Query sessions only for known NAS job names instead of all jobs
                $allNasSessions = @()
                foreach ($nasJob in $nasBackup) {
                    $jobSessions = Get-VBRBackupSession -Name $nasJob.Name | Where-Object { $_.CreationTime -gt $cutoffDate }
                    if ($jobSessions) {
                        $allNasSessions += $jobSessions
                    }
                }
                Write-LogFile("Found " + $allNasSessions.Count + " NAS sessions in the last " + $ReportInterval + " days")

                foreach ($session in $allNasSessions) {
                    $jobId = $session.JobId.ToString()
                    # Only keep the latest session per job ID
                    if (-not $nasSessionLookup.ContainsKey($jobId) -or
                        $session.CreationTime -gt $nasSessionLookup[$jobId].CreationTime) {
                        $nasSessionLookup[$jobId] = $session
                    }
                }
                Write-LogFile("Built NAS session lookup hashtable with " + $nasSessionLookup.Count + " unique jobs")
            }
            catch {
                Write-LogFile("Warning: Failed to get NAS sessions: " + $_.Exception.Message, "Warnings", "WARN")
            }

            # Process each NAS job
            $jobCounter = 0
            foreach ($job in $nasBackup) {
                $jobCounter++
                Write-LogFile("Processing NAS job " + $jobCounter + "/" + $nasBackup.Count + ": " + $job.Name)

                # Initialize defaults
                $onDiskGB = 0
                $sourceGb = 0

                # Lookup NAS session by job ID
                $jobId = $job.Id.ToString()
                if ($nasSessionLookup.ContainsKey($jobId)) {
                    $session = $nasSessionLookup[$jobId]
                    Write-LogFile("  Found NAS session for job: " + $job.Name)

                    # Extract metrics from NAS session - use Progress properties
                    if ($null -ne $session.Progress) {
                        $onDiskGB = $session.Progress.ProcessedUsedSize / 1GB
                        $sourceGb = $session.Progress.TotalSize / 1GB
                        Write-LogFile("  OnDiskGB: " + $onDiskGB + ", SourceGB: " + $sourceGb)
                    }
                }
                else {
                    Write-LogFile("  No NAS session found in last " + $ReportInterval + " days for job: " + $job.Name)
                }

                # Add member properties to job object
                $job | Add-Member -MemberType NoteProperty -Name JobType -Value "NAS Backup"
                $job | Add-Member -MemberType NoteProperty -Name OnDiskGB -Value $onDiskGB
                $job | Add-Member -MemberType NoteProperty -Name SourceGB -Value $sourceGb
                Write-LogFile("  Completed processing job: " + $job.Name)
            }
            Write-LogFile("NAS Jobs collection completed successfully")
        }
        else {
            Write-LogFile("No NAS backup jobs found")
        }

    }
    catch {
        Write-LogFile("NAS Jobs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $nasBackup = $null
    }
    
    Write-LogFile("Starting NAS Backup Copy Jobs collection...")
    try {
        $nasBCJ = Get-VBRNASBackupCopyJob 
        Write-LogFile("Found " + $nasBCJ.Count + " NAS backup copy jobs")
    }
    catch {
        Write-LogFile("NAS Backup Copy Jobs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $nasBCJ = $null
    }
    
    Write-LogFile("Starting Plugin Jobs collection...")
    try {
        $piJob = Get-VBRPluginJob
        Write-LogFile("Found " + $piJob.Count + " plugin jobs")
    }
    catch {
        Write-LogFile("Plugin Jobs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $piJob = $null
    }
    
    Write-LogFile("Starting CDP Policy collection...")
    try {
        $cdpJob = Get-VBRCDPPolicy
        Write-LogFile("Found " + $cdpJob.Count + " CDP policies")
    }
    catch {
        Write-LogFile("CDP Policy collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $cdpJob = $null
    }
    
    Write-LogFile("Starting VCD Replica Jobs collection...")
    try {
        $vcdJob = Get-VBRvCDReplicaJob
        Write-LogFile("Found " + $vcdJob.Count + " VCD replica jobs")
    }
    catch {
        Write-LogFile("VCD Replica Jobs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $vcdJob = $null
    }

    $piJob | Export-VhcCsv -FileName '_pluginjobs.csv'

  
    $vcdJob | Add-Member -MemberType NoteProperty -Name JobType -Value "VCD Replica"
    $vcdJob | Export-VhcCsv -FileName '_vcdjobs.csv'
  
    $cdpJob | Add-Member -MemberType NoteProperty -Name JobType -Value "CDP Policy"
    $cdpJob | Export-VhcCsv -FileName '_cdpjobs.csv'
  
    $nasBackup | Export-VhcCsv -FileName '_nasBackup.csv'
    $nasBCJ | Export-VhcCsv -FileName '_nasBCJ.csv'
  
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
        # adding fix suggestion from issue #76 Ben Thomas
        #$VBRJob = $PSItem
        # Find Restore Points
        try{
        $LastBackup = $Job.GetLastBackup()
            $RestorePoints = Get-VBRRestorePoint -Backup $LastBackup
        $TotalOnDiskGB = 0
        # Extract Restore Point Backup Sizes
        $RestorePoints.Foreach{
            $RestorePoint = $PSItem
            $OnDiskGB = $RestorePoint.GetStorage().Stats.BackupSize / 1GB # Convert from Bytes to GB
            $TotalOnDiskGB += $OnDiskGB
        }
    }
        catch{
            Write-LogFile("Warning: Could not get last backup for job: " + $Job.Name, "Warnings", "WARN")

            $TotalOnDiskGB = 0
        }

        # [pscustomobject]@{
        #     JobName       = $VBRJob.Name
        #     TotalOnDiskGB = $TotalOnDiskGB
        # }
        # log job name and on disk size
        Write-LogFile("Job: " + $Job.Name + " - Total OnDisk GB: " + $TotalOnDiskGB)

        # gather job details
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
        @{n = 'IndexingType'; e = { $Job.VssOptions.GuestFSIndexingType } },
        @{n = 'OnDiskGB'; e = { $TotalOnDiskGB } }
  
        $AllJobs.Add($JobDetails) | Out-Null
    }
  



}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$AllJobs | Export-VhcCsv -FileName '_Jobs.csv'
$configBackup | Export-VhcCsv -FileName '_configBackup.csv'
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
$wan | Export-VhcCsv -FileName '_WanAcc.csv'
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
$licInfo | Export-VhcCsv -FileName '_LicInfo.csv'
<# END LICENSE SECTION
#>
#########################################################################################################
#########################################################################################################
#########################################################################################################

<# Malware Detection Section #>
if ($VBRVersion -ge 12) {
    try {
        Get-VBRMalwareDetectionOptions | Export-VhcCsv -FileName 'malware_settings.csv'
        Get-VBRMalwareDetectionObject | Export-VhcCsv -FileName 'malware_infectedobject.csv'
        Get-VBRMalwareDetectionEvent | Export-VhcCsv -FileName 'malware_events.csv'
        Get-VBRMalwareDetectionExclusion | Export-VhcCsv -FileName 'malware_exclusions.csv'
    }
    catch {
        Write-LogFile("Failed on Malware Detection")
        Write-LogFile($error[0])
    }

    <# END Malware Detection Section #>

    <# Security Section #>
    if ($VBRVersion -gt 11) {
        Write-LogFile("VBR Version ($VBRVersion) supports Security & Compliance - starting collection...")
        try {
            try {
                # Force new scan
                Write-LogFile("Starting Security & Compliance scan...")
                Write-LogFile("Calling Start-VBRSecurityComplianceAnalyzer...")
                
                try {
                    Start-VBRSecurityComplianceAnalyzer -ErrorAction Stop -WarningAction SilentlyContinue -InformationAction SilentlyContinue
                    Write-LogFile("Start-VBRSecurityComplianceAnalyzer completed successfully")
                }
                catch {
                    $errMsg = ""
                    $errType = ""
                    try {
                        $errMsg = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
                        $errType = if ($_.Exception) { $_.Exception.GetType().FullName } else { "Unknown" }
                    }
                    catch {
                        $errMsg = "Unable to get error details"
                        $errType = "Unknown"
                    }
                    Write-LogFile("Start-VBRSecurityComplianceAnalyzer failed: " + $errMsg, "Errors", "ERROR")
                    Write-LogFile("Exception Type: " + $errType, "Errors", "ERROR")
                    throw
                }
                
                Write-LogFile("Waiting 45 seconds for scan to complete...")
                Start-Sleep -Seconds 45
                
                # Capture scanner results
                Write-LogFile("Attempting to retrieve scan results...")
                try {
                    # VBR 13+ uses Get-VBRSecurityComplianceAnalyzerResults cmdlet
                    # VBR 12 uses [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
                    if ($VBRVersion -ge 13) {
                        Write-LogFile("Using Get-VBRSecurityComplianceAnalyzerResults for VBR v13+")
                        $SecurityCompliances = Get-VBRSecurityComplianceAnalyzerResults
                    }
                    else {
                        Write-LogFile("Using database method for VBR v12")
                        $SecurityCompliances = [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
                    }
                    Write-LogFile("Retrieved " + $SecurityCompliances.Count + " compliance items")
                }
                catch {
                    $errMsg = ""
                    try {
                        $errMsg = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
                    }
                    catch {
                        $errMsg = "Unable to get error details"
                    }
                    Write-LogFile("Failed to retrieve compliance data: " + $errMsg, "Errors", "ERROR")
                    throw
                }
                
                Write-LogFile("Security & Compliance scan completed.")

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
                    'FirewallEnabled'                         = 'Windows firewall is enabled'
                    'ConfigurationBackupEnabledAndEncrypted'  = 'Configuration backup is enabled and use encryption'
                    'HardenedRepositoryNotVirtual'            = 'Hardened repositories are not hosted in virtual machines'
                    'ConfigurationBackupRepositoryNotLocal'   = 'The configuration backup is not stored on the backup server'
                    'PostgreSqlUseRecommendedSettings'        = 'PostgreSQL server uses recommended settings'
                    'LossProtectionEnabled'                   = 'Password loss protection is enabled'
                    'TrafficEncryptionEnabled'                = 'Encryption network rules added for LAN traffic'
                    'NetBiosDisabled'                         = 'NetBIOS protocol should be disabled on all network interfaces'
                    'HardenedRepositoryNotContainsNBDProxies' = 'Hardened repository should not be used as proxy'
                    'LsassProtectedProcess'                   = 'Local Security Authority Server Service (LSASS) running as protected process'
                    'PasswordsComplexityRules'                = 'Backup encryption password length and complexity recommendations should be followed'
                    'EncryptionPasswordsComplexityRules'      = 'Backup encryption password length and complexity recommendations should be followed'
                    'CredentialsPasswordsComplexityRules'     = 'Credentials password length and complexity recommendations should be followed'
                    'CredentialsGuardConfigured'              = 'Credential Guard is configured'
                    'LinuxAuditBinariesOwnerIsRoot'           = 'Linux audit binaries owner is root'
                    'LinuxAuditdConfigured'                   = 'Linux auditd is configured'
                    'LinuxDisableProblematicServices'         = 'Linux problematic services are disabled'
                    'LinuxOsHasVaRandomization'               = 'Linux OS has VA randomization enabled'
                    'LinuxOsIsFipsEnabled'                    = 'Linux OS has FIPS enabled'
                    'LinuxOsUsesTcpSyncookies'                = 'Linux OS uses TCP syncookies'
                    'LinuxUsePasswordPolicy'                  = 'Linux uses password policy'
                    'SecureBootEnable'                        = 'Secure Boot is enabled'
                    'LinuxUseSecurityModule'                  = 'Linux uses security module (SELinux/AppArmor)'
                    'LinuxWorldDirectoriesPermissions'        = 'Linux world-writable directories have proper permissions'


                }
                $StatusObj = @{
                    'Ok'            = "Passed"
                    'Violation'     = "Not Implemented"
                    'UnableToCheck' = "Unable to detect"
                    'Suppressed'    = "Suppressed"
                }
                $OutObj = New-Object System.Collections.ArrayList
                Write-LogFile("Processing " + $SecurityCompliances.Count + " compliance rules...")
                Write-LogFile("RuleTypes hashtable has " + $RuleTypes.Count + " entries")
                Write-LogFile("StatusObj hashtable has " + $StatusObj.Count + " entries")
                
                $processedCount = 0
                $skippedCount = 0
                $errorCount = 0
                
                foreach ($SecurityCompliance in $SecurityCompliances) {
                    try {
                        $complianceType = $null
                        $complianceStatus = $null
                        
                        # Safely get Type property
                        if ($SecurityCompliance.Type) {
                            $complianceType = $SecurityCompliance.Type.ToString()
                        }
                        else {
                            Write-LogFile("Warning: Compliance item has null Type - skipping", "Main", "WARNING")
                            $skippedCount++
                            continue
                        }
                        
                        # Safely get Status property
                        if ($SecurityCompliance.Status) {
                            $complianceStatus = $SecurityCompliance.Status.ToString()
                        }
                        else {
                            Write-LogFile("Warning: Compliance item '" + $complianceType + "' has null Status - skipping", "Main", "WARNING")
                            $skippedCount++
                            continue
                        }
                        
                        # Check if rule type exists in mapping
                        if (-not $RuleTypes.ContainsKey($complianceType)) {
                            Write-LogFile("Warning: Unknown compliance type '" + $complianceType + "' - skipping", "Main", "WARNING")
                            $skippedCount++
                            continue
                        }
                        
                        # Check if status exists in mapping
                        if (-not $StatusObj.ContainsKey($complianceStatus)) {
                            Write-LogFile("Warning: Unknown compliance status '" + $complianceStatus + "' for type '" + $complianceType + "' - skipping", "Main", "WARNING")
                            $skippedCount++
                            continue
                        }
                        
                        $inObj = [ordered] @{
                            'Best Practice' = $RuleTypes[$complianceType]
                            'Status'        = $StatusObj[$complianceStatus]
                        }
                        [void]$OutObj.Add([pscustomobject]$inobj)
                        $processedCount++
                    }
                    catch {
                        $errorCount++
                        $errMsg = ""
                        $errType = ""
                        try {
                            if ($_.Exception.Message) {
                                $errMsg = $_.Exception.Message.ToString()
                            }
                            else {
                                $errMsg = "No error message available"
                            }
                            if ($_.Exception) {
                                $errType = $_.Exception.GetType().FullName
                            }
                            else {
                                $errType = "Unknown exception type"
                            }
                        }
                        catch {
                            $errMsg = "Unable to retrieve error message"
                            $errType = "Unable to retrieve exception type"
                        }
                        Write-LogFile("Error processing compliance rule (" + $errorCount + "): " + $errMsg, "Errors", "ERROR")
                        Write-LogFile("Exception Type: " + $errType, "Errors", "ERROR")
                        if ($complianceType) {
                            Write-LogFile("Rule Type: " + $complianceType, "Errors", "ERROR")
                        }
                        if ($complianceStatus) {
                            Write-LogFile("Rule Status: " + $complianceStatus, "Errors", "ERROR")
                        }
                    }
                }
                
                Write-LogFile("Foreach loop completed")
                Write-LogFile("Processed " + $processedCount + " compliance rules, skipped " + $skippedCount + ", errors " + $errorCount)
                Write-LogFile("OutObj count: " + $OutObj.Count)
                
                #dump to csv file
                if ($OutObj.Count -gt 0) {
                    try {
                        Write-LogFile("Exporting " + $OutObj.Count + " compliance items to CSV...")
                        $OutObj | Export-VhcCsv -FileName '_SecurityCompliance.csv'
                        Write-LogFile("Security Compliance CSV export completed successfully")
                    }
                    catch {
                        $errMsg = ""
                        $errType = ""
                        $stackTrace = ""
                        try {
                            $errMsg = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
                            $errType = if ($_.Exception) { $_.Exception.GetType().FullName } else { "Unknown" }
                            $stackTrace = if ($_.ScriptStackTrace) { $_.ScriptStackTrace.ToString() } else { "No stack trace" }
                        }
                        catch {
                            $errMsg = "Unable to get error details"
                            $errType = "Unknown"
                            $stackTrace = "Unable to get stack trace"
                        }
                        Write-LogFile("Failed to export Security Compliance CSV: " + $errMsg, "Errors", "ERROR")
                        Write-LogFile("Exception Type: " + $errType, "Errors", "ERROR")
                        Write-LogFile("Stack Trace: " + $stackTrace, "Errors", "ERROR")
                    }
                }
                else {
                    Write-LogFile("No compliance data to export - OutObj is empty", "Main", "WARNING")
                }
            }
            Catch {
                $errMsg = ""
                try {
                    if ($_.Exception.Message) { 
                        $errMsg = $_.Exception.Message.ToString() 
                    }
                    else { 
                        $errMsg = $_.ToString() 
                    }
                }
                catch {
                    $errMsg = "Unable to get error message"
                }
                $errType = if ($_.Exception) { $_.Exception.GetType().FullName } else { "Unknown" }
                $stackTrace = ""
                try {
                    $stackTrace = if ($_.ScriptStackTrace) { $_.ScriptStackTrace.ToString() } else { "No stack trace available" }
                }
                catch {
                    $stackTrace = "Unable to get stack trace"
                }
                Write-LogFile("Security & Compliance summary command failed: " + $errMsg, "Errors", "ERROR")
                Write-LogFile("Exception Type: " + $errType, "Errors", "ERROR")
                Write-LogFile("Stack Trace: " + $stackTrace, "Errors", "ERROR")
            }
        }
        catch {
            $errMsg = ""
            try {
                if ($_.Exception.Message) { 
                    $errMsg = $_.Exception.Message.ToString() 
                }
                else { 
                    $errMsg = $_.ToString() 
                }
            }
            catch {
                $errMsg = "Unable to get error message"
            }
            $stackTrace = ""
            try {
                $stackTrace = if ($_.ScriptStackTrace) { $_.ScriptStackTrace.ToString() } else { "No stack trace available" }
            }
            catch {
                $stackTrace = "Unable to get stack trace"
            }
            Write-LogFile("Security & Compliance summary section failed: " + $errMsg, "Errors", "ERROR")
            Write-LogFile("Stack Trace: " + $stackTrace, "Errors", "ERROR")
        }
    }
    else {
        Write-LogFile("VBR Version ($VBRVersion) does not support Security & Compliance - skipping")
    }


    <# END Security Section #>
}

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
$protected | Export-VhcCsv -FileName '_PhysProtected.csv'
$notprotected | Export-VhcCsv -FileName '_PhysNotProtected.csv'
$protectedHvEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path | sort PoweredOn, Path, Name | Export-VhcCsv -FileName '_HvProtected.csv'
$unprotectedHvEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path, Type | sort Type, PoweredOn, Path, Name | Export-VhcCsv -FileName '_HvUnprotected.csv'
$protectedEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path | sort PoweredOn, Path, Name | Export-VhcCsv -FileName '_ViProtected.csv'
$unprotectedEntityInfo | select Name, PowerState, ProvisionedSize, UsedSize, Path, Type | sort Type, PoweredOn, Path, Name | Export-VhcCsv -FileName '_ViUnprotected.csv'
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
    try {
        $corePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "CorePath"  -ErrorAction SilentlyContinue
        $dbPath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlDataBaseName" -ErrorAction SilentlyContinue
        $instancePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlInstanceName" -ErrorAction SilentlyContinue
        $dbServerPath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" -Name "SqlServerName" -ErrorAction SilentlyContinue
        $dbType = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations" -Name "SqlActiveConfiguration"  -ErrorAction SilentlyContinue
        $pgDbHost = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" -Name "SqlHostName"  -ErrorAction SilentlyContinue
        $pgDbDbName = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" -Name "SqlDatabaseName"  -ErrorAction SilentlyContinue
        $msDbHost = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlServerName"  -ErrorAction SilentlyContinue
        $msDbName = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlDatabaseName"  -ErrorAction SilentlyContinue
        if ($dbType.SqlActiveConfiguration -ne "PostgreSql") {
            if ($instancePath -eq "") {
                $instancePath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" -Name "SqlInstanceName"  -ErrorAction SilentlyContinue
            }
        
        }

        $depDLLPath = Join-Path -Path $corePath.CorePath -ChildPath "Veeam.Backup.Core.dll" -Resolve  -ErrorAction SilentlyContinue
        $file = Get-Item -Path $depDLLPath
        $version = $file.VersionInfo.ProductVersion
        $fixes = $file.VersionInfo.Comments
    }
    Catch {
        Write-LogFile("Failed to get VBR Version info. Possibly due to remote execution.")
    }
    try {
        # log debug line 
        Write-LogFile("Getting MFA Global Setting")
        $MFAGlobalSetting = [Veeam.Backup.Core.SBackupOptions]::get_GlobalMFA()
    }
    catch {
        Write-LogFile("Failed to get MFA Global Setting, likely pre-VBR 12") 
        $MFAGlobalSetting = "N/A - Pre VBR 12"
    }


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
$VbrOutput | Export-VhcCsv -FileName '_vbrinfo.csv'



Disconnect-VBRServer
Pop-Location

function AddTypeInfo ($object, $jobType) {
    $object | Add-Member -MemberType NoteProperty -Name JobType -Value $jobType

}
#Get-VBRConfig -VBRServer localhost -ReportPath 'C:\temp\vHC\Original\VBR'
#Read-host("Complete! Press ENTER to close...")