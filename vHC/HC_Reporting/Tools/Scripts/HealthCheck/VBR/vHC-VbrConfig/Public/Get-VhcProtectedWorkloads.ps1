#Requires -Version 5.1

function Get-VhcProtectedWorkloads {
    <#
    .Synopsis
        Collects protected and unprotected workloads across VMware, Hyper-V, and physical platforms.
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

    $protected             = $null
    $notprotected          = $null
    $protectedHvEntityInfo = $null
    $unprotectedHvEntityInfo = $null
    $protectedEntityInfo   = $null
    $unprotectedEntityInfo = $null

    # VMware workloads
    try {
        $vmbackups = Get-VBRBackup | Where-Object { $_.TypeToString -eq "VMware Backup" }

        try {
            $vmNames = $vmbackups.GetLastOibs($true)
        }
        catch {
            try {
                $vmNames = $vmbackups.GetLastOibs()
            }
            catch {}
        }

        $unprotectedEntityInfo = Find-VBRViEntity | Where-Object { $_.Name -notin $vmNames.Name }
        if ($null -eq $vmNames -or $null -eq $vmNames.Name) {
            $protectedEntityInfo = Find-VBRViEntity -Name " "
        }
        else {
            $protectedEntityInfo = Find-VBRViEntity -Name $vmNames.Name
        }
    }
    catch {
        Write-LogFile "Failed on VMware workloads: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProtectedWorkloads' -ErrorMessage $_.Exception.Message
    }

    # Hyper-V workloads
    try {
        $hvvmbackups = Get-VBRBackup | Where-Object { $_.TypeToString -eq "Hyper-v Backup" }

        try {
            $hvvmNames = $hvvmbackups.GetLastOibs($true)
        }
        catch {
            try {
                $hvvmNames = $hvvmbackups.GetLastOibs()
            }
            catch {}
        }

        $unprotectedHvEntityInfo = Find-VBRHvEntity | Where-Object { $_.Name -notin $hvvmNames.Name }
        if ($null -eq $hvvmNames.Name) {
            $protectedHvEntityInfo = Find-VBRHvEntity -Name " "
        }
        else {
            $protectedHvEntityInfo = Find-VBRHvEntity -Name $hvvmNames.Name
        }
    }
    catch {
        Write-LogFile "Failed on Hyper-V workloads: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ProtectedWorkloads' -ErrorMessage $_.Exception.Message
    }

    # Physical workloads
    try {
        $phys = Get-VBRDiscoveredComputer

        $physbackups = Get-VBRBackup | Where-Object { $_.TypeToString -like "*Agent*" }

        try {
            $pvmNames = $physbackups.GetLastOibs($true)
        }
        catch {
            try {
                $pvmNames = $physbackups.GetLastOibs()
            }
            catch {}
        }

        $notprotected = $phys | Where-Object { $_.Name -notin $pvmNames.Name }
        $protected    = $phys | Where-Object { $_.Name -in $pvmNames.Name }
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
