#Requires -Version 5.1

function Get-VhcLocation {
    <#
    .Synopsis
        Collects the geographic Locations defined in VBR and the infrastructure objects
        assigned to each, and exports them to _Locations.csv.
    .Description
        VBR Locations (Get-VBRLocation) are named sites an admin groups repositories and
        hypervisor hosts into. A VBRLocation carries only Name and Id, so the useful
        part is the assignment: Get-VBRLocation -Object <component> returns the location
        a component belongs to.

        One row per (location, assigned object). A location with nothing assigned gets
        one row with the object columns blank, so every defined location is reported.
        No row is written when VBR has no locations (Export-VhciCsv skips empty input).

        Columns: Location, LocationId, ObjectType, ObjectName, ObjectId

        Read-only. Objects the cmdlet rejects ("Type ... is not supported", or "does not
        have any location assigned") are skipped quietly: that is the normal answer for
        most components, not an error. Get-VBRLocation is VBR 12.1+; on older builds the
        collector records one module error and exports nothing.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting location info..."
    Write-LogFile $message

    try {
        if (-not (Get-Command -Name Get-VBRLocation -ErrorAction SilentlyContinue)) {
            Write-LogFile ($message + "SKIPPED (Get-VBRLocation not available on this VBR version)") -LogLevel "WARNING"
            return
        }

        $locations = @(Get-VBRLocation -ErrorAction Stop)
        if ($locations.Count -eq 0) {
            Write-LogFile ($message + "DONE (no locations defined)")
            return
        }

        # Objects a location can be assigned to. Each lookup is isolated: a failure on
        # one object kind (or one object) never costs the others.
        $candidates = [System.Collections.Generic.List[object]]::new()
        $sources = [ordered]@{
            'Repository'  = { Get-VBRBackupRepository }
            'ObjectStore' = { Get-VBRObjectStorageRepository }
            'Server'      = { Get-VBRServer }
            'ViProxy'     = { Get-VBRViProxy }
            'HvProxy'     = { Get-VBRHvProxy }
        }
        foreach ($kind in $sources.Keys) {
            try {
                foreach ($o in @(& $sources[$kind])) {
                    if ($null -ne $o) { $candidates.Add([pscustomobject]@{ Kind = $kind; Object = $o }) }
                }
            } catch {
                Write-LogFile "Location lookup: could not list $kind objects: $($_.Exception.Message)" -LogLevel "WARNING"
            }
        }

        $assigned = @{}   # location Id -> list of rows
        foreach ($c in $candidates) {
            try {
                $loc = @(Get-VBRLocation -Object $c.Object -ErrorAction Stop) | Select-Object -First 1
            } catch {
                continue   # unsupported type / no location assigned
            }
            if ($null -eq $loc) { continue }
            $key = "$($loc.Id)"
            if (-not $assigned.ContainsKey($key)) { $assigned[$key] = [System.Collections.Generic.List[object]]::new() }
            $assigned[$key].Add([pscustomobject]@{
                    ObjectType = $c.Kind
                    ObjectName = "$($c.Object.Name)"
                    ObjectId   = "$($c.Object.Id)"
                })
        }

        $rows = foreach ($l in $locations) {
            $objs = $assigned["$($l.Id)"]
            if ($objs -and $objs.Count -gt 0) {
                foreach ($o in $objs) {
                    [pscustomobject]@{
                        Location   = "$($l.Name)"
                        LocationId = "$($l.Id)"
                        ObjectType = $o.ObjectType
                        ObjectName = $o.ObjectName
                        ObjectId   = $o.ObjectId
                    }
                }
            } else {
                [pscustomobject]@{
                    Location   = "$($l.Name)"
                    LocationId = "$($l.Id)"
                    ObjectType = ''
                    ObjectName = ''
                    ObjectId   = ''
                }
            }
        }

        Write-LogFile ($message + "DONE")
        $rows | Export-VhciCsv -FileName '_Locations.csv'
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'Location' -ErrorMessage $_.Exception.Message
    }
}
