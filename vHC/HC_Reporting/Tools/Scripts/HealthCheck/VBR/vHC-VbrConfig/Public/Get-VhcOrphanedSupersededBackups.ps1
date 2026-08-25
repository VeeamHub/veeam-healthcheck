#Requires -Version 5.1

function Get-VhcOrphanedSupersededBackups {
    <#
    .Synopsis
        Reads the $script:VhcOrphanedSupersededCache Get-VhcJob populates,
        excludes Tape Backups, classifies each remaining BackupId group's
        restore points as Orphaned or Superseded, splits into one row per
        (BackupId, ObjectId), and exports _orphanedSupersededBackups.csv.
    .Parameter RepositoryDetails
        ArrayList of [pscustomobject]@{ID; Name} rows returned by
        Get-VhcRepository - the same object Get-VhcJob takes, used the same
        way: resolving RepositoryId to a human-readable RepositoryName for
        the report's per-repo grouping. May be $null - RepositoryName will
        be blank in that case, matching Get-VhcJob's own RepoName behavior.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object]$RepositoryDetails = $null
    )

    $message = "Collecting orphaned and superseded backups..."
    Write-LogFile $message

    $Rows = [System.Collections.Generic.List[object]]::new()

    if ($null -eq $script:VhcOrphanedSupersededCache) {
        Write-LogFile "No sweep cache available - Get-VhcJob did not run first, or found nothing to retain." -LogLevel "WARNING"
        return
    }

    if (-not $script:VhcOrphanedSupersededCache.SweepRan) {
        # Orphaned detection needs the global sweep; an environment made
        # entirely of ADR 0022 "safe" allowlist job types never triggers it.
        # This is an accepted gap (design doc, Q6/Q10) - Superseded/#197
        # coverage from the per-job stale-ObjectId guard is unaffected,
        # since that guard doesn't depend on the sweep having run, so
        # StaleObject candidates (if any) still export normally below.
        Write-LogFile "Global restore-point sweep did not run for this environment - Orphaned Backup detection not evaluated." -LogLevel "INFO"
    }

    foreach ($Candidate in $script:VhcOrphanedSupersededCache.CandidateGroups) {
        try {
            $RestorePoints = @($Candidate.RestorePoints)
            if ($RestorePoints.Count -eq 0) { continue }

            $Representative = $RestorePoints[0]
            $Backup = $null
            try { $Backup = $Representative.GetBackup() } catch { $Backup = $null }
            if ($null -eq $Backup) { continue }

            $ParentOrThis = $null
            try { $ParentOrThis = $Backup.GetParentOrThis() } catch { $ParentOrThis = $Backup }
            if ($null -eq $ParentOrThis) { $ParentOrThis = $Backup }

            # Tape exclusion (#192): checked on the immediate GetBackup()
            # object, not GetParentOrThis(), which walks past the tape-copy
            # relationship. Confirmed live: TypeToString is unreliable across
            # platforms for this (a Proxmox-to-tape copy reads "Proxmox", no
            # "Tape" substring), but IsTapeBackup is a consistent boolean at
            # this level.
            $IsTape = $false
            try { $IsTape = [bool]$Backup.IsTapeBackup } catch { $IsTape = $false }
            if ($IsTape) { continue }

            $JobName        = $null
            $OriginalType   = $null
            $RepositoryId   = $null
            try { $JobName      = $ParentOrThis.Name } catch {}
            try { $OriginalType = $ParentOrThis.TypeToString } catch {}
            try { $RepositoryId = $ParentOrThis.RepositoryId } catch {}

            # Same resolution Get-VhcJob.ps1 already does for its own
            # RepoName column - RepositoryId alone is a bare Guid with no
            # human-readable meaning to a report reader.
            $RepositoryName = $null
            if ($RepositoryDetails -and $RepositoryId) {
                $RepositoryName = $RepositoryDetails |
                    Where-Object { $_.Id -eq $RepositoryId } |
                    Select-Object -First 1 -ExpandProperty Name
            }

            $CurrentJobId = if ($Candidate.CurrentJobId) { $Candidate.CurrentJobId } else { [guid]::Empty.ToString() }
            $Category     = if ($Candidate.CurrentJobId) { 'Superseded' } else { 'Orphaned' }

            $ByObjectId = @{}
            foreach ($RestorePoint in $RestorePoints) {
                $ObjKey = $RestorePoint.ObjectId.ToString()
                if (-not $ByObjectId.ContainsKey($ObjKey)) {
                    $ByObjectId[$ObjKey] = [System.Collections.Generic.List[object]]::new()
                }
                $ByObjectId[$ObjKey].Add($RestorePoint)
            }

            foreach ($ObjKey in $ByObjectId.Keys) {
                $ObjectPoints = $ByObjectId[$ObjKey]
                $Fulls        = @($ObjectPoints | Where-Object { $_.Type -eq 'Full' })
                $Increments   = @($ObjectPoints | Where-Object { $_.Type -eq 'Increment' })
                $Sorted       = $ObjectPoints | Sort-Object CreationTimeUtc

                $AvgFullSize = if ($Fulls.Count -gt 0) {
                    ($Fulls | Measure-Object -Property ApproxSize -Average).Average
                } else { 0 }
                $AvgIncrementalSize = if ($Increments.Count -gt 0) {
                    ($Increments | Measure-Object -Property ApproxSize -Average).Average
                } else { 0 }
                $TotalSize = ($ObjectPoints | Measure-Object -Property ApproxSize -Sum).Sum

                $Rows.Add([PSCustomObject]@{
                    RepositoryId             = $RepositoryId
                    RepositoryName           = $RepositoryName
                    JobName                  = $JobName
                    CurrentJobId             = $CurrentJobId
                    Category                 = $Category
                    OriginalJobType          = $OriginalType
                    ObjectId                 = $ObjectPoints[0].ObjectId
                    BackupId                 = $ObjectPoints[0].BackupId
                    ObjectName               = $ObjectPoints[0].Name
                    FullCount                = $Fulls.Count
                    IncrementalCount         = $Increments.Count
                    AvgFullSizeBytes         = $AvgFullSize
                    AvgIncrementalSizeBytes  = $AvgIncrementalSize
                    TotalSizeBytes           = $TotalSize
                    OldestRestorePoint       = $Sorted[0].CreationTimeUtc
                    NewestRestorePoint       = $Sorted[-1].CreationTimeUtc
                })
            }
        } catch {
            Write-LogFile "Could not process an orphaned/superseded candidate group: $($_.Exception.Message)" -LogLevel "WARNING"
            Add-VhciModuleError -CollectorName 'OrphanedSupersededBackups' -ErrorMessage $_.Exception.Message
        }
    }

    $Rows | Export-VhciCsv -FileName '_orphanedSupersededBackups.csv'

    # Meta file, always exactly one row: Export-VhciCsv skips writing
    # entirely when there are zero rows to export, so an empty/missing
    # _orphanedSupersededBackups.csv can't distinguish "sweep never ran"
    # from "ran, found nothing." This one-row file always exports (never
    # zero rows), giving the C# side a real signal to tell them apart.
    [PSCustomObject]@{
        SweepRan = $script:VhcOrphanedSupersededCache.SweepRan
    } | Export-VhciCsv -FileName '_orphanedSupersededBackupsMeta.csv'

    Write-LogFile ($message + "DONE")
}
