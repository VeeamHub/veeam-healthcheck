#Requires -Version 5.1

function Get-VhcBackupSessions {
    <#
    .Synopsis
        Fetches VBR backup sessions created within the reporting window and returns them
        as pipeline output. The caller (orchestrator) captures the output and passes it
        explicitly to Get-VhcSessionReport via the -BackupSessions parameter.

        Returns a mixed array of two session object types:
        - VM and Backup Copy sessions (CBackupSession / CBackupCopySession) via
          Get-VBRBackupSession.
        - Agent/computer backup sessions (VBRSession) via Get-VBRComputerBackupJobSession.
        Both types are accepted by Get-VBRTaskSession, which Get-VhcSessionReport uses
        to resolve task-level detail. See ADR 0012.
    .Parameter ReportInterval
        Number of days back to collect sessions for. Matches the -ReportInterval parameter
        passed to Get-VBRConfig.ps1.
    .Outputs
        [object[]] -- Mixed array of Veeam backup session objects.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [int] $ReportInterval
    )

    Write-LogFile "Fetching backup sessions for the last $ReportInterval days..."
    $cutoff = (Get-Date).AddDays(-$ReportInterval)

    $sessions = @(Get-VBRBackupSession | Where-Object { $_.CreationTime -gt $cutoff })
    Write-LogFile "Collected $($sessions.Count) VM/Backup Copy sessions."

    $agentSessions = @()
    try {
        $agentSessions = @(Get-VBRComputerBackupJobSession | Where-Object { $_.CreationTime -gt $cutoff })
        Write-LogFile "Collected $($agentSessions.Count) agent backup sessions."
    } catch {
        Write-LogFile "Failed to collect agent backup sessions: $($_.Exception.Message)" -LogLevel "WARNING"
    }

    return @($sessions) + @($agentSessions)
}
