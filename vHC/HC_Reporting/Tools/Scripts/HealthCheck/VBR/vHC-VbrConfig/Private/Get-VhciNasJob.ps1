#Requires -Version 5.1

function Get-VhciNasJob {
    <#
    .Synopsis
        Collects NAS backup jobs (with restore-point-based size metrics) and NAS backup copy jobs.
        Exports _nasBackup.csv, _nasBCJ.csv.
        Size metrics use Get-VBRUnstructuredBackupRestorePoint + CNasBackup/.GetNasBackupShortTerm()
        rather than session Progress fields, which are unreliable when the latest session failed.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting NAS jobs..."
    Write-LogFile $message

    $nasBackup = $null
    $nasBCJ    = $null

    Write-LogFile "Calling Get-VBRUnstructuredBackupJob..."
    $nasBackup = Get-VBRUnstructuredBackupJob
    Write-LogFile "Found $(@($nasBackup).Count) NAS backup jobs"

    if (@($nasBackup).Count -gt 0) {

        # Get-VBRUnstructuredBackupRestorePoint requires VBRUnstructuredBackup objects,
        # not the VBRNASBackupJob objects returned by Get-VBRUnstructuredBackupJob.
        Write-LogFile "Calling Get-VBRUnstructuredBackup for restore-point size metrics..."
        $unstructuredBackups = @(Get-VBRUnstructuredBackup)
        Write-LogFile "Found $(@($unstructuredBackups).Count) unstructured backup chain(s)"

        $jobCounter = 0
        foreach ($job in $unstructuredBackups) {
            $jobCounter++
            Write-LogFile "Processing NAS job $jobCounter/$(@($unstructuredBackups).Count): $($job.Name)"

            $onDiskGB = 0
            $sourceGB = 0

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
                        Write-LogFile "    Warning: size fetch failed for share '$shareName' in '$($job.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
                    }
                }

                Write-LogFile "  Total - OnDiskGB: $onDiskGB, SourceGB: $sourceGB"
            } catch {
                Write-LogFile "  Warning: Failed to get restore points for job '$($job.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
            }

            $nasJob = @($nasBackup) | Where-Object { $_.Name -eq $job.Name } | Select-Object -First 1
            if ($null -ne $nasJob) {
                $nasJob | Add-Member -MemberType NoteProperty -Name JobType  -Value "NAS Backup" -Force
                $nasJob | Add-Member -MemberType NoteProperty -Name OnDiskGB -Value $onDiskGB    -Force
                $nasJob | Add-Member -MemberType NoteProperty -Name SourceGB -Value $sourceGB    -Force
            }
        }
    }

    $nasBCJ = Get-VBRNASBackupCopyJob
    Write-LogFile "Found $(@($nasBCJ).Count) NAS backup copy jobs"

    $nasBackup | Export-VhciCsv -FileName '_nasBackup.csv'
    $nasBCJ    | Export-VhciCsv -FileName '_nasBCJ.csv'

    Write-LogFile ($message + "DONE")
}
