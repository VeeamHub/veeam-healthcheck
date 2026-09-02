#Requires -Version 7.0

# Invoke-VhcvHarness.ps1
# ---------------------------------------------------------------------------
# Orchestrator for the empirical validation harness:
#   baseline snapshot -> (create -> collect -> verify per type) -> cleanup -> reconcile
#
# HARD GATE: this script CREATES and DELETES real VBR objects. It refuses to run
# unless VHC_ALLOW_LIVE_MUTATION=YES_I_HAVE_A_LAB, and it connects to the server
# named in VHC_LAB_VBR (must be a lab, never production).
#
# CHUNK 0.2 STATUS: scaffold only. Guards + snapshot + reconcile are wired and the
# safety gate is enforced, but the per-type job factories are NOT implemented yet
# (they land in Phase 6). With -ScaffoldCheck, this performs a SAFE no-mutation
# round-trip: baseline snapshot -> (no creation) -> reconcile, proving the guard
# pipeline before any factory exists.
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # Safe self-test: snapshot then immediately reconcile with zero objects created.
    # Still requires the live gate (it connects to read inventory) but mutates nothing.
    [switch] $ScaffoldCheck
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
. (Join-Path $here 'VhcvGuards.ps1')
. (Join-Path $here 'VhcvPrereq.ps1')

# --- Safety gate -------------------------------------------------------------
if ($env:VHC_ALLOW_LIVE_MUTATION -ne 'YES_I_HAVE_A_LAB') {
    throw "Live validation harness refused: set VHC_ALLOW_LIVE_MUTATION=YES_I_HAVE_A_LAB to run. This connects to a live VBR and (in full mode) CREATES and DELETES real objects."
}
$labServer = $env:VHC_LAB_VBR
if (-not $labServer) {
    throw 'Set VHC_LAB_VBR to the lab VBR server name (never a production server).'
}

Write-Host "Connecting to lab VBR '$labServer' ..."
Connect-VBRServer -Server $labServer

try {
    Write-Host 'Capturing baseline inventory snapshot (Guard C) ...'
    $script:Baseline = Get-VhcvInventorySnapshot
    $counts = ($script:VhcvReconcileCollections | ForEach-Object { "$_=$($script:Baseline.$_.Count)" }) -join ' '
    Write-Host "Baseline: $counts"

    if ($ScaffoldCheck) {
        Write-Host 'ScaffoldCheck: skipping all creation (0.2 scaffold) - proving guard round-trip only.'
        # No factories run. Cleanup is a no-op (nothing prefixed exists), reconcile must pass.
        Remove-VhcvArtifacts
        [void](Assert-VhcvReconciled)
        Write-Host 'SCAFFOLD CHECK PASSED: baseline -> reconcile round-trip clean.'
        return
    }

    throw 'Job factories not yet implemented (Phase 6). Run with -ScaffoldCheck for the guard round-trip, or wait for the Phase 6 chunk.'
}
finally {
    # Belt-and-suspenders: even on error, attempt prefix-only cleanup + reconcile.
    try { Remove-VhcvArtifacts; [void](Assert-VhcvReconciled) } catch { Write-Warning "Cleanup/reconcile during teardown: $_" }
    Disconnect-VBRServer -ErrorAction SilentlyContinue
}
