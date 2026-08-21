#Requires -Version 5.1

function Get-VhcJob {
    <#
    .Synopsis
        Collects all VBR job types and exports detailed job configuration data.
        Calls nine private sub-functions for each job family, then runs the main
        Get-VBRJob loop with restore-point size calculation.
        Exports _Jobs.csv, _configBackup.csv.
        Source: Get-VBRConfig.ps1 lines 1090-1587.
    .Parameter RepositoryDetails
        ArrayList of [pscustomobject]@{ID; Name} rows returned by Get-VhcRepository.
        Used to resolve TargetRepositoryId to a human-readable name in _Jobs.csv.
        May be $null - repo names will be blank in that case.
    .Parameter VBRVersion
        Major VBR version integer. Reserved for future per-version branching.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object]$RepositoryDetails = $null,

        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0
    )

    $message = "Collecting jobs info..."
    Write-LogFile $message

    # ------------------------------------------------------------------
    # Fetch primary job list and config backup job
    # ------------------------------------------------------------------
    $Jobs         = $null
    $configBackup = $null

    try {
        $Jobs = Get-VBRJob -WarningAction SilentlyContinue
    } catch {
        Write-LogFile "Main jobs collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    # Standalone (unmanaged) agent jobs are not returned by Get-VBRJob.
    # Enumerate them via the backup objects they own; .GetJob() returns
    # a CBackupJob with the same shape Get-VBRJob produces, so they flow
    # through the projection below unchanged.
    try {
        $standaloneBackups = @(Get-VBRBackup -WarningAction SilentlyContinue |
            Where-Object { $_.IsAgentStandaloneJob -eq $true })
        $standaloneJobs = @($standaloneBackups | ForEach-Object {
            $backup = $_
            try {
                $backup.GetJob()
            } catch {
                $msg = "Orphaned standalone backup skipped: Id={0} Name='{1}' Error={2}" -f $backup.Id, $backup.Name, $_.Exception.Message
                Write-LogFile $msg -LogLevel "WARNING"
                $null
            }
        } | Where-Object { $_ })
        Write-LogFile "Standalone agent jobs collected: $($standaloneJobs.Count)"
        if ($standaloneJobs.Count -gt 0) {
            $Jobs = @($Jobs) + $standaloneJobs
        }
    } catch {
        Write-LogFile "Standalone agent job collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    try {
        $configBackup = Get-VBRConfigurationBackupJob
    } catch {
        Write-LogFile "Configuration Backup Job collection failed: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
    }

    # ------------------------------------------------------------------
    # Sub-function collectors (each exports its own CSVs)
    # Each sub-collector is individually fault-isolated inside Invoke-VhciJobSubCollectors.
    # ------------------------------------------------------------------
    Invoke-VhciJobSubCollectors -Jobs @($Jobs)

    # ------------------------------------------------------------------
    # Restore-point matching: performance gate + global sweep
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate)
    # ------------------------------------------------------------------
    $KnownSafeJobTypes = @(
        'VMware Backup',
        'Hyper-V Backup',
        'Windows Agent Backup',
        'Windows Agent Policy',
        'Linux Agent Backup',
        'Cloud Director Backup'
    )

    $NonReplicaJobs = @($Jobs | Where-Object { $_.TypeToString -notlike '*Replication*' })
    $NeedsSweep     = [bool]($NonReplicaJobs | Where-Object { $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)

    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)

            $Tier1Matched = 0
            foreach ($RestorePoint in $AllRestorePoints) {
                $SourceJob = $null
                try { $SourceJob = $RestorePoint.GetSourceJob() } catch {}
                if ($null -eq $SourceJob) { continue }

                $JobIdKey = $SourceJob.Id.ToString()
                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint)
                $Tier1Matched++
            }

            Write-LogFile "Restore point sweep: $($AllRestorePoints.Count) restore points found server-wide, $Tier1Matched matched via tier 1"
        } catch {
            Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR"
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
        }
    }

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
    # ------------------------------------------------------------------
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in @($Jobs)) {
        try {
            $RestorePoints = @()
            if ($NeedsSweep) {
                $JobIdKey = $Job.Id.ToString()
                if ($RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePoints = $RestorePointsByJob[$JobIdKey]
                }
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = Get-VBRRestorePoint -Backup $LastBackup
                }
            }
            $TotalOnDiskGB = 0

            $RestorePoints.ForEach{
                $RestorePoint  = $PSItem
                $OnDiskGB      = $RestorePoint.GetStorage().Stats.BackupSize / 1GB
                $TotalOnDiskGB += $OnDiskGB
            }

            # CalculatedOriginalSize: prefer ApproxSize from latest restore point per object;
            # fall back to IncludedSize for legacy backups or when no restore points exist.
            $CalculatedOriginalSize = 0
            try {
                if ($RestorePoints -and $RestorePoints.Count -gt 0) {
                    $LatestPoints = $RestorePoints |
                        Group-Object -Property { $_.ObjectId } |
                        ForEach-Object {
                            $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1
                        }
                    $ApproxSum = ($LatestPoints |
                        Where-Object { $null -ne $_.ApproxSize } |
                        Measure-Object -Property ApproxSize -Sum).Sum

                    if ($ApproxSum -and $ApproxSum -gt 0) {
                        $CalculatedOriginalSize = $ApproxSum
                    } else {
                        $CalculatedOriginalSize = $Job.Info.IncludedSize
                    }
                } else {
                    $CalculatedOriginalSize = $Job.Info.IncludedSize
                }
            } catch {
                $CalculatedOriginalSize = $Job.Info.IncludedSize
            }
        } catch {
            Write-LogFile "Could not calculate restore point sizes for job: $($Job.Name) - $($_.Exception.Message)" -LogLevel "WARNING"
            $TotalOnDiskGB          = 0
            $CalculatedOriginalSize = $Job.Info.IncludedSize
        }

        Write-LogFile "Job: $($Job.Name) - Total OnDisk GB: $TotalOnDiskGB"

        $JobDetails = $Job | Select-Object -Property 'Name', 'JobType',
            'SheduleEnabledTime', 'ScheduleOptions',
            @{n = 'RestorePoints';                  e = { $Job.Options.BackupStorageOptions.RetainCycles } },
            @{n = 'RepoName';                       e = { $RepositoryDetails | Where-Object { $_.Id -eq $job.Info.TargetRepositoryId.Guid } | Select-Object -ExpandProperty Name } },
            @{n = 'Algorithm';                      e = { $Job.Options.BackupTargetOptions.Algorithm } },
            @{n = 'FullBackupScheduleKind';         e = { $Job.Options.BackupTargetOptions.FullBackupScheduleKind } },
            @{n = 'FullBackupDays';                 e = { $Job.Options.BackupTargetOptions.FullBackupDays } },
            @{n = 'TransformFullToSyntethic';       e = { $Job.Options.BackupTargetOptions.TransformFullToSyntethic } },
            @{n = 'TransformIncrementsToSyntethic'; e = { $Job.Options.BackupTargetOptions.TransformIncrementsToSyntethic } },
            @{n = 'TransformToSyntethicDays';       e = { $Job.Options.BackupTargetOptions.TransformToSyntethicDays } },
            @{n = 'PwdKeyId';                       e = { $_.Info.PwdKeyId } },
            @{n = 'OriginalSize';                   e = { $CalculatedOriginalSize } },
            @{n = 'RetentionType';                  e = { $Job.BackupStorageOptions.RetentionType } },
            @{n = 'RetentionCount';                 e = { $Job.BackupStorageOptions.RetainCycles } },
            @{n = 'RetainDaysToKeep';               e = { $Job.BackupStorageOptions.RetainDaysToKeep } },
            @{n = 'DeletedVmRetentionDays';         e = { $Job.BackupStorageOptions.RetainDays } },
            @{n = 'DeletedVmRetention';             e = { $Job.BackupStorageOptions.EnableDeletedVmDataRetention } },
            @{n = 'CompressionLevel';               e = { $Job.BackupStorageOptions.CompressionLevel } },
            @{n = 'Deduplication';                  e = { $Job.BackupStorageOptions.EnableDeduplication } },
            @{n = 'BlockSize';                      e = { $Job.BackupStorageOptions.StgBlockSize } },
            @{n = 'IntegrityChecks';                e = { $Job.BackupStorageOptions.EnableIntegrityChecks } },
            @{n = 'SpecificStorageEncryption';      e = { $Job.BackupStorageOptions.UseSpecificStorageEncryption } },
            @{n = 'StgEncryptionEnabled';           e = { $Job.BackupStorageOptions.StorageEncryptionEnabled } },
            @{n = 'KeepFirstFullBackup';            e = { $Job.BackupStorageOptions.KeepFirstFullBackup } },
            @{n = 'EnableFullBackup';               e = { $Job.BackupStorageOptions.EnableFullBackup } },
            @{n = 'BackupIsAttached';               e = { $Job.BackupStorageOptions.BackupIsAttached } },
            @{n = 'GfsWeeklyIsEnabled';             e = { $Job.options.gfspolicy.weekly.IsEnabled } },
            @{n = 'GfsWeeklyCount';                 e = { $Job.options.gfspolicy.weekly.KeepBackupsForNumberOfWeeks } },
            @{n = 'GfsMonthlyEnabled';              e = { $Job.options.gfspolicy.Monthly.IsEnabled } },
            @{n = 'GfsMonthlyCount';                e = { $Job.options.gfspolicy.Monthly.KeepBackupsForNumberOfMonths } },
            @{n = 'GfsYearlyEnabled';               e = { $Job.options.gfspolicy.yearly.IsEnabled } },
            @{n = 'GfsYearlyCount';                 e = { $Job.options.gfspolicy.yearly.KeepBackupsForNumberOfYears } },
            @{n = 'IndexingType';                   e = { $Job.VssOptions.GuestFSIndexingType } },
            @{n = 'OnDiskGB';                       e = { $TotalOnDiskGB } },
            @{n = 'AAIPEnabled';                    e = { $Job.VssOptions.VssSnapshotOptions.Enabled } },
            @{n = 'VSSEnabled';                     e = { $Job.VssOptions.VssSnapshotOptions.ApplicationProcessingEnabled } },
            @{n = 'VSSIgnoreErrors';                e = { $Job.VssOptions.VssSnapshotOptions.IgnoreErrors } },
            @{n = 'GuestFSIndexingEnabled';         e = { $Job.VssOptions.GuestFSIndexingOptions.IsEnabled } },
            # IsScheduleEnabled reflects whether the job itself is active, not whether it has a schedule
            @{n = 'IsJobEnabled';                  e = { $Job.IsScheduleEnabled } },
            # RunManually = True means the job is enabled but has no schedule configured (runs on demand only)
            @{n = 'IsScheduleDisabled';            e = { $Job.Options.JobOptions.RunManually } },
            @{n = 'Platform';                      e = {
                $key = if ($Job.Name) { $Job.Name.ToLowerInvariant() } else { '' }
                if ($script:PlatformMap -and $script:PlatformMap.ContainsKey($key)) {
                    $script:PlatformMap[$key]
                } else { '' }
            }},
            @{n = 'TypeToString';                  e = { $Job.TypeToString } }

        $AllJobs.Add($JobDetails) | Out-Null
    }

    $AllJobs      | Export-VhciCsv -FileName '_Jobs.csv'
    $configBackup | Export-VhciCsv -FileName '_configBackup.csv'

    Write-LogFile ($message + "DONE")
}
