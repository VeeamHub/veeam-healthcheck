#Requires -Version 5.1
<#
.SYNOPSIS
    Ad hoc, read-only comparison of today's per-job GetLastBackup() sizing
    against the proposed global-restore-point-sweep matching approach,
    including the tier-2 name-based fallback for public cloud plug-in
    platforms (AWS EC2/RDS/FSx/EFS, Azure IaaS/SQL, GCE) where
    GetSourceJob() throws outright.

.DESCRIPTION
    Validates whether the redesign in
    docs/superpowers/specs/2026-08-21-job-sizing-restore-point-matching-design.md
    resolves the 0 MB / 0 GB Source Size / Est. On Disk GB currently reported
    for:
      - Tier 1 (GetSourceJob + GetParentJob walk-up): HPE Morpheus VME,
        Nutanix AHV, oVirt KVM, and policy-driven Managed Agent jobs.
      - Tier 2 (GetBackup().GetParentOrThis().Name, name-matched against
        $Jobs): public cloud plug-in jobs, where GetSourceJob() itself
        throws "Unable to get job for backup: <id>" and there is no
        Id-yielding path back to a job object at all.

    Tier 2 is GATED: it only runs against restore points tier 1 could not
    resolve at all, and its name-matched result is only accepted if that
    job has zero tier-1 matches. A prior ungated version of this test
    showed HPE Morpheus/Nutanix AHV restore point counts increasing when
    tier 2 was added even though tier 1 already matched something for
    those jobs - suspected to be tier 2 picking up restore points from
    machines no longer an active member of the policy, just because the
    backup object still carries the old policy name. Gating trusts tier 1
    completely once it has any match, and only lets tier 2 rescue jobs
    tier 1 found nothing for at all.

    A third approach (Job.GetObjectsInJob() -> ObjectId -> Get-VBRRestorePoint
    -ObjectId) was tested and rejected - see the comment above the removed
    Get-ForwardJobSizing function's former location for why.

    NOT part of vHC. Standalone scratch script - read-only, makes no changes
    to VBR or to this repo's code. Delete when done.

    Run in a PowerShell session already connected to VBR (Connect-VBRServer,
    or directly on the VBR server console), with the Veeam.Backup.PowerShell
    module loaded.

.Note (2026-08-22, ADR 0023)
    Updated to validate BackupId-grouped matching before it lands in
    Get-VhcJob.ps1. MUST be run against live VBR labs by someone with
    access before this change is considered validated - in particular,
    against an environment with a Storage-Snapshot Backup population,
    which no lab used for ADR 0021's original validation had, and which
    is the population that most likely caused the original "100% throw
    rate" measurement (see ADR 0023).
#>

if (-not (Get-Module -Name Veeam.Backup.PowerShell)) {
    Write-Warning "Veeam.Backup.PowerShell module not detected in this session. Import it / connect to VBR first."
}

# ---------------------------------------------------------------------------
# OLD approach: reproduces Get-VhcJob.ps1's current per-job GetLastBackup()
# logic exactly, but with the error captured instead of silently swallowed,
# so we can see WHY a job reports 0/0 today.
# ---------------------------------------------------------------------------
function Get-OldJobSizing {
    param($Job)

    $result = [PSCustomObject]@{
        OldRestorePointCount = 0
        OldSourceGB          = 0
        OldOnDiskGB          = 0
        OldLastBackupNull    = $false
        OldError             = $null
    }

    $LastBackup = $null
    try {
        $LastBackup = $Job.GetLastBackup()
    } catch {
        $result.OldError = "GetLastBackup(): $($_.Exception.Message)"
    }

    if ($null -eq $LastBackup) { $result.OldLastBackupNull = $true }

    $RestorePoints = @()
    if ($null -ne $LastBackup) {
        try {
            $RestorePoints = @(Get-VBRRestorePoint -Backup $LastBackup)
        } catch {
            $result.OldError = "Get-VBRRestorePoint -Backup: $($_.Exception.Message)"
        }
    }

    $result.OldRestorePointCount = $RestorePoints.Count

    $totalOnDiskGB = 0
    foreach ($rp in $RestorePoints) {
        try { $totalOnDiskGB += ($rp.GetStorage().Stats.BackupSize / 1GB) } catch {}
    }
    $result.OldOnDiskGB = [Math]::Round($totalOnDiskGB, 2)

    $calculatedOriginalSize = 0
    try {
        if ($RestorePoints.Count -gt 0) {
            $latestPoints = $RestorePoints |
                Group-Object -Property { $_.ObjectId } |
                ForEach-Object { $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1 }
            $approxSum = ($latestPoints | Where-Object { $null -ne $_.ApproxSize } | Measure-Object -Property ApproxSize -Sum).Sum
            $calculatedOriginalSize = if ($approxSum -and $approxSum -gt 0) { $approxSum } else { $Job.Info.IncludedSize }
        } else {
            $calculatedOriginalSize = $Job.Info.IncludedSize
        }
    } catch {
        $calculatedOriginalSize = $Job.Info.IncludedSize
    }
    $result.OldSourceGB = [Math]::Round($calculatedOriginalSize / 1GB, 2)

    return $result
}

# ---------------------------------------------------------------------------
# FORWARD approach (Job.GetObjectsInJob() -> ObjectId -> Get-VBRRestorePoint
# -ObjectId) was tested and REJECTED - dropped from this script. Confirmed
# unsafe across two independent labs whenever two jobs protect the same
# object: on-prem it duplicated Hyper-V - Windows/Linux's numbers onto
# Hyper-V - Replicas and VMware - HPE - BfSS's onto - Snapshot Only; on the
# cloud lab, "VMs DIrect to Vault" (13.03 GB) and "VMs for Direct Restore to
# Azure / AWS" (12.74 GB) - both real, distinct jobs - collapsed to 0.00 GB
# and 25.77 GB, where 25.77 = 13.03 + 12.74 exactly: Forward pooled both
# jobs' restore points onto one and zeroed the other. -ObjectId returns every
# restore point for that object across ANY job that ever touched it, with no
# way to filter to "produced by this job" - a structural limitation, not a
# bug. It also silently returned 0/0 for VBR Managed Agents - Linux (no
# error at all), and only found 1 of 9 real objects for a VMware Cloud
# Director vApp job (GetObjectsInJob() returned the vApp container, not its
# nested VMs). It was faster than the sweep on-prem (9.6s vs 17.35s) but 5x
# SLOWER on the cloud lab (6.52s vs 1.27s) - its cost scales with job count
# x per-job round-trip, not restore-point volume, so "faster" was an
# artifact of that lab's ratio, not a property of the approach.
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Fetch jobs, build the tier-2 name lookup
# ---------------------------------------------------------------------------
Write-Host "Collecting jobs..." -ForegroundColor Cyan
$Jobs = @(Get-VBRJob -WarningAction SilentlyContinue)

# Mirror Get-VhcJob.ps1's own standalone-agent-job collection (lines 43-67) -
# without this, restore points that tier 1 correctly matches to a standalone
# agent job's Id have nowhere to land in $Jobs, undercounting the comparison.
$standaloneBackups = @(Get-VBRBackup -WarningAction SilentlyContinue |
    Where-Object { $_.IsAgentStandaloneJob -eq $true })
$standaloneJobs = @($standaloneBackups | ForEach-Object {
    try { $_.GetJob() } catch { $null }
} | Where-Object { $_ })
if ($standaloneJobs.Count -gt 0) {
    $Jobs = @($Jobs) + $standaloneJobs
}

Write-Host "Found $($Jobs.Count) jobs ($($standaloneJobs.Count) standalone agent)."

$jobIdByName = @{}
$jobNameById = @{}
$nameCollisions = @()
foreach ($j in $Jobs) {
    if ($jobIdByName.ContainsKey($j.Name)) {
        $nameCollisions += $j.Name
    } else {
        $jobIdByName[$j.Name] = $j.Id.ToString()
    }
    $jobNameById[$j.Id.ToString()] = $j.Name
}
if ($nameCollisions.Count -gt 0) {
    Write-Warning "Duplicate job names detected (tier 2 name matching will use the first job seen for each): $($nameCollisions -join ', ')"
}

# ---------------------------------------------------------------------------
# NEW approach (ADR 0023): one global restore-point sweep, grouped by
# BackupId (confirmed scoped to one protected object's chain within one
# job), resolved in two full passes over GROUPS instead of individual
# points.
#   Pass 1 (Tier 1): GetSourceJob() (+ GetParentJob() walk-up for
#           per-machine child jobs under policy-driven platforms) on ONE
#           representative per group, applied to every point in the group.
#   Pass 2 (Tier 2): only for GROUPS tier 1 couldn't resolve at all -
#           GetBackup().GetParentOrThis().Name on the representative,
#           matched against $Jobs by name, gated the same way as before
#           (job must have zero tier-1 matches, checked only after the
#           FULL tier-1 pass over every group completes).
# Replica-type jobs are no longer special-cased - their restore points
# (Type=Snapshot) are grouped and resolved through this same pipeline.
# ---------------------------------------------------------------------------
Write-Host "Running global restore-point sweep (one-time)..." -ForegroundColor Cyan
$fetchStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$allRestorePoints = @(Get-VBRRestorePoint -WarningAction SilentlyContinue)
$fetchStopwatch.Stop()
Write-Host "Found $($allRestorePoints.Count) restore points server-wide."

$restorePointsByJob = @{}
$tier1MatchedJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
$tier1Matched       = 0

$knownJobIds = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($j in @($Jobs)) { [void]$knownJobIds.Add($j.Id.ToString()) }
$tier1UnknownIdCount = 0

$groupStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$backupIdGroups = @($allRestorePoints | Group-Object -Property BackupId)
$groupStopwatch.Stop()
Write-Host ("Grouped {0} restore points into {1} BackupId groups in {2}s." -f $allRestorePoints.Count, $backupIdGroups.Count, [Math]::Round($groupStopwatch.Elapsed.TotalSeconds, 3))

$getSourceJobOkMs = 0.0;    $getSourceJobOkCount = 0
$getSourceJobNullMs = 0.0;  $getSourceJobNullCount = 0
$getSourceJobThrowMs = 0.0; $getSourceJobThrowCount = 0
$getParentJobMs = 0.0;      $getParentJobCount = 0

$tier1UnresolvedGroups = [System.Collections.ArrayList]::new()

$tier1LoopStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($group in $backupIdGroups) {
    $representative = $group.Group[0]
    $sourceJob = $null

    $callSw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $sourceJob = $representative.GetSourceJob()
        $callSw.Stop()
        if ($null -ne $sourceJob) {
            $getSourceJobOkMs += $callSw.Elapsed.TotalMilliseconds
            $getSourceJobOkCount++
        } else {
            $getSourceJobNullMs += $callSw.Elapsed.TotalMilliseconds
            $getSourceJobNullCount++
        }
    } catch {
        $callSw.Stop()
        $getSourceJobThrowMs += $callSw.Elapsed.TotalMilliseconds
        $getSourceJobThrowCount++
        $sourceJob = $null
    }

    $jobIdKey = $null
    if ($null -ne $sourceJob) {
        $parentSw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $parentJob = $sourceJob.GetParentJob()
            if ($null -ne $parentJob) { $sourceJob = $parentJob }
        } catch {}
        $parentSw.Stop()
        $getParentJobMs += $parentSw.Elapsed.TotalMilliseconds
        $getParentJobCount++

        try { $jobIdKey = $sourceJob.Id.ToString() } catch {}

        if ($jobIdKey -and -not $knownJobIds.Contains($jobIdKey)) {
            $tier1UnknownIdCount++
            $jobIdKey = $null
        }
    }

    if ($jobIdKey) {
        if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
            $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
        }
        foreach ($rp in $group.Group) { [void]$restorePointsByJob[$jobIdKey].Add($rp) }
        [void]$tier1MatchedJobIds.Add($jobIdKey)
        $tier1Matched += $group.Count
    } else {
        [void]$tier1UnresolvedGroups.Add($group)
    }
}
$tier1LoopStopwatch.Stop()

$tier2Matched         = 0
$tier2Suppressed      = 0
$unmatched            = 0
$getBackupMs          = 0.0
$getParentOrThisMs    = 0.0
$tier2SuppressedDetails = [System.Collections.ArrayList]::new()

$tier2LoopStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($group in $tier1UnresolvedGroups) {
    $representative = $group.Group[0]
    $jobIdKey = $null
    $backup = $null
    try {
        $backupSw = [System.Diagnostics.Stopwatch]::StartNew()
        $backup = $representative.GetBackup()
        $backupSw.Stop()
        $getBackupMs += $backupSw.Elapsed.TotalMilliseconds

        $parentSw = [System.Diagnostics.Stopwatch]::StartNew()
        $parentBackupName = $backup.GetParentOrThis().Name
        $parentSw.Stop()
        $getParentOrThisMs += $parentSw.Elapsed.TotalMilliseconds

        if ($parentBackupName -and $jobIdByName.ContainsKey($parentBackupName)) {
            $jobIdKey = $jobIdByName[$parentBackupName]
        }
    } catch {}

    if (-not $jobIdKey) {
        $unmatched += $group.Count
        continue
    }

    if ($tier1MatchedJobIds.Contains($jobIdKey)) {
        $tier2Suppressed += $group.Count
        $backupId = $null
        try { $backupId = $backup.Id } catch {}
        [void]$tier2SuppressedDetails.Add([PSCustomObject]@{
            RestorePointName = $representative.Name
            CreationTime     = $representative.CreationTimeUtc
            BackupId         = $backupId
            ResolvedJobId    = $jobIdKey
            ResolvedJobName  = if ($jobNameById.ContainsKey($jobIdKey)) { $jobNameById[$jobIdKey] } else { '(unknown)' }
        })
        continue
    }

    if (-not $restorePointsByJob.ContainsKey($jobIdKey)) {
        $restorePointsByJob[$jobIdKey] = [System.Collections.ArrayList]::new()
    }
    foreach ($rp in $group.Group) { [void]$restorePointsByJob[$jobIdKey].Add($rp) }
    $tier2Matched += $group.Count
}
$tier2LoopStopwatch.Stop()

$sweepTotalSeconds = $fetchStopwatch.Elapsed.TotalSeconds + $groupStopwatch.Elapsed.TotalSeconds + $tier1LoopStopwatch.Elapsed.TotalSeconds + $tier2LoopStopwatch.Elapsed.TotalSeconds

Write-Host ""
Write-Host "=== Sweep timing breakdown ===" -ForegroundColor Magenta
Write-Host ("Fetch Get-VBRRestorePoint ({0} points):          {1}s" -f $allRestorePoints.Count, [Math]::Round($fetchStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("Group by BackupId ({0} points -> {1} groups):    {2}s" -f $allRestorePoints.Count, $backupIdGroups.Count, [Math]::Round($groupStopwatch.Elapsed.TotalSeconds, 3))
Write-Host ("Tier 1 loop total ({0} group lookups):            {1}s" -f $backupIdGroups.Count, [Math]::Round($tier1LoopStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("  GetSourceJob() succeeded ({0} calls):           {1}s" -f $getSourceJobOkCount, [Math]::Round($getSourceJobOkMs / 1000, 2))
Write-Host ("  GetSourceJob() returned null ({0} calls):       {1}s" -f $getSourceJobNullCount, [Math]::Round($getSourceJobNullMs / 1000, 2))
Write-Host ("  GetSourceJob() threw ({0} calls):               {1}s" -f $getSourceJobThrowCount, [Math]::Round($getSourceJobThrowMs / 1000, 2))
Write-Host ("  GetParentJob() walk-up ({0} calls):             {1}s" -f $getParentJobCount, [Math]::Round($getParentJobMs / 1000, 2))
Write-Host ("Tier 2 loop total ({0} group lookups):             {1}s" -f $tier1UnresolvedGroups.Count, [Math]::Round($tier2LoopStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("  GetBackup():                                    {0}s" -f [Math]::Round($getBackupMs / 1000, 2))
Write-Host ("  GetParentOrThis().Name:                         {0}s" -f [Math]::Round($getParentOrThisMs / 1000, 2))
Write-Host ("Sweep grand total:                                {0}s" -f [Math]::Round($sweepTotalSeconds, 2))
$sweepStopwatch = [PSCustomObject]@{ Elapsed = [TimeSpan]::FromSeconds($sweepTotalSeconds) }

Write-Host ""
Write-Host "Matched via tier 1 (GetSourceJob/GetParentJob):        $tier1Matched"
Write-Host ('Tier 1 resolved to an Id NOT in $Jobs (rerouted to tier 2): ' + $tier1UnknownIdCount)
Write-Host "Matched via tier 2 (GetParentOrThis().Name):           $tier2Matched"
Write-Host "Tier 2 suppressed (job already had tier-1 matches):    $tier2Suppressed"
Write-Host "Still unmatched (orphaned/imported/out-of-scope):       $unmatched"

if ($tier2SuppressedDetails.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Tier 2 suppressed restore points (job already had tier-1 matches) ===" -ForegroundColor Magenta
    Write-Host "Is this a stale/out-of-scope machine, or a second valid chain for the same job being discarded?"
    $tier2SuppressedDetails | Sort-Object ResolvedJobId, CreationTime | Format-Table -AutoSize

    Write-Host ""
    Write-Host "=== Tier 1 matches for those same jobs, for direct comparison ===" -ForegroundColor Magenta
    Write-Host "Same Name/BackupId as a suppressed row above => suppression is discarding a real extra chain for a machine already counted."
    Write-Host "Different Name/BackupId => suppression is correctly protecting against an unrelated/stale machine."
    $affectedJobIds = @($tier2SuppressedDetails.ResolvedJobId | Select-Object -Unique)
    foreach ($jid in $affectedJobIds) {
        $jobName = if ($jobNameById.ContainsKey($jid)) { $jobNameById[$jid] } else { $jid }
        Write-Host ""
        Write-Host ("  -- $jobName (tier-1 matched, currently kept) --")
        if ($restorePointsByJob.ContainsKey($jid)) {
            $restorePointsByJob[$jid] |
                Select-Object Name, CreationTimeUtc, BackupId |
                Sort-Object CreationTimeUtc |
                Format-Table -AutoSize
        } else {
            Write-Host "    (no tier-1 matches recorded for this job Id)"
        }
    }
}

# ---------------------------------------------------------------------------
# BackupId grouping-assumption audit (ADR 0023): the pipeline above only
# calls GetSourceJob()/GetBackup() on ONE representative per group and
# trusts every member resolves identically. This section is the empirical
# proof of that trust - for every multi-point group, call GetSourceJob() on
# EVERY member (not just the representative) and confirm they all agree.
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== BackupId grouping-assumption audit ===" -ForegroundColor Cyan
$groupingViolations = [System.Collections.Generic.List[string]]::new()
$multiPointGroups = @($backupIdGroups | Where-Object { $_.Count -gt 1 })
Write-Host "Total groups: $($backupIdGroups.Count), multi-point groups: $($multiPointGroups.Count)"
foreach ($group in $multiPointGroups) {
    $resolvedOutcomes = $group.Group | ForEach-Object {
        try {
            $sj = $_.GetSourceJob()
            if ($null -ne $sj) { $sj.Id.ToString() } else { '<null>' }
        } catch {
            '<throw>'
        }
    }
    $distinct = $resolvedOutcomes | Select-Object -Unique
    if ($distinct.Count -gt 1) {
        $groupingViolations.Add("BackupId $($group.Name): $($group.Count) points resolved to $($distinct.Count) DIFFERENT outcomes: $($distinct -join ', ')")
    }
}
if ($groupingViolations.Count -gt 0) {
    Write-Host "VIOLATIONS FOUND - grouping assumption does NOT hold in this environment:" -ForegroundColor Red
    $groupingViolations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host "No violations - every multi-point BackupId group resolved identically ($($multiPointGroups.Count) groups checked)." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Grouping performance measurement ===" -ForegroundColor Cyan
Write-Host ("{0} restore points reduced to {1} BackupId groups ({2}% fewer GetSourceJob()/GetBackup() calls than the pre-grouping, one-call-per-point approach)." -f $allRestorePoints.Count, $backupIdGroups.Count, [math]::Round(100 - ($backupIdGroups.Count / [math]::Max($allRestorePoints.Count, 1) * 100), 1))
Write-Host "NOTE: run this against an environment with a Storage-Snapshot Backup population specifically (not just VM replication) to measure the reduction factor against the population that most likely caused ADR 0021's original 5,461-throw measurement (see ADR 0023)."

# ---------------------------------------------------------------------------
# Diagnostic: is VMware Cloud Director's ~2x SourceGB a double-counted vApp
# container object riding alongside its 8 real per-VM objects, or two
# genuinely separate ObjectId groups per VM? One row ~1367GB + 8 per-VM rows
# confirms the container theory; 16-18 rows would mean something else. This
# mirrors the exact Group-Object/ApproxSize logic Get-NewJobSizing uses below.
# ---------------------------------------------------------------------------
$vcdJob = $Jobs | Where-Object { $_.Name -like "*Cloud Director*" -or $_.Name -like "*vApp*" } | Select-Object -First 1
if ($vcdJob -and $restorePointsByJob.ContainsKey($vcdJob.Id.ToString())) {
    Write-Host ""
    Write-Host "=== VCD SourceGB double-count check: $($vcdJob.Name) ===" -ForegroundColor Magenta
    $restorePointsByJob[$vcdJob.Id.ToString()] |
        Group-Object -Property { $_.ObjectId } |
        ForEach-Object { $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1 } |
        Select-Object Name, ObjectId, @{n='ApproxGB'; e={ [Math]::Round($_.ApproxSize / 1GB, 2) }} |
        Sort-Object -Descending ApproxGB |
        Format-Table -AutoSize
}

# ---------------------------------------------------------------------------
# Diagnostic: Backup Copy jobs' OLD RPCount is showing up implausibly large
# (in one lab exactly equal to the server-wide restore point total) - checks
# whether Get-VBRRestorePoint -Backup $LastBackup is actually scoped to that
# one backup, or silently ignoring -Backup and returning everything. If the
# BackupId grouping below shows more than one distinct Id, today's OLD
# numbers for Backup Copy jobs are wrong, not the new sweep.
# ---------------------------------------------------------------------------
$bcJobs = @($Jobs | Where-Object { $_.TypeToString -eq 'Backup Copy' })
foreach ($bcJob in $bcJobs) {
    Write-Host ""
    Write-Host "=== Backup Copy -Backup scoping check: $($bcJob.Name) ===" -ForegroundColor Magenta
    $lb = $null
    try { $lb = $bcJob.GetLastBackup() } catch { Write-Host "  GetLastBackup() threw: $($_.Exception.Message)"; continue }
    if ($null -eq $lb) { Write-Host "  GetLastBackup() returned null"; continue }
    Write-Host "  LastBackup.Id: $($lb.Id)"

    $scoped = @(Get-VBRRestorePoint -Backup $lb -WarningAction SilentlyContinue)
    Write-Host "  Get-VBRRestorePoint -Backup returned $($scoped.Count) point(s)"

    Write-Host "  Grouped by GetBackup().Id (should be ONE group matching LastBackup.Id above if -Backup is honored):"
    $scoped | Select-Object @{n='BackupId'; e={ try { $_.GetBackup().Id } catch { 'THREW' } }} |
        Group-Object BackupId | Select-Object Count, Name | Format-Table -AutoSize

    Write-Host "  Grouped by GetSourceJob().Name (who these points actually resolve to):"
    $scoped | Select-Object @{n='SrcJob'; e={ try { $_.GetSourceJob().Name } catch { 'THREW' } }} |
        Group-Object SrcJob | Select-Object Count, Name | Format-Table -AutoSize

    # A "<CopyJob>\<SourceJob>" compound name from GetSourceJob() means it
    # resolved to a per-source-job CHILD object, not the copy job itself -
    # same pattern as the policy-driven platforms. Does GetParentJob() walk
    # that child up to $bcJob's own Id, same as it does for those platforms?
    Write-Host "  This job's own Id (compare against ParentId below): $($bcJob.Id)"
    Write-Host "  Per-point GetSourceJob()/GetParentJob() resolution (first 3):"
    $scoped | Select-Object -First 3 Name,
        @{n='SrcJobId';   e={ try { $_.GetSourceJob().Id } catch { 'THREW' } }},
        @{n='SrcJobName'; e={ try { $_.GetSourceJob().Name } catch { 'THREW' } }},
        @{n='ParentId';   e={ try { $j = $_.GetSourceJob(); $p = $j.GetParentJob(); if ($p) { $p.Id } else { 'NULL' } } catch { 'THREW' } }},
        @{n='ParentName'; e={ try { $j = $_.GetSourceJob(); $p = $j.GetParentJob(); if ($p) { $p.Name } else { 'NULL' } } catch { 'THREW' } }} |
        Format-List

    # Does GetParentJob() go up a SECOND level? And separately - does tier
    # 2's exact resolution path (GetBackup().GetParentOrThis().Name) land on
    # this copy job's own name, even though GetSourceJob()/GetParentJob()
    # (tier 1) never does? If so, tier 1 just needs to fall through to tier 2
    # instead of accepting a child-job Id that isn't in $Jobs as a "match".
    Write-Host "  Second-level GetParentJob() and tier-2's GetBackup().GetParentOrThis().Name path (first 3):"
    $scoped | Select-Object -First 3 Name,
        @{n='Parent2Id';       e={ try { $j = $_.GetSourceJob().GetParentJob(); $p2 = $j.GetParentJob(); if ($p2) { $p2.Id } else { 'NULL' } } catch { 'THREW' } }},
        @{n='Parent2Name';     e={ try { $j = $_.GetSourceJob().GetParentJob(); $p2 = $j.GetParentJob(); if ($p2) { $p2.Name } else { 'NULL' } } catch { 'THREW' } }},
        @{n='Tier2NameResult'; e={ try { $_.GetBackup().GetParentOrThis().Name } catch { 'THREW' } }} |
        Format-List
}

function Get-NewJobSizing {
    param($Job, $RestorePointsByJob)

    $result = [PSCustomObject]@{
        NewRestorePointCount = 0
        NewSourceGB          = 0
        NewOnDiskGB          = 0
    }

    $jobIdKey = $Job.Id.ToString()
    $RestorePoints = @()
    if ($RestorePointsByJob.ContainsKey($jobIdKey)) {
        $RestorePoints = $RestorePointsByJob[$jobIdKey]
    }
    $result.NewRestorePointCount = $RestorePoints.Count

    $totalOnDiskGB = 0
    foreach ($rp in $RestorePoints) {
        try { $totalOnDiskGB += ($rp.GetStorage().Stats.BackupSize / 1GB) } catch {}
    }
    $result.NewOnDiskGB = [Math]::Round($totalOnDiskGB, 2)

    $calculatedOriginalSize = 0
    try {
        if ($RestorePoints.Count -gt 0) {
            $latestPoints = $RestorePoints |
                Group-Object -Property { $_.ObjectId } |
                ForEach-Object { $_.Group | Sort-Object CreationTimeUtc -Descending | Select-Object -First 1 }
            $approxSum = ($latestPoints | Where-Object { $null -ne $_.ApproxSize } | Measure-Object -Property ApproxSize -Sum).Sum
            $calculatedOriginalSize = if ($approxSum -and $approxSum -gt 0) { $approxSum } else { $Job.Info.IncludedSize }
        } else {
            $calculatedOriginalSize = $Job.Info.IncludedSize
        }
    } catch {
        $calculatedOriginalSize = $Job.Info.IncludedSize
    }
    $result.NewSourceGB = [Math]::Round($calculatedOriginalSize / 1GB, 2)

    return $result
}

# ---------------------------------------------------------------------------
# Compare, per job
# ---------------------------------------------------------------------------
Write-Host "Comparing old vs. new sizing per job..." -ForegroundColor Cyan
$oldTotalStopwatch = [System.Diagnostics.Stopwatch]::new()
$newTotalStopwatch = [System.Diagnostics.Stopwatch]::new()

$comparison = foreach ($Job in $Jobs) {
    $oldTotalStopwatch.Start()
    $old = Get-OldJobSizing -Job $Job
    $oldTotalStopwatch.Stop()

    $newTotalStopwatch.Start()
    $new = Get-NewJobSizing -Job $Job -RestorePointsByJob $restorePointsByJob
    $newTotalStopwatch.Stop()

    [PSCustomObject]@{
        JobName           = $Job.Name
        JobType           = $Job.TypeToString
        OldRPCount        = $old.OldRestorePointCount
        NewRPCount        = $new.NewRestorePointCount
        OldSourceGB       = $old.OldSourceGB
        NewSourceGB       = $new.NewSourceGB
        OldOnDiskGB       = $old.OldOnDiskGB
        NewOnDiskGB       = $new.NewOnDiskGB
        OldLastBackupNull = $old.OldLastBackupNull
        OldError          = $old.OldError
    }
}

$comparison | Sort-Object JobType, JobName | Format-Table JobName, JobType, OldRPCount, NewRPCount, OldSourceGB, NewSourceGB, OldOnDiskGB, NewOnDiskGB -AutoSize

$oldTotalSeconds       = $oldTotalStopwatch.Elapsed.TotalSeconds
$newLookupTotalSeconds = $newTotalStopwatch.Elapsed.TotalSeconds
$newGrandTotalSeconds  = $sweepStopwatch.Elapsed.TotalSeconds + $newLookupTotalSeconds

Write-Host ""
Write-Host "=== Performance: old vs. new (sweep + lookup) ===" -ForegroundColor Magenta
Write-Host ("Old total (sum of {0} per-job GetLastBackup+Get-VBRRestorePoint calls): {1}s" -f $Jobs.Count, [Math]::Round($oldTotalSeconds, 2))
Write-Host ("New - sweep (one-time, {0} restore points):                              {1}s" -f $allRestorePoints.Count, [Math]::Round($sweepStopwatch.Elapsed.TotalSeconds, 2))
Write-Host ("New - per-job dictionary lookups (sum of {0} jobs):                      {1}s" -f $Jobs.Count, [Math]::Round($newLookupTotalSeconds, 2))
Write-Host ("New total (sweep + lookups):                                             {0}s" -f [Math]::Round($newGrandTotalSeconds, 2))
if ($oldTotalSeconds -gt 0) {
    Write-Host ("New total is {0}x the old total" -f [Math]::Round($newGrandTotalSeconds / $oldTotalSeconds, 2))
}

$fixedRows     = $comparison | Where-Object { $_.OldOnDiskGB -eq 0 -and $_.OldSourceGB -eq 0 -and ($_.NewOnDiskGB -gt 0 -or $_.NewSourceGB -gt 0) }
$regressedRows = $comparison | Where-Object { ($_.OldOnDiskGB -gt 0 -or $_.OldSourceGB -gt 0) -and $_.NewOnDiskGB -eq 0 -and $_.NewSourceGB -eq 0 }
$unchangedRows = $comparison | Where-Object { $_.OldOnDiskGB -eq $_.NewOnDiskGB -and $_.OldSourceGB -eq $_.NewSourceGB }
# Anything not caught above (e.g. Old had real data, New has DIFFERENT real
# data - not 0/0, not equal) - without this bucket these rows silently fall
# through every other category and never get looked at.
$changedRows   = $comparison | Where-Object {
    -not ($_.OldOnDiskGB -eq 0 -and $_.OldSourceGB -eq 0 -and ($_.NewOnDiskGB -gt 0 -or $_.NewSourceGB -gt 0)) -and
    -not (($_.OldOnDiskGB -gt 0 -or $_.OldSourceGB -gt 0) -and $_.NewOnDiskGB -eq 0 -and $_.NewSourceGB -eq 0) -and
    -not ($_.OldOnDiskGB -eq $_.NewOnDiskGB -and $_.OldSourceGB -eq $_.NewSourceGB)
}

Write-Host ""
Write-Host "=== Jobs that went from 0/0 to a real number under the new approach ($($fixedRows.Count)) ===" -ForegroundColor Green
$fixedRows | Format-Table JobName, JobType, OldSourceGB, NewSourceGB, OldOnDiskGB, NewOnDiskGB, OldLastBackupNull, OldError -AutoSize -Wrap

Write-Host "=== Jobs that regressed to 0/0 under the new approach ($($regressedRows.Count)) - investigate! ===" -ForegroundColor Red
$regressedRows | Format-Table JobName, JobType, OldSourceGB, NewSourceGB, OldOnDiskGB, NewOnDiskGB -AutoSize

Write-Host "=== Jobs unchanged between old and new (sanity check) ($($unchangedRows.Count)) ===" -ForegroundColor Cyan
$unchangedRows | Format-Table JobName, JobType, OldSourceGB, NewSourceGB, OldOnDiskGB, NewOnDiskGB -AutoSize

Write-Host "=== Jobs that changed to a DIFFERENT non-zero number ($($changedRows.Count)) - investigate! ===" -ForegroundColor Red
$changedRows | Format-Table JobName, JobType, OldRPCount, NewRPCount, OldSourceGB, NewSourceGB, OldOnDiskGB, NewOnDiskGB -AutoSize

$csvPath = Join-Path -Path $PSScriptRoot -ChildPath "job-sizing-comparison-2way.csv"
$comparison | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host ""
Write-Host "Full comparison exported to $csvPath" -ForegroundColor Yellow
