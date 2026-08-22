#Requires -Version 5.1

function Get-VhcProtectedWorkloads {
    <#
    .Synopsis
        Collects protected and unprotected workloads across VMware, Hyper-V, and physical platforms.

        Protection status is resolved by partitioning the live inventory against the distinct names
        of the most-recent restore points (OIBs) per platform. This replaces the prior
        Find-VBR*Entity -Name <array> lookup, which:
          * over-matched container/template nodes -- reporting MORE "protected" VMs than exist in
            inventory (lab: 112 "protected" vs 104 total objects / 98 real VMs), and
          * under-matched at multi-vCenter scale (customer issue #125: 11 "protected" of 2849).
        The same array lookup also pulled the whole hierarchy tree (Datacenter/Cluster/Host/Folder
        container nodes), not just VMs, which corrupted totals.

        New approach (proven on vbr-v13-rtm.home.lab):
          * VMware  -- inventory = Find-VBRViEntity filtered to Type 'Vm' leaf objects, de-duplicated
                       by VM display name so a VM enumerated in multiple inventory views is counted
                       once. (No stable OIB<->inventory id exists at the VBR API surface; name is the
                       only available correlation, confirmed empirically against the live lab.)
          * Hyper-V -- inventory = Find-VBRHvEntity, partitioned by name membership (symmetric with
                       the existing unprotected logic; only the broken -Name protected lookup changes).
          * Physical-- inventory = Get-VBRDiscoveredComputer. Agent backups expose no OIBs via
                       GetLastOibs(), so protected names fall back to restore-point enumeration.

        Each platform has its own inner try/catch so a failure on one does not skip the others.
        Preserves the GetLastOibs($true) -> GetLastOibs() fallback pattern.
        Exports _PhysProtected.csv, _PhysNotProtected.csv, _HvProtected.csv,
                _HvUnprotected.csv, _ViProtected.csv, _ViUnprotected.csv.
        Source: Get-VBRConfig.ps1 lines 2000-2099.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting protected workloads info..."
    Write-LogFile $message

    $protected               = $null
    $notprotected            = $null
    $protectedHvEntityInfo   = $null
    $unprotectedHvEntityInfo = $null
    $protectedEntityInfo     = $null
    $unprotectedEntityInfo   = $null

    # Returns the distinct, non-empty restore-point (OIB) names for a set of backups, as a
    # case-insensitive HashSet for O(1) membership tests. Uses the documented
    # GetLastOibs($true) -> GetLastOibs() fallback; when both yield nothing (agent/physical
    # backups expose no OIBs this way) it falls back to restore-point enumeration.
    function Get-VhcProtectedNames {
        param($Backups)
        $names = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
        if ($null -eq $Backups) { return , $names }
        $oibs = $null
        try { $oibs = $Backups.GetLastOibs($true) }
        catch {
            try { $oibs = $Backups.GetLastOibs() } catch { $oibs = $null }
        }
        foreach ($o in @($oibs)) {
            if ($null -ne $o -and $o.Name) { [void]$names.Add([string]$o.Name) }
        }
        if ($names.Count -eq 0) {
            foreach ($b in @($Backups)) {
                try {
                    foreach ($rp in @(Get-VBRRestorePoint -Backup $b)) {
                        if ($rp.Name) { [void]$names.Add([string]$rp.Name) }
                    }
                }
                catch {}
            }
        }
        return , $names
    }

    # VMware workloads
    try {
        $vmbackups      = Get-VBRBackup | Where-Object { $_.TypeToString -eq "VMware Backup" }
        $protectedNames = Get-VhcProtectedNames -Backups $vmbackups

        # VM leaf objects only (Type 'Vm') -- excludes Datacenter/Cluster/Host/Folder container
        # nodes that Find-VBRViEntity otherwise returns and that previously corrupted totals.
        # De-duplicate by VM display name so a VM enumerated in multiple inventory views collapses
        # to one workload row (the multi-vCenter duplication behind #125).
        # NOTE: Veeam exposes no stable id linking a restore point (OIB) to an inventory VM -- name
        # is the only available correlation -- so protection status is name-resolved, and the
        # inventory is name-keyed to keep protected/unprotected internally consistent. In the rare
        # case where genuinely distinct VMs share a display name, they count as one workload.
        $viInventory = Find-VBRViEntity |
            Where-Object { "$($_.Type)" -eq 'Vm' } |
            Group-Object { [string]$_.Name } |
            ForEach-Object { $_.Group[0] }

        $protectedEntityInfo   = $viInventory | Where-Object { $protectedNames.Contains([string]$_.Name) }
        $unprotectedEntityInfo = $viInventory | Where-Object { -not $protectedNames.Contains([string]$_.Name) }
    }
    catch {
        Write-LogFile "Failed on VMware workloads: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProtectedWorkloads' -ErrorMessage $_.Exception.Message
    }

    # Hyper-V workloads
    try {
        $hvvmbackups      = Get-VBRBackup | Where-Object { $_.TypeToString -eq "Hyper-V Backup" }
        $hvProtectedNames = Get-VhcProtectedNames -Backups $hvvmbackups

        # Partition a single Find-VBRHvEntity enumeration by name membership -- symmetric protected
        # / unprotected, replacing the broken Find-VBRHvEntity -Name <array> protected lookup.
        $hvInventory = @(Find-VBRHvEntity)

        $protectedHvEntityInfo   = $hvInventory | Where-Object { $hvProtectedNames.Contains([string]$_.Name) }
        $unprotectedHvEntityInfo = $hvInventory | Where-Object { -not $hvProtectedNames.Contains([string]$_.Name) }
    }
    catch {
        Write-LogFile "Failed on Hyper-V workloads: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProtectedWorkloads' -ErrorMessage $_.Exception.Message
    }

    # Physical workloads
    try {
        $phys               = Get-VBRDiscoveredComputer
        $physbackups        = Get-VBRBackup | Where-Object { $_.TypeToString -like "*Agent*" }
        $physProtectedNames = Get-VhcProtectedNames -Backups $physbackups

        $protected    = $phys | Where-Object { $physProtectedNames.Contains([string]$_.Name) }
        $notprotected = $phys | Where-Object { -not $physProtectedNames.Contains([string]$_.Name) }
    }
    catch {
        Write-LogFile "Failed on physical workloads: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProtectedWorkloads' -ErrorMessage $_.Exception.Message
    }

    Write-LogFile "Exporting Protected Workloads files..."
    $protected             | Export-VhciCsv -FileName '_PhysProtected.csv'
    $notprotected          | Export-VhciCsv -FileName '_PhysNotProtected.csv'
    $protectedHvEntityInfo   | Select-Object Name, PowerState, ProvisionedSize, UsedSize, Path |
        Sort-Object PoweredOn, Path, Name | Export-VhciCsv -FileName '_HvProtected.csv'
    $unprotectedHvEntityInfo | Select-Object Name, PowerState, ProvisionedSize, UsedSize, Path, Type |
        Sort-Object Type, PoweredOn, Path, Name | Export-VhciCsv -FileName '_HvUnprotected.csv'
    $protectedEntityInfo     | Select-Object Name, PowerState, ProvisionedSize, UsedSize, Path |
        Sort-Object PoweredOn, Path, Name | Export-VhciCsv -FileName '_ViProtected.csv'
    $unprotectedEntityInfo   | Select-Object Name, PowerState, ProvisionedSize, UsedSize, Path, Type |
        Sort-Object Type, PoweredOn, Path, Name | Export-VhciCsv -FileName '_ViUnprotected.csv'
    Write-LogFile "Exporting Protected Workloads files...OK"
}
