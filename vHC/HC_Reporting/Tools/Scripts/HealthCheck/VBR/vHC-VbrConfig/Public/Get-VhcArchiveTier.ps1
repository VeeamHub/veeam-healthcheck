#Requires -Version 5.1

function Get-VhcArchiveTier {
    <#
    .Synopsis
        Collects SOBR archive tier extent data and exports to _archTier.csv.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting archive tier info..."
    $archOut = $null
    Write-LogFile $message

    try {
        $arch = Get-VBRBackupRepository -ScaleOut | Get-VBRArchiveExtent
        $archOut = $arch | Select-Object Status,
            ParentId,
            @{n = 'RepoId';                    e = { $_.Repository.Id } },
            @{n = 'Name';                      e = { $_.Repository.Name } },
            @{n = 'ArchiveType';               e = { $_.Repository.ArchiveType } },
            @{n = 'BackupImmutabilityEnabled'; e = { $_.Repository.BackupImmutabilityEnabled } },
            @{n = 'GatewayMode';               e = { $_.Repository.GatewayMode } },
            @{n = 'GatewayServer';             e = { $_.Repository.GatewayServer.Name -join '; ' } }

        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ArchiveTier' -ErrorMessage $_.Exception.Message
    }

    $archOut | Export-VhciCsv -FileName '_archTier.csv'
}
