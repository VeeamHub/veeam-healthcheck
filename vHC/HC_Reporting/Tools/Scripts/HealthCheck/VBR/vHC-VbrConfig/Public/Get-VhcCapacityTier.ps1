#Requires -Version 5.1

function Get-VhcCapacityTier {
    <#
    .Synopsis
        Collects SOBR capacity tier extent data and exports to _capTier.csv.
        Note: DataCloudVault repositories (Type = 6) do not expose BackupImmutabilityEnabled.
        Immutability for these is inferred from ImmutabilityPeriod > 0.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting capacity tier info..."
    $capOut  = $null
    Write-LogFile $message

    try {
        $cap = Get-VBRBackupRepository -ScaleOut | Get-VBRCapacityExtent
        $capOut = $cap | Select-Object Status,
            @{n = 'Type';               e = { $_.Repository.Type } },
            @{n = 'Immute';             e = {
                # DataCloudVault repositories (Type = 6) don't expose BackupImmutabilityEnabled property
                # Instead, determine immutability from ImmutabilityPeriod: if period > 0, immutability is enabled
                if ($_.Repository.Type -eq 6) {
                    if ($_.Repository.ImmutabilityPeriod -gt 0) { "True" } else { "False" }
                } else {
                    $_.Repository.BackupImmutabilityEnabled
                }
            } },
            @{n = 'immutabilityperiod'; e = { $_.Repository.ImmutabilityPeriod } },
            @{n = 'ImmutabilityMode';   e = { $_.Repository.ImmutabilityMode } },
            @{n = 'SizeLimitEnabled';   e = { $_.Repository.SizeLimitEnabled } },
            @{n = 'SizeLimit';          e = { $_.Repository.SizeLimit } },
            @{n = 'RepoId';             e = { $_.Repository.Id } },
            @{n = 'ConnectionType';     e = { $_.Repository.ConnectionType } },
            @{n = 'GatewayServer';      e = { $_.Repository.GatewayServer.Name -join '; ' } },
            parentid,
            @{n = 'Name';               e = { $_.Repository.Name } }

        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'CapacityTier' -ErrorMessage $_.Exception.Message
    }

    $capOut | Export-VhciCsv -FileName '_capTier.csv'
}
