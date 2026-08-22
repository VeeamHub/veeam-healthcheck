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
    # (ADR 0021: tiered sweep; ADR 0022: allowlist gate;
    #  ADR 0023: BackupId-grouped matching for all job types)
    # ------------------------------------------------------------------
    $KnownSafeJobTypes = @(
        'VMware Backup',
        'Hyper-V Backup',
        'Windows Agent Backup',
        'Windows Agent Policy',
        'Linux Agent Backup',
        'Cloud Director Backup',
        'VMware Replication',
        'Hyper-V Replication'
    )

    $NeedsSweep = [bool]($Jobs | Where-Object { $null -ne $_ -and $_.TypeToString -notin $KnownSafeJobTypes } | Select-Object -First 1)

    $RestorePointsByJob = @{}
    if ($NeedsSweep) {
        try {
            $AllRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })

            # Same null-placeholder risk both structures below guard against -
            # a $Jobs element can be a non-null object with no populated Id
            # (or, per f21f03b, $Jobs itself can carry a null placeholder when
            # Get-VBRJob throws) - either way, .Id.ToString() on it aborts the
            # whole sweep via the outer catch, not just this one lookup. Built
            # in a single pass: $KnownJobIds only needs $j/$j.Id non-null,
            # $JobIdByName additionally requires a non-empty, first-seen Name -
            # keep that extra condition scoped to $JobIdByName's own branch,
            # not folded into the shared guard, or a job with an Id but no
            # Name would silently vanish from $KnownJobIds too.
            #
            # OrdinalIgnoreCase: $RestorePointsByJob (a Hashtable) looks up
            # keys case-insensitively, so this gate must not be stricter than
            # the thing it's gating - a case-sensitive HashSet here would risk
            # silently dropping a resolved Id that only ever mismatches on case.
            $JobIdByName = @{}
            $KnownJobIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($j in @($Jobs)) {
                if ($null -eq $j -or $null -eq $j.Id) { continue }
                $idStr = $j.Id.ToString()
                [void]$KnownJobIds.Add($idStr)
                if ($j.Name -and -not $JobIdByName.ContainsKey($j.Name)) { $JobIdByName[$j.Name] = $idStr }
            }

            $Tier1Matched = 0
            $Tier1Failed  = 0
            $Tier2Matched = 0

            # Group by BackupId (ADR 0023): confirmed live to be scoped to one
            # protected object's chain within one job, so every restore point
            # sharing a BackupId must resolve to the same owning job. Resolving
            # ownership once per group instead of once per point turns "one
            # GetSourceJob() call per restore point" into "one call per
            # protected object's retention chain" - the actual cost driver
            # the old Type=Snapshot skip existed to work around, addressed
            # directly instead of by excluding an entire Type value.
            $Groups = @($AllRestorePoints | Group-Object -Property BackupId)

            # Tier 1: Id-based via GetSourceJob() (+ GetParentJob() walk-up),
            # one call per group, applied to every restore point in it.
            $UnresolvedGroups = [System.Collections.ArrayList]::new()
            foreach ($Group in $Groups) {
                $Representative = $Group.Group[0]

                $SourceJob   = $null
                $LookupThrew = $false
                try { $SourceJob = $Representative.GetSourceJob() } catch { $LookupThrew = $true }

                if ($null -ne $SourceJob) {
                    # A GetParentJob() throw means this job type doesn't
                    # implement it - falling back to the child's own Id is a
                    # valid outcome, not a failure, so it isn't counted
                    # alongside $Tier1Failed.
                    $ParentJob = $null
                    try { $ParentJob = $SourceJob.GetParentJob() } catch {}
                    if ($null -ne $ParentJob) { $SourceJob = $ParentJob }
                }

                $JobIdKey = $null
                if ($null -ne $SourceJob -and $null -ne $SourceJob.Id) {
                    $CandidateKey = $SourceJob.Id.ToString()
                    if ($KnownJobIds.Contains($CandidateKey)) { $JobIdKey = $CandidateKey }
                }

                if ($JobIdKey) {
                    if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                        $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                    }
                    foreach ($RestorePoint in $Group.Group) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                    $Tier1Matched += $Group.Count
                } else {
                    [void]$UnresolvedGroups.Add($Group)
                    if ($LookupThrew) { $Tier1Failed += $Group.Count }
                }
            }

            # Tier 2: name-based fallback, GATED - only accepted if the
            # resolved job has zero tier-1 matches. Display names collide
            # across genuinely different backup objects, so this cannot be
            # trusted to override an existing Id-based match.
            #
            # Snapshot Tier 1's output once, here, after the full pass over
            # every group completes - not a live check during tier 2, and not
            # interleaved tier1->tier2 per group. Either of those would let a
            # stale-name group get accepted before a same-job Id-match group
            # (processed later) arrives, reintroducing exactly the
            # misattribution ADR 0021's gating was built to prevent.
            $Tier1MatchedJobIds = [System.Collections.Generic.HashSet[string]]::new(
                [string[]]$RestorePointsByJob.Keys,
                [System.StringComparer]::OrdinalIgnoreCase
            )
            foreach ($Group in $UnresolvedGroups) {
                $Representative = $Group.Group[0]

                $JobIdKey = $null
                try {
                    $ParentName = $Representative.GetBackup().GetParentOrThis().Name
                    if ($ParentName -and $JobIdByName.ContainsKey($ParentName)) { $JobIdKey = $JobIdByName[$ParentName] }
                } catch {}
                if (-not $JobIdKey) { continue }
                if ($Tier1MatchedJobIds.Contains($JobIdKey)) { continue }

                if (-not $RestorePointsByJob.ContainsKey($JobIdKey)) {
                    $RestorePointsByJob[$JobIdKey] = [System.Collections.ArrayList]::new()
                }
                foreach ($RestorePoint in $Group.Group) { [void]$RestorePointsByJob[$JobIdKey].Add($RestorePoint) }
                $Tier2Matched += $Group.Count
            }

            try {
                Write-LogFile "Restore point matching: $Tier1Matched matched via tier 1, $Tier1Failed tier-1 lookup failures, $Tier2Matched tier-2, $($AllRestorePoints.Count - $Tier1Matched - $Tier2Matched) unmatched/orphaned/snapshot"
            } catch {}
            try {
                Write-LogFile "BackupId grouping: $($AllRestorePoints.Count) restore points reduced to $($Groups.Count) groups ($($Groups.Count) tier-1 lookups + $($UnresolvedGroups.Count) tier-2 lookups attempted, vs. $($AllRestorePoints.Count) lookups pre-grouping)"
            } catch {}
        } catch {
            # This is the sweep's outermost catch - unlike the Write-LogFile
            # calls inside the try block (which just fall through to here),
            # a throw from this one has nowhere left to go: it unwinds
            # Get-VhcJob entirely and _Jobs.csv/_configBackup.csv never
            # export at all, which is worse than merely disabling the sweep.
            try { Write-LogFile "Restore point sweep failed: $($_.Exception.Message)" -LogLevel "ERROR" } catch {}
            Add-VhciModuleError -CollectorName 'Jobs' -ErrorMessage $_.Exception.Message
            $NeedsSweep = $false
        }
    }

    # ------------------------------------------------------------------
    # Main VBR job processing loop - restore point size calculation
    # ------------------------------------------------------------------
    [System.Collections.ArrayList]$AllJobs = @()

    foreach ($Job in @($Jobs)) {
        if ($null -eq $Job) { continue }
        try {
            $RestorePoints = @()
            if ($NeedsSweep) {
                if ($null -ne $Job.Id) {
                    $JobIdKey = $Job.Id.ToString()
                    if ($RestorePointsByJob.ContainsKey($JobIdKey)) {
                        $RestorePoints = $RestorePointsByJob[$JobIdKey]
                    }
                }
            } else {
                $LastBackup = $Job.GetLastBackup()
                if ($null -ne $LastBackup) {
                    $RestorePoints = @(Get-VBRRestorePoint -Backup $LastBackup)
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

        try { Write-LogFile "Job: $($Job.Name) - Total OnDisk GB: $TotalOnDiskGB" } catch {}

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
