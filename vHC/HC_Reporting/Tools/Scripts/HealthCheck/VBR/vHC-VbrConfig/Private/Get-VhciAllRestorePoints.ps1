#Requires -Version 5.1

function Get-VhciAllRestorePoints {
    <#
    .Synopsis
        Lists every restore point on the server for Get-VhcJob's global sweep, tolerating
        backups that reference a repository which no longer exists.
    .Description
        The unscoped Get-VBRRestorePoint call is the fast path. When it fails with
        "Unable to find repository id <guid>" (one backup's repository was deleted), the
        restore points are read backup by backup instead, so only the broken backup is lost:
        it is logged as a WARNING and recorded for the report. Restore points are
        de-duplicated by Id because a parent backup can list its children's points too.
        Any other error is rethrown, so the sweep still fails (and is logged as ERROR by
        Get-VhcJob) on a real failure.
    .Outputs
        [object[]] the restore points that could be read.
    #>
    [CmdletBinding()]
    param()

    try {
        return @(Get-VBRRestorePoint -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })
    } catch {
        $repoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
        if (-not $repoId) { throw }
        Write-LogFile "Restore point sweep: a backup references repository $repoId, which no longer exists, so Veeam cannot list all restore points at once. Reading them backup by backup instead." -LogLevel "WARNING"
    }

    $seen   = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $points = [System.Collections.Generic.List[object]]::new()

    foreach ($backup in @(Get-VBRBackup -WarningAction SilentlyContinue | Where-Object { $null -ne $_ })) {
        try {
            foreach ($point in @(Get-VBRRestorePoint -Backup $backup -WarningAction SilentlyContinue)) {
                if ($null -eq $point) { continue }
                $key = $null
                try { $key = [string]$point.Id } catch { $key = $null }
                if ($key -and -not $seen.Add($key)) { continue }
                $points.Add($point)
            }
        } catch {
            $backupRepoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
            if (-not $backupRepoId) { throw }
            Register-VhciBrokenRepositoryReference -ObjectType 'Backup' -ObjectName ([string]$backup.Name) -RepositoryId $backupRepoId -Collector 'Jobs' `
                -Consequence 'Its restore points are left out of job sizing and the orphaned/superseded check; every other backup is included.'
        }
    }

    return $points.ToArray()
}
