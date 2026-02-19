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
    [int]$ReportInterval = 14,
    [Parameter(Mandatory = $false)]
    [switch]$RescanHosts
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
} else {
    # Windows PowerShell 5.1 - try PSSnapin first, then module
    if (!(Get-PSSnapin -Name VeeamPSSnapIn -ErrorAction SilentlyContinue)) {
        try {
            Add-PSSnapin -Name VeeamPSSnapIn -ErrorAction Stop
        } catch {
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
    } catch {
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

$BackupServer = Get-VBRBackupServerInfo
$VBRVersion = $BackupServer.Build.Major

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
else{
$version = $BackupServer.Build
Write-LogFile("Detected Version: " + $version)
$majorVersion = $BackupServer.Build.Major
Write-LogFile("Major Version: " + $majorVersion)
}
if ($VBRVersion -eq 0 -and $VBRVersion -ne 13) {
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

    $VServers = Get-VBRServer
    $Servers = $VServers | Select-Object -Property "Info", "ParentId", "Id", "Uid", "Name", "Reference", "Description", "IsUnavailable", "Type", "ApiVersion", "PhysHostId", "ProxyServicesCreds", @{name = 'Cores'; expression = { $_.GetPhysicalHost().hardwareinfo.CoresCount } }, @{name = 'CPUCount'; expression = { $_.GetPhysicalHost().hardwareinfo.CPUCount } }, @{name = 'RAM'; expression = { $_.GetPhysicalHost().hardwareinfo.PhysicalRamTotal } }, @{name = 'OSInfo'; expression = { $_.Info.Info } }
    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$Servers | Export-VhcCsv -FileName '_Servers.csv'



#Components collection - concurrency inspector

Write-LogFile("Collecting component info for concurrency inspection...")

#Requirements as per Veeam User Guide - Verify them and change if needed

#VMware - Hyper-V Proxy Requirements:
$VPProxyRAMReq = 1    #1 GB per task
$VPProxyCPUReq = 0.5  #1 CPU core per 2 tasks
$VPProxyOSCPUReq = 2 #2 CPU core per OS Vi and Hv
$VPProxyOSRAMReq = 2  #2 GB per OS Vi and Hv

#General Purprose Proxy
$GPProxyRAMReq = 4  #4GB per task
$GPProxyCPUReq = 2   #2 CPU core per task
$GPProxyOSCPUReq = 2 #2 CPU core per OS General Purpose Proxy
$GPProxyOSRAMReq = 4  #2 GB per OS General Purpose Proxy

# Repository / Gateway Requirements:
$RepoGWRAMReq = 1    #1 GB per task
$RepoGWCPUReq = 0.5  #1 CPU core per 2 tasks
$RepoOSCPUReq = 1   #1 CPU core per OS Repository/Gateway
$RepoOSRAMReq = 4   #4 GB per OS Repository/Gateway

# CDP Proxy Requirements:
$CDPProxyRAMReq = 8    #8 GB
$CDPProxyCPUReq = 4    #4 CPU core 

#Backup Server
if($VBRVersion -eq 13){
    $BSCPUReq = 8
    $BSRAMReq = 16
    } else {
    $BSCPUReq = 4
    $BSRAMReq = 8
    }

#SQL Server Requirements, if it is on the same server with any backup component, otherwise, skipped - printed only
$SQLRAMReq = 2    #2 GB, min.
$SQLCPUReq = 1    #1 CPU core  min.

#convert to GB from Bytes
function ConverttoGB ($inBytes) {
    $inGB = [math]::Floor($inBytes / 1GB)
    return $inGB
}

#ensure values are non-negative
function EnsureNonNegative {
    param (
        [int]$Value
    )
    
    if ($Value -lt 0) {
        return 0
    } else {
        return $Value
    }
}

#avoid null values
function SafeValue($value) {
if ($null -eq $value) { 0 } else { $value }
}

# Check the user's response
if ($RescanHosts) {
try{
    Write-LogFile("Rescanning all hosts... Please wait.")
    Rescan-VBREntity -AllHosts -Wait
    Write-LogFile("Rescan complete. Proceeding with data retrieval.")
    }
catch {
    Write-LogFile("Rescan FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
} else {
    Write-LogFile("Skipping rescan. Using existing data.")
}

try {
    $message = "Collecting Proxy and Repository info..."
    Write-LogFile($message)

     #Get all VMware proxies - avoid null
    $VMwareProxies = @(Get-VBRViProxy)

    #Get all Hyper-V Off-Host proxies
    $HyperVProxies = @(Get-VBRHvProxy)

    # Get all CDP proxies
    $CDPProxies = @(Get-VBRCDPProxy)

    $VPProxies = $VMwareProxies + $HyperVProxies 

    # Get all VBR Repositories
    $VBRRepositories = @(Get-VBRBackupRepository)

    #Get All GP Proxies
    $GPProxies = @(Get-VBRNASProxyServer)

    Write-LogFile($message + "DONE")

}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

$ProxyData = @()
$CDPProxyData = @()
$GWData = @()
$RepoData = @()
$GPProxyData = @()
$RequirementsComparison = @()
$hostRoles = @{}

#Get SQL Server Host
function Get-SqlSName {
    # Define registry paths and keys
    $basePath = "HKLM:\SOFTWARE\Veeam\Veeam Backup and Replication"
    $databaseConfigurationPath = "$basePath\DatabaseConfigurations"
    $sqlActiveConfigurationKey = "SqlActiveConfiguration"
    $postgreSqlPath = "$databaseConfigurationPath\PostgreSql"
    $msSqlPath = "$databaseConfigurationPath\MsSql"
    $sqlServerNameKey = "SqlServerName"
    $sqlHostNameKey = "SqlHostName"
    $SQLSName = $null

    try {
        $SQLSName = (Get-ItemProperty -Path $basePath -Name $sqlServerNameKey -ErrorAction Stop).SqlServerName
    } catch {
        try {
            $sqlActiveConfig = Get-ItemProperty -Path $databaseConfigurationPath -Name $sqlActiveConfigurationKey -ErrorAction Stop
            $activeConfigValue = $sqlActiveConfig.$sqlActiveConfigurationKey

            if ($activeConfigValue -eq "PostgreSql") {
                $SQLSName = (Get-ItemProperty -Path $postgreSqlPath -Name $sqlHostNameKey -ErrorAction Stop).SqlHostName
            } else {
                $SQLSName = (Get-ItemProperty -Path $msSqlPath -Name $sqlServerNameKey -ErrorAction Stop).SqlServerName
            }
        } catch {
            Write-Error "Unable to retrieve SQL Server name from registry."
        }
    }

    If ($SQLSName -eq "localhost") {
        $SQLSName = $VBRServer
    }
return $SQLSName
}

#Gather GP Proxy Data
try{
    $message = "Calculating GP Proxy Data..."
    Write-LogFile($message)

    foreach ($GPProxy in $GPProxies) {
        $NrofGPProxyTasks = $GPProxy.ConcurrentTaskNumber
        $Serv = $VServers | Where-Object {$_.Name -eq $GPProxy.Server.Name}
        $GPProxyCores = $Serv.GetPhysicalHost().HardwareInfo.CoresCount
        $GPProxyRAM = ConverttoGB($Serv.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
    
        $GPProxyDetails = [PSCustomObject]@{
            "ConcurrentTaskNumber"  = $NrofGPProxyTasks
            "Host"                  = $GPProxy.Server.Name
            "HostId"                = $GPProxy.Server.Id
        }                        

        $GPProxyData += $GPProxyDetails

        # Track host roles with Proxy.Name
        if (-not $hostRoles.ContainsKey($GPProxy.Server.Name)) {
            $hostRoles[$GPProxy.Server.Name] = [ordered]@{
                "Roles" = @("GPProxy")
                "Names" = @($GPProxy.Server.Name) 
                "TotalTasks" = 0
                "Cores" = $GPProxyCores
                "RAM" = $GPProxyRAM
                "Task" = $NrofGPProxyTasks
                "TotalGPProxyTasks" = 0
            }
        } else {
            $hostRoles[$GPProxy.Server.Name].Roles += "GPProxy"
            $hostRoles[$GPProxy.Server.Name].Names += $GPProxy.Server.Name
        }
        $hostRoles[$GPProxy.Server.Name].TotalGPProxyTasks += $NrofGPProxyTasks
        $hostRoles[$GPProxy.Server.Name].TotalTasks += $NrofGPProxyTasks
    }
        Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

# Gather VMware and Hyper-V Proxy Data
try{
    $message = "Calculating Vi and HV Proxy Data..."
    Write-LogFile($message)

    foreach ($Proxy in $VPProxies) {
        $NrofProxyTasks = $Proxy.MaxTasksCount
       try { $ProxyCores = $Proxy.GetPhysicalHost().HardwareInfo.CoresCount
        $ProxyRAM = ConverttoGB($Proxy.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal) }
        catch{
         $Server = $VServers | Where-Object {$_.Name -eq $Proxy.Name}
                $ProxyCores = $Server.GetPhysicalHost().HardwareInfo.CoresCount
                $ProxyRAM = ConverttoGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
        }
    
        if ($proxy.Type -eq "Vi") { $proxytype = "VMware" } else {$proxytype = $proxy.Type}

        $ProxyDetails = [PSCustomObject]@{
            "Id"                 = $Proxy.Id
            "Name"               = $Proxy.Name
            "Description"        = $Proxy.Description
            "Info"               = $Proxy.Info
            "HostId"             = $Proxy.Host.Id
            "Host"               = $Proxy.Host.Name
            "Type"               = $proxytype
            "IsDisabled"         = $Proxy.IsDisabled
            "Options"            = $Proxy.Options
            "MaxTasksCount"      = $NrofProxyTasks
            "UseSsl"             = if ($proxy.Type -eq "Vi") { $Proxy.Options.UseSsl } else { "" }
            "FailoverToNetwork"  = if ($proxy.Type -eq "Vi") { $Proxy.Options.FailoverToNetwork } else { "" }
            "TransportMode"      = if ($proxy.Type -eq "Vi") { $Proxy.Options.TransportMode } else { "" }
            "IsVbrProxy"         = ""
            "ChosenVm"           = if ($proxy.Type -eq "Vi") { $Proxy.Options.ChosenVm } else { "" }
            "ChassisType"        = $Proxy.ChassisType
        }                       

        $ProxyData += $ProxyDetails

        # Track host roles with Proxy.Name
        if (-not $hostRoles.ContainsKey($Proxy.Host.Name)) {
            $hostRoles[$Proxy.Host.Name] = [ordered]@{
                "Roles" = @("Proxy")
                "Names" = @($Proxy.Name) 
                "TotalTasks" = 0
                "Cores" = $ProxyCores
                "RAM" = $ProxyRAM
                "TotalVpProxyTasks" = 0
            }
        } else {
            $hostRoles[$Proxy.Host.Name].Roles += "Proxy"
            $hostRoles[$Proxy.Host.Name].Names += $Proxy.Name
        }
        $hostRoles[$Proxy.Host.Name].TotalVpProxyTasks += $NrofProxyTasks
        $hostRoles[$Proxy.Host.Name].TotalTasks += $NrofProxyTasks
    }
        Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

# Gather CDP Proxy Data
try{
    $message = "Calculating CDP Proxy Data..."
    Write-LogFile($message)

    foreach ($CDPProxy in $CDPProxies) {
        $CDPServer = $VServers | Where-Object {$_.Id -eq $CDPProxy.ServerId}
    
        $CDPProxyCores = $CDPServer.GetPhysicalHost().HardwareInfo.CoresCount
        $CDPProxyRAM = ConverttoGB($CDPServer.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
        
        $CDPProxyDetails = [PSCustomObject]@{
            "ServerId"                  = $CDPProxy.ServerId
            "CacheSize"                 = $CDPProxy.CacheSize
            "CachePath"                 = $CDPProxy.CachePath
            "IsEnabled"                 = $CDPProxy.IsEnabled
            "SourceProxyTrafficPort"    = $CDPProxy.SourceProxyTrafficPort
            "TargetProxyTrafficPort"    = $CDPProxy.TargetProxyTrafficPort
            "Id"                        = $CDPProxy.Id
            "Name"                      = $CDPProxy.Name
            "Description"               = $CDPProxy.Description
        }

        $CDPProxyData += $CDPProxyDetails

        # Track host roles with CDPProxy.Name
        if (-not $hostRoles.ContainsKey($CDPServer.Name)) {
            $hostRoles[$CDPServer.Name] = [ordered]@{
                "Roles" = @("CDPProxy")
                "Names" = @($CDPProxy.Name)  
                "TotalTasks" = 0
                "Cores" = $CDPProxyCores
                "RAM" = $CDPProxyRAM
                "TotalCDPProxyTasks" = 0
            }
        } else {
            $hostRoles[$CDPServer.Name].Roles += "CDPProxy"
            $hostRoles[$CDPServer.Name].Names += $CDPProxy.Name 
        }
        $hostRoles[$CDPServer.Name].TotalCDPProxyTasks += 1
    }
        Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!" + $CDPServer +  $CDPProxyCores)
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

# Gather Repository and Gateway Data
try{
    $message = "Calculating Repository and GW Data..."
    Write-LogFile($message)

    foreach ($Repository in $VBRRepositories) {
        $NrofRepositoryTasks = $Repository.Options.MaxTaskCount
        $gatewayServers = $Repository.GetActualGateways()
        $NrofgatewayServers = $gatewayServers.Count
        
        if ($NrofRepositoryTasks -eq -1) {
        $NrofRepositoryTasks = 128
        } 

        if ($gatewayServers.Count -gt 0) {
            foreach ($gatewayServer in $gatewayServers) {
                $Server = $VServers | Where-Object {$_.Name -eq $gatewayServer.Name}
                $GWCores = $Server.GetPhysicalHost().HardwareInfo.CoresCount
                $GWRAM = ConverttoGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)

                $RepositoryDetails = [PSCustomObject]@{
                    "Repository Name"   = $Repository.Name
                    "Gateway Server"    = $gatewayServer.Name
                    "Gateway Cores"     = $GWCores
                    "Gateway RAM (GB)"  = $GWRAM        
                    "Concurrent Tasks"  = $NrofRepositoryTasks / $NrofgatewayServers
                }                        
                $GWData += $RepositoryDetails

                # Track host roles
                if (-not $hostRoles.ContainsKey($gatewayServer.Name)) {
                    $hostRoles[$gatewayServer.Name] = [ordered]@{
                        "Roles" = @("Gateway")
                        "Names" = @($gatewayServer.Name) 
                        "TotalTasks" = 0
                        "Cores" = $GWCores
                        "RAM" = $GWRAM
                        "TotalGWTasks" = 0
                    }
                } else {
                    $hostRoles[$gatewayServer.Name].Roles += "Gateway"
                    $hostRoles[$gatewayServer.Name].Names += $Repository.Name
                }
                $hostRoles[$gatewayServer.Name].TotalGWTasks += $NrofRepositoryTasks / $NrofgatewayServers
                $hostRoles[$gatewayServer.Name].TotalTasks += $NrofRepositoryTasks / $NrofgatewayServers 
            }
        } else {
            # Handle the repository host
            $Server = $VServers | Where-Object {$_.Name -eq $Repository.Host.Name}
            $RepoCores = $Server.GetPhysicalHost().HardwareInfo.CoresCount
            $RepoRAM = ConverttoGB($Server.GetPhysicalHost().HardwareInfo.PhysicalRAMTotal)
            
            $RepositoryDetails = [PSCustomObject]@{
                "Repository Name"   = $Repository.Name
                "Repository Server" = $Repository.Host.Name
                "Repository Cores"  = $RepoCores
                "Repository RAM (GB)" = $RepoRAM        
                "Concurrent Tasks"   = $NrofRepositoryTasks
            }            
            $RepoData += $RepositoryDetails
   
            # Track host roles
            if (-not $hostRoles.ContainsKey($Repository.Host.Name)) {
                $hostRoles[$Repository.Host.Name] = [ordered]@{
                    "Roles" = @("Repository")
                    "Names" = @($Repository.Name)
                    "TotalTasks" = 0
                    "Cores" = $RepoCores
                    "RAM" = $RepoRAM
                    "TotalRepoTasks" = 0
                }
            } else {
                $hostRoles[$Repository.Host.Name].Roles += "Repository"
                $hostRoles[$Repository.Host.Name].Names += $Repository.Name
            }
            $hostRoles[$Repository.Host.Name].TotalRepoTasks += $NrofRepositoryTasks
            $hostRoles[$Repository.Host.Name].TotalTasks += $NrofRepositoryTasks
        }
    }
        Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

#Add Backup Server role to existing components
 try{    
    if($VBRServer -eq "localhost"){
    $tempserv = [System.Net.Dns]::GetHostByName(($env:computerName)).HostName
    $hostRoles[$tempserv].Roles += ("BackupServer" -join '/ ')
    }
    else {
    $hostRoles[$VBRServer].Roles += ("BackupServer" -join '/ ')
    }
    Write-LogFile("Backup Server role is added for the host: "  + $VBRServer)
} 
catch{
    Write-LogFile("Backup Server: " + $VBRServer + "is not used in another role.")
}

$BackupServerType = ($VServers | Where-Object {$_.Name -eq $BackupServer.Name}).Type

if($BackupServerType -ne "Linux") {
$SQLServer = Get-SqlSName

try {
    $hostRoles[$SQLServer].Roles += ("SQLServer" -join ', ')
    Write-LogFile("SQL Server role is added for the host: "  + $SQLServer)
} catch {
    Write-LogFile("SQLServer is: "  + $SQLServer)
    }   
}

#Calculate requirements based on aggregated resources for multi-role servers
try{
    $message = "Calculating Requirements based on aggregated resources for multi-role servers..."
    Write-LogFile($message)

    foreach ($server in $hostRoles.GetEnumerator()) {
        $SuggestedTasksByCores = 0 
        $SuggestedTasksByRAM = 0
        $serverName = $server.Key

        # Pre-compute OS overhead conditionals (PS5.1 compatible - no ternary operator)
        $RepoGWOSCPUOverhead  = if ((SafeValue $server.Value.TotalRepoTasks) -gt 0 -or (SafeValue $server.Value.TotalGWTasks) -gt 0) { $RepoOSCPUReq } else { 0 }
        $VpProxyOSCPUOverhead = if ((SafeValue $server.Value.TotalVpProxyTasks) -gt 0) { $VPProxyOSCPUReq } else { 0 }
        $GPProxyOSCPUOverhead = if ((SafeValue $server.Value.TotalGPProxyTasks) -gt 0) { $GPProxyOSCPUReq } else { 0 }
        $CDPProxyOSCPUOverhead = if ((SafeValue $server.Value.TotalCDPProxyTasks) -gt 0) { $CDPProxyOSCPUReq } else { 0 }
        $RepoGWOSRAMOverhead  = if ((SafeValue $server.Value.TotalRepoTasks) -gt 0 -or (SafeValue $server.Value.TotalGWTasks) -gt 0) { $RepoOSRAMReq } else { 0 }
        $VpProxyOSRAMOverhead = if ((SafeValue $server.Value.TotalVpProxyTasks) -gt 0) { $VPProxyOSRAMReq } else { 0 }
        $GPProxyOSRAMOverhead = if ((SafeValue $server.Value.TotalGPProxyTasks) -gt 0) { $GPProxyOSRAMReq } else { 0 }
        $CDPProxyOSRAMOverhead = if ((SafeValue $server.Value.TotalCDPProxyTasks) -gt 0) { $CDPProxyOSRAMReq } else { 0 }

        $RequiredCores = [Math]::Ceiling(
            (SafeValue $server.Value.TotalRepoTasks)    * $RepoGWCPUReq +
            (SafeValue $server.Value.TotalGWTasks)      * $RepoGWCPUReq +
            (SafeValue $server.Value.TotalVpProxyTasks) * $VPProxyCPUReq +
            (SafeValue $server.Value.TotalGPProxyTasks)* $GPProxyCPUReq +
            (SafeValue $server.Value.TotalCDPProxyTasks)* $CDPProxyCPUReq +

            # OS overhead added if the server hosts that role (any tasks > 0)
            $RepoGWOSCPUOverhead +
            $VpProxyOSCPUOverhead +
            $GPProxyOSCPUOverhead
        )

        $RequiredRAM = [Math]::Ceiling(
            (SafeValue $server.Value.TotalRepoTasks)    * $RepoGWRAMReq +
            (SafeValue $server.Value.TotalGWTasks)      * $RepoGWRAMReq +
            (SafeValue $server.Value.TotalVpProxyTasks) * $VPProxyRAMReq +
            (SafeValue $server.Value.TotalGPProxyTasks)* $GPProxyRAMReq +
            (SafeValue $server.Value.TotalCDPProxyTasks)* $CDPProxyRAMReq +

            # OS overhead added if the server hosts that role (any tasks > 0)
            $RepoGWOSRAMOverhead +
            $VpProxyOSRAMOverhead +
            $GPProxyOSRAMOverhead
        )
  
        $coresAvailable = $server.Value.Cores
        $ramAvailable = $server.Value.RAM
        $totalTasks = $server.Value.TotalTasks
    
        #suggestion cores / RAM are only to calculate the suggested nr of tasks. 
        $SuggestedTasksByCores = [Math]::Floor(
            (SafeValue $coresAvailable) -

            # OS overhead subtracted if the server hosts that role (any tasks > 0)
            $RepoGWOSCPUOverhead -
            $VpProxyOSCPUOverhead -
            $GPProxyOSCPUOverhead -
            $CDPProxyOSCPUOverhead
        )
 
        $SuggestedTasksByRAM = [Math]::Floor(
            (SafeValue $ramAvailable) - 

            # OS overhead subtracted if the server hosts that role (any tasks > 0)
            $RepoGWOSRAMOverhead -
            $VpProxyOSRAMOverhead -
            $GPProxyOSRAMOverhead -
            $CDPProxyOSRAMOverhead
        )
   
        if ($serverName -contains $BackupServerName) {
            $RequiredCores += $BSCPUReq  #CPU core requirement for Backup Server added
            $RequiredRAM += $BSRAMReq    #RAM requirement for Backup Server added
            $SuggestedTasksByCores -= $BSCPUReq
            $SuggestedTasksByRAM -= $BSRAMReq
        }

        $NonNegativeCores = EnsureNonNegative($SuggestedTasksByCores*2)
        $NonNegativeRAM = EnsureNonNegative($SuggestedTasksByRAM)


        # Calculate the max suggested tasks using non-negative values
        $MaxSuggestedTasks = [Math]::Min($NonNegativeCores, $NonNegativeRAM)

        $RequirementComparison = [PSCustomObject]@{
            "Server"          = $serverName
            "Type"            = ($server.Value.Roles -join '/ ')
            "Required Cores"  = $RequiredCores
            "Available Cores" = $coresAvailable
            "Required RAM (GB)" = $RequiredRAM
            "Available RAM (GB)" = $ramAvailable
            "Concurrent Tasks" = $totalTasks
            "Suggested Tasks"  = $MaxSuggestedTasks
            "Names"           = ($server.Value.Names -join '/ ')
        }
        $RequirementsComparison += $RequirementComparison
    }
        Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}

# Output summary of Repositories, Proxies, and CDP Proxies found
Write-LogFile("Found components:")
Write-LogFile($RepoData.Count.ToString() + " Repositories")
Write-LogFile($GWData.Count.ToString() + " Gateway Servers")
Write-LogFile($ProxyData.Count.ToString() + " Vi and HV Proxy Servers")
Write-LogFile($CDPProxyData.Count.ToString() + " CDP Proxies")
Write-LogFile($GPProxyData.Count.ToString() + " GP Proxies")
$ProxyData.Count

# Detect and mention which hosts are used for multiple roles
$multiRoleServers = $hostRoles.GetEnumerator() | Where-Object { $_.Value.Roles.Count -gt 1 }

if ($multiRoleServers) {
    $multiRoleServers | ForEach-Object {        
        $message = "$($_.Key) has roles: $($_.Value.Roles -join '/ ') - Names: $($_.Value.Names -join '/ ')"
        Write-LogFile($message)
    }
} else {
    Write-LogFile("No servers are being used for multiple roles.")
}

# Output the requirements comparison
Write-Host "Requirements Comparison:"

# Exporting the data to CSV files
$RepoData | Export-VhcCsv -FileName '_RepositoryServers.csv'
$GWData | Export-VhcCsv -FileName '_Gateways.csv'
$ProxyData | Export-VhcCsv -FileName '_Proxies.csv'
$CDPProxyData | Export-VhcCsv -FileName '_CdpProxy.csv'
$GPProxyData | Export-VhcCsv -FileName '_NasProxy.csv'
$RequirementsComparison | Export-VhcCsv -FileName '_AllServersRequirementsComparison.csv'

Write-LogFile("Concurrency inspection files are exported.")  





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
    $capOut = $cap | Select-Object Status, @{n = 'Type'; e = { $_.Repository.Type } }, @{n = 'Immute'; e = { 
        # DataCloudVault repositories (Type = 6) don't expose BackupImmutabilityEnabled property
        # Instead, determine immutability from ImmutabilityPeriod: if period > 0, immutability is enabled
        if ($_.Repository.Type -eq 6) {
            if ($_.Repository.ImmutabilityPeriod -gt 0) { "True" } else { "False" }
        } else {
            $_.Repository.BackupImmutabilityEnabled
        }
    } }, @{n = 'immutabilityperiod'; e = { $_.Repository.ImmutabilityPeriod } }, @{n = 'ImmutabilityMode'; e = { $_.Repository.ImmutabilityMode } }, @{n = 'SizeLimitEnabled'; e = { $_.Repository.SizeLimitEnabled } }, @{n = 'SizeLimit'; e = { $_.Repository.SizeLimit } }, @{n = 'RepoId'; e = { $_.Repository.Id } }, @{n = 'ConnectionType'; e = { $_.Repository.ConnectionType } }, @{n = 'GatewayServer'; e = { $_.Repository.GatewayServer.Name -join '; ' } }, parentid, @{n = 'Name'; e = { $_.Repository.Name }}


    Write-LogFile($message + "DONE")
  
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$capOut | Export-VhcCsv -FileName '_capTier.csv'

# archive extent grab
try {
    $message = "Collecting archive tier info..."
    Write-LogFile($message)

    $arch = Get-VBRBackupRepository -ScaleOut | Get-VBRArchiveExtent
    $archOut = $arch | Select-Object Status, ParentId, @{n = 'RepoId'; e = { $_.Repository.Id } }, @{n = 'Name'; e = { $_.Repository.Name } }, @{n = 'ArchiveType'; e = { $_.Repository.ArchiveType } }, @{n = 'BackupImmutabilityEnabled'; e = { $_.Repository.BackupImmutabilityEnabled } }, @{n = 'GatewayMode'; e = { $_.Repository.GatewayMode } }, @{n = 'GatewayServer'; e = { $_.Repository.GatewayServer.Name -join '; ' } }

    Write-LogFile($message + "DONE")
}
catch {
    Write-LogFile($message + "FAILED!")
    $err = $Error[0].Exception
    Write-LogFile($err.message)
}
$archOut | Export-VhcCsv -FileName '_archTier.csv'

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
            if ($VBRVersion -ge 12) {
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
    $SOBROutput = $SOBRs | Select-Object -Property "PolicyType", @{n = "Extents"; e = { $SOBRs.extent.name -as [String] } } , "UsePerVMBackupFiles", "PerformFullWhenExtentOffline", "EnableCapacityTier", "OperationalRestorePeriod", "OverridePolicyEnabled", "OverrideSpaceThreshold", "OffloadWindowOptions", "CapacityExtent", "EncryptionEnabled", "EncryptionKey", "CapacityTierCopyPolicyEnabled", "CapacityTierMovePolicyEnabled", "ArchiveTierEnabled", "ArchiveExtent", "ArchivePeriod", "CostOptimizedArchiveEnabled", "ArchiveFullBackupModeEnabled", "PluginBackupsOffloadEnabled", "CopyAllPluginBackupsEnabled", "CopyAllMachineBackupsEnabled", "Id", "Name", "Description", "ArchiveTierEncryptionEnabled"
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

    # Tape Servers
    Write-LogFile("Starting Tape Servers collection...")
    try {
        $tapeServers = Get-VBRTapeServer
        Write-LogFile("Found " + $tapeServers.Count + " tape servers")
    }
    catch {
        Write-LogFile("Tape Servers collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $tapeServers = $null
    }
    $tapeServers | Export-VhcCsv -FileName '_TapeServers.csv'

    # Tape Libraries
    Write-LogFile("Starting Tape Libraries collection...")
    try {
        $tapeLibraries = Get-VBRTapeLibrary
        Write-LogFile("Found " + $tapeLibraries.Count + " tape libraries")
    }
    catch {
        Write-LogFile("Tape Libraries collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $tapeLibraries = $null
    }
    $tapeLibraries | Export-VhcCsv -FileName '_TapeLibraries.csv'

    # Tape Media Pools
    Write-LogFile("Starting Tape Media Pools collection...")
    try {
        $tapeMediaPools = Get-VBRTapeMediaPool
        Write-LogFile("Found " + $tapeMediaPools.Count + " tape media pools")
    }
    catch {
        Write-LogFile("Tape Media Pools collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $tapeMediaPools = $null
    }
    $tapeMediaPools | Export-VhcCsv -FileName '_TapeMediaPools.csv'

    # Tape Vaults
    Write-LogFile("Starting Tape Vaults collection...")
    try {
        $tapeVaults = Get-VBRTapeVault
        Write-LogFile("Found " + $tapeVaults.Count + " tape vaults")
    }
    catch {
        Write-LogFile("Tape Vaults collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $tapeVaults = $null
    }
    $tapeVaults | Export-VhcCsv -FileName '_TapeVaults.csv'

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

    # Replica Jobs
    Write-LogFile("Starting Replica Jobs collection...")
    try {
        $replicaJobs = Get-VBRJob | Where-Object { $_.JobType -eq "Replica" }
        Write-LogFile("Found " + @($replicaJobs).Count + " replica jobs")
    }
    catch {
        Write-LogFile("Replica Jobs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $replicaJobs = $null
    }
    $replicaJobs | Export-VhcCsv -FileName '_ReplicaJobs.csv'

    # Replicas
    Write-LogFile("Starting Replicas collection...")
    try {
        $replicas = Get-VBRReplica
        Write-LogFile("Found " + @($replicas).Count + " replicas")
    }
    catch {
        Write-LogFile("Replicas collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $replicas = $null
    }
    $replicas | Export-VhcCsv -FileName '_Replicas.csv'

    # Failover Plans
    Write-LogFile("Starting Failover Plans collection...")
    try {
        $failoverPlans = Get-VBRFailoverPlan
        Write-LogFile("Found " + @($failoverPlans).Count + " failover plans")
    }
    catch {
        Write-LogFile("Failover Plans collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $failoverPlans = $null
    }
    $failoverPlans | Export-VhcCsv -FileName '_FailoverPlans.csv'

    # SureBackup Application Groups
    Write-LogFile("Starting SureBackup Application Groups collection...")
    try {
        $sbAppGroups = Get-VSBApplicationGroup
        Write-LogFile("Found " + @($sbAppGroups).Count + " SureBackup application groups")
    }
    catch {
        Write-LogFile("SureBackup Application Groups collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $sbAppGroups = $null
    }
    $sbAppGroups | Export-VhcCsv -FileName '_SureBackupAppGroups.csv'

    # SureBackup Virtual Labs
    Write-LogFile("Starting SureBackup Virtual Labs collection...")
    try {
        $sbVirtualLabs = Get-VSBVirtualLab
        Write-LogFile("Found " + @($sbVirtualLabs).Count + " SureBackup virtual labs")
    }
    catch {
        Write-LogFile("SureBackup Virtual Labs collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $sbVirtualLabs = $null
    }
    $sbVirtualLabs | Export-VhcCsv -FileName '_SureBackupVirtualLabs.csv'

    # Cloud Connect Gateways
    Write-LogFile("Starting Cloud Connect Gateways collection...")
    try {
        $cloudGateways = Get-VBRCloudGateway
        Write-LogFile("Found " + @($cloudGateways).Count + " cloud gateways")
    }
    catch {
        Write-LogFile("Cloud Connect Gateways collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $cloudGateways = $null
    }
    $cloudGateways | Export-VhcCsv -FileName '_CloudGateways.csv'

    # Cloud Connect Tenants
    Write-LogFile("Starting Cloud Connect Tenants collection...")
    try {
        $cloudTenants = Get-VBRCloudTenant
        Write-LogFile("Found " + @($cloudTenants).Count + " cloud tenants")
    }
    catch {
        Write-LogFile("Cloud Connect Tenants collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $cloudTenants = $null
    }
    $cloudTenants | Export-VhcCsv -FileName '_CloudTenants.csv'

    # Email Notification Settings
    Write-LogFile("Starting Email Notification Settings collection...")
    try {
        $emailNotification = Get-VBRMailNotification
        Write-LogFile("Email notification collected")
    }
    catch {
        Write-LogFile("Email Notification Settings collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $emailNotification = $null
    }
    $emailNotification | Export-VhcCsv -FileName '_EmailNotification.csv'

    # Credentials (passwords are not exported by Veeam cmdlets)
    Write-LogFile("Starting Credentials collection...")
    try {
        $credentials = Get-VBRCredentials | Select-Object Name, UserName, Description, CurrentUser, LastModified
        Write-LogFile("Found " + @($credentials).Count + " credentials")
    }
    catch {
        Write-LogFile("Credentials collection failed: " + $Error[0].Exception.Message, "Errors", "ERROR")
        $credentials = $null
    }
    $credentials | Export-VhcCsv -FileName '_Credentials.csv'

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
            # Calculate OriginalSize from the latest restore point per protected object
            $CalculatedOriginalSize = 0
            try {
                if ($RestorePoints -and $RestorePoints.Count -gt 0) {
                    $LatestPoints = $RestorePoints | Group-Object -Property { $_.ObjectId } | ForEach-Object {
                        $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1
                    }
                    $ApproxSum = ($LatestPoints | Where-Object { $null -ne $_.ApproxSize } | Measure-Object -Property ApproxSize -Sum).Sum
                    if ($ApproxSum -and $ApproxSum -gt 0) {
                        $CalculatedOriginalSize = $ApproxSum
                    } else {
                        # Fallback: restore points exist but lack ApproxSize (legacy backups)
                        $CalculatedOriginalSize = $Job.Info.IncludedSize
                    }
                } else {
                    # No restore points available, fall back to cached IncludedSize
                    $CalculatedOriginalSize = $Job.Info.IncludedSize
                }
            } catch {
                $CalculatedOriginalSize = $Job.Info.IncludedSize
            }
        }
        catch{
            Write-LogFile("Warning: Could not get last backup for job: " + $Job.Name, "Warnings", "WARN")

            $TotalOnDiskGB = 0
            $CalculatedOriginalSize = $Job.Info.IncludedSize
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
        @{n = 'OriginalSize'; e = { $CalculatedOriginalSize } },
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
        Get-VBRMalwareDetectionOptions | Export-VhcCsv -FileName '_malware_settings.csv'
        Get-VBRMalwareDetectionObject | Export-VhcCsv -FileName '_malware_infectedobject.csv'
        Get-VBRMalwareDetectionEvent | Export-VhcCsv -FileName '_malware_events.csv'
        Get-VBRMalwareDetectionExclusion | Export-VhcCsv -FileName '_malware_exclusions.csv'
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
                
                # Poll for scan results instead of sleeping a fixed 45 seconds
                $maxWaitSeconds = 45
                $pollIntervalSeconds = 3
                $elapsed = 0
                $SecurityCompliances = $null
                Write-LogFile("Polling for scan results (max ${maxWaitSeconds}s, every ${pollIntervalSeconds}s)...")

                while ($elapsed -lt $maxWaitSeconds) {
                    Start-Sleep -Seconds $pollIntervalSeconds
                    $elapsed += $pollIntervalSeconds
                    try {
                        if ($VBRVersion -ge 13) {
                            $SecurityCompliances = Get-VBRSecurityComplianceAnalyzerResults
                        }
                        else {
                            $SecurityCompliances = [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
                        }
                        if ($SecurityCompliances -and $SecurityCompliances.Count -gt 0) {
                            Write-LogFile("Scan results ready after ${elapsed}s - retrieved " + $SecurityCompliances.Count + " compliance items")
                            break
                        }
                    }
                    catch {
                        Write-LogFile("Poll attempt at ${elapsed}s not ready yet, retrying...")
                    }
                }

                # Final attempt if polling didn't get results
                if (-not $SecurityCompliances -or $SecurityCompliances.Count -eq 0) {
                    Write-LogFile("Polling timed out after ${maxWaitSeconds}s. Final retrieval attempt...")
                    try {
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
                        } catch {
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
                        } catch {
                            $errMsg = "Unable to get error details"
                            $errType = "Unknown"
                            $stackTrace = "Unable to get stack trace"
                        }
                        Write-LogFile("Failed to export Security Compliance CSV: " + $errMsg, "Errors", "ERROR")
                        Write-LogFile("Exception Type: " + $errType, "Errors", "ERROR")
                        Write-LogFile("Stack Trace: " + $stackTrace, "Errors", "ERROR")
                    }
                } else {
                    Write-LogFile("No compliance data to export - OutObj is empty", "Main", "WARNING")
                }
            }
            Catch {
                $errMsg = ""
                try {
                    if ($_.Exception.Message) { 
                        $errMsg = $_.Exception.Message.ToString() 
                    } else { 
                        $errMsg = $_.ToString() 
                    }
                } catch {
                    $errMsg = "Unable to get error message"
                }
                $errType = if ($_.Exception) { $_.Exception.GetType().FullName } else { "Unknown" }
                $stackTrace = ""
                try {
                    $stackTrace = if ($_.ScriptStackTrace) { $_.ScriptStackTrace.ToString() } else { "No stack trace available" }
                } catch {
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
                } else { 
                    $errMsg = $_.ToString() 
                }
            } catch {
                $errMsg = "Unable to get error message"
            }
            $stackTrace = ""
            try {
                $stackTrace = if ($_.ScriptStackTrace) { $_.ScriptStackTrace.ToString() } else { "No stack trace available" }
            } catch {
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