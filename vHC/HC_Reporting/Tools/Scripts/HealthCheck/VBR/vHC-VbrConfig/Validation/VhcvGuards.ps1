#Requires -Version 7.0

# VhcvGuards.ps1
# ---------------------------------------------------------------------------
# Reversibility guards for the empirical validation harness (Question 5).
# These are the SAFETY CONTRACT that lets the harness create real VBR objects
# without risk to a user's environment:
#
#   Guard A  New-VhcvName            - every created object carries the hard prefix.
#   Guard B  New-VhcvJob             - every job is created DISABLED (never runs).
#   Guard C  Get-VhcvInventorySnapshot - capture baseline before anything is created.
#   Guard D  Remove-VhcvArtifacts    - cleanup deletes ONLY the prefix.
#            Assert-VhcvReconciled    - inventory must equal baseline afterwards.
#
# Scope of THIS chunk (0.2): guards + snapshot + reconcile only. No job factories,
# no actual creation. The orchestrator (Invoke-VhcvHarness.ps1) wires these together
# but refuses to run real creation until a later chunk + explicit opt-in.
# ---------------------------------------------------------------------------

$script:VhcvPrefix = 'vHC-VALIDATE-'

# Collections reconciled between baseline and post-run. Single source of truth so
# snapshot and reconcile can never drift out of sync.
$script:VhcvReconcileCollections = @(
    'Jobs', 'TapeJobs', 'NasJobs', 'SureBackup', 'Labs', 'AppGroups', 'Repos'
)

# Ledger of everything this run created (populated by New-VhcvJob / factories).
$script:Created = [System.Collections.ArrayList]::new()

# --- Guard A -----------------------------------------------------------------
function New-VhcvName {
    <#
    .Synopsis
        Produce a uniquely-suffixed, hard-prefixed object name. NOTHING in the
        harness is created without a name from here - that prefix is what every
        cleanup filter keys on, so it is the linchpin of reversibility.
    #>
    param(
        [Parameter(Mandatory)] [string] $Type,
        [string] $Variant = 'base'
    )
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 6)
    return "$($script:VhcvPrefix)$Type-$Variant-$suffix"
}

# --- Guard B -----------------------------------------------------------------
function New-VhcvJob {
    <#
    .Synopsis
        Run a job-creation scriptblock, then immediately disable the job and verify
        the disable took. Throws (aborting the run) if the job is still enabled.
        Records the job in the $script:Created ledger.
    .Parameter Creator
        Scriptblock that returns the created job object, e.g.
        { Add-VBRViBackupJob -Name $n -Entity $vm -BackupRepository $repo }
    #>
    param([Parameter(Mandatory)] [scriptblock] $Creator)

    $job = & $Creator
    if (-not $job) { throw 'GUARD B FAIL: creator returned no job object.' }

    Disable-VBRJob -Job $job | Out-Null
    $fresh = Get-VBRJob -Name $job.Name
    if ($fresh.IsScheduleEnabled) {
        throw "GUARD B FAIL: $($job.Name) is still enabled after Disable-VBRJob - aborting."
    }
    [void]$script:Created.Add([pscustomobject]@{ Id = $job.Id; Name = $job.Name; Kind = 'Job' })
    return $job
}

# --- Guard C -----------------------------------------------------------------
function Get-VhcvInventorySnapshot {
    <#
    .Synopsis
        Capture the current VBR object inventory across every collection the harness
        can touch. Taken once as baseline, and again after cleanup for reconciliation.
        Uses Id+Name only - cheap and stable.
    #>
    [pscustomobject]@{
        Jobs       = @(Get-VBRJob                                      | Select-Object Id, Name)
        TapeJobs   = @(Get-VBRTapeJob          -ErrorAction SilentlyContinue | Select-Object Id, Name)
        NasJobs    = @(Get-VBRNASBackupJob     -ErrorAction SilentlyContinue | Select-Object Id, Name)
        SureBackup = @(Get-VBRSureBackupJob    -ErrorAction SilentlyContinue | Select-Object Id, Name)
        Labs       = @(Get-VBRVirtualLab       -ErrorAction SilentlyContinue | Select-Object Id, Name)
        AppGroups  = @(Get-VBRApplicationGroup -ErrorAction SilentlyContinue | Select-Object Id, Name)
        Repos      = @(Get-VBRBackupRepository                          | Select-Object Id, Name)
    }
}

# --- Guard D (cleanup) -------------------------------------------------------
function Remove-VhcvArtifacts {
    <#
    .Synopsis
        Delete every harness-created object, in dependency order. CRITICAL: every
        Remove-* is filtered by the hard prefix, so only harness objects can ever
        be deleted - a user's objects are structurally untouchable here.
    #>
    $p = "$($script:VhcvPrefix)*"
    Get-VBRJob              | Where-Object Name -like $p | ForEach-Object { Remove-VBRJob              -Job $_ -Confirm:$false }
    Get-VBRTapeJob          -ErrorAction SilentlyContinue | Where-Object Name -like $p | ForEach-Object { Remove-VBRTapeJob          -Job $_ -Confirm:$false }
    Get-VBRNASBackupJob     -ErrorAction SilentlyContinue | Where-Object Name -like $p | ForEach-Object { Remove-VBRNASBackupJob     -Job $_ -Confirm:$false }
    Get-VBRSureBackupJob    -ErrorAction SilentlyContinue | Where-Object Name -like $p | ForEach-Object { Remove-VBRSureBackupJob    -Job $_ -Confirm:$false }
    Get-VBRVirtualLab       -ErrorAction SilentlyContinue | Where-Object Name -like $p | ForEach-Object { Remove-VBRVirtualLab       -VirtualLab $_ -Confirm:$false }
    Get-VBRApplicationGroup -ErrorAction SilentlyContinue | Where-Object Name -like $p | ForEach-Object { Remove-VBRApplicationGroup -ApplicationGroup $_ -Confirm:$false }
    Get-VBRBackup           | Where-Object Name -like $p | ForEach-Object { Remove-VBRBackup           -Backup $_ -FromDisk -Confirm:$false }
    Get-VBRBackupRepository | Where-Object Name -like $p | ForEach-Object { Remove-VBRBackupRepository -Repository $_ -Confirm:$false }
}

# --- Guard D (reconcile) -----------------------------------------------------
function Assert-VhcvReconciled {
    <#
    .Synopsis
        Assert that, after cleanup, (1) no prefixed object survives, and (2) every
        baseline object is still present. Either failure is a hard error demanding
        manual inspection. Parameters are injectable so the logic is unit-testable
        without a live VBR; they default to the script-scope baseline/live snapshot.
    .Parameter Baseline
        The pre-run snapshot (defaults to $script:Baseline).
    .Parameter Current
        The post-cleanup snapshot (defaults to a fresh Get-VhcvInventorySnapshot).
    #>
    param(
        [object] $Baseline = $script:Baseline,
        [object] $Current
    )
    if (-not $Baseline) { throw 'Assert-VhcvReconciled: no baseline snapshot available.' }
    if (-not $Current)  { $Current = Get-VhcvInventorySnapshot }

    foreach ($coll in $script:VhcvReconcileCollections) {
        # 1. nothing with our prefix may survive cleanup
        $leaked = @($Current.$coll | Where-Object Name -like "$($script:VhcvPrefix)*")
        if ($leaked) { throw "RECONCILE FAIL: leaked $coll - $($leaked.Name -join ', ')" }

        # 2. every baseline object must still exist (we deleted nothing of the user's)
        $currentIds = @($Current.$coll.Id)
        $missing    = @($Baseline.$coll | Where-Object { $_.Id -notin $currentIds })
        if ($missing) { throw "RECONCILE FAIL: baseline $coll missing - $($missing.Name -join ', ')" }
    }
    Write-Host 'RECONCILE OK: inventory == baseline, zero vHC-VALIDATE residue.'
    return $true
}
