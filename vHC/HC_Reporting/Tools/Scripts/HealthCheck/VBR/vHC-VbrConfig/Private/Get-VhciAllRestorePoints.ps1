#Requires -Version 5.1

function Get-VhciAllRestorePoints {
    <#
    .Synopsis
        Lists every restore point on the server for Get-VhcJob's global sweep, so that one
        backup Veeam cannot read does not cost the sweep every other backup.
    .Description
        The unscoped Get-VBRRestorePoint call is the fast path. It fails as a whole when any
        single backup is unreadable: a backup whose repository was deleted ("Unable to find
        repository id <guid>"), or one that is encrypted or created by an enterprise
        application plug-in ("Cannot get restore points from backup <name>, ..."). On any
        such failure the restore points are read backup by backup instead. A backup that
        still cannot be read is skipped with a WARNING naming it and the reason; a missing
        repository is also recorded for the report. Restore points are de-duplicated by Id
        because a parent backup can list its children's points too.

        If there are no backups to fall back to, the backups themselves cannot be listed,
        or not one backup can be read, the original error is rethrown, so a real failure (the VBR server is
        unreachable, say) still fails the sweep and is logged as ERROR by Get-VhcJob.
    .Outputs
        [object[]] the restore points that could be read.
    #>
    [CmdletBinding()]
    param()

    $firstError = $null
    try {
        return @(Get-VBRRestorePoint -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })
    } catch {
        $firstError = $_
    }

    $backups = @(Get-VBRBackup -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })
    if ($backups.Count -eq 0) { throw $firstError }

    $repoId = Get-VhciMissingRepositoryId -Message $firstError.Exception.Message
    if ($repoId) {
        Write-LogFile "Restore point sweep: a backup references repository $repoId, which no longer exists, so Veeam cannot list all restore points at once. Reading them backup by backup instead." -LogLevel "WARNING"
    } else {
        Write-LogFile "Restore point sweep: Veeam cannot list all restore points at once ($($firstError.Exception.Message)). Reading them backup by backup instead." -LogLevel "WARNING"
    }

    $seen   = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $points = [System.Collections.Generic.List[object]]::new()
    $backupsRead = 0

    foreach ($backup in $backups) {
        try {
            foreach ($point in @(Get-VBRRestorePoint -Backup $backup -WarningAction SilentlyContinue)) {
                if ($null -eq $point) { continue }
                $key = $null
                try { $key = [string]$point.Id } catch { $key = $null }
                if ($key -and -not $seen.Add($key)) { continue }
                $points.Add($point)
            }
            $backupsRead++
        } catch {
            $backupRepoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
            if ($backupRepoId) {
                Register-VhciBrokenRepositoryReference -ObjectType 'Backup' -ObjectName ([string]$backup.Name) -RepositoryId $backupRepoId -Collector 'Jobs' `
                    -Consequence 'Its restore points are left out of job sizing and the orphaned/superseded check; every other backup is included.'
            } else {
                Write-LogFile "Restore point sweep: backup '$([string]$backup.Name)' was skipped, its restore points could not be read: $($_.Exception.Message)" -LogLevel "WARNING"
            }
        }
    }

    # Reading no backup at all recovered nothing; an empty sweep would report a clean
    # server and skip per-job sizing, so fail the sweep as the bulk call did.
    if ($backupsRead -eq 0) { throw $firstError }

    return $points.ToArray()
}
