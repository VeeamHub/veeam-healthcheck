#Requires -Version 5.1

function Get-VhcSessionReport {
    <#
    .Synopsis
        Generates VeeamSessionReport.csv from the backup sessions passed via -BackupSessions.
        Replaces the standalone Get-VeeamSessionReport.ps1 and Get-VeeamSessionReportVersion13.ps1.

        Receives a mixed array of live session objects from Get-VhcBackupSessions:
        - VM and Backup Copy sessions (CBackupSession / CBackupCopySession)
        - Agent/computer backup sessions (VBRSession)
        Objects must remain in the same process to keep .NET method access.

        Calls Get-VBRTaskSession on each session to resolve task-level detail, producing one
        row per machine (task) rather than one row per job run. See ADR 0004 and ADR 0012.

        For agent task sessions, $task.JobName contains the machine name appended by Veeam.
        The clean job name is taken from the parent $session.Name instead. See ADR 0012.
    .Parameter BackupSessions
        Live Veeam backup session objects returned by Get-VhcBackupSessions. Pass $null or an
        empty array to produce a descriptive error rather than a silent empty CSV.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object[]] $BackupSessions
    )

    if (-not $BackupSessions -or @($BackupSessions).Count -eq 0) {
        throw "No backup sessions available. Ensure Get-VhcBackupSessions completed successfully before calling Get-VhcSessionReport."
    }

    Write-LogFile "Generating session report for $(@($BackupSessions).Count) sessions..."

    # Build a JobId -> canonical-name lookup from the active job lists.
    # When a job is renamed in Veeam, existing sessions retain the historical
    # name. The fast-path query is by job_id, so it returns these alongside
    # current-name sessions and they would otherwise appear as their own row
    # in jobSessionSummary. Looking the JobId up here and overriding the
    # CSV's JobName collapses historical and current sessions onto the same
    # row, letting the C# rollup aggregate them as one job.
    $vbrJobs = @()
    try {
        $vbrJobs = @(Get-VBRJob -ErrorAction SilentlyContinue)
    } catch {
        Write-LogFile "Get-VBRJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    $agentJobs = @()
    try {
        $agentJobs = @(Get-VBRComputerBackupJob -ErrorAction SilentlyContinue)
    } catch {
        Write-LogFile "Get-VBRComputerBackupJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    $epJobs = @()
    try {
        $epJobs = @(Get-VBREPJob -ErrorAction SilentlyContinue)
    } catch {
        Write-LogFile "Get-VBREPJob unavailable: $($_.Exception.Message)" -LogLevel 'WARNING'
    }

    $jobIdMap = @{}
    foreach ($j in $vbrJobs + $agentJobs + $epJobs) {
        if ($null -ne $j.Id -and $null -ne $j.Name) { $jobIdMap[$j.Id] = $j.Name }
    }

    [System.Collections.ArrayList]$allOutput = @()

    # Use Get-VBRTaskSession per session to resolve task-level detail (one row per machine).
    # Replaces the array-level GetTaskSessions() call; Get-VBRTaskSession accepts all session
    # types (VM, Backup Copy, agent) via VBRSessionTransformationAttribute. See ADR 0004, 0012.
    $LogRegex            = [regex]'\bUsing \b.+\s(\[[^\]]*\])'

    foreach ($session in $BackupSessions) {
        $tasks = @()
        try {
            $tasks = @(Get-VBRTaskSession -Session $session)
        } catch {
            Write-LogFile "Failed to get task sessions for '$($session.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
            continue
        }

        foreach ($task in $tasks) {
            try {
                $logRecords = Get-VhciSessionLogWithTimeout -Session $task -TimeoutSeconds 30

                $ProcessingLogMatches = $logRecords | Where-Object Title -match $LogRegex
                $ProcessingLogTitles  = $(($ProcessingLogMatches.Title -replace '\bUsing \b.+\s\[', '') -replace ']', '')
                $ProcessingMode       = $($ProcessingLogTitles | Select-Object -Unique) -join ';'

                $bi = $task.JobSess.Progress.BottleneckInfo
                # Guard on sum > 0: on v12 the Bottleneck enum is always NotDefined (0) even when
                # percentages are populated, so $bi.Bottleneck is not a reliable presence sentinel.
                $biHasData = $bi -and (($bi.Source + $bi.Proxy + $bi.Network + $bi.Target) -gt 0)
                $BottleneckDetails        = if ($biHasData) {
                    "Source $($bi.Source)% > Proxy $($bi.Proxy)% > Network $($bi.Network)% > Target $($bi.Target)%"
                } else { '' }
                $PrimaryBottleneckDetails = if ($biHasData) {
                    $bottleneckStr = "$($bi.Bottleneck)"
                    if ($bottleneckStr -and $bottleneckStr -ne 'NotDefined' -and $bottleneckStr -ne '0') {
                        # v13+: EBottleneck enum resolves to a named component string
                        $bottleneckStr
                    } else {
                        # v12: EBottleneck is NotDefined; derive primary from highest percentage
                        @{ Source = [int]$bi.Source; Proxy = [int]$bi.Proxy; Network = [int]$bi.Network; Target = [int]$bi.Target }.GetEnumerator() |
                            Sort-Object Value -Descending | Select-Object -First 1 | ForEach-Object { $_.Key }
                    }
                } else { '' }

                try { $jobDuration  = $task.JobSess.Progress.Duration.ToString() } catch { $jobDuration  = '' }
                try { $taskDuration = $task.WorkDetails.WorkDuration.ToString() }  catch { $taskDuration = '' }

                # Agent task JobName has the machine name appended by Veeam; use the parent
                # session Name for the clean job name instead. See ADR 0012.
                $jobName = if ($task.ObjectPlatform.IsEpAgentPlatform) { $session.Name } else { $task.JobName }

                # If this session belongs to a currently-active job (by JobId),
                # override with the canonical name. Catches historical-name
                # sessions retained after a Veeam job rename, plus any other
                # case where $task.JobName / $session.Name disagrees with the
                # active job list. Per-machine child sessions have a different
                # JobId from the parent and fall through unchanged so the C#
                # rollup can still detect them as children.
                if ($null -ne $session.JobId -and $jobIdMap.ContainsKey($session.JobId)) {
                    $jobName = $jobIdMap[$session.JobId]
                }

                # Capture parent-link properties exposed by VBR on every session.
                # Children carry the parent's PolicyTag (GUID) and PolicyName,
                # enabling the C# layer to roll up per-machine sessions under the
                # parent without any name-prefix parsing. See ADR 0019.
                $policyName = ''
                $policyTag  = [guid]::Empty
                try { if ($session.Info.PolicyName) { $policyName = $session.Info.PolicyName } } catch {}
                try { if ($session.Info.PolicyTag)  { $policyTag  = $session.Info.PolicyTag  } } catch {}

                # If the PolicyTag points at a currently-active job, canonicalize
                # the PolicyName to the job's current Name (handles renames the
                # same way $jobName is already canonicalized above).
                if ($policyTag -ne [guid]::Empty -and $jobIdMap.ContainsKey($policyTag)) {
                    $policyName = $jobIdMap[$policyTag]
                }

                $row = [pscustomobject][ordered]@{
                    'JobName'           = $jobName
                    'VMName'            = $task.Name
                    'Status'            = $task.Status
                    'IsRetry'           = $task.JobSess.IsRetryMode
                    'ProcessingMode'    = $ProcessingMode
                    'JobDuration'       = $jobDuration
                    'TaskDuration'      = $taskDuration
                    'TaskAlgorithm'     = $task.WorkDetails.TaskAlgorithm
                    'CreationTime'      = $task.JobSess.CreationTime
                    # NAS jobs leave BackupStats at 0; fall back to Progress fields (see ADR 0005)
                    'BackupSizeGB'      = if ($task.JobSess.BackupStats.BackupSize -gt 0) {
                        [math]::Round(($task.JobSess.BackupStats.BackupSize / 1GB), 4)
                    } else {
                        [math]::Round(($task.JobSess.Progress.TransferedSize / 1GB), 4)
                    }
                    'DataSizeGB'        = if ($task.JobSess.BackupStats.DataSize -gt 0) {
                        [math]::Round(($task.JobSess.BackupStats.DataSize / 1GB), 4)
                    } else {
                        [math]::Round(($task.JobSess.Progress.ReadSize / 1GB), 4)
                    }
                    'DedupRatio'        = $task.JobSess.BackupStats.DedupRatio
                    'CompressRatio'     = $task.JobSess.BackupStats.CompressRatio
                    'BottleneckDetails' = $BottleneckDetails
                    'PrimaryBottleneck' = $PrimaryBottleneckDetails
                    'JobType'           = $task.ObjectPlatform.Platform
                    'JobAlgorithm'      = $task.JobSess.Info.SessionAlgorithm
                    'JobId'             = if ($session.JobId) { "$($session.JobId)" } else { '' }
                    'PolicyName'        = $policyName
                    'PolicyTag'         = if ($policyTag -ne [guid]::Empty) { "$policyTag" } else { '' }
                }
                if ($row) { $null = $allOutput.Add($row) }
            } catch {
                Write-LogFile "Failed to process task '$($task.Name)' in job '$($session.Name)': $($_.Exception.Message)" -LogLevel "WARNING"
            }
        }
    }

    $csvPath = Join-Path -Path $script:ReportPath -ChildPath "VeeamSessionReport.csv"
    if ($allOutput.Count -gt 0) {
        $allOutput | Protect-VhciCsvInjection | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
        Write-LogFile "Exported $($allOutput.Count) session rows to $csvPath"
    } else {
        Write-LogFile "No session rows produced - writing header-only CSV" -LogLevel "WARNING"
        # Export-Csv with zero objects writes nothing; write header line explicitly instead.
        $headerLine = ([pscustomobject][ordered]@{
            'JobName'=''; 'VMName'=''; 'Status'=''; 'IsRetry'=''; 'ProcessingMode'='';
            'JobDuration'=''; 'TaskDuration'=''; 'TaskAlgorithm'=''; 'CreationTime'='';
            'BackupSizeGB'=''; 'DataSizeGB'=''; 'DedupRatio'=''; 'CompressRatio'='';
            'BottleneckDetails'=''; 'PrimaryBottleneck'=''; 'JobType'=''; 'JobAlgorithm'='';
            'JobId'=''; 'PolicyName'=''; 'PolicyTag'=''
        } | ConvertTo-Csv -NoTypeInformation | Select-Object -First 1)
        Out-File -FilePath $csvPath -InputObject $headerLine -Encoding UTF8
    }

}
