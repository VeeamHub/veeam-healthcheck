#Requires -Version 5.1

function Invoke-VhciCBackupJobFetch {
    <#
    .Synopsis
        Single-purpose wrapper around [Veeam.Backup.Core.CBackupJob]::GetAll()
        so that Pester can mock the .NET edge in tests. Do not add logic here.
        See ADR 0018 (same seam pattern as Invoke-VhciCBackupSessionFetch).

        Used only by Get-VhcJob's tier C job discovery (issue #222), which
        is version-gated to VBR < 13: on 12.3.x, Get-VBRJob (and
        Get-VBRPluginJob) do not return native Nutanix AHV/Proxmox/HPE
        Morpheus VME jobs at all, and this is the last-resort path that
        reads the core job model directly instead of a supported cmdlet.
        The caller (Get-VhcJob) owns the per-item GetParent() walk, dedup,
        and logging - nothing here beyond the raw fetch.
    .Outputs
        [Veeam.Backup.Core.CBackupJob[]] from the live VBR config database.
    #>
    [CmdletBinding()]
    param()

    return [Veeam.Backup.Core.CBackupJob]::GetAll()
}
