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

    $Rows     = [System.Collections.Generic.List[object]]::new()
    $SweepRan = $false

    if ($null -eq $script:VhcOrphanedSupersededCache) {
        # Deliberately falls through to the meta-CSV export below instead of
        # returning here: that file's entire purpose is letting the C# side
        # tell "sweep never ran / cache never existed" apart from "ran, found
        # nothing" (Get-VhcJob's sub-collectors can throw before ever setting
        # the cache - a real, reachable path via Invoke-VhciJobSubCollectors).
        # An early return here would recreate the exact missing-file
        # ambiguity the meta file exists to remove.
        Write-LogFile "No sweep cache available - Get-VhcJob did not run first, or found nothing to retain." -LogLevel "WARNING"
    } else {
        # [bool] cast, not a bare assignment: if the cache object exists but
        # SweepRan is missing or explicitly $null, an unguarded assignment
        # would carry $null into the meta CSV (a blank cell) instead of the
        # $false the C# side needs to read as a real signal.
        $SweepRan = [bool]$script:VhcOrphanedSupersededCache.SweepRan
        if (-not $SweepRan) {
            # Orphaned detection needs the global sweep; an environment made
            # entirely of ADR 0022 "safe" allowlist job types never triggers
            # it. This is an accepted gap (see ADR 0025) - Superseded/#197
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

                # [guid]::Empty is truthy in PowerShell (`if ($g)` is $true
                # even when $g -eq [guid]::Empty), so a "no repository"
                # all-zero-Guid sentinel from the VBR SDK would otherwise
                # pass the "$RepositoryId present" check below and the
                # `RepositoryId = $RepositoryId` export a few lines down,
                # landing in the CSV as the literal all-zero Guid string
                # instead of blank/$null. Normalizing here, right after the
                # read, means every downstream use (the RepositoryName
                # lookup's truthiness check, and the exported column) treats
                # it the same as a genuinely-missing RepositoryId - falling
                # into the "unknown" bucket the C# table already has for
                # $null (COrphanedSupersededBackupsTable.Render groups by
                # `r.RepositoryId ?? "unknown"`), rather than a distinct,
                # meaningless all-zero-Guid group.
                if ($RepositoryId -eq [guid]::Empty) { $RepositoryId = $null }

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

                # Category must come from Candidate.Reason, not from
                # CurrentJobId's truthiness: a 'StaleObject' candidate (#197)
                # always belongs to a live, currently-configured job by
                # construction (Get-VhcJob.ps1 only emits it after confirming
                # $MatchedAny on that job's current membership) - but its
                # CurrentJobId can still be $null in the edge case where
                # $Job.Id itself returned null. Deriving Category from
                # CurrentJobId's truthiness alone would misclassify that
                # candidate as 'Orphaned' ("no live job") when the opposite
                # is true. Only 'Unresolved' (Tier 1 + Tier 2 both failed to
                # resolve any job) is genuinely Orphaned; 'Tier2Suppressed'
                # and 'StaleObject' both name a real, current job.
                $Category = if ($Candidate.Reason -eq 'Unresolved') { 'Orphaned' } else { 'Superseded' }

                # A null ObjectId can't be safely merged under any key shared
                # with another restore point - two unrelated null-ObjectId
                # points grouped together would misattribute Full/Increment
                # counts and sizes across them. Calling .ToString() on a null
                # ObjectId also throws, and (unguarded) that exception would
                # be caught by this candidate's own try/catch above, silently
                # discarding every OTHER ObjectId in this group along with
                # the null one. Get-VhcJob.ps1's own stale-ObjectId guard
                # treats a null ObjectId as "uncertain -> keep it, don't
                # exclude, don't let it take anything else down" - mirrored
                # here by giving each null-ObjectId point its own singleton
                # key instead of ever calling .ToString() on it.
                $ByObjectId = @{}
                $NullObjectIdSequence = 0
                foreach ($RestorePoint in $RestorePoints) {
                    if ($null -eq $RestorePoint.ObjectId) {
                        # NOT $($NullObjectIdSequence++) - a bare postfix
                        # increment used as a value (even inside a string
                        # subexpression) doesn't write to the output stream
                        # in PowerShell, so that form evaluates to an empty
                        # string every time and every null-ObjectId point
                        # collapses onto the same "null-objectid-" key -
                        # silently re-merging unrelated points' Full/
                        # Increment counts and sizes, exactly what this
                        # singleton-key approach exists to prevent.
                        $ObjKey = "null-objectid-$NullObjectIdSequence"
                        $NullObjectIdSequence++
                    } else {
                        $ObjKey = $RestorePoint.ObjectId.ToString()
                    }
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

                    # Pin an explicit, invariant wire format for the date/double
                    # columns instead of leaving them as raw [DateTime]/[double]
                    # values for Export-Csv (via Export-VhciCsv) to stringify
                    # implicitly. Export-Csv formats non-string properties using
                    # the collecting host's CURRENT culture, not invariant - on
                    # a comma-decimal host (most of continental Europe) a value
                    # like 8500000000.5 would be written as "8500000000,5", and
                    # on a dd/MM host (UK, AU/NZ, India, LatAm, most of Europe)
                    # a date would be written "07.03.2026 10:30:00" with no
                    # unambiguous year-first ordering. The C# consumer
                    # (OrphanedSupersededBackupAggregator.MapRow) parses these
                    # columns with CultureInfo.InvariantCulture, so a
                    # current-culture string either silently misparses (e.g.
                    # "8500000000,5" read as 85000000005 - ~10x inflated,
                    # or day/month swapped - both with no exception) or throws
                    # and the whole row is silently dropped. Round-trip ISO
                    # 8601 ("o") for dates and InvariantCulture for doubles are
                    # the exact mutual inverse of what the consumer parses.
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
                        AvgFullSizeBytes         = $AvgFullSize.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                        AvgIncrementalSizeBytes  = $AvgIncrementalSize.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                        TotalSizeBytes           = $TotalSize.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                        OldestRestorePoint       = $Sorted[0].CreationTimeUtc.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
                        NewestRestorePoint       = $Sorted[-1].CreationTimeUtc.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
                    })
                }
            } catch {
                Write-LogFile "Could not process an orphaned/superseded candidate group: $($_.Exception.Message)" -LogLevel "WARNING"
                Add-VhciModuleError -CollectorName 'OrphanedSupersededBackups' -ErrorMessage $_.Exception.Message
            }
        }
    }

    $Rows | Export-VhciCsv -FileName '_orphanedSupersededBackups.csv'

    # Meta file, always exactly one row, even when the cache was entirely
    # $null: Export-VhciCsv skips writing entirely when there are zero rows
    # to export, so an empty/missing _orphanedSupersededBackups.csv can't
    # distinguish "sweep never ran"/"cache never existed" from "ran, found
    # nothing." This one-row file always exports (never zero rows), giving
    # the C# side a real signal to tell them apart.
    [PSCustomObject]@{
        SweepRan = $SweepRan
    } | Export-VhciCsv -FileName '_orphanedSupersededBackupsMeta.csv'

    Write-LogFile ($message + "DONE")
}
