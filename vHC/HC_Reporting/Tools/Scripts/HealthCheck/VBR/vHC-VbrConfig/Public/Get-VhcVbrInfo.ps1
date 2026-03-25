#Requires -Version 5.1

function Get-VhcVbrInfo {
    <#
    .Synopsis
        Collects VBR version, database configuration, and MFA settings.
        Outputs _vbrinfo.csv. Should be the last collector to run as it reads
        many registry paths that must not block earlier collectors if they fail.
    .Parameter VBRVersion
        Major VBR version integer (as returned by Get-VhcMajorVersion).
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [int] $VBRVersion,
        [Parameter(Mandatory = $false)] [bool] $RemoteExecution = $false
    )

    Write-LogFile "Collecting VBR Version info... (VBRVersion=$VBRVersion)"

    $version          = $null
    $fixes            = $null
    $dbServerPath     = $null
    $instancePath     = $null
    $pgDbHost         = $null
    $pgDbDbName       = $null
    $msDbHost         = $null
    $msDbName         = $null
    $dbType           = $null
    $MFAGlobalSetting = $null

    # Registry reads - all silently continue to tolerate remote-execution scenarios
    try {
        $instancePath   = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" `
                                           -Name "SqlInstanceName"  -ErrorAction SilentlyContinue
        $dbServerPath   = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" `
                                           -Name "SqlServerName"    -ErrorAction SilentlyContinue
        $dbType         = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations" `
                                           -Name "SqlActiveConfiguration" -ErrorAction SilentlyContinue
        $pgDbHost       = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" `
                                           -Name "SqlHostName"      -ErrorAction SilentlyContinue
        $pgDbDbName     = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\PostgreSql" `
                                           -Name "SqlDatabaseName"  -ErrorAction SilentlyContinue
        $msDbHost       = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" `
                                           -Name "SqlServerName"    -ErrorAction SilentlyContinue
        $msDbName       = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" `
                                           -Name "SqlDatabaseName"  -ErrorAction SilentlyContinue

        if ($dbType.SqlActiveConfiguration -ne "PostgreSql") {
            if (-not $instancePath -or $instancePath.SqlInstanceName -eq "") {
                $instancePath = Get-ItemProperty `
                    -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\DatabaseConfigurations\MsSql" `
                    -Name "SqlInstanceName" -ErrorAction SilentlyContinue
            }
        }

        # DLL version and patch notes
        # TODO: Replace this entire block with a REST API call to GET /api/v1/serverInfo
        # (default port 9419) once REST API collection is implemented. The response
        # provides buildVersion, patches, and databaseVendor without registry access,
        # making it the correct long-term solution for both local and remote runs.
        if ($RemoteExecution) {
            # Short-term workaround: registry is not accessible in remote sessions;
            # use the VBR PowerShell API instead (same source as Get-VhcMajorVersion).
            try {
                $backupServer = Get-VBRBackupServerInfo
                $version = $backupServer.Build.ToString()
            } catch {
                Write-LogFile "Get-VhcVbrInfo: remote version detection via Get-VBRBackupServerInfo failed: $($_.Exception.Message)" -LogLevel "WARNING"
            }
        } else {
            $coreRegPath = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" `
                                            -Name "CorePath" -ErrorAction SilentlyContinue
            if ($coreRegPath) {
                $dllPath = Join-Path -Path $coreRegPath.CorePath -ChildPath "Veeam.Backup.Core.dll" -Resolve -ErrorAction SilentlyContinue
                if ($dllPath) {
                    $dllFile = Get-Item -Path $dllPath -ErrorAction SilentlyContinue
                    if ($dllFile) {
                        $version = $dllFile.VersionInfo.ProductVersion
                        $fixes   = $dllFile.VersionInfo.Comments
                    }
                }
            }
        }
    } catch {
        Write-LogFile "Get-VhcVbrInfo: failed to read registry values: $($_.Exception.Message)" -LogLevel "WARNING"
        Add-VhciModuleError -CollectorName 'VbrInfo' -ErrorMessage $_.Exception.Message
    }

    # MFA global setting - only available on VBR 12+; fails gracefully on earlier versions
    try {
        Write-LogFile "Getting MFA Global Setting"
        $MFAGlobalSetting = [Veeam.Backup.Core.SBackupOptions]::get_GlobalMFA()
    } catch {
        Write-LogFile "Failed to get MFA Global Setting, likely pre-VBR 12"
        $MFAGlobalSetting = "N/A - Pre VBR 12"
    }

    [pscustomobject][ordered]@{
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
    } | Export-VhciCsv -FileName '_vbrinfo.csv'

    Write-LogFile "Collecting VBR Version info...DONE"
}
