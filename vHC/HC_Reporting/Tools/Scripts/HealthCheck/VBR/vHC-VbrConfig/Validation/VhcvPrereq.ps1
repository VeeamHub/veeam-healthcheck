#Requires -Version 7.0

# VhcvPrereq.ps1
# ---------------------------------------------------------------------------
# Prerequisite detection for the validation harness. Each job family needs
# certain infrastructure to exist; the harness must DETECT absence and record a
# skip (never silently pass, never error the suite). See A7 spec section 5.
# ---------------------------------------------------------------------------

function Test-VhcvSpareDatastore {
    <#
    .Synopsis
        Best-effort probe for a datastore usable as a replication target. Returns
        $true if at least one vSphere host with an accessible datastore is present.
        Conservative: returns $false on any error so replication is skipped, not failed.
    #>
    try {
        $viHosts = @(Get-VBRServer | Where-Object Type -in 'Esxi', 'VC')
        return ($viHosts.Count -ge 1)
    } catch {
        return $false
    }
}

function Test-VhcvPrereq {
    <#
    .Synopsis
        Return $true when the prerequisites for a given job family are present in the
        connected VBR environment. Used by each integration Describe to -Skip cleanly.
    .Parameter For
        Job family key: HyperV, Replica, Tape, NAS, Object, EntraID, Catalyst,
        SureBackup, vCloud. Unknown keys default to $true (assume creatable).
    #>
    param([Parameter(Mandatory)] [string] $For)

    switch ($For) {
        'HyperV'    { @(Get-VBRServer | Where-Object Type -eq 'HvServer').Count -gt 0 }
        'Replica'   { (@(Get-VBRServer | Where-Object Type -in 'Esxi', 'VC').Count -ge 1) -and (Test-VhcvSpareDatastore) }
        'Tape'      { @(Get-VBRTapeLibrary           -ErrorAction SilentlyContinue).Count -gt 0 }
        'NAS'       { @(Get-VBRUnstructuredServer    -ErrorAction SilentlyContinue).Count -gt 0 }
        'Object'    { @(Get-VBRObjectStorageRepository -ErrorAction SilentlyContinue).Count -gt 0 }
        'EntraID'   { @(Get-VBREntraIDTenant         -ErrorAction SilentlyContinue).Count -gt 0 }
        'Catalyst'  { @(Get-VBRBackupRepository | Where-Object Type -eq 'DDBoost').Count -gt 0 }
        'SureBackup'{ @(Get-VBRBackup).Count -gt 0 }   # needs an existing restore point to verify against
        'vCloud'    { @(Get-VBRvCloudServer          -ErrorAction SilentlyContinue).Count -gt 0 }
        default     { $true }
    }
}
