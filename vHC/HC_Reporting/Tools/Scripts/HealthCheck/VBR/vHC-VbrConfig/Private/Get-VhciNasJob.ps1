#Requires -Version 5.1

function Get-VhciNasJob {
    <#
    .Synopsis
        Collects NAS backup jobs (with restore-point-based size metrics) and NAS backup copy jobs.
        Exports _nasBackup.csv, _nasBCJ.csv.
        Size metrics use Get-VBRUnstructuredBackupRestorePoint + CNasBackup/.GetNasBackupShortTerm()
        rather than session Progress fields, which are unreliable when the latest session failed.

        A job, backup or share that references a repository which no longer exists is skipped
        or left with blank sizes, logged as a WARNING and recorded for the report; every other
        object is still collected and both CSVs are still written. Any other failure is
        rethrown to the caller as before.
    .Parameter Jobs
        Jobs found by job discovery (Get-VBRJob does not resolve repositories, so it still
        lists a NAS job whose repository is gone). Only used when Veeam aborts a NAS job
        listing at a broken object: the names of the NAS jobs in it are then read one by one,
        so just the broken ones are lost.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [object[]]$Jobs = @()
    )

    $message = "Collecting NAS jobs..."
    Write-LogFile $message

    $nasBackup = $null
    $nasBCJ    = $null

    $nasBackupNames = @(@($Jobs) | Where-Object { $null -ne $_ -and [string]$_.JobType -in @('NasBackup', 'ENasBackup') } | ForEach-Object { [string]$_.Name })
    $nasCopyNames   = @(@($Jobs) | Where-Object { $null -ne $_ -and [string]$_.JobType -eq 'NasBackupCopy' } | ForEach-Object { [string]$_.Name })

    Write-LogFile "Calling Get-VBRUnstructuredBackupJob..."
    $nasBackup = @(Invoke-VhciTolerantEnumeration -ObjectType 'NAS backup job' -Collector 'NAS' `
        -Command { Get-VBRUnstructuredBackupJob -ErrorAction Continue } `
        -PerObjectCommand { param($Name) Get-VBRUnstructuredBackupJob -Name $Name } `
        -ObjectNames $nasBackupNames)
    Write-LogFile "Found $(@($nasBackup).Count) NAS backup jobs"

    if (@($nasBackup).Count -gt 0) {

        # Get-VBRUnstructuredBackupRestorePoint requires VBRUnstructuredBackup objects,
        # not the VBRNASBackupJob objects returned by Get-VBRUnstructuredBackupJob.
        Write-LogFile "Calling Get-VBRUnstructuredBackup for restore-point size metrics..."
        $unstructuredBackups = @(Invoke-VhciTolerantEnumeration -ObjectType 'NAS backup' -Collector 'NAS' `
            -Command { Get-VBRUnstructuredBackup -ErrorAction Continue })
        Write-LogFile "Found $(@($unstructuredBackups).Count) unstructured backup chain(s)"

        $jobCounter = 0
        foreach ($job in $unstructuredBackups) {
            $jobCounter++
            Write-LogFile "Processing NAS job $jobCounter/$(@($unstructuredBackups).Count): $($job.Name)"

            $onDiskGB        = 0
            $sourceGB        = 0
            $sizesUnresolved = $false

            try {
                $points = Get-VBRUnstructuredBackupRestorePoint -Backup $job

                # Keep first-seen per ServerName - API returns newest-first, so first = latest.
                $shareLatest = @{}
                foreach ($point in @($points)) {
                    if (-not $shareLatest.ContainsKey($point.ServerName)) {
                        $shareLatest[$point.ServerName] = $point
                    }
                }
                Write-LogFile "  $($shareLatest.Count) unique share(s)"

                foreach ($shareName in $shareLatest.Keys) {
                    try {
                        $point      = $shareLatest[$shareName]
                        $backupInfo = [Veeam.Backup.Core.CNasBackup]::GetByNasPointId($point.Id)
                        $pointInfo  = [Veeam.Backup.Core.CNasBackupPoint]::Get($point.Id)
                        $shortTerm  = $backupInfo.GetNasBackupShortTerm()

                        $shareOnDisk = [Math]::Round(($shortTerm.Info.DataSize + $shortTerm.Info.MetaSize) / 1GB, 2)
                        $shareSource = [Math]::Round($pointInfo.Info.ProtectedSize / 1GB, 2)

                        $onDiskGB += $shareOnDisk
                        $sourceGB += $shareSource
                        Write-LogFile "    Share '$shareName': SourceGB=$shareSource, OnDiskGB=$shareOnDisk"
                    } catch {
                        $shareRepoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
                        if ($shareRepoId) {
                            Register-VhciBrokenRepositoryReference -ObjectType 'NAS share' -ObjectName "$($job.Name) / $shareName" -RepositoryId $shareRepoId -Collector 'NAS' `
                                -Consequence 'Its size is left out of the job total; the other shares are included.'
                        } else {
                            Write-LogFile "    Warning: size fetch failed for share '$shareName' in '$($job.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
                        }
                    }
                }

                Write-LogFile "  Total - OnDiskGB: $onDiskGB, SourceGB: $sourceGB"
            } catch {
                $jobRepoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
                if ($jobRepoId) {
                    # The size is unknown, not zero: leave it blank rather than report 0 GB.
                    $sizesUnresolved = $true
                    Register-VhciBrokenRepositoryReference -ObjectType 'NAS backup' -ObjectName $job.Name -RepositoryId $jobRepoId -Collector 'NAS' `
                        -Consequence 'Its restore points cannot be read, so its size is left blank; the job is still listed.'
                } else {
                    Write-LogFile "  Warning: Failed to get restore points for job '$($job.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
                }
            }

            $nasJob = @($nasBackup) | Where-Object { $_.Name -eq $job.Name } | Select-Object -First 1
            if ($null -ne $nasJob) {
                $nasJob | Add-Member -MemberType NoteProperty -Name JobType  -Value "NAS Backup" -Force
                if ($sizesUnresolved) {
                    $nasJob | Add-Member -MemberType NoteProperty -Name OnDiskGB -Value $null -Force
                    $nasJob | Add-Member -MemberType NoteProperty -Name SourceGB -Value $null -Force
                } else {
                    $nasJob | Add-Member -MemberType NoteProperty -Name OnDiskGB -Value $onDiskGB -Force
                    $nasJob | Add-Member -MemberType NoteProperty -Name SourceGB -Value $sourceGB -Force
                }
            }
        }
    }

    $nasBCJ = @(Invoke-VhciTolerantEnumeration -ObjectType 'NAS backup copy job' -Collector 'NAS' `
        -Command { Get-VBRNASBackupCopyJob -ErrorAction Continue } `
        -PerObjectCommand { param($Name) Get-VBRNASBackupCopyJob -Name $Name } `
        -ObjectNames $nasCopyNames)
    Write-LogFile "Found $(@($nasBCJ).Count) NAS backup copy jobs"

    $nasBackup | Export-VhciCsv -FileName '_nasBackup.csv'
    $nasBCJ    | Export-VhciCsv -FileName '_nasBCJ.csv'

    Write-LogFile ($message + "DONE")
}
