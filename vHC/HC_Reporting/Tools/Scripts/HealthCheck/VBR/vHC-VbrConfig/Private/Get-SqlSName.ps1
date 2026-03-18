#Requires -Version 5.1

function Get-SqlSName {
    <#
    .Synopsis
        Reads the VBR database server hostname from the Windows registry.
        Supports both legacy SqlServerName key and the newer PostgreSql/MsSql sub-key layout.
    .Parameter VBRServer
        The VBR server hostname. Used to resolve "localhost" entries to the actual FQDN.
    .Outputs
        [string] SQL/PostgreSQL host name, or $null if the registry lookup fails.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param (
        [Parameter(Mandatory)] [string] $VBRServer
    )

    $basePath                    = 'HKLM:\SOFTWARE\Veeam\Veeam Backup and Replication'
    $databaseConfigurationPath   = "$basePath\DatabaseConfigurations"
    $sqlActiveConfigurationKey   = 'SqlActiveConfiguration'
    $postgreSqlPath              = "$databaseConfigurationPath\PostgreSql"
    $msSqlPath                   = "$databaseConfigurationPath\MsSql"
    $sqlServerNameKey            = 'SqlServerName'
    $sqlHostNameKey              = 'SqlHostName'
    $SQLSName                    = $null

    try {
        $SQLSName = (Get-ItemProperty -Path $basePath -Name $sqlServerNameKey -ErrorAction Stop).$sqlServerNameKey
    } catch {
        try {
            $sqlActiveConfig  = Get-ItemProperty -Path $databaseConfigurationPath `
                                                 -Name $sqlActiveConfigurationKey `
                                                 -ErrorAction Stop
            $activeConfigValue = $sqlActiveConfig.$sqlActiveConfigurationKey

            if ($activeConfigValue -eq 'PostgreSql') {
                $SQLSName = (Get-ItemProperty -Path $postgreSqlPath -Name $sqlHostNameKey -ErrorAction Stop).$sqlHostNameKey
            } else {
                $SQLSName = (Get-ItemProperty -Path $msSqlPath -Name $sqlServerNameKey -ErrorAction Stop).$sqlServerNameKey
            }
        } catch {
            Write-LogFile "Unable to retrieve SQL Server name from registry." -LogLevel "WARNING"
            return $null
        }
    }

    if ($SQLSName -eq 'localhost') {
        $SQLSName = $VBRServer
    }

    return $SQLSName
}
