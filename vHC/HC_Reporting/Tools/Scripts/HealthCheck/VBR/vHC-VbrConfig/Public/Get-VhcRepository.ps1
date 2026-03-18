#Requires -Version 5.1

function Get-VhcRepository {
    <#
    .Synopsis
        Collects VBR repositories, SOBRs, and SOBR extents.
        Exports _Repositories.csv, _SOBRs.csv, _SOBRExtents.csv.
        Returns the $RepositoryDetails ArrayList (ID -> Name map) for use by Get-VhcJob.
    .Parameter VBRVersion
        Major VBR version integer. Version >= 12 adds gatewayHosts to SOBR extent output.
    .Outputs
        [System.Collections.ArrayList] of [pscustomobject]@{ID; Name} rows, or $null on failure.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.ArrayList])]
    param(
        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0
    )

    $message = "Collecting repositories info..."
    Write-LogFile $message

    try {
        $Repositories = Get-VBRBackupRepository
        $SOBRs        = Get-VBRBackupRepository -ScaleOut

        $repoOptionsColumns = @(
            @{name = 'Options(maxtasks)';                    expression = { $_.Options.MaxTaskCount } },
            @{name = 'Options(Unlimited Tasks)';             expression = { $_.Options.IsTaskCountUnlim } },
            @{name = 'Options(MaxArchiveTaskCount)';         expression = { $_.Options.MaxArchiveTaskCount } },
            @{name = 'Options(CombinedDataRateLimit)';       expression = { $_.Options.CombinedDataRateLimit } },
            @{name = 'Options(Uncompress)';                  expression = { $_.Options.Uncompress } },
            @{name = 'Options(OptimizeBlockAlign)';          expression = { $_.Options.OptimizeBlockAlign } },
            @{name = 'Options(RemoteAccessLimitation)';      expression = { $_.Options.RemoteAccessLimitation } },
            @{name = 'Options(EpEncryptionEnabled)';         expression = { $_.Options.EpEncryptionEnabled } },
            @{name = 'Options(OneBackupFilePerVm)';          expression = { $_.Options.OneBackupFilePerVm } },
            @{name = 'Options(IsAutoDetectAffinityProxies)'; expression = { $_.Options.IsAutoDetectAffinityProxies } },
            @{name = 'Options(NfsRepositoryEncoding)';       expression = { $_.Options.NfsRepositoryEncoding } }
        )

        [System.Collections.ArrayList]$RepositoryDetails = @()

        foreach ($Repo in (@($Repositories) + @($SOBRs))) {
            $null = $RepositoryDetails.Add([pscustomobject][ordered]@{
                'ID'   = $Repo.ID
                'Name' = $Repo.Name
            })
        }

        [System.Collections.ArrayList]$AllSOBRExtents = @()

        foreach ($SOBR in $SOBRs) {
            $Extents = Get-VBRRepositoryExtent -Repository $SOBR

            foreach ($Extent in $Extents) {
                if ($VBRVersion -ge 12) {
                    $ExtentDetails = $Extent.Repository | Select-Object *,
                        @{n = 'SOBR_Name';         e = { $SOBR.Name } },
                        @{name = 'CachedFreeSpace'; expression = { $_.GetContainer().cachedfreespace.InGigabytes } },
                        @{name = 'CachedTotalSpace'; expression = { $_.GetContainer().cachedtotalspace.InGigabytes } },
                        @{name = 'gatewayHosts';    expression = { $_.GetActualGateways().Name } }
                } else {
                    $ExtentDetails = $Extent.Repository | Select-Object *,
                        @{n = 'SOBR_Name';         e = { $SOBR.Name } },
                        @{name = 'CachedFreeSpace'; expression = { $_.GetContainer().cachedfreespace.InGigabytes } },
                        @{name = 'CachedTotalSpace'; expression = { $_.GetContainer().cachedtotalspace.InGigabytes } }
                }
                $ExtentDetails | Add-Member -NotePropertyName 'IsImmutabilitySupported' `
                    -NotePropertyValue ($Extent.Repository.GetImmutabilitySettings().IsEnabled) -Force
                $AllSOBRExtents.Add($ExtentDetails) | Out-Null
            }
        }

        $SOBROutput = $SOBRs | Select-Object -Property `
            "PolicyType",
            @{n = "Extents"; e = { ($_.Extent.Name -join ", ") } },
            "UsePerVMBackupFiles", "PerformFullWhenExtentOffline", "EnableCapacityTier",
            "OperationalRestorePeriod", "OverridePolicyEnabled", "OverrideSpaceThreshold",
            "OffloadWindowOptions", "CapacityExtent", "EncryptionEnabled", "EncryptionKey",
            "CapacityTierCopyPolicyEnabled", "CapacityTierMovePolicyEnabled", "ArchiveTierEnabled",
            "ArchiveExtent", "ArchivePeriod", "CostOptimizedArchiveEnabled",
            "ArchiveFullBackupModeEnabled", "PluginBackupsOffloadEnabled",
            "CopyAllPluginBackupsEnabled", "CopyAllMachineBackupsEnabled",
            "Id", "Name", "Description", "ArchiveTierEncryptionEnabled"

        $AllSOBRExtentsOutput = $AllSOBRExtents | Select-Object -Property (
            @(
                @{name = 'Host'; expression = { $_.host.name } },
                "Id", "Name", "HostId", "MountHostId", "Description", "CreationTime", "Path",
                "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable",
                "Group", "UseNfsOnMountHost", "VersionOfCreation", "Tag", "IsTemporary",
                "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId",
                "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage",
                "SplitStoragesPerVm", "IsImmutabilitySupported", "SOBR_Name"
            ) + $repoOptionsColumns + @(
                "CachedFreeSpace", "CachedTotalSpace", "gatewayHosts", "ObjectLockEnabled"
            )
        )

        $repoInfo = $Repositories | Select-Object -Property (
            @(
                "Id", "Name", "HostId", "Description", "CreationTime", "Path",
                "FullPath", "FriendlyPath", "ShareCredsId", "Type", "Status", "IsUnavailable",
                "Group", "UseNfsOnMountHost", "VersionOfCreation", "Tag", "IsTemporary",
                "TypeDisplay", "IsRotatedDriveRepository", "EndPointCryptoKeyId",
                "Options", "HasBackupChainLengthLimitation", "IsSanSnapshotOnly", "IsDedupStorage",
                "SplitStoragesPerVm",
                @{n = "IsImmutabilitySupported"; e = { $_.GetImmutabilitySettings().IsEnabled } }
            ) + $repoOptionsColumns + @(
                @{n = 'CachedTotalSpace'; e = { $_.GetContainer().CachedTotalSpace.InGigabytes } },
                @{n = 'CachedFreeSpace';  e = { $_.GetContainer().CachedFreeSpace.InGigabytes } },
                @{name = 'gatewayHosts';  expression = { $_.GetActualGateways().Name } }
            )
        )

        $repoInfo             | Export-VhciCsv -FileName '_Repositories.csv'
        $SOBROutput           | Export-VhciCsv -FileName '_SOBRs.csv'
        $AllSOBRExtentsOutput | Export-VhciCsv -FileName '_SOBRExtents.csv'

        Write-LogFile ($message + "DONE")

        return $RepositoryDetails
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Repository' -ErrorMessage $_.Exception.Message
        return $null
    }
}
